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

---

## ISA-101 HMI Design Rules — Bắt buộc áp dụng

### Triết lý: 90% xám, 10% màu có ý nghĩa

Màu sắc mang **ý nghĩa ngữ nghĩa**, không trang trí. Nền toàn bộ là tông xám trung tính.
Màu chỉ xuất hiện khi có trạng thái cần thông báo.

### Semantic Colors — BẤT BIẾN (giống nhau ở cả Dark và Light theme)

```xml
<!-- Trong Colors.Dark.xaml VÀ Colors.Light.xaml — giá trị HEX GIỐNG NHAU -->
<Color x:Key="Status.NormalColor">#4CAF50</Color>      <!-- Running / On -->
<Color x:Key="Status.WarningColor">#FFC107</Color>     <!-- Warning / Advisory -->
<Color x:Key="Status.AlarmColor">#F44336</Color>       <!-- Alarm / Error -->
<Color x:Key="Status.CriticalColor">#B71C1C</Color>    <!-- Critical / E-Stop -->
<Color x:Key="Status.DisabledColor">#9E9E9E</Color>    <!-- Off / Disabled -->
<Color x:Key="Status.AcknowledgedColor">#FF8F00</Color><!-- Alarm acked, chưa clear -->
<Color x:Key="Status.ManualColor">#1E88E5</Color>      <!-- Manual mode -->
<Color x:Key="Status.InterlockColor">#7B1FA2</Color>   <!-- Interlock active -->
```

### Background Tokens — THAY ĐỔI theo theme

```
Token                    Dark       Light      Mô tả
─────────────────────────────────────────────────────────────
Screen.BackgroundBrush   #1A1A1A   #F0F2F5   Nền toàn màn hình
Panel.BackgroundBrush    #252525   #FFFFFF   Nền card / panel
Panel.Background.AltBrush#2D2D2D  #F8F9FA   Zebra rows
Header.BackgroundBrush   #1F1F1F   #E8ECF0   Top bar, side menu
Equipment.NormalBrush    #3A3A3A   #D0D5DB   Thiết bị normal (KHÔNG dùng xanh lá)
Border.DefaultBrush      #3D3D3D   #D1D5DB   Viền panel nhẹ
Border.StrongBrush       #555555   #9CA3AF   Viền nổi bật
Text.PrimaryBrush        #E0E0E0   #212121   Text chính
Text.SecondaryBrush      #9E9E9E   #757575   Label, text phụ
Text.LiveValueBrush      #FFFFFF   #000000   Giá trị live (đậm nhất)
Chart.BackgroundBrush    #0D1B2A   #F8FAFC   Nền biểu đồ
```

### Quy tắc màu KHÔNG ĐƯỢC VI PHẠM

```
❌ KHÔNG dùng màu đỏ cho bất cứ thứ gì NGOÀI alarm/error
❌ KHÔNG dùng màu vàng cho trang trí
❌ KHÔNG hardcode hex màu trong Controls.xaml — dùng {DynamicResource TokenBrush}
❌ KHÔNG dùng màu xanh lá cho thiết bị ở trạng thái bình thường (dùng xám)
❌ KHÔNG dùng animation trừ khi có ý nghĩa (chỉ: Critical alarm nhấp nháy 1 Hz)
✅ Equipment normal: màu XÁM — thiết bị chạy bình thường KHÔNG phải xanh lá
✅ Alarm nhấp nháy: chỉ Critical = 1 Hz, E-Stop = 2 Hz — không vượt 3 Hz (SEMI S8)
✅ Mọi màu kết hợp thêm icon hoặc text (hỗ trợ người mù màu ~8% nam giới)
```

### Layout màn hình chuẩn (ISA-101) — IPC 1920×1080, 21–24"

> Chi tiết kích thước/màu/touch: skill `am-hmi-design`. Tóm tắt: nội dung KHÔNG giãn hết 1920px (khối ≤ ~1400px, chia cột).

```
┌─────────────────────────────────────────────────────────────────────┐
│ HEADER (80–96px): Logo|Machine|State|Mode|Recipe|đèn tháp|[Start/Stop/Reset]|User|Lang|Clock │
├──────────────┬──────────────────────────────────────────────────────┤
│ NAV          │                                                      │
│ (220–260px,  │          CONTENT AREA (lưới nhiều cột, ≤~1400px)     │
│  collapse    │          (Level 1-4 screens)                         │
│  →64px icon) │                                                      │
├──────────────┴──────────────────────────────────────────────────────┤
│ ALARM BAR (48–56px): alarm mới nhất (đỏ nếu active) + [Acknowledge]  │
├──────────────────────────────────────────────────────────────────────┤
│ STATUS BAR (32–40px): chip kết nối PLC RFID CAM MES HIVE SECS/GEM DB │
└──────────────────────────────────────────────────────────────────────┘
```
Touch (đeo găng — SEMI S8): nút chính ≥60×60px · nút thường ≥44×44px · gap ≥8px · nút nguy hiểm cách ≥48px.

### Phân cấp màn hình (4 Levels)

```
Level 1 — Overview: tổng quan toàn máy, state, alarm count, UPH
Level 2 — Process Area: chi tiết một station/khu vực, live values
Level 3 — Faceplate: chi tiết 1 thiết bị (popup/flyout), jog, set
Level 4 — Engineering/Diagnostic: yêu cầu quyền Engineer+, không dùng khi sản xuất
```

### Button Rules (kích thước bắt buộc)

```
Primary action:  120 × 40 px  — Start, Confirm (accent color)
Danger action:   120 × 40 px  — Stop, Delete (đỏ #F44336)
E-Stop:           80 × 80 px  — Đỏ đậm #B71C1C, góc trên phải, LUÔN accessible
Secondary:       100 × 36 px  — Cancel, Back (xám viền)
Touch target tối thiểu: 44 × 44 px (touchscreen) / 60 × 60 px (găng tay)
Khoảng cách giữa nút nguy hiểm: tối thiểu 48 px
```

### DataTrigger cho trạng thái — cách đúng trong WPF

```xml
<Style x:Key="DeviceStatusStyle" TargetType="Border">
  <!-- Nền thiết bị: theo theme (xám) -->
  <Setter Property="Background" Value="{DynamicResource Equipment.NormalBrush}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="BorderBrush" Value="{DynamicResource Border.DefaultBrush}"/>
  <Style.Triggers>
    <!-- Viền màu khi có trạng thái — StaticResource vì semantic không đổi theo theme -->
    <DataTrigger Binding="{Binding State}" Value="Running">
      <Setter Property="BorderBrush" Value="{StaticResource Status.NormalBrush}"/>
      <Setter Property="BorderThickness" Value="2"/>
    </DataTrigger>
    <DataTrigger Binding="{Binding State}" Value="Warning">
      <Setter Property="BorderBrush" Value="{StaticResource Status.WarningBrush}"/>
      <Setter Property="BorderThickness" Value="2"/>
    </DataTrigger>
    <DataTrigger Binding="{Binding State}" Value="Fault">
      <Setter Property="BorderBrush" Value="{StaticResource Status.AlarmBrush}"/>
      <Setter Property="BorderThickness" Value="2"/>
    </DataTrigger>
  </Style.Triggers>
</Style>
```

### Animation chỉ cho Critical Alarm

```xml
<!-- ĐÚNG — chỉ animate Critical alarm -->
<DataTrigger Binding="{Binding IsCriticalAlarm}" Value="True">
  <DataTrigger.EnterActions>
    <BeginStoryboard>
      <Storyboard RepeatBehavior="Forever">
        <DoubleAnimation Storyboard.TargetProperty="Opacity"
                         From="1" To="0.3" Duration="0:0:0.5" AutoReverse="True"/>
      </Storyboard>
    </BeginStoryboard>
  </DataTrigger.EnterActions>
</DataTrigger>
<!-- ❌ SAI — không animate trang trí, không vượt 3 Hz (SEMI S8 epilepsy) -->
```

### Virtualization cho danh sách dài (Alarm, Log)

```xml
<ListView VirtualizingPanel.IsVirtualizing="True"
          VirtualizingPanel.VirtualizationMode="Recycling"
          ScrollViewer.IsDeferredScrollingEnabled="True">
```

### Theme Switcher — đổi Dark/Light runtime không restart

```csharp
public static class ThemeService
{
    public static void SwitchTheme(AppTheme theme)
    {
        var dicts = Application.Current.Resources.MergedDictionaries;
        // Xoá theme cũ, load theme mới — WPF tự cập nhật mọi DynamicResource
        var old = dicts.FirstOrDefault(d => d.Source?.OriginalString.Contains("Colors.") == true);
        if (old != null) dicts.Remove(old);
        var path = theme == AppTheme.Dark ? "Themes/Colors.Dark.xaml" : "Themes/Colors.Light.xaml";
        dicts.Insert(0, new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });
        CurrentTheme = theme;
    }
}
```

### Số liệu và đơn vị — format chuẩn

```
Position (mm):   2 chữ số thập phân  → "123.45 mm"
Velocity (mm/s): 1 chữ số            → "50.0 mm/s"
Temperature:     1 chữ số            → "25.3 °C"
Counter:         0 chữ số            → "1234"
Luôn: 1 space giữa số và đơn vị     → "42.3 °C" không phải "42.3°C"
Thời gian:       24h format          → "14:32:05" không AM/PM
Timestamp alarm: với milliseconds    → "14:32:05.123"
```

### ISA-101 Checklist trước khi release màn hình mới

```
□ Nền xám (không có màu trang trí không có ý nghĩa)
□ Màu đỏ/vàng CHỈ cho alarm/warning
□ Equipment ở trạng thái bình thường = màu XÁM
□ Screenshot grayscale → vẫn đọc được thông tin
□ Contrast text/nền ≥ 4.5:1
□ E-Stop button lớn nhất, đỏ đậm, góc cố định
□ Khoảng cách nút nguy hiểm ≥ 48 px
□ Touch target ≥ 44 × 44 px
□ Mọi giá trị số có đơn vị
□ Alarm bar visible trên mọi màn hình (Status Bar)
□ Không hardcode màu hex trong XAML — dùng {DynamicResource}
□ Đổi theme Dark/Light không mất thông tin
□ Font ≥ 12 pt, giá trị live to hơn label ≥ 2 pt
```
