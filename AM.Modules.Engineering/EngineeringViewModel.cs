// -------------------------------------------------------
// File:    EngineeringViewModel.cs
// Project: AM.Modules.Engineering
// Purpose: Màn Engineering/Debug — auto-discovery Station/Mechanism qua [StationUI]/[MechanismUI]
//          + chạy SubRoutine (gate quyền/state) + E-Stop từng cụm.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Reflection;
using AM.Core.Abstractions.Interfaces;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Attributes;
using AM.Core.Enums;
using AM.Core.Exceptions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Engineering;

/// <summary>Một cụm cơ học hiển thị (DisplayName từ [MechanismUI]; Ready/Busy poll live).</summary>
public sealed partial class MechanismVm : ObservableObject
{
    private readonly IMechanism _mechanism;

    /// <summary>Tên hiển thị (từ [MechanismUI] hoặc Name).</summary>
    public string DisplayName { get; }

    /// <summary>Nhóm (từ [MechanismUI]).</summary>
    public string Group { get; }

    /// <summary>Phân loại phần cứng.</summary>
    public string Category { get; }

    [ObservableProperty] private bool _isReady;
    [ObservableProperty] private bool _isBusy;

    /// <summary>Tạo VM từ mechanism (đọc metadata [MechanismUI]).</summary>
    public MechanismVm(IMechanism mechanism)
    {
        ArgumentNullException.ThrowIfNull(mechanism);
        _mechanism = mechanism;
        var attr = mechanism.GetType().GetCustomAttribute<MechanismUIAttribute>();
        DisplayName = attr?.DisplayName ?? mechanism.Name;
        Group = attr?.Group ?? "General";
        Category = mechanism.Category.ToString();
        Refresh();
    }

    /// <summary>Đọc lại Ready/Busy từ mechanism.</summary>
    public void Refresh()
    {
        IsReady = _mechanism.IsReady;
        IsBusy = _mechanism.IsBusy;
    }

    /// <summary>Dừng khẩn cấp cụm này.</summary>
    public void EmergencyStop() => _mechanism.EmergencyStop();
}

/// <summary>Một station hiển thị (DisplayName từ [StationUI]) + danh sách mechanism.</summary>
public sealed partial class StationVm : ObservableObject
{
    private readonly IStation _station;

    /// <summary>Tên hiển thị (từ [StationUI] hoặc Name).</summary>
    public string DisplayName { get; }

    /// <summary>Các cụm cơ học của station.</summary>
    public ObservableCollection<MechanismVm> Mechanisms { get; } = [];

    [ObservableProperty] private string _state = string.Empty;

    /// <summary>Tạo VM từ station (đọc metadata [StationUI]).</summary>
    public StationVm(IStation station)
    {
        ArgumentNullException.ThrowIfNull(station);
        _station = station;
        var attr = station.GetType().GetCustomAttribute<StationUIAttribute>();
        DisplayName = attr?.DisplayName ?? station.Name;
        foreach (var m in station.Mechanisms) Mechanisms.Add(new MechanismVm(m));
        Refresh();
    }

    /// <summary>Cập nhật state + Ready/Busy của mọi mechanism.</summary>
    public void Refresh()
    {
        State = _station.State.ToString();
        foreach (var m in Mechanisms) m.Refresh();
    }
}

/// <summary>Một subroutine hiển thị (tên + mô tả + quyền cần).</summary>
public sealed class SubRoutineVm
{
    /// <summary>Tên subroutine.</summary>
    public string Name { get; }
    /// <summary>Mô tả.</summary>
    public string Description { get; }
    /// <summary>Quyền tối thiểu.</summary>
    public string RequiredLevel { get; }

    /// <summary>Tạo VM từ subroutine.</summary>
    public SubRoutineVm(ISubRoutine sub)
    {
        ArgumentNullException.ThrowIfNull(sub);
        Name = sub.Name;
        Description = sub.Description;
        RequiredLevel = sub.RequiredLevel.ToString();
    }
}

/// <summary>
/// ViewModel màn Engineering: tự khám phá Station/Mechanism từ <see cref="IMasterController.Stations"/>
/// (metadata qua [StationUI]/[MechanismUI]), poll Ready/Busy live, chạy SubRoutine qua
/// <see cref="ISubRoutineRunner"/> (gate quyền/state), E-Stop từng cụm.
/// </summary>
public sealed partial class EngineeringViewModel : ObservableObject, IDisposable
{
    private readonly ISubRoutineRunner _runner;
    private readonly ILogger<EngineeringViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>Các station của máy.</summary>
    public ObservableCollection<StationVm> Stations { get; } = [];

    /// <summary>Các subroutine có thể chạy.</summary>
    public ObservableCollection<SubRoutineVm> SubRoutines { get; } = [];

    /// <summary>Thông báo trạng thái (kết quả chạy subroutine).</summary>
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Tạo VM, khám phá station/mechanism + subroutine, bắt đầu poll.</summary>
    public EngineeringViewModel(IMasterController master, ISubRoutineRunner runner,
        ILogger<EngineeringViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(logger);
        _runner = runner;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;

        foreach (var s in master.Stations) Stations.Add(new StationVm(s));
        foreach (var sub in runner.Available) SubRoutines.Add(new SubRoutineVm(sub));

        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    [RelayCommand]
    private async Task RunSubRoutine(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        StatusMessage = string.Empty;
        try
        {
            await _runner.RunAsync(name).ConfigureAwait(true);
            StatusMessage = $"'{name}' — xong";
        }
        catch (UnauthorizedAccessException ex) { StatusMessage = ex.Message; }
        catch (InvalidOperationException ex) { StatusMessage = ex.Message; }
        catch (AlarmException ex) { StatusMessage = $"[{ex.AlarmCode}] {ex.Message}"; }
        catch (KeyNotFoundException ex) { StatusMessage = ex.Message; }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log + báo
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Engineering] Lỗi chạy subroutine {Name}", name);
            StatusMessage = "Lỗi chạy subroutine — xem log";
        }
    }

    [RelayCommand]
    private void EmergencyStopMechanism(MechanismVm? mechanism)
    {
        if (mechanism is null) return;
        mechanism.EmergencyStop();
        StatusMessage = $"E-Stop: {mechanism.DisplayName}";
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                RunOnUIThread(() => { foreach (var s in Stations) s.Refresh(); });
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
#pragma warning disable CA1031 // Poll loop: nuốt lỗi để không sập task nền
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Engineering] Poll loop error");
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
