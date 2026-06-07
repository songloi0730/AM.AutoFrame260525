# CHANGELOG — AM.AutoFrame
> Ghi lại mọi thay đổi có ý nghĩa theo từng session làm việc.
> Format: `## [Session N] YYYY-MM-DD — Tiêu đề ngắn`

---

## [Session 33] 2026-06-07 — Mở maximized + chữ to chuẩn công nghiệp + icon Segoe MDL2

**Commit:** `84bce47`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User: muốn mở full màn ngay; maximize bị viền đen phải/dưới; chữ hơi bé; icon emoji chưa hợp công nghiệp.

### ✅ Khắc phục
1. **Mở maximized ngay:** `WindowState=Maximized` (vẫn có title bar, ResizeMode=CanResize).
2. **Hết viền đen khi maximize:** bỏ `MaxWidth/MaxHeight` (trước set = WorkArea làm window không lấp đầy được).
   `ClampToWorkArea` giờ chỉ giới hạn kích thước **restore** (khi rời maximize) để không tràn ở scale >100%.
3. **Chữ to hơn (ISA-101: data 16–20pt, label 12–14pt):** Window `FontSize=15` base; bump Font tokens trong App.xaml
   (Body 13→15, Small 11→13, H2 18→22, H3 16→18, LiveValue 15→18, KpiValue 28→32, H1 24→28). Nav label 15, nút nav cao 48.
4. **Icon chuẩn:** thay emoji bằng **Segoe MDL2 Assets** (icon hệ thống Windows). Lưu **mã hex** (ASCII) → convert glyph
   runtime (`(char)0xE80F`) để source không chứa ký tự PUA. Map: Home/Ringer/Connect/Settings/Document/Contact + hamburger.

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error** (24 projects). `dotnet test` → **143 passed**.

---

## [Session 39] 2026-06-07 — Engineering/Debug UI (tiêu thụ [StationUI]/[MechanismUI] + chạy SubRoutine) — KHÉP NỀN

**Commit:** `f02b39d`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục 7 (cuối) — màn kỹ sư bring-up máy: `[StationUI]`/`[MechanismUI]` đã gắn nhưng chưa ai đọc.

### ✅ AM.Modules.Engineering (project mới — thứ 25)
- `EngineeringViewModel`: **auto-discovery** Station/Mechanism từ `IMasterController.Stations`, đọc metadata
  `[StationUI]`/`[MechanismUI]` qua **reflection** (DisplayName/Group). Poll **Ready/Busy** live (500ms).
- `StationVm`/`MechanismVm`/`SubRoutineVm`. **Chạy SubRoutine** qua `ISubRoutineRunner` (gate quyền/state, bắt
  Unauthorized/InvalidOperation/Alarm → StatusMessage). **E-Stop từng cụm** (`IMechanism.EmergencyStop`).
- `EngineeringView.xaml`: cột trái Station→Mechanism (đèn Ready/Busy + E-Stop), cột phải nút SubRoutine (tên+mô tả+quyền).
- `[ModuleNavigation("Nav.Engineering", order: 80)]` + i18n (Kỹ thuật/Engineering/工程) + glyph Segoe MDL2.
- Wire Shell: ProjectReference + `AddUiViewModels` + .sln + nav glyph.

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error** (25 projects). `dotnet test` → **165 passed**.

### 🏁 KHÉP NỀN WorkStation (mục 1–7 trong gap analysis ĐÃ XONG)
1. StepSequence · 2. AxisMap/MachineConfig (+concrete IAxis) · 3. Recipe extensibility · 4. Safety interlock+andon ·
5. wire Production · 6. SubRoutines · 7. Engineering UI.
> **Đã đủ nền để dựng `AM.WorkStation.{Máy}` thật.** Còn lại là phần TÙY MÁY (driver hãng, recipe/steps/mechanisms theo máy)
> và các UI module hiển thị (Production/Vision/Logging/Diagnostics).

---

## [Session 38] 2026-06-07 — SubRoutines base (Home/Calibration/SafetyCheck chạy tay, gate quyền/state)

**Commit:** `265eb1f`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục 6 gap — thao tác setup/bảo trì chạy tay ngoài auto-cycle cần khung tái dùng + gate an toàn.

### ✅ Khung SubRoutine
- `ISubRoutine` (Abstractions): Name/Description/`RequiredLevel`/IsBusy/`ExecuteAsync`.
- **`SubRoutineBase`** (Infrastructure): busy-guard (không chạy đồng thời) + log; subclass chỉ viết `ExecuteCoreAsync`.
- `ISubRoutineRunner` + **`SubRoutineRunner`** (AM.Services): UI gọi runner, runner **gate**:
  quyền (`IUserService.HasPermission`) → `UnauthorizedAccessException`; trạng thái máy (KHÔNG chạy khi Running/Paused)
  → `InvalidOperationException`; bọc `AlarmException` → raise alarm + ném lại.
- Demo: `HomeAllSubRoutine` (Engineer, `DemoStation.HomeAsync`) + `SafetyCheckSubRoutine` (Operator, kiểm tra `ISafetyInput`).
- DI: đăng ký từng subroutine `ISubRoutine` + `ISubRoutineRunner` trong `AddDemoMachine` (máy khác thêm subroutine của mình).
- Test: `SubRoutineRunnerTests` (chạy khi đủ quyền+Idle · chặn thiếu quyền · chặn khi Running · tên lạ · raise alarm).

### 🔍 Kết quả
- `dotnet build` → **0 Error**. `dotnet test` → **165 passed** (Services 70→75).

> Còn lại trước khi dựng máy: mục 7 (Debug/Engineering UI tiêu thụ `[MechanismUI]`/`[StationUI]` + chạy SubRoutine).

---

## [Session 37] 2026-06-07 — Wire Production: CycleCompleted → tự ghi ProductionRecord (UPH/yield/SN)

**Commit:** `651c25f`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục 5 gap — `IProductionService.RecordAsync` có sẵn nhưng KHÔNG ai gọi → chưa có số liệu UPH/yield thật.

### ✅ ProductionRecorder
- `IProductionRecorder` (Abstractions) + **`ProductionRecorder`** (AM.Services): lắng nghe `IMasterController.CycleCompleted`
  → sinh **serial number** (ngày + số tăng dần) + ghi `ProductionRecord` (IsPassed=true, `CycleTimeMs` từ
  `CycleDurationMs`, recipe đang chạy) qua `IProductionService.RecordAsync`.
- **Captive dependency**: recorder là Singleton nhưng `IProductionService` là Scoped (EF DbContext) → tạo **scope mỗi lần ghi**
  bằng `IServiceScopeFactory` (đúng chuẩn DI). Lỗi ghi không làm sập sequence (try/catch + log).
- Model fault-stop: mỗi cycle hoàn thành = 1 PASS (NG ném AlarmException → không có CycleCompleted). Máy chạy
  reject-and-continue có thể ghi NG trực tiếp qua `IProductionService.RecordAsync`.
- DI: đăng ký singleton + `Start()` ở App (sau watchdog/towerlight).
- Test: `ProductionRecorderTests` (ghi PASS record đúng cycle-time/recipe/SN · SN duy nhất mỗi cycle · Dispose hủy đăng ký)
  bằng Moq (mock `IServiceScopeFactory`).

### 🔍 Kết quả
- `dotnet build` → **0 Error**. `dotnet test` → **160 passed** (Services 67→70).
- Chạy máy: mỗi cycle hoàn thành tự ghi 1 record → Dashboard/Production có UPH/yield/cycle-time thật.

> Còn lại trước khi dựng máy: mục 6 (SubRoutines base), 7 (Debug/Engineering UI). Production UI module (mục B) hiển thị stats.

---

## [Session 36] 2026-06-07 — Recipe extensibility: RecipeBase + recipe theo máy (bỏ cứng P&P)

**Commit:** `f2626c2`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục 3 gap — `Recipe` đang cứng field Pick&Place; máy khác không khớp. Tách base + recipe theo máy.

### ✅ Refactor Recipe đa hình
- **`RecipeBase`** (Core, abstract): chỉ metadata chung (Id/Name/ProductCode/Version/CreatedAt/ModifiedAt/ModifiedBy/IsActive).
- Xoá `Recipe.cs` (cứng P&P) → **`PickPlaceRecipe : RecipeBase`** chuyển về **AM.WorkStation.Demo/Recipe/** (recipe THEO MÁY,
  giữ [ParamView] P&P). Máy khác tạo lớp recipe riêng.
- `IRecipeService`/`RecipeEventArgs`/`ParameterViewModel` làm việc qua **`RecipeBase`** (đa hình).
- **`RecipeService` không còn cứng**: bỏ seed P&P hardcode → ctor nhận `IEnumerable<RecipeBase>? seedRecipes` (máy cung cấp);
  **`ValidateAsync` attribute-driven** (reflect `[ParamView]` Min/Max + Name/ProductCode bắt buộc) → đúng cho MỌI loại recipe.
- **`ParameterViewModel`**: reflect `[ParamView]` theo **runtime type** của recipe + **Clone đa hình** (JSON round-trip) →
  form tự render đúng tham số của từng máy, không sửa code.
- DI: `IRecipeService` chuyển sang `AddDemoMachine` kèm **seed `PickPlaceRecipe` mặc định** (máy khác seed recipe riêng).
- Demo: `DemoPickMechanism`/`Step02Inspect` nhận `PickPlaceRecipe`; `DemoStation` cast `ActiveRecipe as PickPlaceRecipe`.

### 🔍 Kết quả
- `dotnet build` → **0 Error**. `dotnet test` → **157 passed** (`RecipeServiceTests` viết lại dùng `TestRecipe : RecipeBase`
  + validate attribute-driven).

> Còn lại trước khi dựng máy: mục 5 (wire Production), 6 (SubRoutines), 7 (Debug/Engineering UI).

---

## [Session 35] 2026-06-07 — Safety interlock + ILightController (đèn tháp andon)

**Commit:** `fa28883`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục 4 trong gap analysis — an toàn là bắt buộc trước khi dựng máy thật.

### ✅ Interlock an toàn (gating Start)
- `BaseMasterController` nhận thêm `ISafetyInput?` (tuỳ chọn): **`StartAsync` chặn Start** khi `!IsAllSafe`
  (E-Stop/Guard/Light Curtain) → raise `SafetyInterlockBreach`, máy ở nguyên Idle. `DemoMasterController` truyền ISafetyInput.
- Test: `SafetyGateTests` (chặn khi unsafe + raise alarm · cho phép khi safe).

### ✅ Đèn tháp (andon) — ILightController + TowerLightService
- `TowerLightState` (Core): record R/Y/G + Buzzer + preset (Off/Run/Attention/Fault/FaultBuzzer).
- `ILightController` (Abstractions) + **`SimulatedLightController`** (Hardware.IO).
- `ITowerLightService` + **`TowerLightService`** (AM.Services): tự đặt đèn theo **ưu tiên an toàn → alarm → state**
  (mất an toàn = đỏ+còi; alarm = đỏ; Running/Idle = xanh; Paused/Init/Reset = vàng). Subscribe state/alarm/safety.
- DI: `ILightController` (HardwareFactory) + đăng ký `MainLight` vào HardwareManager + `ITowerLightService` start ở App.
- Test: `TowerLightServiceTests` (Idle→xanh · mất an toàn→đỏ+còi · alarm→đỏ) bằng Moq + SimulatedLight/Safety.

### 🔍 Kết quả
- `dotnet build` → **0 Error**. `dotnet test` → **157 passed** (Infra 55→57, Services 64→67).
- Connection chip "MainLight" xuất hiện thêm ở status bar.

> Còn lại mục 3 (Recipe extensibility), 5 (wire Production), 6 (SubRoutines), 7 (Debug UI).

---

## [Session 34] 2026-06-07 — Nền WorkStation: chuẩn hoá StepSequence + AxisMap/MachineConfig (concrete IAxis)

**Commit:** `d6ad153`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Làm 2 nền tảng bắt buộc trước khi dựng máy thật (mục 1 + 2 trong phân tích gap).

### ✅ Mục 1 — Chuẩn hoá Sequence (bỏ pattern trùng)
- Thêm **`AM.Infrastructure/StepSequence.cs`** — step-runner tái dùng: foreach Step (sắp theo StepNumber) +
  Validate + Execute; **để exception nổi lên** `BaseMasterController.RunLoopAsync` (đã chuẩn ISA-88: Alarm→RunAlarm→Reset,
  Cancel→dừng). Mỗi máy KHÔNG copy vòng lặp + catch-3-exception nữa.
- **Xoá `DemoMachineSequence.cs`** (mồ côi, không ai dùng, trùng vai trò với BaseMasterController).
- Cập nhật skill `am-sequence-patterns`: Station/MasterController chạy `StepSequence` thay vì viết lại vòng lặp.
- Test: `StepSequenceTests` (thứ tự step, propagate AlarmException, Validate-fail chặn Execute, cancel).

### ✅ Mục 2 — AxisMap + MachineConfig (logical → physical) + concrete IAxis ĐẦU TIÊN
- `AxisConfig` (Core): tên logic → controller + index + đơn vị + vận tốc mặc định + soft-limit.
- **`MotionAxisAdapter`** (Infrastructure) — **concrete `IAxis` đầu tiên** của framework: bọc `IMotionController`+index,
  áp vận tốc mặc định, clamp soft-limit; trạng thái cache theo lệnh.
- `IAxisMap` + **`JsonAxisMap`** (nạp `axismap.json`, `ResolveAxis(name)` → IAxis bind controller qua HardwareManager, cache).
- `MachineConfig`/`StationConfig` + `IMachineConfigProvider` + **`JsonMachineConfigProvider`** (nạp `machine.json` — layout máy).
- Sample `axismap.json` + `machine.json` (Shell, copy PreserveNewest) + đăng ký DI singleton.
- Test: `JsonAxisMapTests` (nạp, Get/TryGet, ResolveAxis cache, Home→Move cập nhật state, clamp soft-limit) —
  dùng SimulatedMotionController + HardwareManagerService thật.

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error**. `dotnet test` → **152 passed** (Infra 46→**55**: +4 StepSequence, +5 AxisMap).

> Còn lại trước khi dựng máy (mục 3–7): Recipe extensibility, Safety interlock + ILightController, wire Production,
> SubRoutines base, Debug/Engineering UI tiêu thụ [MechanismUI]/[StationUI].

---

## [Session 32] 2026-06-07 — Fix: cửa sổ tràn màn hình laptop khi scale 125% (DIP vs pixel)

**Commit:** `6f6a17e`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User báo cửa sổ tràn ra ngoài màn laptop, không kéo/resize được.

### 🐞 Nguyên nhân
- WPF `Width`/`Height` tính theo **DIP** (1/96"), không phải pixel vật lý. Màn 1920×1080 đặt **Scale 125%** → 1 DIP = 1.25px.
- Cửa sổ đặt cố định **1680×1040 DIP** = **2100×1300 px vật lý** → lớn hơn màn hình. `ResizeMode=CanMinimize` (khung cố định)
  khiến không resize được để sửa.

### ✅ Khắc phục (MainWindow)
- `ResizeMode` → **CanResize** (kéo được); kích thước mặc định giảm còn 1440×900 DIP + MinHeight/MinWidth.
- Thêm `ClampToWorkArea()` trong constructor: giới hạn Width/Height và đặt MaxWidth/MaxHeight theo
  **`SystemParameters.WorkArea`** (DIP, đã tính DPI/scale của Windows) → luôn vừa màn ở mọi mức scale (100/125/150%).

### 🔍 Kết quả
- Biên dịch OK (lỗi build trước đó chỉ do app đang chạy khóa file .exe — đóng app rồi build lại sạch).
- Chạy lại: cửa sổ vừa màn, căn giữa, kéo resize được.

---

## [Session 31] 2026-06-07 — Re-tune Shell layout theo chuẩn IPC ISA-101 (header lệnh toàn cục + alarm/status bar)

**Commit:** `9d80aff`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Áp guidance S30 vào Shell đang chạy — bố cục 4 vùng cho IPC 1920×1080.

### ✅ Shell layout mới (MainWindow + ShellViewModel)
- **Header (88px):** tên máy + **state chip** (màu theo MachineState) + Mode + Recipe đang chạy +
  **nút lệnh toàn cục Init/Start/Stop/Reset** (≥76×56, màu semantic, CanExecute theo state) + User (từ IUserService) +
  ComboBox ngôn ngữ + đồng hồ realtime.
- **Nav (240px, collapse→64px):** nút module có **glyph + label**; nút ☰ thu gọn (ẩn label). Auto-discovery giữ nguyên.
- **Content:** giới hạn bề rộng **MaxWidth 1500px** (không giãn hết màn).
- **Alarm bar (52px) — dải riêng:** alarm mới nhất + nút Acknowledge (hiện khi có alarm); nền đỏ khi active.
- **Status bar (36px) — chip kết nối:** sinh từ `IHardwareManagerService.GetMonitoredDevices()`, chấm xanh/đỏ theo
  `IsConnected`, poll 1s (DispatcherTimer).
- `ShellViewModel` (internal, DI singleton) bám IMasterController/IAlarmService/IRecipeService/IUserService/
  IHardwareManagerService + `Loc.Strings` (state/label đa ngữ, refresh khi đổi ngôn ngữ). 2 converter:
  `MachineStateToBrushConverter`, `ConnectionToBrushConverter`.
- Cửa sổ: 1680×1040, CenterScreen, ResizeMode=CanMinimize (khung cố định). i18n thêm Shell.Mode/Guest/NoAlarm.

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error** (24 projects). `dotnet test` → **143 passed**.
- Kiểm tra trực quan: `dotnet run --project AM.Application.Shell` → header có state chip + lệnh toàn cục;
  alarm bar + dãy chip kết nối ở dưới; nav ☰ thu gọn được.

> Lưu ý: Dashboard module vẫn còn cụm control + state riêng (trùng một phần với header) — có thể tinh gọn sau.

---

## [Session 30] 2026-06-07 — Cập nhật guidance UI: target IPC 21–24" 1920×1080 (ISA-101/SEMI E95)

**Commit:** `3cedd47`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User cung cấp 2 tài liệu tham khảo; chỉ ra UI hiện thiết kế như HMI panel nhỏ (~10"), trong khi mục tiêu là
**máy tính công nghiệp 21–24" / 1920×1080, chuột + cảm ứng**. Cập nhật các file guidance để Claude thiết kế đúng hơn.

### ✅ Cập nhật (chỉ tài liệu/guidance — không đụng code, 143 tests giữ nguyên)
- **`.claude/skills/am-hmi-design/SKILL.md`** (rewrite): thêm mục **Target hardware (IPC 21–24")**; layout Shell 1920×1080
  (Header 80–96px có nút lệnh toàn cục · Nav 220–260px collapse→64px · Content lưới nhiều cột **≤~1400px** không giãn hết
  1920 · **Alarm bar 48–56px + Status bar 32–40px là 2 dải riêng**); nền theo 4 cấp ISA-101; typography (data 16–20pt);
  touch SEMI S8 (≥60×60 / ≥44×44 / gap ≥8); **Connection Status chips** (PLC/RFID/Camera/MES/HIVE/SECS-GEM/DB, SECS/GEM
  hiện cả COMM+CONTROL state); **sitemap** màn hình rộng; checklist cập nhật.
- **`docs/HMI_Components_Catalog.md`** (mới): checklist thành phần/tham số cho 17 nhóm màn (Dashboard/Auto/IO/Settings/
  Motion/Calibration/Alarm/Manual/Recipe/Connectivity/User/Maintenance/History/OEE/Vision/Traceability/System).
- **`.claude/skills/am-wpf-mvvm/SKILL.md`**: sửa sơ đồ layout từ panel nhỏ (48/200/32px) → IPC (80–96/220–260/48–56/32–40px) + touch sizing.
- **`CLAUDE.md`**: thêm dòng "UI target = IPC 21–24" 1920×1080 chuột+cảm ứng" + trỏ đọc skill/docs; thêm 2 doc HMI vào bảng tham khảo.
- **`.claude/commands/am-new-screen.md`**: ghi rõ target IPC + đọc `am-hmi-design` + components catalog.

> Lưu ý: đây là cập nhật **guidance để thiết kế đúng từ giờ**. Shell đang chạy (Header 40px, chưa tách alarm/status bar,
> chưa có connection chips) NÊN re-tune theo spec mới ở session sau — chưa làm trong session này.

---

## [Session 29] 2026-06-07 — Light theme + cửa sổ kích thước cố định + i18n toàn module

**Commit:** `58b7257`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Phản hồi sau khi chạy thử: (1) nhiều chuỗi không đổi theo ngôn ngữ, (2) đổi sang light theme, (3) khởi động kích thước cố định.

### ✅ 1. i18n toàn module (sửa "đổi ngôn ngữ không đổi theo")
- **Vấn đề:** proxy i18n `LocalizedStrings` là `internal` trong Shell → module không bind được; chuỗi trong View hardcode.
- **Giải pháp:** project mới **`AM.UI.Localization`** với proxy dùng chung `Loc.Strings` (INotifyPropertyChanged, indexer);
  Shell gọi `Loc.Strings.Attach(localization)` lúc khởi động. Mọi module bind
  `{Binding [Key], Source={x:Static loc:Loc.Strings}}` → đổi ngôn ngữ cập nhật live.
- Localize **6 module View** (Dashboard/Alarm/IoMonitor/Motion/Parameter/Identity): tiêu đề, nút, header cột DataGrid, nhãn.
- **DashboardViewModel:** nhãn machine-state lấy từ catalog key `State.{enum}`, refresh khi đổi ngôn ngữ (subscribe `Loc.Strings`).
- Bổ sung ~55 key vào `strings.{vi,en,zh}.json` (Col.*, State.*, Dash.*, Alarm.*, Io.*, Motion.*, Param.*, Id.*).

### ✅ 2. Light theme (mặc định)
- `App.xaml`: đổi palette nền/panel/text/input/border/header sang tông sáng (giữ nguyên màu semantic status ISA-101).
- Sửa foreground các nút màu (Initialize/Start/Stop/Save/...) sang `Status.ForegroundBrush` (trắng) để đọc rõ trên nền màu.

### ✅ 3. Cửa sổ kích thước cố định
- `MainWindow.xaml`: `WindowState=Normal`, `WindowStartupLocation=CenterScreen`, `ResizeMode=CanMinimize`,
  `SizeToContent=Manual`, 1180×720 → khởi động khung cố định, căn giữa, không full màn hình.

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error** (24 projects). `dotnet test` → **143 passed**.
- Kiểm tra trực quan: `dotnet run --project AM.Application.Shell` → light theme, cửa sổ cố định, đổi ngôn ngữ vi/en/zh
  cập nhật toàn bộ tiêu đề/nút/cột.

> Lưu ý: status-message runtime trong VM (Motion/Parameter/Identity) vẫn tiếng Việt — có thể i18n sau nếu cần.

---

## [Session 28] 2026-06-07 — UI module Parameter/Recipe (attribute-driven form) — mục B.3

**Commit:** `f428427`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục B.3 — Recipe editor bám ĐÚNG `IRecipeService` (`ActiveRecipe`/`GetRecipeNamesAsync`/`LoadRecipeAsync`/`SaveRecipeAsync`/`ValidateAsync`). Dùng `[ParamView]` để UI tự render (lần đầu attribute này có tác dụng).

### ✅ Recipe gắn `[ParamView]` (AM.Core)
- Gắn `[ParamView]` cho 13 tham số kỹ thuật có setter (Pick/Place X/Y/Z, Vận tốc/Gia tốc, Vision score/timeout, Timing step/clamp/vacuum) — group + unit + min/max + order. Thuần additive, không đổi hành vi (143 tests vẫn xanh).

### ✅ AM.Modules.Parameter (project mới — project thứ 23)
- `ParameterViewModel` (bám IRecipeService + IUserService): nạp danh sách recipe + recipe active, **sửa trên bản clone**
  (Reload huỷ được, không đụng cache RecipeService), Validate (`ValidateAsync`), **Save gate quyền Engineer**
  (`IUserService.HasPermission`), operatorId = CurrentUser. Bắt `ArgumentException` (validate-fail khi save) → list lỗi.
- `ParamRowVm`: sinh từ `[ParamView]` qua **reflection**, đọc/ghi giá trị về property (int làm tròn), hiện khoảng hợp lệ.
- `ParameterView.xaml`: ComboBox chọn recipe + Nạp/Khôi phục/Kiểm tra/Lưu; form group theo `[ParamView].Group`
  bằng `CollectionViewSource` + `GroupStyle`; list lỗi validate + status. Theme ISA-101.
- `[ModuleNavigation("Nav.Parameter", icon: "recipe", order: 50)]`.

### ✅ Wiring
- Shell: ProjectReference + `AddUiViewModels` đăng ký `ParameterViewModel` + thêm project .sln.
- i18n: key `Nav.Parameter` (vi/en "Recipe", zh "配方").

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error** (23 projects). `dotnet test` → **143 passed**.
- Kiểm tra trực quan: `dotnet run --project AM.Application.Shell` → menu "Recipe": Nạp Default → sửa tham số →
  Kiểm tra/Lưu (cần đăng nhập engineer/admin ở màn Tài khoản, nếu không sẽ báo "Cần quyền Engineer").

> Tiếp theo (mục B): Production (UPH/yield — dùng `CycleDurationMs` đã thêm ở S25), Logging, Diagnostics, Vision.

---

## [Session 27] 2026-06-07 — UI module Motion (jog/home/move + Point Table) — mục B.2

**Commit:** `8d07549` (+ cleanup: gỡ tracking parameters.json/users.json runtime, thêm gitignore)
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục B.2 — Motion module bám ĐÚNG `IMotionController`/`IAxis`. Point Table tách toạ độ khỏi code (file JSON).

### ✅ Point Table — service nền (AM.Services, testable)
- `MotionPoint` (AM.Core): `Name` + `Positions` (IReadOnlyList&lt;double&gt;, tránh CA1819) + `Velocity`.
- `IPointTableService` (Abstractions) + `PointTableService` (AM.Services): nạp/lưu `points.json`,
  `AddOrUpdate` (teach theo tên, không phân biệt hoa thường), `Remove`, `Find`, `SaveAsync`/`ReloadAsync`.
  Thread-safe (Lock) + ghi file qua SemaphoreSlim, IDisposable. Đăng ký DI singleton.
- **Tests (+6 → AM.Services.Tests 64):** `PointTableServiceTests` — add/update, remove, find, save+reload, reload-discard.

### ✅ AM.Modules.Motion (project mới — project thứ 22)
- `MotionViewModel`: dựng `AxisVm` theo `IMotionController.AxisCount`, **poll vị trí** live (PeriodicTimer 300ms:
  GetPosition/IsMoving/IsHomed). Lệnh: HomeAxis/HomeAll, JogPlus/JogMinus (MoveRel theo bước), MoveAbs, StopAxis/StopAll;
  vận tốc dùng chung. Bọc `RunMotionAsync` bắt `AlarmException` (vd chưa home → MotionNotHomed) → `StatusMessage`.
- **Point Table UI:** Teach (lưu vị trí hiện tại theo tên), Go (di chuyển mọi trục tới điểm), Xoá, Lưu.
- `AxisVm` (live position + JogStep/MoveTarget người dùng nhập), `PositionsToTextConverter` hiển thị toạ độ điểm.
- `MotionView.xaml` — DynamicResource theme ISA-101, indicator Homed/Moving bằng DataTrigger.
- `[ModuleNavigation("Nav.Motion", icon: "motion", order: 40)]` → sidebar tự sinh.

### ✅ Wiring
- Shell: ProjectReference + `AddUiViewModels` đăng ký `MotionViewModel` + thêm project vào .sln.
- i18n: key `Nav.Motion` (vi "Chuyển động", en "Motion", zh "运动").

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (22 projects).
- `dotnet test` → **143 passed** (64 services + 46 infra + 27 hardware + 6 architecture).
- WPF không chạy được ở môi trường này → kiểm tra trực quan: `dotnet run --project AM.Application.Shell`
  (Home All → đèn Homed xanh → Jog/Move → Teach điểm → Go).

> Tiếp theo (mục B): Parameter/Recipe (bám `IRecipeService.ActiveRecipe/GetRecipeNamesAsync/...`), rồi Production/Logging/Diagnostics/Vision.

---

## [Session 26] 2026-06-07 — UI module Identity (login/logout/RBAC) — mục B.1

**Commit:** `a7ab3a8`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Bắt đầu mục B (UI modules) — làm lại ĐÚNG API. Module đầu: Identity dùng `IUserService` (đã có từ S24).

### ✅ AM.Modules.Identity (project mới — project thứ 21)
- `IdentityViewModel` (CommunityToolkit.Mvvm): `Username`, `IsLoggedIn`, `CurrentUser`, `CurrentLevel`, `StatusMessage`, `IsBusy`.
  `LoginCommand(password)` + `LogoutCommand` (CanExecute gate), lắng nghe `IUserService.UserChanged` →
  cập nhật trạng thái qua SynchronizationContext (R-UI: không import System.Windows trong VM). IDisposable hủy đăng ký.
- **Bảo mật:** mật khẩu KHÔNG lưu thành property — `PasswordBox.Password` đọc ở code-behind, truyền vào LoginCommand.
- `IdentityView.xaml` — form login + panel phiên (toggle bằng `BoolToVisibilityConverter` hỗ trợ `Invert`),
  Enter để đăng nhập, gợi ý user mặc định. Dùng DynamicResource theme (ISA-101).
- `[ModuleNavigation("Nav.Identity", icon: "user", order: 90)]` → sidebar tự sinh (auto-discovery), đặt cuối menu.

### ✅ Wiring
- Shell: thêm ProjectReference + `AddUiViewModels` đăng ký `IdentityViewModel` + thêm project vào .sln.
- i18n: thêm key `Nav.Identity` (vi: "Tài khoản", en: "Account", zh: "账户").

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (21 projects).
- WPF không chạy được trong môi trường này → cần `dotnet run --project AM.Application.Shell` để kiểm tra trực quan
  (login operator/operator123 → thấy cấp quyền + nút Đăng xuất; đổi ngôn ngữ → menu "Tài khoản/Account/账户").

> Chưa làm: gate các thao tác theo `HasPermission` ở từng module; wire `IAlarmCatalogService` vào Alarm UI.
> Tiếp theo (mục B.2): Motion module — AxisControlView + Point Table.

---

## [Session 25] 2026-06-07 — Nền backend: cycle-time đo thực + alarm catalog đa ngữ

**Commit:** `d9a6f8e`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Nốt 2 mục nền (mục A trong handoff) TRƯỚC khi làm UI modules — Production cần cycle-time, Alarm UI cần tên alarm đa ngữ.

### ✅ Cycle-time đo thực (Production cần)
- `CycleCompletedEventArgs` thêm `double CycleDurationMs` (constructor optional, mặc định 0 → tương thích call cũ).
  Validate `ThrowIfNegative`.
- `BaseMasterController.RunLoopAsync` đo thời gian quanh `RunOneCycleAsync` bằng
  `Stopwatch.GetTimestamp`/`GetElapsedTime` (không cấp phát Stopwatch object), log kèm `{Duration:F0}ms`,
  truyền vào event. Dashboard ViewModel không đổi (vẫn đọc `CycleCount`).

### ✅ Alarm catalog đa ngữ (i18n §7.3 — tách tên alarm khỏi UI strings)
- `IAlarmCatalogService` (Abstractions): `GetName(code)` + `GetRemedy(code)`, dịch theo `ILocalizationService.CurrentCulture`.
- `JsonAlarmCatalogService` (AM.Infrastructure/Localization): nạp `Alarms.{culture}.json`
  (mã alarm → {name, remedy}) từ thư mục `lang/`; tra theo culture hiện tại lúc gọi (đổi ngôn ngữ runtime tự phản ánh),
  fallback culture mặc định → `"Alarm {code}"`. Bất biến sau nạp → không lock khi đọc.
- Data: `lang/Alarms.{vi,en,zh}.json` — đủ 44 mã alarm (Motion/Vision/IO/System/Comm/Production/Safety).
- Đăng ký DI singleton (`AddCoreServices`), dùng chung thư mục `lang/` với strings.*.json (đã copy PreserveNewest).

### ✅ Tests (+5 → AM.Infrastructure.Tests 46, tổng 137)
- `JsonAlarmCatalogServiceTests` — dịch theo culture, fallback default, culture lạ, mã không tồn tại, remedy.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **137 passed** (58 services + 46 infra + 27 hardware + 6 architecture).

> Còn lại của mục A: (đã xong cả 2). Tiếp theo (mục B): UI Identity (IUserService) → Motion + Point Table.
> Lưu ý: tên alarm hiển thị ở Alarm module chưa wire `IAlarmCatalogService` vào `AlarmListViewModel` —
> sẽ làm khi dựng lại Alarm UI đúng (thêm cột Name + refresh khi đổi ngôn ngữ).

---

## [Session 24] 2026-06-07 — Nền backend: IUserService (login/RBAC) + lưu lựa chọn ngôn ngữ

**Commit:** `6094df5`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Hoàn thiện phần nền (service layer) TRƯỚC khi làm UI — tránh lặp lại tình trạng UI viết theo API chưa có.

### ✅ IUserService + UserService (login / RBAC)
- `IUserService` (Abstractions): CurrentUser/CurrentLevel/IsLoggedIn, `LoginAsync`, `Logout`, `HasPermission(UserLevel)`,
  event `UserChanged`. `UserChangedEventArgs` (AM.Core).
- `UserService` (AM.Services): user store JSON (`users.json`), mật khẩu băm **BCrypt** (Verify chạy ngoài thread);
  seed mặc định lần đầu (operator/engineer/admin — log cảnh báo đổi mật khẩu trước sản xuất). Thread-safe (Lock).
- Đăng ký DI singleton (AddCoreServices) + thêm ref BCrypt.Net-Next vào AM.Services.

### ✅ Lưu lựa chọn ngôn ngữ (i18n §7.4)
- `App` khôi phục culture đã lưu từ `parameters.json` (key `ui.culture`) lúc khởi động → `SetCulture`;
  mỗi lần đổi ngôn ngữ tự lưu lại (qua `IParameterService`). Lần mở sau giữ nguyên ngôn ngữ đã chọn.

### ✅ Tests (+8 → AM.Services.Tests 58)
- `UserServiceTests` — seed default, login đúng/sai/unknown, HasPermission theo cấp, logout + event, reload từ file.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **132 passed** (58 services + 41 infra + 27 hardware + 6 architecture).

> Phần nền sẵn sàng cho UI: Identity module (dùng IUserService), gate Engineer/Force ở các màn (HasPermission).

---

## [Session 23] 2026-06-07 — Revert 7-screen commit (vỡ build) + thêm HMI master template

**Commit:** `1cf7e27` (+ revert commit)
**Người thực hiện:** Claude (Cowork) + Nhan

### 🔙 Revert
- Revert commit `9c667ec` ("7 màn hình ISA-101") — code viết theo **API không tồn tại** làm vỡ build toàn solution:
  `IUserService` (chưa có), `IRecipeService.CurrentRecipe/AllRecipes/LoadAsync/SaveAsync` (thực tế là
  `ActiveRecipe/GetRecipeNamesAsync/LoadRecipeAsync/SaveRecipeAsync`), `CycleCompletedEventArgs.CycleDurationMs`
  (không có), Motion XAML set Border.Style 2 lần, + vi phạm analyzer.
- Đưa solution về trạng thái xanh (= Session 22): Dashboard + Alarm + IoMonitor + i18n + nav auto-discovery.

### ✅ Thêm
- `docs/HMI_UI_Architecture_Template.md` — master reference HMI (ISA-101/SEMI E95/PackML) để dùng khi sinh màn hình.

### 📌 Đánh giá template (tóm tắt) — sẽ làm lại các màn hình ĐÚNG API
- Đã có: i18n runtime (§7), Persistent Frame/ISA-101 (§3), nav config-driven ([ModuleNavigation], §2/§3.1), color/status (§5).
- Nên bổ sung (làm lại đúng API): `IUserService`/Identity (§4/§8), AxisControlView + Point Table (§6.2),
  ManualControlView (§6.1), ErrorDetailView (§6.5), lưu lựa chọn ngôn ngữ (§7.4).
- Không áp: Prism regions (§8 — đã dùng MS DI + auto-discovery), Group Menu 4-module cứng (§2).

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **124 passed** (50 services + 41 infra + 27 hardware + 6 architecture).

---

## [Session 22] 2026-06-05 — G0 Nav auto-discovery + AM.Modules.IoMonitor

**Commit:** `4246aaf`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Theo sơ đồ kiến trúc — lấp tầng Modules. Làm nền điều hướng trước để thêm module không phình Shell.

### ✅ G0 — Sidebar tự sinh từ [ModuleNavigation]
- `[ModuleNavigation(displayKey, icon, order)]` gắn lên DashboardView/AlarmListView/IoMonitorView.
- `NavigationBuilder` (Shell): quét assembly `AM.Modules.*` đã nạp, tìm View có attribute → danh sách entry sắp theo Order.
- `MainWindow` sinh nút nav động: Content bind i18n proxy (đổi ngôn ngữ cập nhật ngay); click → resolve View +
  ViewModel theo convention ("XxxView"→"XxxViewModel") từ DI, cache view. **Thêm module = gắn attribute, KHÔNG sửa Shell.**

### ✅ IO Monitor (AM.Modules.IoMonitor)
- `IoMonitorViewModel` — poll DI realtime (`ReadAllDiAsync`, PeriodicTimer 300ms, marshalling SynchronizationContext),
  toggle DO (`WriteDiAsync`); `IoChannelVm` (Index/Label/Value). IDisposable dừng poll.
- `IoMonitorView.xaml` — DI grid (ellipse màu ON/OFF) + DO grid (nút toggle màu theo trạng thái); `BoolToIoBrushConverter`.
- App: `ConnectAllAsync` lúc khởi động → IO Monitor/Dashboard có dữ liệu live + DO toggle hoạt động + watchdog có baseline.
- Bổ sung file dịch `strings.en.json` (đang thiếu) + key `Nav.IoMonitor` cho vi/en/zh.

### ✅ Tests
- `AM.Architecture.Tests` +1: IoMonitor module không phụ thuộc hardware concrete (6 arch tests).

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (20 projects).
- `dotnet test` → **124 passed** (50 services + 41 infra + 27 hardware + 6 architecture).

---

## [Session 21] 2026-06-05 — i18n foundation: đổi ngôn ngữ runtime (vi/en/zh) + log retention config

**Commit:** `a4fd2bd`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Theo sơ đồ kiến trúc mục tiêu — lấp tầng Infrastructure i18n. Hạ tầng cross-cutting làm TRƯỚC
khi thêm module mới (IO Monitor/Motion/Identity) để tránh retrofit.

### ✅ i18n — đổi ngôn ngữ runtime, không restart
- `ILocalizationService` (AM.Core.Abstractions): `this[key]`, `Format(key,args)`, `CurrentCulture`,
  `AvailableCultures`, `SetCulture`, event `LanguageChanged`. `LanguageChangedEventArgs` (AM.Core).
- `JsonLocalizationService` (AM.Infrastructure/Localization): nạp `strings.{culture}.json` (flat key→value)
  từ thư mục; đổi culture runtime phát event; fallback về key nếu thiếu dịch; thread-safe.
- File dịch `lang/strings.{vi,en,zh}.json` (copy ra output).
- `LocalizedStrings` (Shell): proxy WPF — binding `{Binding [Key]}` tự refresh khi đổi ngôn ngữ
  (raise PropertyChanged indexer qua Dispatcher) → **hot-reload, không restart**.
- Shell: ComboBox chọn ngôn ngữ trong side-nav; nav text bind qua proxy → đổi là cập nhật ngay.

### ✅ Log retention theo config
- `ConfigureLogging(config)`: `retainedFileCountLimit = AutoMachine:LogRetentionDays` (mặc định 30) —
  log cũ hơn N ngày tự xoá. App build config TRƯỚC khi cấu hình logging.

### ✅ Tests (+6 → AM.Infrastructure.Tests 41)
- `JsonLocalizationServiceTests` — nạp culture, đổi culture + event, không raise khi cùng culture,
  culture lạ bị bỏ qua, fallback key, Format có args.

### ⚠️ Ghi chú
- Log file giữ nguyên ngôn ngữ trong code (chuẩn cho kỹ sư) — không localize log; user reqs cho phép phương án này.
- String hiện tại của Dashboard/Alarm/App.xaml chưa migrate hết sang proxy — module mới sẽ dùng proxy từ đầu;
  migrate XAML cũ làm dần (incremental).

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **123 passed** (50 services + 41 infra + 27 hardware + 5 architecture).

---

## [Session 20] 2026-06-04 — Phase F5/F7: WordRegisterPlcBase (dedup PLC) + đổi folder docs/

**Commit:** `d0012ae`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ F5 — WordRegisterPlcBase (gom code chung PLC, có chọn lọc)
- `AM.Hardware.Comm/Plc/WordRegisterPlcBase.cs` — abstract base cho PLC **word-register, little-endian word order**:
  cung cấp sẵn `ReadWord/ReadDWord/WriteDWord/ReadFloat/WriteFloat` compose từ `ReadWordsAsync/WriteWordsAsync`.
- `InovancePlcDevice` + `MitsubishiPlcDevice` kế thừa base → bỏ ~5 typed-method trùng lặp mỗi driver.
  `WriteWordAsync` để **virtual**: Inovance override dùng FC06 (single), Mitsubishi dùng default FC16 (batch) → giữ đúng wire.
- **Có chọn lọc:** Siemens S7 (byte-oriented, big-endian) KHÔNG dùng base — protocol khác hẳn. Tránh over-abstraction.
- Dispose theo pattern `Dispose(bool)` (CA1063). InovancePlcDeviceTests vẫn pass → behavior-preserving.

### ✅ F7 — Đổi tên folder tài liệu
- `file hướng dẫn code/` (tên có dấu + khoảng trắng, gây phiền CI/cross-platform) → **`docs/`** (git mv).
- Cập nhật reference trong CLAUDE.md + PROJECT_STATUS.md (giữ nguyên CHANGELOG lịch sử).

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **117 passed** (50 services + 35 infra + 27 hardware + 5 architecture).

---

## [Session 19] 2026-06-04 — Phase F2/F3/F4/F6: tách Bootstrapper + vendor enum + arch test + log path

**Commit:** `3aaa5f6`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Thực hiện các điểm ĐÚNG còn lại của review bên thứ 3.

### ✅ F2 — Tách Bootstrapper (God-Composition-Root → extension groups)
- `ServiceCollectionExtensions.cs`: `AddAutoMachineOptions / AddDataAccess / AddCoreServices / AddUiViewModels /
  AddDemoMachine / AddHardware`. Bootstrapper còn ~90 dòng, chỉ điều phối.
- Thêm hardware/station mới → sửa đúng nhóm, không phình một file lớn.

### ✅ F3 — Vendor enum thay magic string
- `AM.Core/Enums/HardwareVendors.cs`: `MotionVendor/PlcVendor/IoVendor/ScannerVendor/VisionVendor/RobotVendor`.
- `ParseVendor<TEnum>` (Enum.TryParse ignoreCase) trong ServiceCollectionExtensions + HardwareFactory →
  hết so sánh chuỗi "KEYENCE"/"GTS", type-safe + IntelliSense.

### ✅ F4 — Architecture test (enforce Dependency Inversion)
- Project mới `AM.Architecture.Tests` (NetArchTest): **5 test** chặn `AM.Services`, `AM.Modules.Dashboard`,
  `AM.Modules.Alarm`, `AM.Core.Abstractions`, `AM.Infrastructure` phụ thuộc `AM.Hardware.*` concrete.
- Chặn "lách luật" gọi SDK hãng trực tiếp từ UI/logic — chỉ qua AM.Core.Abstractions.

### ✅ F6 — Log path cross-platform
- `ConfigureLogging`: `Path.Combine(AppContext.BaseDirectory, "logs", "automachine-.log")` thay literal `@"logs\..."`.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (19 projects).
- `dotnet test` → **117 passed** (50 services + 35 infra + 27 hardware + 5 architecture).

---

## [Session 18] 2026-06-04 — Fix: gỡ SQLite DB khỏi Git tracking (.gitignore)

**Commit:** `b53ecf7`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Review bên thứ 3 — điểm đúng & nghiêm trọng: file DB sống bị commit.

### 🔧 Sửa (Phase F1)
- `.gitignore`: thêm `*.db`, `*.db-shm`, `*.db-wal`, `*.sqlite`, `*.sqlite3`.
- `git rm --cached automachine.db{,-shm,-wal}` — gỡ khỏi tracking (file vẫn còn trên đĩa để chạy).
- DB tự tạo runtime qua `InitializeDatabaseAsync` → `EnsureCreatedAsync` nên không cần commit DB.
- Tránh: phình repo, merge conflict binary, ghi đè data sống (Recipe/Log) khi pull.

---

## [Session 17] 2026-06-04 — Phase E: README + CI/CD + AM.Modules.Alarm + Shell navigation

**Commit:** `98382af`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Phase E (roadmap) — onboarding + UI vận hành. Làm lát cắt giá trị cao: README, CI, module UI thứ 2.

### ✅ E1 — README.md
- Tổng quan, nguyên tắc HAL, kiến trúc 3 tầng, bảng 17 project, quick-start (build/test/run),
  bảng đổi hãng phần cứng qua appsettings, watchdog/alarm ranges, quy ước phát triển (CPM, commit script).

### ✅ E2 — CI/CD (GitHub Actions)
- `.github/workflows/ci.yml` — on push/PR/dispatch tới main: `windows-latest` (bắt buộc vì WPF net9.0-windows),
  setup .NET 9 → restore → build Release (TreatWarningsAsErrors) → test.

### ✅ E3 — AM.Modules.Alarm (UI vận hành thứ 2) + điều hướng Shell
- Project mới `AM.Modules.Alarm` (net9.0-windows, CPM):
  - `AlarmListViewModel` — ObservableCollection ActiveAlarms, đồng bộ realtime với AlarmService (AlarmRaised/Cleared),
    command Acknowledge/Clear/ClearAll, UI-thread marshalling qua SynchronizationContext, IDisposable.
  - `AlarmListView.xaml` (+code-behind) — DataGrid ISA-101: ellipse màu theo Level, Code/Station/Message/Time/Ack,
    nút Ack/Clear mỗi dòng + Clear All; `AlarmLevelToColorConverter` (Low/Medium/High/Critical → Status brush).
- Shell: thêm **side-nav** (Dashboard / Alarms) chuyển `MainContent`; đăng ký `AlarmListViewModel` DI.

### ⚠️ Ghi chú
- ViewModel của module WPF (net9.0-windows) chưa unit-test riêng (giống Dashboard) — logic AlarmService đã được
  test đầy đủ ở AM.Services.Tests; VM chủ yếu là binding/marshalling.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (18 projects).
- `dotnet test` → **112 passed** (50 services + 35 infrastructure + 27 hardware).

---

## [Session 16] 2026-06-04 — Fix IDE1006 cho private static readonly fields

**Commit:** `3e8db3a`
**Người thực hiện:** Claude (Cowork) + Nhan

### 🔧 Sửa
- `.editorconfig`: rule `private_fields_should_be_camel_case` áp cho MỌI private field (kể cả `static readonly`)
  → IDE1006 "Missing prefix '_'" báo sai trên `Transitions`, `JsonOptions`, `DefaultSimEndpoint`...
  (mâu thuẫn với CS07: static readonly dùng PascalCase).
- Thêm rule `static_readonly_fields_should_be_pascal_case` **đặt trước** rule underscore — Roslyn lấy rule khớp
  đầu tiên theo thứ tự file → `private static readonly` dùng PascalCase, instance field vẫn bắt buộc `_camelCase`.
- Xác minh bằng `EnforceCodeStyleInBuild=true`: 0 IDE1006 trên toàn solution (4 field: BaseMasterController,
  ParameterService, JsonIoTagMap, SimulatedOpcUaClient).

---

## [Session 15] 2026-06-04 — Phase D: CPM + IOptions validation + ProductionService + DeviceNames

**Commit:** `506d45f`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Khép các điểm "đúng nhưng nhẹ" còn lại của đánh giá: #12, #16, #14, #5.

### ✅ D4 — DeviceNames constants (claim #5)
- `AM.Core/Constants/DeviceNames.cs` — 12 hằng số tên device (MainMotion/MainCamera/...).
- Bootstrapper.RegisterHardwareDevices dùng `DeviceNames.X` thay magic string → IntelliSense + an toàn refactor.

### ✅ D3 — ProductionService (claim #14)
- `IProductionService` + `ProductionService` (AM.Services) trên `IProductionRepository` có sẵn:
  - `RecordAsync` ghi record mỗi cycle; `GetStatisticsAsync` tính **UPH / yield / avg cycle time**.
- `ProductionStatistics` record (AM.Core). Đăng ký DI Scoped (khớp lifetime EF repo).
- `ProductionServiceTests` (+3): record persist, yield/UPH/avgCycle đúng, empty khi không có data.

### ✅ D2 — IOptions + validate fail-fast (claim #16)
- `AutoMachineOptions` (Shell/Configuration) cho section "AutoMachine".
- Bootstrapper: `AddOptions<AutoMachineOptions>().Bind(...).Validate(...)` (DatabasePath không rỗng,
  LogRetentionDays 1..3650, DataRetentionDays 1..36500).
- App.xaml.cs: **ép resolve `.Value` lúc startup** → config sai ném OptionsValidationException ngay (fail-fast),
  hiển thị dialog lỗi thay vì chạy với giá trị sai.
- Thêm package `Microsoft.Extensions.Options.ConfigurationExtensions`.

### ✅ D1 — Central Package Management (claim #12)
- `Directory.Packages.props` (root) `ManagePackageVersionsCentrally=true` — gom version NuGet một chỗ.
- Strip `Version=` khỏi **14 .csproj** + `Directory.Build.props` (analyzers).
- Pin các version thả nổi (CPM cấm floating): NetAnalyzers 9.0.0, SonarAnalyzer 9.32.0.97167.
- Vendor SDK comm (NModbus4/FluentModbus/libplctag/OPCFoundation/Basler.Pylon) đang comment trong csproj →
  ghi chú thêm PackageVersion cụ thể khi kích hoạt.
- → chống version drift giữa 17 projects; một nguồn version duy nhất.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **112 passed** (50 services + 35 infrastructure + 27 hardware).

---

## [Session 14] 2026-06-04 — Phase C: Hardware Watchdog + IsConnected base + auto-reconnect

**Commit:** `f48f874`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Claim #2 (watchdog 6.2) — mất kết nối giữa chừng không làm sập máy. Gắn trực tiếp với IHardwareDevice (Phase A).

### ✅ IsConnected vào IHardwareDevice base
- Thêm `bool IsConnected { get; }` vào `IHardwareDevice`; **gỡ khỏi 11 interface con** (additive, 0 driver bị sửa —
  implementation đã có sẵn IsConnected nay thoả base). `ISerialDevice` bridge `IsConnected => IsOpen` (default member).
- → cho phép health-monitoring **generic** thay vì switch theo kiểu.

### ✅ Registry hỗ trợ monitor
- `IHardwareManagerService.GetMonitoredDevices()` → `IReadOnlyList<MonitoredDevice>` (Name/Category/IHardwareDevice).
- `MonitoredDevice` record mới (AM.Core.Abstractions).

### ✅ HardwareWatchdogService (AM.Services)
- `IHardwareWatchdogService` + impl: poll `IsConnected` mỗi chu kỳ; khi connected→disconnected:
  - raise alarm `CommConnectionFail`,
  - phát `DeviceDisconnected` event (MasterController subscribe → EmergencyStop),
  - auto-reconnect bằng **RetryHelper** (back-off luỹ tiến) — best-effort, không làm sập watchdog.
- `HardwareDisconnectedEventArgs` (AM.Core). `PollOnceAsync` public → unit test deterministic.
- Thêm ProjectReference AM.Services → AM.CommonTools (dùng RetryHelper sẵn có).

### ✅ Wiring
- Bootstrapper: đăng ký `IHardwareWatchdogService` singleton.
- App.xaml.cs: subscribe `DeviceDisconnected → masterController.EmergencyStop` + `watchdog.Start()`;
  cleanup qua provider disposal (IDisposable).

### ✅ Tests (+5 → AM.Services.Tests 47)
- `HardwareWatchdogServiceTests` — không alarm khi vẫn connected; drop → alarm + reconnect; fire event;
  reconnect thất bại không throw + giữ disconnected; Start/Stop toggles IsRunning.

### ⚠️ Quyết định scope (minh bạch)
- **KHÔNG** thêm `ErrorOccurred`/`GetLastError` (claim 0.1) vào base — event trên interface buộc ~20 driver
  phải implement (events không có default impl thực dụng). Watchdog dùng **IsConnected polling** là đủ phát hiện lỗi.
  Retrofit error-surface để dành làm khi thực sự cần (mỗi driver muốn báo lỗi chi tiết).

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **109 passed** (47 services + 35 infrastructure + 27 hardware).

---

## [Session 13] 2026-06-04 — Phase B: AM.Infrastructure.Tests (13 transitions) + end-to-end sequence

**Commit:** `6e37960`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Vá khoảng trống test lõi đúng như claim #11 (BaseMasterController/BaseMechanism/StationBase chưa có test).

### ✅ B1 — AM.Infrastructure.Tests (project mới, 35 tests)
- `TestDoubles.cs` — RecordingAlarmService + TestMechanism/TestStation/TestMasterController (expose FireTrigger).
- `BaseMasterControllerTests` — **đủ 13 transition ISA-88 hợp lệ** (Theory/MemberData) + transition không hợp lệ
  bị từ chối (Fire=false, state giữ nguyên) + StateChanged (Previous/New/Trigger) + SetOperationMode chỉ trong Idle.
- `BaseMechanismTests` — IsReady sau Initialize; **IsBusy guard** (Interlocked) từ chối thao tác đồng thời + nhả guard
  sau exception; EmergencyStop KHÔNG throw dù OnEmergencyStop ném.
- `StationBaseTests` — RunCycle Running→Idle; AlarmException → RunAlarm + rethrow; EmergencyStop → RunAlarm.

### ✅ B2 — End-to-end Simulated sequence (EndToEndSequenceTests)
- Run loop chạy nhiều cycle → CycleCount tăng + CycleCompleted fire → Stop về Idle.
- **Pause halt / Resume continue**: paused thì loop dừng ở checkpoint (cycle không tăng), resume thì tiếp tục.
- **Safety-trip**: `SimulatedSafetyInput.ForceState(guard mở)` giữa chu trình → cycleBody ném AlarmException →
  run loop FireTrigger(Error) → **RunAlarm** + alarm 70010 được raise.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (17 projects).
- `dotnet test` → **104 passed** (42 services + 35 infrastructure + 27 hardware).

---

## [Session 12] 2026-06-04 — Phase A: Wire Dashboard + IHardwareDevice base + doc drift

**Commit:** `2795775`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Phản biện đánh giá bên thứ 3 (dựa trên snapshot Session 8 cũ) → thực thi 8 điểm đúng, bắt đầu Phase A.

### ✅ A1 — Wire Dashboard vào Shell (claim #1 đúng)
- `MainWindow.xaml`: thay placeholder bằng `ContentControl x:Name="MainContent"`.
- `MainWindow.xaml.cs`: `OnWindowLoaded` resolve `DashboardViewModel` từ DI (UI thread → capture SynchronizationContext), set `MainContent.Content = new DashboardView{...}`.
- Shell csproj + ProjectReference `AM.Modules.Dashboard`; Bootstrapper đăng ký `DashboardViewModel` singleton.
- **DoD:** app khởi động hiển thị Dashboard (state indicator + alarm list + Start/Stop/Reset).

### ✅ A2 — IHardwareDevice base + bỏ switch trong ConnectAllAsync (claim #4 → fix #2)
- `IHardwareDevice { ConnectAsync; DisconnectAsync; }` mới — hợp đồng lifecycle chung.
- **11 interface** kế thừa `IHardwareDevice`, gỡ khai báo Connect/Disconnect trùng lặp (additive — KHÔNG sửa implementation nào): Motion/Camera/IO/Modbus/Tcp/OpcUa/EthernetIp/Plc/Robot/BarcodeScanner/SafetyInput.
- `ISerialDevice`: bắc cầu bằng default interface method (`ConnectAsync→OpenAsync`, `DisconnectAsync→CloseAsync`) — không đụng SerialPortWrapper/Simulated.
- `HardwareManagerService.ConnectAllAsync/DisconnectAllAsync` → **vòng lặp generic `is IHardwareDevice`**, xoá switch 8-nhánh. Thêm hardware mới không phải sửa manager.
- Đăng ký thêm 4 device vào registry (MainPLC/MainRobot/MainScanner/MainSafety) + `HardwareCategory.SafetyTerminal=16`.

### ✅ A3 — Dọn doc drift (claim #8, #10)
- `CLAUDE.md`: sửa danh sách interface Hardware + project AM.Hardware.* cho khớp code thật; đánh dấu rõ TODO chưa có (IAxisGroup, ILightController, EStopMonitor→thay bằng ISafetyInput).

### ✅ Tests (+4 → AM.Services.Tests 42)
- `HardwareManagerServiceTests` — ConnectAll/DisconnectAll generic qua IHardwareDevice, bỏ qua object không phải device, Resolve case-insensitive.

### 📌 Phản biện đánh giá bên thứ 3 (tóm tắt)
- **Sai dữ kiện:** #6 (Parameter lưu JSON chứ không phải EF/SQLite → không có "bất nhất"), #13 (Serilog ĐÃ có file rolling 30 ngày), #15 (CheckPauseAsync + ct ĐÃ propagate trong run loop).
- **Lỗi thời:** "32 tests" (thực tế 69), #8 (ISafetyInput đã thêm S11).
- **Opinion/YAGNI:** #3 (Stateless lib), #7 (dual-generic Station), #9 (tách SECS process).
- **Đúng, đã/đang làm:** #1 (A1), #2+#4 (A2), #10 (A3). Còn lại #5/#12/#14/#16 + UI modules → Phase B–E.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **69 passed** (42 services + 27 hardware).

---

## [Session 11] 2026-06-04 — HAL abstraction (EPIC 0) + Scanner/Vision/Safety/IO-tagmap

**Commit:** `6888d66`
**Người thực hiện:** Claude (Cowork) + Nhan
**Tham khảo:** Kế hoạch Task — Tách phần cứng (HAL). Làm phần additive/buildable ngay, không cần SDK hãng.

### ✅ EPIC 0 — Abstraction trung lập (nền tảng)
- DTO trung lập (AM.Core):
  - `Enums/PixelFormat.cs` (Mono8/Mono16/Rgb24/Bgr24/BayerRg8 — không phụ thuộc System.Drawing).
  - `Models/FrameData.cs` (Pixels/Width/Height/Format/Timestamp) — ảnh trung lập hãng.
  - `Models/MotionStatus.cs` (Positions/Homed/Moving/FaultCode, đơn vị mm).
  - `Models/EventArgs/BarcodeReceivedEventArgs.cs`, `SafetyStateChangedEventArgs.cs` (CA1003).
- Interface mới (AM.Core.Abstractions/Interfaces/Hardware):
  - `IVisionProcessor` — LoadJob/RunJob(FrameData)→VisionResult, tách hẳn camera khỏi vision tool.
  - `IBarcodeScanner` — TriggerAsync + CodeReceived event, trung lập Keyence/Cognex.
  - `ISafetyInput` — CHỈ ĐỌC E-Stop/Guard/LightCurtain + SafetyStateChanged (an toàn vật lý do mạch lo).
  - `IAxis` — trục đơn đơn vị mm (MoveAbs/MoveRel/Home/Position/IsMoving/IsHomed).
  - `IIoTagMap` — phân giải tag IO → kênh.
- `VisionResult` tách khỏi ICameraDevice → file riêng, **bổ sung X/Y/AngleDeg/Pass** (additive, không phá vỡ smart-camera path hiện có).

### ✅ EPIC 4.3 — SimulatedVisionProcessor (AM.Hardware.Vision)
- Trả VisionResult giả lập theo passRate; chạy end-to-end không cần VisionPro/dongle.

### ✅ EPIC 2.3 — SimulatedSafetyInput (AM.Hardware.IO)
- Mặc định all-safe; `ForceState` mô phỏng E-Stop/mở cửa → phát SafetyStateChanged để khoá logic.

### ✅ EPIC 2.2 — IO tag-map (AM.Hardware.IO)
- `JsonIoTagMap` nạp `io.map.json` (tag→kênh, case-insensitive) + `IoTagExtensions`
  (ReadDiByTag/WriteDoByTag/WriteAndWaitConfirmByTag). Logic máy gọi IO bằng tag, đổi đấu dây chỉ sửa JSON.
- `AM.Application.Shell/io.map.json` mẫu (PartPresent_A, Vac_A, TowerGreen...).

### ✅ EPIC 5 — Scanner (project mới AM.Hardware.Scanner, protocol-only theo plan 5.1)
- `TcpBarcodeScannerBase` (TCP line-based, timeout, AlarmException, no-read detection) +
  `KeyenceScanner` (LON) + `CognexScanner` (TRIGGER ON, "NO READ") + `SimulatedBarcodeScanner` (queue/serial).

### ✅ EPIC 0.4/6.1 (một phần) — HardwareFactory
- `AM.Application.Shell/HardwareFactory.cs` — điểm chọn vendor cho peripherals
  (IVisionProcessor/IBarcodeScanner/ISafetyInput/IIoTagMap) theo `appsettings`, ép Simulated khi UseSimulation.
- Bootstrapper gọi `HardwareFactory.RegisterPeripherals`. `appsettings.json`: thêm block Vision/Scanner + Io:TagMapFile.

### ✅ Tests (AM.Hardware.Tests +10 → 27 total)
- `HalAbstractionTests` — SimVision, SimSafety (event), JsonIoTagMap (resolve/load/case-insensitive), IoTagExtensions, SimScanner.
- `ScannerLoopbackTests` — Keyence/Cognex qua scanner server giả loopback (line protocol + no-read alarm).

### ⚠️ Quyết định scope (minh bạch)
- **Nhóm B (SDK-nặng) CHƯA làm**: EtherCAT motion (PCIE-1203+Leadshine), Beckhoff ADS, Basler/Hik camera, VisionPro.
  → Cần SDK hãng; sẽ tạo project riêng + Simulated khi có DLL (libs/ đã chuẩn bị).
- **Triệt để tách project** cho driver Session 9 (Inovance/Mitsubishi/Siemens/GTS...) là refactor cơ học lớn,
  **để pha riêng** tránh trộn với feature mới. HardwareFactory hiện quản peripherals; motion/io/plc vẫn ở RegisterRealHardware.
- Interface cũ (IMotionController/IIoModule/ICameraDevice) **chưa** thêm ErrorOccurred/GetLastError (0.1) — sẽ retrofit kèm watchdog 6.2 để tránh phá vỡ 8 driver hiện có.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (16 projects).
- `dotnet test` → **65 passed** (38 services + 27 hardware).

---

## [Session 10] 2026-06-03 — libs/ Vendor DLL Structure

**Commit:** `0fb161c`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới

- `libs/README.md` — Hướng dẫn chi tiết từng vendor: nơi tải SDK, DLL cần copy, đường dẫn đích, link website
- `libs/Motion/Gts/{x64,x86}/.placeholder` — GTS 固高 Googoltech GTS-400/800 (`gts.dll`, P/Invoke)
- `libs/Motion/Advantech/{x64,x86}/.placeholder` — Advantech PCI-1245/1265 (`ADVMOT.dll`, P/Invoke)
- `libs/Vision/Cognex/x64/.placeholder` — Cognex VisionPro (`Cognex.VisionPro.dll + CogSocketServer.dll`)
- `libs/Vision/HIK/x64/.placeholder` — HIK Robot MVS (`MvCameraControl.Net.dll + MVSDKmd.dll`)
- `libs/Vision/Basler/x64/.placeholder` — Basler Pylon (`PylonC.NET.dll` hoặc NuGet `Basler.Pylon`)
- `libs/IO/Advantech-ADAM`, `Mitsubishi-QSeries`, `Omron-NX` — không cần DLL (Modbus/TCP protocol)

### 🔧 Sửa đổi
- `.gitignore` — Thêm rule loại trừ `*.dll` trong `libs/` nhưng giữ `.placeholder` và `README.md`
- `AM.Hardware.Motion.csproj` — Thêm comment hướng dẫn uncomment khi có `gts.dll` / `ADVMOT.dll`
- `AM.Hardware.Vision.csproj` — Thêm comment hướng dẫn cho Cognex/HIK/Basler DLL reference

### 🔧 Quy tắc sử dụng
- **DLL không commit lên Git** (bản quyền vendor)
- Developer clone repo → tự copy DLL từ SDK theo hướng dẫn trong `libs/README.md`
- Khi có DLL → uncomment phần `<Content>` hoặc `<Reference>` trong `.csproj` tương ứng
- Build vẫn pass khi **không có DLL** (chỉ Simulated drivers hoạt động)

---

## [Session 9] 2026-06-03 — Real hardware drivers (Modbus, Inovance, 固高 GTS, Advantech, Mitsubishi, Siemens, Robot)

**Commit:** `5183b0d`
**Người thực hiện:** Claude (Cowork) + Nhan
**Mục tiêu:** Bổ sung driver phần cứng thật, chạy được cho sản phẩm thật, giữ build clean (0 warning).

### ✅ Abstractions mới (AM.Core.Abstractions / AM.Core)
- `IPlcDevice` — đọc/ghi bit/word/dword/float theo địa chỉ vendor (D100, M10, X0...).
- `IRobotDevice` — move/pose/IO/raw-command cho robot qua socket.
- `RobotPose` record (Cartesian X/Y/Z/Rx/Ry/Rz) trong AM.Core/Models.
- `HardwareCategory`: thêm `Plc=14`, `Servo=15`.

### ✅ Modbus TCP thật (AM.Hardware.Comm/Modbus/ModbusTcpClient.cs)
- Thay skeleton bằng implementation thật: tự dựng khung **MBAP trên raw TcpClient**, zero NuGet.
- Đủ FC01–FC06/FC15/FC16, big-endian, thread-safe, timeout + AlarmException mapping.

### ✅ Inovance (AM.Hardware.Comm/Inovance)
- `InovancePlcDevice : IPlcDevice` — PLC H3U/H5U/AM qua Modbus; parse D/M/X/Y + base-offset cấu hình.
- `InovanceServoDrive : IMotionController` — servo IS620/SV660 qua Modbus, **CiA402 Profile Position**
  (enable 06→07→0F, new-setpoint bit4, poll target-reached), register map cấu hình được.
- `SimulatedPlcDevice : IPlcDevice` (AM.Hardware.Comm/Plc) — in-memory.

### ✅ 固高 GTS motion (AM.Hardware.Motion/Gts) — chạy thật trên PC có card
- `GtsNative` — P/Invoke `gts.dll` (GT_Open/Reset/AxisOn/PrfTrap/SetPos/Update/GetEncPos/GetSts/Stop...).
- `GtsMotionController : IMotionController` — trapezoid point-to-point, đổi mm↔pulse, poll status bit.
- Biên dịch không cần DLL (DllImport resolve runtime).

### ✅ Advantech (AM.Hardware.Motion/Advantech + AM.Hardware.IO/Advantech)
- `AdvantechNative` + `AdvantechMotionController : IMotionController` — P/Invoke Common Motion API (ADVMOT.dll).
- `AdvantechAdamIoModule : IIoModule` — ADAM-6000 series qua Modbus TCP (DI/DO/AI + WriteAndWaitConfirm).

### ✅ Mitsubishi + Siemens PLC (tự implement protocol, zero dependency)
- `MitsubishiPlcDevice : IPlcDevice` — **MC Protocol 3E binary** qua socket (D/M/X/Y/W/R/B/L, hex/dec radix).
- `SiemensS7PlcDevice : IPlcDevice` — **S7comm / ISO-on-TCP (RFC1006)**: COTP CR + Setup Comm + ReadVar/WriteVar,
  vùng DB/M/I/Q, big-endian, địa chỉ DB10.DBW20 / DB10.DBX0.1 / MW100...

### ✅ Robot (AM.Hardware.Comm/Robot)
- `SocketRobotDevice : IRobotDevice` — TCP ASCII command/response theo dòng, template lệnh cấu hình.
- `SimulatedRobotDevice : IRobotDevice` — in-memory.

### ✅ DI wiring + config
- `Bootstrapper.RegisterRealHardware()` — chọn driver theo `appsettings` (UseSimulation=false):
  Motion: Simulated|Gts|Advantech|InovanceServo · Plc: Inovance|Mitsubishi|Siemens · Io: Simulated|AdvantechAdam · Robot: Simulated|Socket.
- Simulation branch: thêm `IPlcDevice`→SimulatedPlcDevice, `IRobotDevice`→SimulatedRobotDevice.
- `appsettings.json`: thêm block `Motion`/`Plc`/`Robot`/`Io` (host/port/slave/vendor).

### ✅ Tests (AM.Hardware.Tests — project mới, 17 tests)
- `ModbusTcpClientTests` — round-trip FC03/FC06/FC16 qua **Modbus slave giả loopback** (verify MBAP thật trên wire).
- `InovancePlcDeviceTests` — ánh xạ địa chỉ + word/dword/float/bit qua SimulatedModbusClient.
- `SocketRobotDeviceTests` — giao thức line-based qua **robot server giả loopback**.
- `SimulatedDeviceTests` — SimulatedPlcDevice, SimulatedRobotDevice, AdvantechAdamIoModule.

### ⚠️ Ghi chú phần cứng SDK-native
- SDK 固高 GTS (`gts.dll`) và Advantech (`ADVMOT.dll`) là DLL độc quyền theo card, **không tải qua NuGet được**.
  Driver dùng P/Invoke nên build không cần DLL và chạy thật khi PC sản xuất đã cài driver/SDK của card.
- Các hằng số bit-status (GTS/Advantech) và register map (Inovance servo) cần đối chiếu manual của thiết bị.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **Build succeeded, 0 Warning, 0 Error**.
- `dotnet test` → **55 passed** (38 services + 17 hardware).

---

## [Session 8] 2026-06-02 — DI wiring + Demo 3-tier + Unit Tests + Dashboard

**Commit:** `9f6898f`
**Người thực hiện:** Claude (Cowork) + Nhan
**Mục tiêu:** Vá 3 lỗ hổng làm framework "viết xong nhưng chưa dùng được" + thêm UI đầu tiên.

### ✅ Task 1 — Fix Bootstrapper DI (Gap 1)
- `AM.Application.Shell/Bootstrapper.cs`:
  - Đăng ký `IHardwareManagerService → HardwareManagerService` và `IStationSyncService → StationSyncService`
    (trước đây code đã viết nhưng KHÔNG có trong DI → mọi `Resolve<T>()` fail at runtime).
  - Thêm `RegisterDemoMachine()` — đăng ký DemoPick/DemoInspect Mechanism, DemoStation,
    DemoMasterController, và map `IMasterController → DemoMasterController` (cho Dashboard resolve).
  - Thêm `RegisterHardwareDevices()` — đăng ký 8 hardware device vào named registry của
    HardwareManagerService (MainMotion/MainCamera/MainIO/MainModbus/MainSerial/MainTcp/MainOpcUA/MainEthernetIP).
- `AM.Application.Shell/App.xaml.cs`: gọi `RegisterHardwareDevices(_serviceProvider)` sau khi build container.

### ✅ Task 2 — Demo machine 3-tier (Gap 2)
Minh hoạ kiến trúc 3 tầng cho developer làm máy mới:
- `AM.WorkStation.Demo/Mechanisms/DemoPickMechanism.cs` — `[MechanismUI]`, extends BaseMechanism, gọi IMotionController.
- `AM.WorkStation.Demo/Mechanisms/DemoInspectMechanism.cs` — extends BaseMechanism, gọi ICameraDevice.
- `AM.WorkStation.Demo/Stations/DemoStation.cs` — `[StationUI]`, extends StationBase<DemoStation>, điều phối 2 mechanism.
- `AM.WorkStation.Demo/Controllers/DemoMasterController.cs` — extends BaseMasterController, template methods
  InitializeCoreAsync/RunOneCycleAsync/ResetCoreAsync/ShouldReinitialize.
- `AM.WorkStation.Demo.csproj`: thêm ProjectReference `AM.Infrastructure`.

### ✅ Task 3 — Unit Tests (Gap 3)
- `AM.Services.Tests/` (xUnit + Moq + FluentAssertions, net9.0):
  - `AlarmServiceTests.cs` — raise/ack/clear, level resolution theo code range (safety critical).
  - `RecipeServiceTests.cs` — load/save/getall.
  - `StationSyncServiceTests.cs` — RegisterSlot, Signal/WaitAsync, timeout, cancellation, ResetAll, multi-slot.
  - `GlobalUsings.cs` — global using Xunit.
- Thêm project vào solution. **38/38 tests pass, 0 warning** (đã fix xUnit1031, CA2007, S6608).

### ✅ Task 4 — WPF Dashboard module
- `AM.Modules.Dashboard/` (net9.0-windows, UseWPF, CommunityToolkit.Mvvm):
  - `DashboardViewModel.cs` — ObservableObject; bind MachineState + CycleCount + ActiveAlarms;
    RelayCommand Initialize/Start/Stop/Pause/Resume/Reset với CanExecute theo state; subscribe
    StateChanged/CycleCompleted/AlarmRaised/AlarmCleared; UI-thread marshalling qua SynchronizationContext; IDisposable.
  - `DashboardView.xaml` + `.xaml.cs` — ISA-101 layout: state indicator (Ellipse màu theo state),
    control buttons (48px, nút Dừng tách 48px màu đỏ), DataGrid alarm list. Dùng DynamicResource color tokens.
  - `Converters/MachineStateToColorConverter.cs` — MachineState → Status.*Brush.
- Thêm project vào solution.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **Build succeeded, 0 Warning, 0 Error**.
- `dotnet test` → **38 passed**.

---

## [Session 7] 2026-05-31 — Solution Structure Docs + HMI Design Rules

**Commit:** `8fa4568`
**Người thực hiện:** Claude (Cowork) + Nhan
**Nguồn tham khảo:** AutoMachine_Solution_Structure.md + HMI_UI_Design_Rules_1.md

### ✅ Thêm mới / Cập nhật

- `CLAUDE.md` — Cập nhật **Cấu trúc solution** đầy đủ:
  - Thêm `AM.Hardware.Communication` (Serial/TCP/Modbus/SECS-GEM)
  - Thêm interfaces còn thiếu: `IAxisGroup`, `ILightController`, `ICommunicationDevice`, `IUserService`, `IProductionService`, `ILocalizationService`
  - Thêm `AM.Modules.*` (10 UI modules: Alarm, Parameter, Production, IO, Motion, Vision, Identity, Logging, Diagnostics, SecsGem)
  - Thêm `AM.UI.Controls/` và `AM.UI.Resources/` (Themes: Colors.Dark/Light, Typography, Controls, StatusStyles)
  - Thêm cấu trúc `AM.WorkStation.{MachineName}/` đầy đủ (Steps, SubRoutines, Recipe, Config/AxisMap+IOMap)
- `.claude/skills/am-wpf-mvvm/SKILL.md` — Bổ sung **ISA-101 HMI Design Rules**:
  - Semantic color tokens (Status.Normal/Warning/Alarm/Critical/Disabled — bất biến giữa themes)
  - Background tokens Dark/Light đầy đủ với hex values
  - Quy tắc màu KHÔNG ĐƯỢC VI PHẠM (đỏ/vàng chỉ cho alarm, equipment normal = xám)
  - DataTrigger pattern cho device state
  - Animation rules (chỉ Critical alarm 1 Hz, không vượt 3 Hz SEMI S8)
  - ListView virtualization template
  - ThemeService runtime switch Dark/Light
  - Format số liệu và đơn vị (decimal places, timestamp 24h)
  - ISA-101 checklist trước khi release màn hình
- `.claude/skills/am-hmi-design/SKILL.md` — **Skill mới**: HMI design reference đầy đủ:
  - 4-level screen hierarchy (Overview → Process Area → Faceplate → Engineering)
  - Layout shell cố định (Top Bar/Side Menu/Content/Status Bar)
  - Alarm display rules ISA-18.2/EEMUA 191 (mức độ, màu, nhấp nháy, rate)
  - Equipment state symbols (≥2 cách biểu thị: màu + icon/text)
  - I/O display rules (DI ○/● khác DO □/■)
  - Navigation rules (max 3 click, breadcrumb)
  - Performance targets (response time, update rate per data type)
  - Release checklist đầy đủ (màu, alarm, navigation, data, ergonomics, i18n)

### 🔧 Quyết định chọn lọc
- **CLAUDE.md**: chỉ cập nhật cấu trúc solution (bản đồ file), không copy toàn bộ implementation detail
- **am-wpf-mvvm**: thêm ISA-101 rules vào skill sẵn có (không tạo file trùng lặp)
- **am-hmi-design**: tạo skill riêng vì HMI design là domain riêng biệt, lazy-load khi cần
- **Không thêm**: toàn bộ XAML ResourceDictionary files (200+ dòng) → quá dài, developer tự viết theo template

---

## [Session 6] 2026-05-31 — 3-Tier Base Classes + Services + Coding Rules Alignment

**Commit:** `832266f`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới

- `AM.Infrastructure/BaseMechanism.cs` — Abstract base cho Mechanism: `IsBusy` guard dùng `Interlocked`, `EmergencyStop` wrapper không throw, template methods (`InitializeCoreAsync`, `HomeCoreAsync`, `OnEmergencyStop`), `ExecuteWithBusyGuardAsync<T>` helper, `IAsyncDisposable`
- `AM.Infrastructure/StationBase.cs` — Abstract base cho Station: `RegisterMechanism`, `SetState` + `StateChanged` event, `RunCycleAsync` template, parallel `HomeAsync` cho tất cả mechanisms, typed logger `ILogger<TStation>`
- `AM.Infrastructure/BaseMasterController.cs` — Abstract base cho MasterController: ISA-88 transition table (13 transitions), `FireTrigger` thread-safe, `CheckPauseAsync` gate (SemaphoreSlim), run loop với 3-catch hierarchy, `CancelAsync` cho stop/dispose
- `AM.Services/HardwareManagerService.cs` — Implement `IHardwareManagerService`: device registry, `Resolve<T>` type-safe, `ConnectAllAsync/DisconnectAllAsync` dùng pattern matching (IMotionController/ICameraDevice/IIoModule)
- `AM.Services/StationSyncService.cs` — Implement `IStationSyncService`: `SemaphoreSlim`-based pipeline sync, `RegisterSlot/Signal/WaitAsync(timeout)`, `ResetAll`, `IDisposable`
- `AM.Core/Models/EventArgs/MachineStateChangedEventArgs.cs` — EventArgs cho StateChanged (CA1003): `PreviousState`, `NewState`, `Trigger`, `ChangedAt`
- `AM.Core/Models/EventArgs/CycleCompletedEventArgs.cs` — EventArgs cho CycleCompleted (CA1003): `CycleCount`, `CompletedAt`

### 🔧 Sửa đổi

- `AM.Core.Abstractions/Interfaces/Machine/IStation.cs` — `StateChanged` event: `EventHandler<MachineState>` → `EventHandler<MachineStateChangedEventArgs>` (CA1003)
- `AM.Core.Abstractions/Interfaces/Machine/IMasterController.cs` — `StateChanged` + `CycleCompleted` event: dùng EventArgs wrappers (CA1003)
- `AM.Infrastructure/AM.Infrastructure.csproj` — Thêm `AM.Core.Abstractions` project reference
- `AM.Application.Shell/AM.Application.Shell.csproj` — Thêm explicit `Microsoft.Extensions.Configuration.*` packages (fix wpftmp build)
- `AM.Application.Shell/App.xaml` — **Bug fix**: Xóa `StartupUri` (crash runtime), thêm color token + string ResourceDictionary
- `AM.Application.Shell/MainWindow.xaml` — `{DynamicResource ...}` thay hardcoded colors, `{StaticResource ...}` thay hardcoded strings
- `AM.Application.Shell/MainWindow.xaml.cs` — `System.Windows.Application.Current.Resources[...]` thay `Brushes.Red/Green`
- `AM.Application.Shell/Bootstrapper.cs` — CA1515→`internal`, S125 remove commented code, CA1305 + CultureInfo.InvariantCulture, CA2007 fix
- `AM.Services/AlarmService.cs` — Fix `ResolveLevel`: dùng range constants (10000-69999→High, 40000-49999+70000-79999→Critical) thay vì specific alarm codes (MotionTimeout/SystemCritical đã bị miss)
- `AM.Data/Repositories/AlarmRepository.cs` — Fix `MapToModel`: restore `IsAcknowledged`, `AcknowledgedAt`, `AcknowledgedBy` từ entity
- `AM.Core/Models/AlarmModel.cs` — `Acknowledge(operatorId, DateTime?)` — optional timestamp để preserve gốc khi load từ DB
- `AM.WorkStation.Demo/Steps/Step01Initialize.cs` — Rename file (bỏ underscore, B1 fix)
- `AM.WorkStation.Demo/Steps/Step02Inspect.cs` — Rename file (bỏ underscore, B2 fix)
- `Directory.Build.props` — `TargetFramework`: `net8.0-windows` → `net9.0` (khớp với .csproj thực tế)

### 🐛 Bugs đã fix

- **Runtime crash**: `StartupUri="MainWindow.xaml"` + `async void OnStartup` tạo 2 MainWindow (thiếu parameterless ctor → crash)
- **CA1003**: Events `StateChanged<MachineState>`, `CycleCompleted<int>` → phải dùng `EventArgs` subclass
- **Alarm severity bug**: `MotionTimeout` (10001) và `MotionNotHomed` (10002) bị resolve thành `Medium` vì range check dùng `AlarmCodes.MotionEstop` (10003) làm lower bound
- **B1/B2 CA1707**: File names `Step01_Initialize.cs`, `Step02_Inspect.cs` đổi thành không có underscore
- **DB data loss**: `AlarmRepository.MapToModel` bỏ sót acknowledged state khi load history

### 🔧 Quyết định kiến trúc

1. **`BaseMasterController` dùng static `Dictionary<>` transition table** (không dùng `Stateless` NuGet): Tránh thêm dependency, state machine đủ đơn giản, performance tốt hơn với Dictionary direct access (CA1859).
2. **`StationBase<TStation>` generic** (CRTP pattern): `ILogger<TStation>` cho phép log entries hiển thị tên concrete class thay vì `StationBase` — suppressed S6672 với justification rõ ràng.
3. **`HardwareManagerService.ConnectAllAsync` dùng pattern matching** (không reflection): Type-safe, no runtime overhead, explicit về hardware interfaces được support — future hardware interfaces cần thêm vào switch.
4. **`EmergencyStop` trong BaseMechanism/StationBase/BaseMasterController**: Luôn wrap trong try-catch với CA1031 pragma — safety critical path KHÔNG được throw bất cứ điều gì.

---

## [Session 5] 2026-05-30 — Karpathy Rules + Alarm Dictionary + Context Management

**Commit:** `be47f2a`
**Người thực hiện:** Claude (Cowork) + Nhan
**Nguồn tham khảo:** `Claude_Effective_Usage_AutoMachine.md` (Karpathy/ECC/Anthropic best practices)

### ✅ Thêm mới / Cập nhật

- `CLAUDE.md` — Thêm **Karpathy 4 Rules** (Think First / Surgical / Simple / Success First) dưới dạng bảng ngắn gọn. Claude tự áp dụng trong mọi task, kể cả khi user không nhắc.
- `file hướng dẫn code/PROMPT_TEMPLATES.md` — Thêm **PT-00 Plan Template**: format plan chuẩn cho task > 30 phút (Files thay đổi, Approach options, Steps, Success Criteria, Confirm).
- `file hướng dẫn code/QUICK_REFERENCE.md` — Thêm 4 section mới:
  - **Magic Phrases** — 8 câu thêm vào prompt để điều chỉnh hành vi Claude
  - **Anti-patterns** — 6 lỗi phổ biến + cách đúng
  - **Hallucination signs** — 6 dấu hiệu Claude đang bịa + cách xử lý
  - **Context Health** — 5 dấu hiệu session đang có vấn đề
- `.claude/skills/am-alarm-dictionary/SKILL.md` — **Skill mới**: alarm code ranges, format message chuẩn, template AlarmCodes.cs, cách throw AlarmException, checklist thêm alarm, giải thích isStoppable.

### 🔧 Quyết định chọn lọc nội dung
Từ 730 dòng tài liệu gốc, chỉ lấy 4 thứ **chưa có** trong dự án:
1. Karpathy rules → CLAUDE.md (hành vi Claude, không phải coding rules)
2. Magic phrases + anti-patterns → QUICK_REFERENCE.md (tham khảo nhanh)
3. Plan template → PROMPT_TEMPLATES.md (dùng trước task lớn)
4. Alarm dictionary skill → skills/ (lazy-load khi cần)

**Không thêm:** model selection table (đã có trong AGENTS.md), daily workflow (đã có), phase workflow (quá meta), context % advice (không kiểm soát được từ user).

---

## [Session 4] 2026-05-29 — Claude Code Hooks

**Commit:** `TBD`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới

- `.claude/hooks/pre-write-arch.sh` — **PreToolUse Write**: Kiểm tra file C# sắp viết đúng layer chưa (interface → Abstractions, hardware driver → Hardware.*, Step naming CA1707, Service không new concrete class)
- `.claude/hooks/post-write-cs.sh` — **PostToolUse Write+Edit**: Sau khi lưu file .cs, tự động kiểm tra CA1707 (underscore), CA2000 (CancellationTokenSource), RSPEC-6602/6605 (LINQ), CA1031 (bare catch), thiếu file header, thiếu XML doc
- `.claude/hooks/post-build.sh` — **PostToolUse Bash**: Sau `dotnet build/test`, parse output thành bảng tóm tắt — violations theo rule code, files có lỗi, top errors cần fix
- `.claude/hooks/check-session-end.sh` — **Stop**: Khi Claude kết thúc, kiểm tra uncommitted changes + hiển thị TODO list từ PROJECT_STATUS.md, nhắc chạy `/am-done`

### 🔧 Sửa đổi
- `.claude/settings.local.json` — Wire 4 hooks: PreToolUse(Write), PostToolUse(Write), PostToolUse(Edit), PostToolUse(Bash), Stop

### 🔧 Quyết định thiết kế
1. **Exit 0 cho tất cả hooks (không block)**: Hooks chỉ cảnh báo, không chặn Claude — tránh false positive làm gián đoạn workflow. Nếu muốn chặn hoàn toàn đổi `exit 0` → `exit 2` trong `pre-write-arch.sh`.
2. **Shell script thay vì Node.js**: Không cần runtime cài thêm, portable trên mọi môi trường Linux/WSL/sandbox.
3. **post-write-cs chạy trên file path** (không phải stdin content): Đọc trực tiếp file vừa lưu để phân tích chính xác hơn là phân tích content từ JSON input.

---

## [Session 3] 2026-05-29 — Hệ thống tracking + Auto-commit workflow

**Commit:** `d068d2e`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới
- `PROJECT_STATUS.md` — **Mới**: Snapshot toàn bộ trạng thái dự án, TODO list, known bugs, key files map. Claude đọc file này đầu mỗi session thay vì scan toàn bộ source.
- `CHANGELOG.md` — **Mới**: Lịch sử thay đổi theo session, ghi cả quyết định kiến trúc.
- `scripts/am-commit.sh` — **Mới**: Shell script git add+commit+push tự động xử lý Windows filesystem index.lock issue (dùng `mv` thay vì `rm`).
- `.claude/commands/am-done.md` — **Mới**: Slash command `/am-done` — workflow cuối session: cập nhật status → thêm changelog entry → commit → push.
- `.claude/hooks/check-session-end.sh` — **Mới**: Hook chạy khi Claude Stop, kiểm tra nếu có uncommitted changes mà status chưa cập nhật thì nhắc chạy `/am-done`.

### 🔧 Sửa đổi
- `CLAUDE.md` — Thêm checklist bắt buộc cuối session (cập nhật status + changelog + commit).
- `.claude/settings.local.json` — Thêm `permissions.allow` cho git commands + `hooks.Stop` trigger hook khi Claude kết thúc.

### 🔧 Quyết định thiết kế
1. **PROJECT_STATUS.md thay vì chỉ CLAUDE.md**: CLAUDE.md mô tả kiến trúc (ít thay đổi), PROJECT_STATUS.md mô tả trạng thái hiện tại (thay đổi mỗi session) — tách biệt để tiết kiệm token khi Claude đọc.
2. **`mv` thay vì `rm` cho index.lock**: Windows filesystem mount không cho phép unlink file trong `.git/`, nhưng cho phép rename/move.
3. **Hook Stop + /am-done command**: Hook nhắc tự động, command cho phép Claude chủ động thực hiện — hai lớp đảm bảo không bỏ sót update.

---

## [Session 2] 2026-05-29 — Kiến trúc 3 tầng + Claude Code Integration

**Commit:** `cb525a5`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới

#### AM.Core — Enums
- `Enums/MachineState.cs` — Cập nhật từ 10 state → **8 state ISA-88**: Uninitialized, Initializing, Idle, Running, Paused, InitAlarm, RunAlarm, Resetting
- `Enums/MachineTrigger.cs` — **Mới**: 10 triggers (Initialize, InitializeDone, Start, Pause, Resume, Stop, Error, Reset, ResetDone, ResetDoneUninitialized)
- `Enums/OperationMode.cs` — **Mới**: Normal, DryRun
- `Enums/HardwareCategory.cs` — **Mới**: 9 categories (General, Axis, IOController, Camera, Robot, Scanner, Instrument, MotionCard, LightController)
- `Enums/UserLevel.cs` — **Mới**: Null=-1, Operator=0, Engineer=1, Administrator=2, SuperUser=3

#### AM.Core — Attributes
- `Attributes/AlarmInfoAttribute.cs` — **Mới**: `[AlarmInfo(displayName, remedy, isStoppable)]` cho alarm code constants
- `Attributes/MechanismUIAttribute.cs` — **Mới**: `[MechanismUI(displayName, group, order)]` cho Mechanism classes
- `Attributes/StationUIAttribute.cs` — **Mới**: `[StationUI(displayName, icon, order)]` cho Station classes
- `Attributes/ModuleNavigationAttribute.cs` — **Mới**: `[ModuleNavigation(displayName, icon, region, order)]` cho Prism Views
- `Attributes/ParamViewAttribute.cs` — **Mới**: `[ParamView(label, unit, min, max, group, order)]` cho Recipe properties

#### AM.Core.Abstractions — Machine interfaces
- `Interfaces/Machine/IMechanism.cs` — **Mới**: Name, Category, IsReady, IsBusy, InitializeAsync, HomeAsync, EmergencyStop
- `Interfaces/Machine/IStation.cs` — **Mới**: Name, State, Mechanisms, StateChanged event, RunCycleAsync, EmergencyStop
- `Interfaces/Machine/IMasterController.cs` — **Mới**: ISA-88 full state machine, InitializeAsync/StartAsync/StopAsync/ResetAsync, SetOperationMode

#### AM.Core.Abstractions — Service interfaces
- `Interfaces/Services/IHardwareManagerService.cs` — **Mới**: Register, Resolve<T>, ResolveAll<T>, IsRegistered, ConnectAllAsync, DisconnectAllAsync
- `Interfaces/Services/IStationSyncService.cs` — **Mới**: RegisterSlot, Signal, WaitAsync (2 overloads), ResetAll

#### Claude Code Integration (.claude/)
- `.claude/rules/common/coding-standards.md` — **Mới**: 17 rules R01–R17 (safety, arch, async, hardware timeout, exceptions, logging, null safety, simulation, IDisposable, file header, XML doc, Sonar table, attributes, UserLevel)
- `.claude/rules/csharp/csharp-patterns.md` — **Mới**: 15 patterns CS01–CS15 (naming, constructor, timeout, exception filter, sequence loop, List.Find/Exists, JsonOptions, ConfigureAwait, state machine switch, WPF dispatch, IDisposable, EF Core, records, sealed)
- `.claude/commands/am-new-driver.md` — **Mới**: Slash command tạo hardware driver
- `.claude/commands/am-new-step.md` — **Mới**: Slash command tạo sequence step (enforces no underscore)
- `.claude/commands/am-new-mechanism.md` — **Mới**: Slash command tạo Mechanism với [MechanismUI]
- `.claude/commands/am-new-station.md` — **Mới**: Slash command tạo Station với [StationUI]
- `.claude/commands/am-new-screen.md` — **Mới**: Slash command tạo WPF screen ISA-101
- `.claude/commands/am-alarm.md` — **Mới**: Slash command thêm alarm code
- `.claude/commands/am-review.md` — **Mới**: Slash command review code (10 categories)
- `.claude/commands/am-test.md` — **Mới**: Slash command tạo unit tests
- `.claude/skills/am-hardware-patterns/SKILL.md` — **Mới**: Interface + real driver + simulator templates
- `.claude/skills/am-sequence-patterns/SKILL.md` — **Mới**: Step + MachineSequence templates
- `.claude/skills/am-mechanism-patterns/SKILL.md` — **Mới**: [MechanismUI] + hwManager.Resolve patterns
- `.claude/skills/am-station-patterns/SKILL.md` — **Mới**: [StationUI] + IStationSyncService patterns
- `.claude/skills/am-testing/SKILL.md` — **Mới**: xUnit + Moq + FluentAssertions templates
- `.claude/skills/am-wpf-mvvm/SKILL.md` — **Mới**: ViewModel + XAML + Prism Module + ISA-101 checklist

#### AI Documentation
- `CLAUDE.md` — **Mới** (project root): Project instructions cho Claude — kiến trúc, build rules, workflow
- `file hướng dẫn code/AGENTS.md` — **Cập nhật**: Thêm ECC routing table + 3 agents mới (MechanismDeveloper, StationDeveloper, MasterControllerDeveloper), cập nhật agents cũ lên .NET 9
- `file hướng dẫn code/PROMPT_TEMPLATES.md` — **Cập nhật**: Thêm PT-12 (Mechanism), PT-13 (Station), PT-14 (MasterController)
- `file hướng dẫn code/QUICK_REFERENCE.md` — **Cập nhật**: Thêm 3-tier arch rules, slash commands, attributes quick ref, state machine diagram, ISA-88 naming conventions
- `.cursorrules` — **Cập nhật**: Thêm R-ARCH-05/06/07, R-MECH-01/02/03/04, R-STATION-01/02/03/04, R-PERM-01/02/03, R-ATTR-01/02

### 🔧 Quyết định kiến trúc

1. **State machine 8 trạng thái thay vì 10**: Giảm phức tạp, bỏ `Stopping` và `Homing` riêng lẻ. `Resetting` bao gồm cả home sequence.
2. **IHardwareManagerService thay vì inject hardware trực tiếp**: Mechanism không nhận `IMotionController` qua DI constructor, thay vào đó gọi `hwManager.Resolve<T>("name")` — linh động hơn khi cấu hình nhiều hardware cùng loại.
3. **StationSyncService dùng SemaphoreSlim**: Không busy-wait, không dùng event/flag thủ công — tránh race condition và deadlock.
4. **Attributes cho auto-registration**: `[MechanismUI]`, `[StationUI]`, `[ModuleNavigation]` cho phép UI tự scan + đăng ký panel/tab/menu mà không cần code thủ công trong Bootstrapper.

---

## [Session 1] 2026-05-28 — Initial Framework Setup

**Commit:** `fe716a9`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Tạo mới từ đầu

#### Solution & Build
- `AM.AutoFrame.sln` 