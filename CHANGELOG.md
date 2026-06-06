# CHANGELOG — AM.AutoFrame
> Ghi lại mọi thay đổi có ý nghĩa theo từng session làm việc.
> Format: `## [Session N] YYYY-MM-DD — Tiêu đề ngắn`

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