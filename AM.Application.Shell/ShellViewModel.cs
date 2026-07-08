// -------------------------------------------------------
// File:    ShellViewModel.cs
// Project: AM.Application.Shell
// Purpose: VM cho Shell v3 (ADR 0009) — header chip AUTO/LOCAL/state + heartbeat,
//          alarm banner multi-alarm (1 alarm ưu tiên cao nhất chưa ACK),
//          action bar (Start/Pause-Resume/Stop/Reset/DryRun) + chip kết nối
//          "Thiết bị n/m · Host n/m" với popup chi tiết Thiết bị│Host.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Threading;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.Core.Sequencing;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AM.Application.Shell;

/// <summary>Một chip kết nối thiết bị/host trên connection bar.</summary>
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
/// ViewModel của Shell v3: header (chip AUTO/DRY · LOCAL · state ISA-88, recipe, clock+heartbeat,
/// user), alarm banner theo quy tắc multi-alarm (EEMUA 201 — duy nhất alarm ưu tiên cao nhất chưa
/// ACK + đếm "+N khác"), action bar dưới (lệnh toàn cục, Pause/Resume một nút) + chip kết nối
/// tổng hợp "Thiết bị n/m · Host n/m" mở popup chi tiết 2 cột + chuỗi phiên bản.
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
    private readonly ISequenceEngine _engine;
    private readonly Sequencing.BannerOperatorPromptService _promptService;
    private readonly SynchronizationContext? _ui;
    private readonly DispatcherTimer _timer;
    private OperatorPromptEventArgs? _activePrompt;
    private Sequencing.ServicePromptEventArgs? _activeServicePrompt;
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InitializeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseResumeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleDryRunCommand))]
    private MachineState _machineState;

    [ObservableProperty] private string _stateText = string.Empty;
    [ObservableProperty] private string _modeText = string.Empty;
    [ObservableProperty] private string _recipeText = "—";
    [ObservableProperty] private string _userText = string.Empty;
    [ObservableProperty] private string _clockText = string.Empty;
    [ObservableProperty] private string _pauseResumeText = string.Empty;

    // Alarm banner — multi-alarm rule: chỉ alarm ưu tiên cao nhất CHƯA ACK
    [ObservableProperty] private string _bannerText = string.Empty;
    [ObservableProperty] private bool _hasUnackedAlarm;
    [ObservableProperty] private bool _isBannerError;
    [ObservableProperty] private int _extraAlarmCount;
    [ObservableProperty] private bool _hasExtraAlarms;

    // Chip kết nối tổng hợp trên action bar (v3) — popup mới liệt kê chi tiết
    [ObservableProperty] private string _deviceOnlineText = "0/0";
    [ObservableProperty] private string _hostOnlineText = "0/0";
    [ObservableProperty] private bool _allConnectionsOk;

    // Operator prompt từ sequence engine (onError: Pause / resume-check) — hiện trên banner (S78)
    [ObservableProperty] private bool _hasOperatorPrompt;
    [ObservableProperty] private string _promptText = string.Empty;
    [ObservableProperty] private bool _promptCanSkip;

    /// <summary>Nút Manual action bar: mở màn Vận hành tay — cần LineLead+ (P1.2).</summary>
    [ObservableProperty] private bool _canOpenManual;

    /// <summary>Banner vàng thường trực: còn tài khoản dùng mật khẩu mặc định (design-notes/0012).</summary>
    [ObservableProperty] private bool _hasSecurityNotice;

    // Service prompt (IOperatorPrompt — station hỏi lúc init, vd liệu sót; P1.6):
    // lựa chọn ĐỘNG theo request, khác 3 nút cố định của engine prompt
    [ObservableProperty] private bool _hasServicePrompt;
    [ObservableProperty] private string _servicePromptText = string.Empty;

    /// <summary>Các lựa chọn của service prompt đang chờ (nút động trên banner).</summary>
    public ObservableCollection<string> ServicePromptChoices { get; } = [];

    /// <summary>Chuỗi phiên bản thường trực ở footer popup kết nối (audit/support).</summary>
    public string VersionText { get; }

    /// <summary>Chip nhóm Thiết bị (PLC, motion, camera, IO...).</summary>
    public ObservableCollection<ConnectionChipVm> DeviceConnections { get; } = [];

    /// <summary>Chip nhóm Host (OPC-UA, DB...).</summary>
    public ObservableCollection<ConnectionChipVm> HostConnections { get; } = [];

    /// <summary>Tạo VM Shell (resolve trên UI thread để capture SynchronizationContext).</summary>
    public ShellViewModel(IMasterController master, IAlarmService alarm, IRecipeService recipe,
        IUserService user, IHardwareManagerService hardware, ISequenceEngine engine,
        Sequencing.BannerOperatorPromptService promptService)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(alarm);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(promptService);
        _master = master;
        _alarm = alarm;
        _recipe = recipe;
        _user = user;
        _hardware = hardware;
        _engine = engine;
        _promptService = promptService;
        _ui = SynchronizationContext.Current;

        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText = $"AM.AutoFrame v{ver?.ToString(3) ?? "0.0.0"}";

        // Connection bar: Host = OPC-UA + DB (luôn local); còn lại = Thiết bị
        foreach (var d in _hardware.GetMonitoredDevices())
        {
            var chip = new ConnectionChipVm(d.Name) { Connected = d.Device.IsConnected };
            if (d.Category == HardwareCategory.OpcUaClient) HostConnections.Add(chip);
            else DeviceConnections.Add(chip);
        }
        HostConnections.Add(new ConnectionChipVm("DB") { Connected = true }); // EF SQLite local
        RefreshConnectionSummary();

        RefreshState();
        RefreshRecipe();
        RefreshUser();
        RefreshAlarm();

        _master.StateChanged += OnStateChanged;
        _alarm.AlarmRaised += OnAlarmChanged;
        _alarm.AlarmCleared += OnAlarmChanged;
        _recipe.RecipeChanged += OnRecipeChanged;
        _user.UserChanged += OnUserChanged;
        _engine.OperatorPromptRequired += OnOperatorPrompt;
        _promptService.PromptRequested += OnServicePrompt;
        Loc.Strings.PropertyChanged += OnLanguageChanged;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();
        OnTick(this, EventArgs.Empty);
    }

    // ─── Lệnh toàn cục (action bar đáy — SEMI E95: cố định mọi màn hình) ──────
    private bool CanInitialize() => MachineState == MachineState.Uninitialized;
    private bool CanStart() => MachineState == MachineState.Idle;
    private bool CanPauseResume() => MachineState is MachineState.Running or MachineState.Paused;
    private bool CanStop() => MachineState is MachineState.Running or MachineState.Paused;
    private bool CanReset() => MachineState is MachineState.InitAlarm or MachineState.RunAlarm;
    private bool CanToggleDryRun() => MachineState == MachineState.Idle;

    [RelayCommand(CanExecute = nameof(CanInitialize))]
    private Task Initialize() => _master.InitializeAsync();

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task Start() => _master.StartAsync();

    /// <summary>Pause/Resume một nút — nhãn tự đổi theo state (template v2 §3.6).</summary>
    [RelayCommand(CanExecute = nameof(CanPauseResume))]
    private Task PauseResume()
        => MachineState == MachineState.Paused ? _master.ResumeAsync() : _master.PauseAsync();

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task Stop() => _master.StopAsync();

    [RelayCommand(CanExecute = nameof(CanReset))]
    private Task Reset() => _master.ResetAsync();

    /// <summary>Đảo chế độ Normal ↔ DryRun (chỉ khi Idle) — badge header đổi AUTO/DRY.</summary>
    [RelayCommand(CanExecute = nameof(CanToggleDryRun))]
    private void ToggleDryRun()
    {
        _master.SetOperationMode(
            _master.OperationMode == OperationMode.Normal ? OperationMode.DryRun : OperationMode.Normal);
        RefreshState();
    }

    // ─── Operator prompt (sequence engine — banner 3 nút, S78 Prompt D) ────────

    /// <summary>
    /// Engine cần operator quyết định (lỗi máy với onError=Pause / resume-check lệch).
    /// Kênh trả lời nằm trong args — VM chỉ hiển thị và gọi Respond; "Bỏ qua" lọc theo
    /// quyền (Engineer+ — học RefSeq-A: lựa chọn bỏ qua chỉ ở chế độ kỹ sư).
    /// </summary>
    private void OnOperatorPrompt(object? sender, OperatorPromptEventArgs e)
    {
        RunOnUi(() =>
        {
            _activePrompt = e;
            PromptText = $"{e.StationName} · {e.Message}";
            PromptCanSkip = e.Choices.Contains(StepErrorAction.Skip)
                && _user.CurrentLevel >= UserLevel.Engineer;
            HasOperatorPrompt = true;
        });
        // Prompt kết thúc từ bất kỳ đâu (trả lời / máy Stop hủy prompt) → tự dọn banner
        _ = e.Decision.ContinueWith(_ => RunOnUi(ClearPrompt),
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private void ClearPrompt()
    {
        _activePrompt = null;
        HasOperatorPrompt = false;
        PromptText = string.Empty;
    }

    /// <summary>Operator chọn Thử lại — chạy lại bước lỗi (đếm retry lại từ đầu).</summary>
    [RelayCommand]
    private void PromptRetry() => _activePrompt?.Respond(StepErrorAction.Retry);

    /// <summary>Operator chọn Bỏ qua bước lỗi (chỉ Engineer+ thấy nút).</summary>
    [RelayCommand]
    private void PromptSkip() => _activePrompt?.Respond(StepErrorAction.Skip);

    /// <summary>Operator chọn Dừng máy (Abort) — máy vào trạng thái lỗi, cần Reset.</summary>
    [RelayCommand]
    private void PromptAbort() => _activePrompt?.Respond(StepErrorAction.Abort);

    // ─── Service prompt (IOperatorPrompt — station hỏi lúc init, P1.6) ─────────

    private void OnServicePrompt(object? sender, Sequencing.ServicePromptEventArgs e)
    {
        RunOnUi(() =>
        {
            _activeServicePrompt = e;
            ServicePromptText = $"{e.Request.Source} · {e.Request.Message}";
            ServicePromptChoices.Clear();
            foreach (string c in e.Request.Choices) ServicePromptChoices.Add(c);
            HasServicePrompt = true;
        });
        _ = e.Decision.ContinueWith(_ => RunOnUi(ClearServicePrompt),
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private void ClearServicePrompt()
    {
        _activeServicePrompt = null;
        HasServicePrompt = false;
        ServicePromptText = string.Empty;
        ServicePromptChoices.Clear();
    }

    /// <summary>Operator bấm một lựa chọn của service prompt (nút động trên banner).</summary>
    [RelayCommand]
    private void RespondServicePrompt(string choice) => _activeServicePrompt?.Respond(choice);

    /// <summary>ACK alarm đang hiển thị trên banner (ưu tiên cao nhất chưa ACK).</summary>
    [RelayCommand]
    private async Task AcknowledgeAlarm()
    {
        var top = HighestUnacked();
        if (top is null) return;
        await _alarm.AcknowledgeAsync(top.AlarmCode, _user.CurrentUser ?? "operator").ConfigureAwait(true);
        RefreshAlarm(); // Acknowledge không raise event — refresh để alarm kế tiếp trồi lên
    }

    // ─── Event handlers ───────────────────────────────────────────────────────
    private void OnStateChanged(object? sender, MachineStateChangedEventArgs e) => RunOnUi(RefreshState);
    private void OnAlarmChanged(object? sender, AlarmEventArgs e) => RunOnUi(RefreshAlarm);
    private void OnRecipeChanged(object? sender, RecipeEventArgs e) => RunOnUi(RefreshRecipe);
    private void OnUserChanged(object? sender, UserChangedEventArgs e) => RunOnUi(RefreshUser);
    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => RunOnUi(() => { RefreshState(); RefreshUser(); RefreshAlarm(); });

    private void OnTick(object? sender, EventArgs e)
    {
        ClockText = DateTime.Now.ToString("HH:mm · dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var d in _hardware.GetMonitoredDevices())
        {
            var chip = DeviceConnections.FirstOrDefault(c => c.Name == d.Name)
                    ?? HostConnections.FirstOrDefault(c => c.Name == d.Name);
            if (chip is not null) chip.Connected = d.Device.IsConnected;
        }
        RefreshConnectionSummary();
        _ = RefreshSecurityNoticeAsync(); // kết quả cache trong UserService — rẻ sau lần tính đầu
    }

    // Banner vàng thường trực khi còn tài khoản dùng mật khẩu mặc định — tắt trong ~1s sau khi đổi hết.
    private async Task RefreshSecurityNoticeAsync()
    {
        try
        {
            bool warn = await _user.HasDefaultPasswordsAsync().ConfigureAwait(false); // RunOnUi tự về UI thread
            if (warn == HasSecurityNotice) return;
            RunOnUi(() =>
            {
                HasSecurityNotice = warn;
                RefreshAlarm();
            });
        }
#pragma warning disable CA1031 // kiểm tra nền lỗi không được phá tick UI — chỉ ghi debug
        catch (Exception ex)
#pragma warning restore CA1031
        {
            System.Diagnostics.Debug.WriteLine($"RefreshSecurityNotice lỗi: {ex}");
        }
    }

    /// <summary>Tổng hợp "n online / m tổng" cho chip kết nối — gọi sau mỗi lần cập nhật chips.</summary>
    private void RefreshConnectionSummary()
    {
        int devOk = DeviceConnections.Count(c => c.Connected);
        int hostOk = HostConnections.Count(c => c.Connected);
        DeviceOnlineText = $"{devOk}/{DeviceConnections.Count}";
        HostOnlineText = $"{hostOk}/{HostConnections.Count}";
        AllConnectionsOk = devOk == DeviceConnections.Count && hostOk == HostConnections.Count;
    }

    private void RefreshState()
    {
        MachineState = _master.State;
        StateText = Loc.Strings[$"State.{_master.State}"];
        ModeText = Loc.Strings[$"Mode.{_master.OperationMode}"];
        PauseResumeText = Loc.Strings[MachineState == MachineState.Paused ? "Dash.Btn.Resume" : "Dash.Btn.Pause"];
    }

    private void RefreshRecipe() => RecipeText = _recipe.ActiveRecipe?.Name ?? "—";

    private void RefreshUser()
    {
        UserText = _user.IsLoggedIn
            ? $"{_user.CurrentUser} · {_user.CurrentLevel}"
            : Loc.Strings["Shell.Guest"];
        CanOpenManual = _user.CurrentLevel >= UserLevel.LineLead; // tab Vận hành tay gate LineLead+
    }

    /// <summary>Alarm chưa ACK ưu tiên cao nhất (Level desc → mới nhất trước).</summary>
    private AlarmModel? HighestUnacked()
        => _alarm.ActiveAlarms
            .Where(a => !a.IsAcknowledged)
            .OrderByDescending(a => a.Level)
            .ThenByDescending(a => a.RaisedAt)
            .FirstOrDefault();

    private void RefreshAlarm()
    {
        var unackedCount = _alarm.ActiveAlarms.Count(a => !a.IsAcknowledged);
        var top = HighestUnacked();
        HasUnackedAlarm = top is not null;
        ExtraAlarmCount = Math.Max(0, unackedCount - 1);
        HasExtraAlarms = ExtraAlarmCount > 0;
        if (top is not null)
        {
            IsBannerError = top.Level >= AlarmLevel.High;
            BannerText = $"[{top.AlarmCode}] {top.Message} · "
                + $"{top.RaisedAt.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)}"
                + $" · {top.Station}";
        }
        else if (HasSecurityNotice)
        {
            // Cảnh báo bảo mật chỉ chiếm banner khi KHÔNG có alarm — alarm luôn ưu tiên hơn
            IsBannerError = false;
            BannerText = Loc.Strings["Shell.DefaultPwdWarn"];
        }
        else
        {
            IsBannerError = false;
            BannerText = Loc.Strings["Shell.NoAlarm"];
        }
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
        _engine.OperatorPromptRequired -= OnOperatorPrompt;
        _promptService.PromptRequested -= OnServicePrompt;
        Loc.Strings.PropertyChanged -= OnLanguageChanged;
    }
}
