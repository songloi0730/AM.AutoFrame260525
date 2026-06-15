// -------------------------------------------------------
// File:    IoMonitorViewModel.cs
// Project: AM.Modules.IoMonitor
// Purpose: ViewModel giám sát DI/DO realtime — poll trạng thái, toggle DO.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.IoMonitor;

/// <summary>Một kênh IO (DI hoặc DO) hiển thị trên lưới.</summary>
public sealed partial class IoChannelVm : ObservableObject
{
    /// <summary>Chỉ số kênh (0-based).</summary>
    public int Index { get; }

    /// <summary>Nhãn hiển thị (vd "DI0", "DO3").</summary>
    public string Label { get; }

    [ObservableProperty] private bool _value;

    public IoChannelVm(int index, string prefix)
    {
        Index = index;
        Label = $"{prefix}{index}";
    }
}

/// <summary>
/// Giám sát I/O realtime: poll DI bằng <see cref="IIoModule.ReadAllDiAsync"/> theo chu kỳ,
/// cho phép toggle DO. Tuân thủ R-UI: không import System.Windows; marshalling qua SynchronizationContext.
/// Ghi ngõ ra ở màn giám sát = <b>Force IO</b> (R3): qua guard engine (trạng thái máy + role ≥ Engineer)
/// và thêm yêu cầu Administrator tại call site (HMI_Manual_Operation_and_Safety — Force IO = Admin),
/// mọi lần ghi/từ chối đều audit.
/// </summary>
public sealed partial class IoMonitorViewModel : ObservableObject, IDisposable
{
    private readonly IIoModule _io;
    private readonly IGuardEngine _guard;
    private readonly IAuditService _audit;
    private readonly IUserService _user;
    private readonly ILogger<IoMonitorViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _pollMs;
    private bool _disposed;

    /// <summary>Danh sách Digital Input.</summary>
    public ObservableCollection<IoChannelVm> DigitalInputs { get; } = [];

    /// <summary>Danh sách Digital Output.</summary>
    public ObservableCollection<IoChannelVm> DigitalOutputs { get; } = [];

    [ObservableProperty] private bool _isConnected;

    /// <summary>True nếu phiên hiện tại được phép ghi ngõ ra (Force IO): guard R3 cho phép + ≥ Administrator.</summary>
    [ObservableProperty] private bool _isWriteAllowed;

    /// <summary>Lý do/khả dụng của thao tác ghi ngõ ra — hiển thị dải khóa (giải thích thay vì giấu).</summary>
    [ObservableProperty] private string _lockText = string.Empty;

    /// <summary>Tạo VM + bắt đầu poll nền.</summary>
    public IoMonitorViewModel(IIoModule io, IGuardEngine guard, IAuditService audit,
        IUserService user, ILogger<IoMonitorViewModel> logger, int pollIntervalMs = 300)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(logger);
        _io = io;
        _guard = guard;
        _audit = audit;
        _user = user;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;
        _pollMs = pollIntervalMs;

        for (int i = 0; i < io.DigitalInputCount; i++) DigitalInputs.Add(new IoChannelVm(i, "DI"));
        for (int i = 0; i < io.DigitalOutputCount; i++) DigitalOutputs.Add(new IoChannelVm(i, "DO"));

        RefreshWriteLock();
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    [RelayCommand]
    private async Task ToggleOutput(IoChannelVm? channel)
    {
        if (channel is null) return;

        bool target = !channel.Value;
        string who = _user.CurrentUser ?? "?";
        string action = string.Create(CultureInfo.InvariantCulture, $"Force DO{channel.Index}={target}");

        // Ghi ngõ ra = Force IO (R3). Guard chặn theo máy/role; thêm yêu cầu Administrator tại call site.
        var guard = _guard.Evaluate(RiskTier.R3);
        bool isAdmin = _user.CurrentLevel >= UserLevel.Administrator;
        if (!guard.Allowed || !isAdmin)
        {
            _audit.Record(who, action, allowed: false, GuardDeniedReason(guard));
            return;
        }
        if (!_io.IsConnected) return;

        try
        {
            await _io.WriteDiAsync(channel.Index, target).ConfigureAwait(true);
            channel.Value = target;
            _audit.Record(who, action, allowed: true);
            _logger.LogDebug("[IoMonitor] DO{Ch} = {Val}", channel.Index, target);
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[IoMonitor] Toggle DO{Ch} failed", channel.Index);
        }
    }

    // Tính khả dụng + dải khóa ghi ngõ ra theo guard R3 + yêu cầu Administrator (Force IO).
    private void RefreshWriteLock()
    {
        var r = _guard.Evaluate(RiskTier.R3);
        bool isAdmin = _user.CurrentLevel >= UserLevel.Administrator;
        IsWriteAllowed = r.Allowed && isAdmin;

        string role = _user.IsLoggedIn ? _user.CurrentLevel.ToString() : Loc.Strings["Shell.Guest"];
        if (!r.Allowed && r.Block == GuardBlock.MachineBusy)
            LockText = Loc.Strings["Io.ForceLockedBusy"];
        else if (IsWriteAllowed)
            LockText = string.Format(CultureInfo.InvariantCulture, Loc.Strings["Io.ForceOk"], role);
        else
            LockText = Loc.Strings["Io.ForceNeedAdmin"];
    }

    // Lý do từ chối ghi ngõ ra: máy đang chạy → bận; còn lại (role thiếu / không phải Admin) → cần Admin.
    private static string GuardDeniedReason(GuardResult r)
        => !r.Allowed && r.Block == GuardBlock.MachineBusy
            ? Loc.Strings["Io.ForceLockedBusy"]
            : Loc.Strings["Io.ForceNeedAdmin"];

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_pollMs));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                bool connected = _io.IsConnected;
                if (!connected)
                {
                    RunOnUIThread(() =>
                    {
                        IsConnected = false;
                        RefreshWriteLock(); // khả dụng ghi phụ thuộc trạng thái máy + role → cập nhật cùng nhịp poll
                    });
                    continue;
                }

                uint diMask = await _io.ReadAllDiAsync(ct).ConfigureAwait(false);
                RunOnUIThread(() =>
                {
                    IsConnected = true;
                    foreach (var ch in DigitalInputs)
                        ch.Value = (diMask & (1u << ch.Index)) != 0;
                    RefreshWriteLock();
                });
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
#pragma warning disable CA1031 // Poll loop: nuốt lỗi để không sập task nền
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[IoMonitor] Poll loop error");
        }
    }

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext) action();
        else _uiContext.Post(_ => action(), null);
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
