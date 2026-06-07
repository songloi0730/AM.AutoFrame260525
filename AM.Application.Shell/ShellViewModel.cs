// -------------------------------------------------------
// File:    ShellViewModel.cs
// Project: AM.Application.Shell
// Purpose: VM cho Shell — header (state/mode/recipe/user/clock + lệnh toàn cục),
//          alarm bar, và status bar (chip kết nối thiết bị). Theo layout IPC ISA-101.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Windows.Threading;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models.EventArgs;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AM.Application.Shell;

/// <summary>Một chip kết nối thiết bị trên status bar.</summary>
internal sealed partial class ConnectionChipVm : ObservableObject
{
    /// <summary>Tên thiết bị hiển thị.</summary>
    public string Name { get; }

    /// <summary>True nếu thiết bị đang kết nối.</summary>
    [ObservableProperty] private bool _connected;

    /// <summary>Khởi tạo chip.</summary>
    public ConnectionChipVm(string name) => Name = name;
}

/// <summary>
/// ViewModel của Shell: cung cấp dữ liệu cho header (trạng thái máy, mode, recipe, user, đồng hồ,
/// lệnh toàn cục Init/Start/Stop/Reset), alarm bar, và status bar (chip kết nối).
/// Bám ISA-101/SEMI E95: lệnh toàn cục ở header, alarm + connection tách 2 dải dưới.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
    Justification = "Khởi tạo qua DI (AddSingleton) + set làm DataContext của MainWindow")]
internal sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IMasterController _master;
    private readonly IAlarmService _alarm;
    private readonly IRecipeService _recipe;
    private readonly IUserService _user;
    private readonly IHardwareManagerService _hardware;
    private readonly SynchronizationContext? _ui;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InitializeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private MachineState _machineState;

    [ObservableProperty] private string _stateText = string.Empty;
    [ObservableProperty] private string _modeText = string.Empty;
    [ObservableProperty] private string _recipeText = "—";
    [ObservableProperty] private string _userText = string.Empty;
    [ObservableProperty] private string _clockText = string.Empty;
    [ObservableProperty] private string _latestAlarmText = string.Empty;
    [ObservableProperty] private bool _hasActiveAlarm;

    /// <summary>Chip kết nối thiết bị (status bar).</summary>
    public ObservableCollection<ConnectionChipVm> Connections { get; } = [];

    /// <summary>Tạo VM Shell (resolve trên UI thread để capture SynchronizationContext).</summary>
    public ShellViewModel(IMasterController master, IAlarmService alarm, IRecipeService recipe,
        IUserService user, IHardwareManagerService hardware)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(alarm);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(hardware);
        _master = master;
        _alarm = alarm;
        _recipe = recipe;
        _user = user;
        _hardware = hardware;
        _ui = SynchronizationContext.Current;

        foreach (var d in _hardware.GetMonitoredDevices())
            Connections.Add(new ConnectionChipVm(d.Name) { Connected = d.Device.IsConnected });

        RefreshState();
        RefreshRecipe();
        RefreshUser();
        RefreshAlarm();

        _master.StateChanged += OnStateChanged;
        _alarm.AlarmRaised += OnAlarmChanged;
        _alarm.AlarmCleared += OnAlarmChanged;
        _recipe.RecipeChanged += OnRecipeChanged;
        _user.UserChanged += OnUserChanged;
        Loc.Strings.PropertyChanged += OnLanguageChanged;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();
        OnTick(this, EventArgs.Empty);
    }

    // ─── Lệnh toàn cục (header) ───────────────────────────────────────────────
    private bool CanInitialize() => MachineState == MachineState.Uninitialized;
    private bool CanStart() => MachineState == MachineState.Idle;
    private bool CanStop() => MachineState is MachineState.Running or MachineState.Paused;
    private bool CanReset() => MachineState is MachineState.InitAlarm or MachineState.RunAlarm;

    [RelayCommand(CanExecute = nameof(CanInitialize))]
    private Task Initialize() => _master.InitializeAsync();

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task Start() => _master.StartAsync();

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task Stop() => _master.StopAsync();

    [RelayCommand(CanExecute = nameof(CanReset))]
    private Task Reset() => _master.ResetAsync();

    /// <summary>Acknowledge alarm mới nhất đang active (alarm bar).</summary>
    [RelayCommand]
    private async Task AcknowledgeAlarm()
    {
        var active = _alarm.ActiveAlarms;
        if (active.Count == 0) return;
        var latest = active[^1];
        await _alarm.AcknowledgeAsync(latest.AlarmCode, _user.CurrentUser ?? "operator").ConfigureAwait(true);
    }

    // ─── Event handlers ───────────────────────────────────────────────────────
    private void OnStateChanged(object? sender, MachineStateChangedEventArgs e) => RunOnUi(RefreshState);
    private void OnAlarmChanged(object? sender, AlarmEventArgs e) => RunOnUi(RefreshAlarm);
    private void OnRecipeChanged(object? sender, RecipeEventArgs e) => RunOnUi(RefreshRecipe);
    private void OnUserChanged(object? sender, UserChangedEventArgs e) => RunOnUi(RefreshUser);
    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => RunOnUi(() => { RefreshState(); RefreshUser(); });

    private void OnTick(object? sender, EventArgs e)
    {
        ClockText = DateTime.Now.ToString("HH:mm:ss  yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var d in _hardware.GetMonitoredDevices())
        {
            var chip = Connections.FirstOrDefault(c => c.Name == d.Name);
            if (chip is not null) chip.Connected = d.Device.IsConnected;
        }
    }

    private void RefreshState()
    {
        MachineState = _master.State;
        StateText = Loc.Strings[$"State.{_master.State}"];
        ModeText = _master.OperationMode.ToString();
    }

    private void RefreshRecipe() => RecipeText = _recipe.ActiveRecipe?.Name ?? "—";

    private void RefreshUser()
        => UserText = _user.IsLoggedIn
            ? $"{_user.CurrentUser} ({_user.CurrentLevel})"
            : Loc.Strings["Shell.Guest"];

    private void RefreshAlarm()
    {
        var active = _alarm.ActiveAlarms;
        HasActiveAlarm = active.Count > 0;
        LatestAlarmText = active.Count > 0
            ? $"[{active[^1].AlarmCode}] {active[^1].Message}"
            : Loc.Strings["Shell.NoAlarm"];
    }

    private void RunOnUi(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui) action();
        else _ui.Post(_ => action(), null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _master.StateChanged -= OnStateChanged;
        _alarm.AlarmRaised -= OnAlarmChanged;
        _alarm.AlarmCleared -= OnAlarmChanged;
        _recipe.RecipeChanged -= OnRecipeChanged;
        _user.UserChanged -= OnUserChanged;
        Loc.Strings.PropertyChanged -= OnLanguageChanged;
    }
}
