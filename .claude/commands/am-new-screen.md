# /am-new-screen

Tạo màn hình WPF hoàn chỉnh: View + ViewModel + Prism Module.

## Usage
```
/am-new-screen {ModuleName} {ScreenName} {Level} {Description}
```
Level: 1=Overview, 2=Workstation, 3=Detail/Faceplate, 4=Engineering

> 🎯 **Target: IPC 21–24" / 1920×1080, chuột + cảm ứng** (không phải HMI panel nhỏ).
> Nội dung KHÔNG giãn hết 1920px — khối dữ liệu ≤ ~1400px, chia cột. Header có nút lệnh toàn cục,
> Alarm bar + Status bar (chip kết nối) là 2 dải riêng. Nút chính ≥60×60, nút thường ≥44×44, gap ≥8px.

## What this command does
1. Read skill `am-hmi-design` (layout/màu/touch IPC) + `am-wpf-mvvm` (template) + `docs/HMI_Components_Catalog.md` (checklist thành phần cho màn này)
2. Create `{ScreenName}View.xaml` + `{ScreenName}View.xaml.cs` (minimal code-behind)
3. Create `{ScreenName}ViewModel.cs` (CommunityToolkit.Mvvm + ObservableObject)
4. Create or update `{ModuleName}Module.cs` with navigation registration
5. Add localization key placeholders (vi-VN format)
6. Apply theme tokens — NO hardcoded colors or strings

## Examples
```
/am-new-screen Alarm AlarmList 2 "Real-time alarm display with filter and acknowledge"
/am-new-screen Production UPHDashboard 2 "Hourly production chart and yield stats"
/am-new-screen Debug AxisFaceplate 3 "Single axis manual control and monitoring"
/am-new-screen Parameter RecipeEditor 4 "Recipe parameter editor for Engineers"
```

## ISA-101 compliance is automatic
- Strings via `{lang:Text Key='...'}`
- Colors via `{DynamicResource ...Brush}`
- Live values: Bold, +2pt vs label
- Dangerous buttons: `Button.DangerStyle`, ≥48px gap
- Loading indicator when `IsBusy = true`
- `IDisposable` with event unsubscribe
