# /am-new-screen

Tạo màn hình WPF hoàn chỉnh: View + ViewModel + Prism Module.

## Usage
```
/am-new-screen {ModuleName} {ScreenName} {Level} {Description}
```
Level: 1=Overview, 2=Workstation, 3=Detail/Faceplate, 4=Engineering

## What this command does
1. Read skill `am-wpf-mvvm` for templates and ISA-101 rules
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
