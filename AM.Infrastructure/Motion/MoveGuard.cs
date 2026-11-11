// -------------------------------------------------------
// File:    MoveGuard.cs
// Project: AM.Infrastructure
// Purpose: Kiểm giới hạn mềm + luật interlock (move-protection.json) trước mọi lệnh chuyển động (0015)
// -------------------------------------------------------

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Infrastructure.Motion;

/// <summary>
/// Triển khai <see cref="IMoveGuard"/> cho MỘT motion controller (các trục trong axismap.json thuộc controller đó).
/// Đọc vị trí/trạng thái về gốc từ controller THẬT (không qua lớp bọc) và DI qua <see cref="IIoTagMap"/>.
/// </summary>
public sealed class MoveGuard : IMoveGuard, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ILogger<MoveGuard> _logger;
    private readonly IMotionController _motion;
    private readonly IIoModule? _io;
    private readonly IIoTagMap? _tags;
    private readonly IAlarmService? _alarms;
    private readonly IUserService? _users;
    private readonly Dictionary<int, AxisConfig> _byIndex = new();
    private readonly Dictionary<string, AxisConfig> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, double> _commandedTarget = new();
    private readonly ConcurrentDictionary<string, string> _bypassed = new(StringComparer.Ordinal);
    private readonly MoveProtectionConfig _config;

    /// <summary>Tạo guard.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="motion">Controller THẬT (bên trong lớp bọc) — nguồn vị trí/về gốc.</param>
    /// <param name="axes">Cấu hình các trục thuộc controller này (giới hạn mềm, tên logic).</param>
    /// <param name="rulesPath">Đường dẫn move-protection.json; không có file = không luật (hợp lệ).</param>
    /// <param name="io">Module IO để đọc DI của luật (null = luật DI luôn không đạt).</param>
    /// <param name="tags">Map tag → kênh DI.</param>
    /// <param name="alarms">Dịch vụ alarm (70011 khi chặn jog, 70012 khi tắt luật, 40014 file hỏng).</param>
    /// <param name="users">Dịch vụ user (quyền tắt luật, tự bật lại khi đổi user).</param>
    public MoveGuard(ILogger<MoveGuard> logger, IMotionController motion, IEnumerable<AxisConfig> axes,
        string rulesPath, IIoModule? io = null, IIoTagMap? tags = null,
        IAlarmService? alarms = null, IUserService? users = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(axes);
        ArgumentNullException.ThrowIfNull(rulesPath);
        _logger = logger;
        _motion = motion;
        _io = io;
        _tags = tags;
        _alarms = alarms;
        _users = users;
        foreach (var a in axes)
        {
            _byIndex[a.Index] = a;
            _byName[a.Name] = a;
        }

        (_config, LoadError) = Load(rulesPath);
        if (_users is not null) _users.UserChanged += OnUserChanged;
    }

    /// <inheritdoc/>
    public IReadOnlyList<MoveProtectionRule> Rules => _config.Rules;

    /// <inheritdoc/>
    public string? LoadError { get; }

    /// <summary>Khoảng nhìn trước tối thiểu khi jog liên tục (mm).</summary>
    public double JogLookaheadMm => _config.JogLookaheadMm;

    /// <summary>Tên logic của trục theo chỉ số (hoặc "AX_n").</summary>
    public string AxisName(int axisIndex)
        => _byIndex.TryGetValue(axisIndex, out var c) ? c.Name : $"AX_{axisIndex}";

    /// <summary>Ghi nhận đích vừa phát cho trục (dùng xét thận trọng khi trục đó đang chạy).</summary>
    public void NoteCommand(int axisIndex, double target) => _commandedTarget[axisIndex] = target;

    /// <summary>Báo lỗi nạp file luật lên alarm (gọi lúc khởi động, sau khi alarm service sẵn sàng).</summary>
    public void ReportStatusAtBoot()
    {
        if (LoadError is null)
        {
            _logger.LogInformation("[MoveGuard] {Count} luật chống va chạm đang hiệu lực", Rules.Count);
            return;
        }
        RaiseFireAndForget(AlarmCodes.SystemMoveProtectionInvalid, "MoveGuard",
            $"move-protection.json không hợp lệ — KHÔNG luật chống va chạm nào được nạp: {LoadError}");
    }

    /// <inheritdoc/>
    public async Task<MoveCheckResult> CheckAsync(int axisIndex, double target, CancellationToken ct = default)
    {
        string name = AxisName(axisIndex);

        if (_byIndex.TryGetValue(axisIndex, out var cfg) && cfg.SoftLimitMin < cfg.SoftLimitMax
            && (target < cfg.SoftLimitMin || target > cfg.SoftLimitMax))
        {
            return new MoveCheckResult(false, AlarmCodes.MotionSoftLimit, string.Format(CultureInfo.InvariantCulture,
                "{0}: đích {1:0.000} ngoài giới hạn mềm [{2:0.000}, {3:0.000}]",
                name, target, cfg.SoftLimitMin, cfg.SoftLimitMax));
        }

        var applicable = _config.Rules.Where(r =>
            string.Equals(r.Moving, name, StringComparison.OrdinalIgnoreCase) && !_bypassed.ContainsKey(r.Id)).ToList();
        if (applicable.Count == 0) return MoveCheckResult.Ok;

        double current = await _motion.GetPositionAsync(axisIndex, ct).ConfigureAwait(false);
        foreach (var rule in applicable)
        {
            if (rule.Zone is not null && !rule.Zone.IntersectsSegment(current, target)) continue;
            foreach (var req in rule.Require)
            {
                string? failure = await EvaluateAsync(req, ct).ConfigureAwait(false);
                if (failure is not null)
                {
                    return new MoveCheckResult(false, AlarmCodes.SafetyCollisionBlocked,
                        $"{name} bị chặn [{rule.Id}]: {failure}");
                }
            }
        }
        return MoveCheckResult.Ok;
    }

    /// <inheritdoc/>
    public bool IsBypassed(string ruleId) => _bypassed.ContainsKey(ruleId);

    /// <inheritdoc/>
    public async Task SetBypassAsync(string ruleId, bool bypassed, string reason, CancellationToken ct = default)
    {
        if (!_config.Rules.Any(r => r.Id == ruleId))
            throw new KeyNotFoundException($"Không có luật chống va chạm '{ruleId}'");
        if (bypassed)
        {
            if (_users is null || _users.CurrentLevel < UserLevel.Administrator)
                throw new UnauthorizedAccessException("Tắt luật chống va chạm cần quyền Administrator");
            _bypassed[ruleId] = reason ?? string.Empty;
            _logger.LogWarning("[MoveGuard] TẮT luật {Rule} bởi {User}: {Reason}", ruleId, _users.CurrentUser, reason);
        }
        else if (_bypassed.TryRemove(ruleId, out _))
        {
            _logger.LogInformation("[MoveGuard] Bật lại luật {Rule}: {Reason}", ruleId, reason);
        }
        await SyncBypassAlarmAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_users is not null) _users.UserChanged -= OnUserChanged;
    }

    // ─── Nội bộ ─────────────────────────────────────────────────────────────────

    private async Task<string?> EvaluateAsync(MoveRequirement req, CancellationToken ct)
    {
        if (req.Axis is not null)
        {
            if (!_byName.TryGetValue(req.Axis, out var other))
                return $"trục '{req.Axis}' không có trong axismap";
            if (!await _motion.IsHomedAsync(other.Index, ct).ConfigureAwait(false))
                return $"{other.Name} chưa về gốc (vị trí không tin được)";

            double pos = await _motion.GetPositionAsync(other.Index, ct).ConfigureAwait(false);
            double end = pos;
            if (await _motion.IsMovingAsync(other.Index, ct).ConfigureAwait(false)
                && _commandedTarget.TryGetValue(other.Index, out double t))
                end = t; // trục kia đang chạy: xét cả đoạn tới đích của nó

            var range = new AxisRange(req.Min, req.Max);
            if (range.ContainsSegment(pos, end)) return null;
            return string.Format(CultureInfo.InvariantCulture, "{0} = {1:0.000}{2}, cần {3}",
                other.Name, pos, end.Equals(pos) ? string.Empty : $" → {end:0.000}", Describe(range));
        }

        if (req.Di is not null)
        {
            if (_io is null || _tags is null || !_tags.ContainsDi(req.Di))
                return $"không đọc được DI '{req.Di}'";
            bool value = await _io.ReadDiAsync(_tags.ResolveDi(req.Di), ct).ConfigureAwait(false);
            if (value == req.On) return null;
            string actual = value ? "ON" : "OFF";
            string wanted = req.On ? "ON" : "OFF";
            return $"DI {req.Di} = {actual}, cần {wanted}";
        }

        return "điều kiện rỗng";
    }

    private static string Describe(AxisRange r) => (r.Min, r.Max) switch
    {
        ({ } lo, { } hi) => string.Format(CultureInfo.InvariantCulture, "trong [{0:0.000}, {1:0.000}]", lo, hi),
        ({ } lo, null) => string.Format(CultureInfo.InvariantCulture, "≥ {0:0.000}", lo),
        (null, { } hi) => string.Format(CultureInfo.InvariantCulture, "≤ {0:0.000}", hi),
        _ => "bất kỳ",
    };

    private (MoveProtectionConfig Config, string? Error) Load(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogInformation("[MoveGuard] Không có {Path} — chỉ kiểm giới hạn mềm", path);
            return (new MoveProtectionConfig(), null);
        }
        try
        {
            var cfg = JsonSerializer.Deserialize<MoveProtectionConfig>(File.ReadAllText(path), JsonOptions)
                      ?? new MoveProtectionConfig();
            string? error = Validate(cfg);
            if (error is not null)
            {
                _logger.LogError("[MoveGuard] {Path} không hợp lệ: {Error}", path, error);
                return (new MoveProtectionConfig(), error);
            }
            return (cfg, null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "[MoveGuard] Không đọc được {Path}", path);
            return (new MoveProtectionConfig(), ex.Message);
        }
    }

    private string? Validate(MoveProtectionConfig cfg)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in cfg.Rules)
        {
            if (string.IsNullOrWhiteSpace(r.Id)) return "có luật thiếu 'id'";
            if (!ids.Add(r.Id)) return $"trùng id luật '{r.Id}'";
            if (!_byName.ContainsKey(r.Moving)) return $"luật '{r.Id}': trục 'moving' = '{r.Moving}' không có trong axismap";
            if (r.Require.Count == 0) return $"luật '{r.Id}': 'require' rỗng";
            foreach (var q in r.Require)
            {
                if ((q.Axis is null) == (q.Di is null))
                    return $"luật '{r.Id}': mỗi điều kiện đặt đúng một trong 'axis' hoặc 'di'";
                if (q.Axis is not null && !_byName.ContainsKey(q.Axis))
                    return $"luật '{r.Id}': trục '{q.Axis}' không có trong axismap";
                if (q.Axis is not null && string.Equals(q.Axis, r.Moving, StringComparison.OrdinalIgnoreCase))
                    return $"luật '{r.Id}': điều kiện không được là chính trục di chuyển (dùng 'zone' hoặc giới hạn mềm)";
            }
        }
        return null;
    }

    private void OnUserChanged(object? sender, UserChangedEventArgs e)
    {
        if (_bypassed.IsEmpty) return;
        _logger.LogWarning("[MoveGuard] Đổi user → bật lại {Count} luật đang tắt", _bypassed.Count);
        _bypassed.Clear();
        _ = SyncBypassAlarmSafeAsync();
    }

    private async Task SyncBypassAlarmSafeAsync()
    {
#pragma warning disable CA1031 // fire-and-forget từ event handler: lỗi alarm không được làm sập app
        try { await SyncBypassAlarmAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogError(ex, "[MoveGuard] Không cập nhật được alarm bypass"); }
#pragma warning restore CA1031
    }

    private async Task SyncBypassAlarmAsync(CancellationToken ct)
    {
        if (_alarms is null) return;
        if (_bypassed.IsEmpty)
        {
            await _alarms.ClearAsync(AlarmCodes.SafetyCollisionRuleBypassed, ct).ConfigureAwait(false);
            return;
        }
        string list = string.Join(", ", _bypassed.Select(kv => $"{kv.Key} ({kv.Value})"));
        await _alarms.RaiseAsync(AlarmCodes.SafetyCollisionRuleBypassed, "MoveGuard",
            $"Luật chống va chạm đang TẮT: {list}", ct).ConfigureAwait(false);
    }

    /// <summary>Raise alarm không chờ (dùng từ vòng giám sát jog / lúc khởi động).</summary>
    internal void RaiseFireAndForget(int code, string station, string message)
    {
        if (_alarms is null) return;
        _ = Task.Run(async () =>
        {
#pragma warning disable CA1031 // fire-and-forget: lỗi alarm chỉ log
            try { await _alarms.RaiseAsync(code, station, message).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogError(ex, "[MoveGuard] Không raise được alarm {Code}", code); }
#pragma warning restore CA1031
        });
    }
}
