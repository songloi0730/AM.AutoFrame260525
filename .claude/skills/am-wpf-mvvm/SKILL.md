---
name: am-wpf-mvvm
description: WPF MVVM templates cho AM.AutoFrame — ViewModel, XAML, Prism Module, ISA-101 compliance
---

# AM WPF MVVM Patterns

## NuGet packages (UI project)

```xml
<ItemGroup>
  <PackageReference Include="Prism.DryIoc" Version="9.*" />
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
  <PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.*" />
</ItemGroup>
```

---

## ViewModel Template

```csharp
// -------------------------------------------------------
// File:    DashboardViewModel.cs
// Project: AM.Modules.Dashboard
// Purpose: ViewModel cho màn hình Dashboard
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Dashboard.ViewModels;

/// <summary>
/// ViewModel cho màn hình Dashboard chính.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IMasterController _controller;
    private readonly IAlarmService _alarmService;
    private readonly ILogger<DashboardViewModel> _logger;
    private bool _disposed;

    /// <summary>Trạng thái máy hiện tại.</summary>
    [ObservableProperty]
    private MachineState _machineState = MachineState.Uninitialized;

    /// <summary>Chế độ vận hành.</summary>
    [ObservableProperty]
    private OperationMode _operationMode = OperationMode.Normal;

    /// <summary>Danh sách alarm đang active.</summary>
    [ObservableProperty]
    [SuppressMessage("Major Code Smell", "S2365",
        Justification = "ObservableCollection is a live binding collection, not a copy")]
    private ObservableCollection<AlarmModel> _activeAlarms = [];

    /// <summary>Trạng thái đang xử lý (hiển thị spinner).</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Khởi tạo DashboardViewModel.
    /// </summary>
    public DashboardViewModel(
        IMasterController controller,
        IAlarmService alarmService,
        ILogger<DashboardViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(logger);

        _controller = controller;
        _alarmService = alarmService;
        _logger = logger;

        // Subscribe events
        _controller.StateChanged += OnMachineStateChanged;
        _alarmService.AlarmRaised += OnAlarmRaised;
        _alarmService.AlarmCleared += OnAlarmCleared;
    }

    // ── Commands ────────────────────────────────────────

    /// <summary>Lệnh Initialize máy.</summary>
    [RelayCommand(CanExecute = nameof(CanInitialize))]
    private async Task InitializeAsync()
    {
        _logger.LogDebug("Starting {Method}", nameof(InitializeAsync));
        IsBusy = true;
        try
        {
            await _controller.InitializeAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanInitialize() =>
        MachineState is MachineState.Uninitialized;

    /// <summary>Lệnh Start máy.</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        IsBusy = true;
        try
        {
            await _controller.StartAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanStart() =>
        MachineState is MachineState.Idle;

    /// <summary>Lệnh Stop máy.</summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync() =>
        await _controller.StopAsync().ConfigureAwait(false);

    private bool CanStop() =>
        MachineState is MachineState.Running or MachineState.Paused;

    /// <summary>Lệnh Reset sau alarm.</summary>
    [RelayCommand(CanExecute = nameof(CanReset))]
    private async Task ResetAsync() =>
        await _controller.ResetAsync().ConfigureAwait(false);

    private bool CanReset() =>
        MachineState is MachineState.InitAlarm or MachineState.RunAlarm;

    // ── Event Handlers ───────────────────────────────────

    private void OnMachineStateChanged(object? sender, MachineState newState)
    {
        // Dispatch về UI thread
        App.Current.Dispatcher.Invoke(() =>
        {
            MachineState = newState;
            // Notify command CanExecute
            InitializeCommand.NotifyCanExecuteChanged();
            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
            ResetCommand.NotifyCanExecuteChanged();
        });
    }

    private void OnAlarmRaised(object? sender, AlarmEventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            ActiveAlarms.Add(e.Alarm);
        });
    }

    private void OnAlarmCleared(object? sender, AlarmEventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            var found = ActiveAlarms.Find(a => a.Code == e.Alarm.Code);
            if (found is not null) ActiveAlarms.Remove(found);
        });
    }

    // ── IDisposable ──────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _controller.StateChanged -= OnMachineStateChanged;
        _alarmService.AlarmRaised -= OnAlarmRaised;
        _alarmService.AlarmCleared -= OnAlarmCleared;
        _disposed = true;
    }
}
```

---

## XAML View Template

```xml
<!-- DashboardView.xaml -->
<UserControl x:Class="AM.Modules.Dashboard.Views.DashboardView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:vm="clr-namespace:AM.Modules.Dashboard.ViewModels"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:DashboardViewModel}">

    <Grid>
        <!-- State Machine Status Bar (ISA-101: top bar) -->
        <DockPanel DockPanel.Dock="Top">
            <TextBlock Text="{Binding MachineState}"
                       FontSize="16" FontWeight="Bold"
                       Foreground="{Binding MachineState,
                           Converter={StaticResource StateToColorConverter}}" />
        </DockPanel>

        <!-- Command Buttons (ISA-101: prominent placement) -->
        <StackPanel Orientation="Horizontal" DockPanel.Dock="Bottom">
            <Button Content="Initialize"
                    Command="{Binding InitializeCommand}"
                    Style="{StaticResource PrimaryButtonStyle}" />
            <Button Content="Start"
                    Command="{Binding StartCommand}"
                    Style="{StaticResource StartButtonStyle}" />
            <Button Content="Stop"
                    Command="{Binding StopCommand}"
                    Style="{StaticResource StopButtonStyle}" />
            <Button Content="Reset"
                    Command="{Binding ResetCommand}"
                    Style="{StaticResource ResetButtonStyle}" />
        </StackPanel>

        <!-- Alarm List -->
        <ListView ItemsSource="{Binding ActiveAlarms}"
                  DockPanel.Dock="Bottom">
            <ListView.ItemTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="{Binding Code}" Width="80" />
                        <TextBlock Text="{Binding Message}" />
                    </StackPanel>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>

        <!-- Busy Overlay -->
        <Grid Visibility="{Binding IsBusy, Converter={StaticResource BoolToVisibilityConverter}}">
            <Rectangle Fill="#80000000" />
            <ProgressBar IsIndeterminate="True" Width="200" Height="20" />
        </Grid>
    </Grid>
</UserControl>
```

---

## Code-Behind Template (minimal)

```csharp
// -------------------------------------------------------
// File:    DashboardView.xaml.cs
// Project: AM.Modules.Dashboard
// Purpose: Code-behind cho DashboardView — chỉ InitializeComponent
// -------------------------------------------------------

namespace AM.Modules.Dashboard.Views;

/// <summary>Dashboard screen — code-behind minimal, logic in ViewModel.</summary>
public partial class DashboardView
{
    /// <summary>Initializes a new instance of DashboardView.</summary>
    public DashboardView() => InitializeComponent();
}
```

---

## Prism Module Template

```csharp
// -------------------------------------------------------
// File:    DashboardModule.cs
// Project: AM.Modules.Dashboard
// Purpose: Prism module đăng ký Views và ViewModels cho Dashboard
// -------------------------------------------------------

using AM.Modules.Dashboard.ViewModels;
using AM.Modules.Dashboard.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace AM.Modules.Dashboard;

/// <summary>
/// Prism module cho Dashboard screen.
/// Được load từ ModuleCatalog trong Bootstrapper.
/// </summary>
public sealed class DashboardModule : IModule
{
    /// <inheritdoc/>
    public void OnInitialized(IContainerProvider containerProvider)
    {
        // Navigate đến DashboardView khi module load
        var regionManager = containerProvider.Resolve<IRegionManager>();
        regionManager.RegisterViewWithRegion<DashboardView>(RegionNames.MainContent);
    }

    /// <inheritdoc/>
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>();
    }
}
```

---

## [ModuleNavigation] Attribute Usage

```csharp
// Đặt trên View class để sidebar tự tạo menu item
[ModuleNavigation(
    displayName: "Dashboard",
    icon: "view-dashboard",
    region: RegionNames.MainContent,
    order: 0)]
public partial class DashboardView : UserControl { ... }
```

---

## ISA-101 HMI Compliance Checklist

- [ ] **State display**: Trạng thái máy luôn hiển thị rõ ràng (màu sắc + text)
- [ ] **E-Stop**: Nút Emergency Stop luôn accessible, không bị block bởi modal
- [ ] **Command acknowledgment**: Mọi command cho user biết đang xử lý (spinner/busy)
- [ ] **Alarm visibility**: Alarm list luôn visible, không cần navigate
- [ ] **Consistent colors**: Green=Normal/Running, Yellow=Warning/Paused, Red=Alarm/Error
- [ ] **No data loss on navigate**: ViewModel persists khi navigate qua lại
- [ ] **Confirmation dialogs**: Destructive actions (Reset, E-Stop) cần confirm
- [ ] **Disable unavailable**: Command buttons disabled khi không hợp lệ (CanExecute)
- [ ] **IDisposable**: ViewModel implement IDisposable, unsubscribe events

---

## Theme Token Reference

```xaml
<!-- Colors từ AM.Application.Shell/Themes/Colors.xaml -->
<SolidColorBrush x:Key="StateRunningBrush"      Color="#2ECC71" />
<SolidColorBrush x:Key="StatePausedBrush"       Color="#F39C12" />
<SolidColorBrush x:Key="StateAlarmBrush"        Color="#E74C3C" />
<SolidColorBrush x:Key="StateIdleBrush"         Color="#3498DB" />
<SolidColorBrush x:Key="StateUninitializedBrush" Color="#95A5A6" />

<!-- Button Styles từ AM.Application.Shell/Themes/Controls.xaml -->
<Style x:Key="PrimaryButtonStyle"  TargetType="Button" />
<Style x:Key="StartButtonStyle"    TargetType="Button" /> <!-- Green -->
<Style x:Key="StopButtonStyle"     TargetType="Button" /> <!-- Red -->
<Style x:Key="ResetButtonStyle"    TargetType="Button" /> <!-- Orange -->
```

---

## StateToColorConverter Template

```csharp
// Đặt trong AM.Application.Shell/Converters/
public sealed class StateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not MachineState state) return Brushes.Gray;

        return state switch
        {
            MachineState.Running     => Brushes.LimeGreen,
            MachineState.Paused      => Brushes.Orange,
            MachineState.InitAlarm
            or MachineState.RunAlarm => Brushes.Red,
            MachineState.Idle        => Brushes.DodgerBlue,
            MachineState.Initializing
            or MachineState.Resetting => Brushes.Yellow,
            _                        => Brushes.Gray,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```
