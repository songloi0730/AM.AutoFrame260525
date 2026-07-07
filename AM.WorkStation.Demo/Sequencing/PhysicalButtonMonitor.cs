// -------------------------------------------------------
// File:    PhysicalButtonMonitor.cs
// Project: AM.WorkStation.Demo
// Purpose: Nút vật lý Start/Stop/Reset (DI.Btn.*) → lệnh master controller (roadmap P1.3)
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Sequencing;
using AM.WorkStation.Demo.Config;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Sequencing;

/// <summary>
/// Poll 3 nút vật lý trên IPC (IO map §1: <c>DI.Btn.Start/Stop/Reset</c>) và gọi lệnh
/// master controller theo SƯỜN LÊN (edge-detect — giữ nút không lặp lệnh).
/// Điều kiện hợp lệ do master tự kiểm: Start có interlock an toàn + FireTrigger từ chối
/// state sai — monitor KHÔNG thêm logic trạng thái riêng (một nguồn sự thật).
/// </summary>
public sealed class PhysicalButtonMonitor : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly IIoService _io;
    private readonly IMasterController _master;
    private readonly ILogger<PhysicalButtonMonitor> _logger;
    private readonly CancellationTokenSource _cts = new();
    private bool _started;
    private bool _disposed;

    /// <summary>Tạo monitor.</summary>
    public PhysicalButtonMonitor(IIoService io, IMasterController master,
        ILogger<PhysicalButtonMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(logger);
        _io = io;
        _master = master;
        _logger = logger;
    }

    /// <summary>Bắt đầu poll nền. Gọi một lần lúc khởi động.</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        _ = Task.Run(() => LoopAsync(_cts.Token));
        _logger.LogInformation("[PhysBtn] Started — poll {Ms}ms các nút Start/Stop/Reset", PollInterval.TotalMilliseconds);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        bool prevStart = false, prevStop = false, prevReset = false;
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                bool start = await _io.ReadDiAsync(IoMap.Di.BtnStart, ct).ConfigureAwait(false);
                bool stop = await _io.ReadDiAsync(IoMap.Di.BtnStop, ct).ConfigureAwait(false);
                bool reset = await _io.ReadDiAsync(IoMap.Di.BtnReset, ct).ConfigureAwait(false);

                // Ưu tiên Stop trước Start trong cùng một tick (an toàn hơn khi bấm đồng thời)
                if (stop && !prevStop) await DispatchAsync("Stop", () => _master.StopAsync(ct)).ConfigureAwait(false);
                if (start && !prevStart) await DispatchAsync("Start", () => _master.StartAsync(ct)).ConfigureAwait(false);
                if (reset && !prevReset) await DispatchAsync("Reset", () => _master.ResetAsync(ct)).ConfigureAwait(false);

                prevStart = start;
                prevStop = stop;
                prevReset = reset;
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường khi Dispose */ }
    }

    private async Task DispatchAsync(string name, Func<Task> command)
    {
        _logger.LogInformation("[PhysBtn] Nút vật lý {Button} — gửi lệnh (master tự kiểm điều kiện)", name);
        try
        {
            await command().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
#pragma warning disable CA1031 // lệnh từ nút vật lý lỗi không được giết vòng poll — chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[PhysBtn] Lệnh {Button} thất bại", name);
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
