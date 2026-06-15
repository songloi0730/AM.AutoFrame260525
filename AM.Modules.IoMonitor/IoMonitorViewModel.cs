// -------------------------------------------------------
// File:    IoMonitorViewModel.cs
// Project: AM.Modules.IoMonitor
// Purpose: ViewModel giám sát DI/DO realtime — set/reset thường (logic vẫn kiểm soát)
//          + Chế độ Force riêng (đóng băng output, cắt logic).
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

    /// <summary>True nếu kênh DO đang bị force (đóng băng — logic không ghi đè được).</summary>
    [ObservableProperty] private bool _isForced;

    /// <summary>True nếu đang chờ chạm xác nhận thứ hai để force (chạm-2-bước).</summary>
    [ObservableProperty] private bool _isArmed;

    public IoChannelVm(int index, string prefix)
    {
        Index = index;
        Label = $"{prefix}{index}";
    }
}

/// <summary>
/// Giám sát I/O realtime: poll DI/DO bằng <see cref="IIoModule"/> theo chu kỳ. Hai chế độ ghi tách bạch
/// (HMI_Manual_Operation_and_Safety — phương án A):
/// <list type="bullet">
/// <item><b>Set/reset thường</b> (mặc định): bấm dòng = ghi DO, <i>logic máy vẫn kiểm soát</i>. Cần Engineer + máy dừng (guard R3).</item>
/// <item><b>Chế độ Force</b> (toggle đầu bảng, Administrator): bấm dòng = <i>đóng băng</i> output (cắt logic), chạm-2-bước xác nhận; có nút Gỡ.</item>
/// </list>
/// Tuân thủ R-UI: không import System.Windows; marshalling qua SynchronizationContext. Mọi thao tác đều audit.
/// </summary>
public sealed partial class IoMonitorViewModel : ObservableObject, IDisposable
{
    private const int ArmTimeoutMs = 4000; // chạm-1 (arm) tự huỷ nếu không xác nhận trong khoảng này

    private readonly IIoModule _io;
    private readonly IGuardEngine _guard;
    private readonly IAuditService _audit;
    private readonly IUserService _user;
    private readonly ILogger<IoMonitorViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _pollMs;
    private bool _suppressForceModeChange; // chống đệ quy khi tự ép ForceMode về false
    private bool _disposed;

    /// <summary>Danh sách Digital Input.</summary>
    public ObservableCollection<IoChannelVm> DigitalInputs { get; } = [];

    /// <summary>Danh sách Digital Output.</summary>
    public ObservableCollection<IoChannelVm> DigitalOutputs { get; } = [];

    [ObservableProperty] private bool _isConnected;

    /// <summary>True nếu được phép set/reset DO thường (guard R3: Engineer + máy dừng).</summary>
    [ObservableProperty] private bool _isWriteAllowed;

    /// <summary>True nếu được phép bật Chế độ Force (≥ Administrator + máy dừng).</summary>
    [ObservableProperty] private bool _isForceModeAllowed;

    /// <summary>Đang ở Chế độ Force (bấm dòng = đóng băng output).</summary>
    [ObservableProperty] private bool _forceMode;

    /// <summary>Số kênh DO đang bị force (badge nhắc gỡ).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ForcedCountText))]
    private int _forcedCount;

    /// <summary>Nhãn bộ đếm force đã localize ("đang FORCE N IO — nhớ gỡ…").</summary>
    public string ForcedCountText =>
        string.Format(CultureInfo.InvariantCulture, Loc.Strings["Io.ForcedCount"], ForcedCount);

    /// <summary>Lý do/khả dụng của set/reset thường — hiển thị dải khóa (giải thích thay vì giấu).</summary>
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

        RefreshLockState();
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    // ─── Set/reset thường (logic vẫn kiểm soát) ──────────────────────────────────

    /// <summary>Bấm dòng ở chế độ thường: set/reset DO. Cần Engineer + máy dừng (guard R3). KHÔNG đòi Admin.</summary>
    [RelayCommand]
    private async Task ToggleOutput(IoChannelVm? channel)
    {
        if (channel is null || ForceMode) return;

        bool target = !channel.Value;
        string who = _user.CurrentUser ?? "?";
        string action = string.Create(CultureInfo.InvariantCulture, $"Set DO{channel.Index}={target}");

        var guard = _guard.Evaluate(RiskTier.R3);
        if (!guard.Allowed)
        {
            _audit.Record(who, action, allowed: false, WriteDeniedReason(guard));
            return;
        }
        if (!_io.IsConnected) return;

        try
        {
            await _io.WriteDiAsync(channel.Index, target).ConfigureAwait(true);
            channel.Value = target;
            _audit.Record(who, action, allowed: true);
            _logger.LogDebug("[IoMonitor] Set DO{Ch} = {Val}", channel.Index, target);
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[IoMonitor] Set DO{Ch} failed", channel.Index);
        }
    }

    // ─── Chế độ Force (đóng băng output, cắt logic) ──────────────────────────────

    // Bật/tắt Chế độ Force: phòng vệ quyền (Admin + máy dừng) + audit. Chống đệ quy khi tự ép về false.
    partial void OnForceModeChanged(bool value)
    {
        if (_suppressForceModeChange) return;
        string who = _user.CurrentUser ?? "?";

        if (value && !IsForceModeAllowed)
        {
            _audit.Record(who, "Force mode ON", allowed: false, Loc.Strings["Io.ForceModeNeedAdmin"]);
            SetForceModeQuietly(false);
            return;
        }

        DisarmAll();
        _audit.Record(who, value ? "Force mode ON" : "Force mode OFF", allowed: true);
    }

    /// <summary>Bấm dòng ở Chế độ Force: chạm-1 arm, chạm-2 (cùng kênh) đóng băng output ở giá trị đảo.</summary>
    [RelayCommand]
    private async Task ForceOutput(IoChannelVm? channel)
    {
        if (channel is null || !ForceMode) return;

        string who = _user.CurrentUser ?? "?";
        var guard = _guard.Evaluate(RiskTier.R3);
        bool isAdmin = _user.CurrentLevel >= UserLevel.Administrator;
        if (!guard.Allowed || !isAdmin)
        {
            _audit.Record(who, string.Create(CultureInfo.InvariantCulture, $"Force DO{channel.Index}"),
                allowed: false, ForceDeniedReason(guard));
            return;
        }

        // Chạm thứ nhất: arm + chờ xác nhận; chạm thứ hai cùng kênh: đóng băng.
        if (!channel.IsArmed)
        {
            ArmForce(channel);
            return;
        }
        channel.IsArmed = false;
        if (!_io.IsConnected) return;

        bool target = !channel.Value;
        string action = string.Create(CultureInfo.InvariantCulture, $"Force DO{channel.Index}={target}");
        try
        {
            await _io.ForceDoAsync(channel.Index, target).ConfigureAwait(true);
            channel.Value = target;
            channel.IsForced = true;
            _audit.Record(who, action, allowed: true);
            _logger.LogInformation("[IoMonitor] {Action}", action);
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[IoMonitor] Force DO{Ch} failed", channel.Index);
        }
    }

    /// <summary>Gỡ force một kênh — trả quyền điều khiển về logic máy.</summary>
    [RelayCommand]
    private async Task UnforceOutput(IoChannelVm? channel)
    {
        if (channel is null || !ForceMode) return;

        string who = _user.CurrentUser ?? "?";
        var guard = _guard.Evaluate(RiskTier.R3);
        bool isAdmin = _user.CurrentLevel >= UserLevel.Administrator;
        string action = string.Create(CultureInfo.InvariantCulture, $"Unforce DO{channel.Index}");
        if (!guard.Allowed || !isAdmin)
        {
            _audit.Record(who, action, allowed: false, ForceDeniedReason(guard));
            return;
        }

        try
        {
            await _io.UnforceDoAsync(channel.Index).ConfigureAwait(true);
            channel.IsForced = false;
            channel.IsArmed = false;
            _audit.Record(who, action, allowed: true);
            _logger.LogInformation("[IoMonitor] {Action}", action);
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[IoMonitor] Unforce DO{Ch} failed", channel.Index);
        }
    }

    private void ArmForce(IoChannelVm channel)
    {
        DisarmAll();
        channel.IsArmed = true;
        _ = DisarmAfterDelayAsync(channel, _cts.Token);
    }

    private static async Task DisarmAfterDelayAsync(IoChannelVm channel, CancellationToken ct)
    {
        try { await Task.Delay(ArmTimeoutMs, ct).ConfigureAwait(true); }
        catch (OperationCanceledException) { return; }
        channel.IsArmed = false;
    }

    private void DisarmAll()
    {
        foreach (var ch in DigitalOutputs) ch.IsArmed = false;
    }

    private void SetForceModeQuietly(bool value)
    {
        _suppressForceModeChange = true;
        ForceMode = value;
        _suppressForceModeChange = false;
    }

    // ─── Dải khóa / quyền ────────────────────────────────────────────────────────

    // Tính khả dụng set/reset + Force mode + dải khóa theo guard R3 + role. Tự thoát Force mode nếu mất quyền.
    private void RefreshLockState()
    {
        var r = _guard.Evaluate(RiskTier.R3);
        bool isAdmin = _user.CurrentLevel >= UserLevel.Administrator;
        IsWriteAllowed = r.Allowed;                // set/reset thường: Engineer + máy dừng
        IsForceModeAllowed = r.Allowed && isAdmin; // Force mode: thêm Administrator

        string role = _user.IsLoggedIn ? _user.CurrentLevel.ToString() : Loc.Strings["Shell.Guest"];
        if (!r.Allowed && r.Block == GuardBlock.MachineBusy)
            LockText = Loc.Strings["Io.WriteLockedBusy"];
        else if (r.Block == GuardBlock.InsufficientRole)
            LockText = string.Format(CultureInfo.InvariantCulture, Loc.Strings["Io.WriteNeedRole"], r.RequiredLevel);
        else
            LockText = string.Format(CultureInfo.InvariantCulture, Loc.Strings["Io.WriteOk"], role);

        // Mất quyền/máy chạy khi đang ở Force mode → tự thoát (force trên HAL vẫn giữ; chỉ thoát chế độ chỉnh).
        if (ForceMode && !IsForceModeAllowed)
        {
            SetForceModeQuietly(false);
            DisarmAll();
            _audit.Record(_user.CurrentUser ?? "?", "Force mode OFF (auto)", allowed: true, LockText);
        }
    }

    // Lý do từ chối set/reset: máy đang chạy → bận; thiếu quyền → cần role tối thiểu.
    private static string WriteDeniedReason(GuardResult r)
        => r.Block == GuardBlock.MachineBusy
            ? Loc.Strings["Io.WriteLockedBusy"]
            : string.Format(CultureInfo.InvariantCulture, Loc.Strings["Io.WriteNeedRole"], r.RequiredLevel);

    // Lý do từ chối force/gỡ: máy đang chạy → bận; còn lại (chưa Admin) → cần Administrator.
    private static string ForceDeniedReason(GuardResult r)
        => r.Block == GuardBlock.MachineBusy
            ? Loc.Strings["Io.WriteLockedBusy"]
            : Loc.Strings["Io.ForceModeNeedAdmin"];

    // ─── Poll nền ────────────────────────────────────────────────────────────────

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_pollMs));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (!_io.IsConnected)
                {
                    RunOnUIThread(() =>
                    {
                        IsConnected = false;
                        RefreshLockState(); // khả dụng phụ thuộc trạng thái máy + role → cập nhật cùng nhịp poll
                    });
                    continue;
                }

                uint diMask = await _io.ReadAllDiAsync(ct).ConfigureAwait(false);
                uint doMask = await _io.ReadAllDoAsync(ct).ConfigureAwait(false);
                var forced = _io.ForcedOutputs;
                RunOnUIThread(() =>
                {
                    IsConnected = true;
                    foreach (var ch in DigitalInputs)
                        ch.Value = (diMask & (1u << ch.Index)) != 0;
                    foreach (var ch in DigitalOutputs)
                    {
                        ch.Value = (doMask & (1u << ch.Index)) != 0;
                        ch.IsForced = forced.Contains(ch.Index);
                    }
                    ForcedCount = forced.Count;
                    RefreshLockState();
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
