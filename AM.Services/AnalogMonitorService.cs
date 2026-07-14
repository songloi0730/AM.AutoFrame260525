// -------------------------------------------------------
// File:    AnalogMonitorService.cs
// Project: AM.Services
// Purpose: Poll kênh analog từ IIoModule, scale raw→engineering, giám sát khoảng an toàn
//          khi máy Running → alarm 30006 có debounce (Gói C, S91)
// -------------------------------------------------------

using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Triển khai <see cref="IAnalogMonitorService"/>: nạp <c>analog.map.json</c> (không có file =
/// máy không có kênh analog — hợp lệ), poll 200ms, scale tuyến tính, debounce 1s cho khoảng
/// an toàn (chỉ khi máy Running — máy đứng thì vacuum về 0 là bình thường, không alarm).
/// </summary>
public sealed class AnalogMonitorService : IAnalogMonitorService, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private const int DebounceSamples = 5; // 5 mẫu × 200ms = vượt liên tục 1s mới alarm

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly IIoModule _io;
    private readonly IMasterController _master;
    private readonly IAlarmService? _alarms;
    private readonly ILogger<AnalogMonitorService> _logger;
    private readonly List<AnalogChannelConfig> _channels = [];
    private readonly Dictionary<string, double> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _outOfRangeStreak = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _alarmed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _sync = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _started;
    private bool _disposed;

    /// <summary>Tạo service, nạp cấu hình kênh.</summary>
    /// <param name="io">IO module (nguồn AI).</param>
    /// <param name="master">Master controller — chỉ giám sát khoảng an toàn khi Running.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="alarms">Alarm service (null = chỉ log khi vượt khoảng an toàn).</param>
    /// <param name="mapPath">Đường dẫn analog.map.json (mặc định cạnh executable).</param>
    public AnalogMonitorService(IIoModule io, IMasterController master,
        ILogger<AnalogMonitorService> logger, IAlarmService? alarms = null, string? mapPath = null)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(logger);
        _io = io;
        _master = master;
        _logger = logger;
        _alarms = alarms;
        LoadChannels(mapPath ?? Path.Combine(AppContext.BaseDirectory, "analog.map.json"));
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell",
        "S2365:Properties should not copy collections",
        Justification = "Danh sách kênh bất biến sau ctor và nhỏ (vài chục phần tử)")]
    public IReadOnlyList<AnalogChannelConfig> Channels => [.. _channels];

    /// <inheritdoc/>
    public double? GetValue(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        lock (_sync)
        {
            return _values.TryGetValue(channelId, out double v) ? v : null;
        }
    }

    /// <summary>Scale tuyến tính raw→engineering (public static — tool/test dùng chung).</summary>
    /// <param name="cfg">Cấu hình kênh.</param>
    /// <param name="raw">Giá trị raw (V/mA).</param>
    public static double Scale(AnalogChannelConfig cfg, double raw)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        double span = cfg.RawMax - cfg.RawMin;
        if (Math.Abs(span) < 1e-9) return cfg.EngMin;
        double t = (raw - cfg.RawMin) / span;
        return cfg.EngMin + t * (cfg.EngMax - cfg.EngMin);
    }

    /// <inheritdoc/>
    public void Start()
    {
        if (_started) return;
        _started = true;
        if (_channels.Count == 0)
        {
            _logger.LogInformation("[Analog] Không có kênh analog (analog.map.json trống/thiếu) — không poll");
            return;
        }
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
        _logger.LogInformation("[Analog] Bắt đầu poll {Count} kênh mỗi {Ms}ms",
            _channels.Count, PollInterval.TotalMilliseconds);
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                foreach (var ch in _channels)
                {
                    try
                    {
                        double raw = await _io.ReadAnalogAsync(ch.AiChannel, ct).ConfigureAwait(false);
                        double eng = Scale(ch, raw);
                        lock (_sync) { _values[ch.Id] = eng; }
                        CheckSafeRange(ch, eng);
                    }
                    catch (OperationCanceledException) { throw; }
#pragma warning disable CA1031 // một kênh đọc lỗi không được giết vòng poll — giá trị kênh đó thành null
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        lock (_sync) { _values.Remove(ch.Id); }
                        _logger.LogDebug(ex, "[Analog] Đọc kênh {Id} (AI{Ch}) lỗi", ch.Id, ch.AiChannel);
                    }
                }
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường khi Dispose */ }
    }

    // Khoảng an toàn: chỉ xét khi máy Running; vượt liên tục DebounceSamples mẫu → alarm MỘT lần
    // cho tới khi trở lại trong khoảng (re-arm).
    private void CheckSafeRange(AnalogChannelConfig ch, double eng)
    {
        if (ch.SafeMin is null && ch.SafeMax is null) return;

        bool running = _master.State == MachineState.Running;
        bool outOfRange = running
            && ((ch.SafeMin is double min && eng < min) || (ch.SafeMax is double max && eng > max));

        if (!outOfRange)
        {
            _outOfRangeStreak[ch.Id] = 0;
            _alarmed.Remove(ch.Id); // trở lại trong khoảng → cho phép alarm lần sau
            return;
        }

        int streak = _outOfRangeStreak.GetValueOrDefault(ch.Id) + 1;
        _outOfRangeStreak[ch.Id] = streak;
        if (streak < DebounceSamples || _alarmed.Contains(ch.Id)) return;

        _alarmed.Add(ch.Id);
        string message = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"Kênh {ch.Name} = {eng:F1} {ch.Unit} ngoài khoảng an toàn [{ch.SafeMin?.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) ?? "-∞"} .. {ch.SafeMax?.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) ?? "+∞"}]");
        _logger.LogError("[Analog] {Message}", message);
        RaiseAlarm(message);
    }

    private void RaiseAlarm(string message)
    {
        if (_alarms is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await _alarms.RaiseAsync(AlarmCodes.IoAnalogOutOfRange, "ANALOG", message)
                    .ConfigureAwait(false);
            }
#pragma warning disable CA1031 // alarm lỗi không được giết vòng poll — đã log lỗi gốc
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[Analog] Không raise được alarm 30006");
            }
        });
    }

    private void LoadChannels(string mapPath)
    {
        try
        {
            if (!File.Exists(mapPath))
            {
                _logger.LogInformation("[Analog] Không thấy {Path} — máy không có kênh analog", mapPath);
                return;
            }
            var channels = JsonSerializer.Deserialize<List<AnalogChannelConfig>>(
                File.ReadAllText(mapPath), JsonOptions);
            if (channels is null) return;
            foreach (var ch in channels.Where(c => !string.IsNullOrWhiteSpace(c.Id)))
                _channels.Add(ch);
            _logger.LogInformation("[Analog] Nạp {Count} kênh từ {Path}", _channels.Count, mapPath);
        }
#pragma warning disable CA1031 // file map hỏng → coi như không có kênh, app vẫn chạy (log rõ)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Analog] Lỗi nạp {Path} — bỏ qua kênh analog", mapPath);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
