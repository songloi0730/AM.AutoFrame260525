# CHANGELOG — AM.AutoFrame
> Ghi lại mọi thay đổi có ý nghĩa theo từng session làm việc.
> Format: `## [Session N] YYYY-MM-DD — Tiêu đề ngắn`

---

## [Session 7] 2026-05-31 — Solution Structure Docs + HMI Design Rules

**Commit:** `TBD`
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
- `AM.AutoFrame.sln` — Solution với 11 projects
- `Directory.Build.props` — TreatWarningsAsErrors=true, AnalysisMode=All, .NET 9, C# 13, WPF
- `.editorconfig` — Code style
- `.gitignore` — Bỏ qua bin/, obj/, *.db, *.log

#### AM.Core
- `Enums/MachineState.cs` — Ban đầu 10 states (sửa lại ở Session 2)
- `Enums/AlarmLevel.cs` — Info, Warning, Error, Critical
- `Models/AlarmModel.cs`, `Recipe.cs`, `ProductionRecord.cs`
- `Models/EventArgs/AlarmEventArgs.cs`, `RecipeEventArgs.cs` — CA1003 compliant
- `Constants/AlarmCodes.cs` — PascalCase (sau khi fix CA1707)
- `Exceptions/AlarmException.cs` — 3 constructors chuẩn (CA1032)

#### AM.Core.Abstractions
- `Interfaces/Hardware/IMotionController.cs` — ConnectAsync, MoveAbsAsync, MoveRelAsync, HomeAsync, GetPositionAsync, StopAsync
- `Interfaces/Hardware/ICameraDevice.cs` — ConnectAsync, GrabAsync, RunToolAsync, GetResultAsync
- `Interfaces/Hardware/IIoModule.cs` — ConnectAsync, ReadDI, WriteDO, ReadAI, WriteAO
- `Interfaces/IStep.cs` — ExecuteAsync, Validate
- `Interfaces/Repositories/IAlarmRepository.cs`, `IProductionRepository.cs`
- `Interfaces/Services/IAlarmService.cs`, `IRecipeService.cs`, `IParameterService.cs`

#### AM.Hardware.* (Simulators only)
- `AM.Hardware.Motion/SimulatedMotionController.cs` — In-memory axis simulation, [SuppressMessage CA5394]
- `AM.Hardware.Vision/SimulatedCameraDevice.cs` — Mock grab + tool result
- `AM.Hardware.IO/SimulatedIoModule.cs` — In-memory DO/DI/AO/AI registers

#### AM.Services
- `AlarmService.cs` — Raise/Clear, in-memory list, events, EF persistence
- `RecipeService.cs` — JSON file-based storage, static readonly JsonOptions (CA1869)
- `ParameterService.cs` — Thread-safe parameter store, SemaphoreSlim

#### AM.Data
- `AutoMachineDbContext.cs` — EF Core 9 + SQLite
- `Entities/AlarmHistoryEntity.cs`, `ProductionRecordEntity.cs`
- `Repositories/AlarmRepository.cs`, `ProductionRepository.cs`

#### AM.Infrastructure
- `DispatcherHelper.cs` — WPF thread dispatch helper
- *(TODO: BaseMechanism, StationBase, BaseMasterController)*

#### AM.WorkStation.Demo
- `DemoMachineSequence.cs` — Demo sequence orchestrator
- `Steps/Step01_Initialize.cs` — **⚠️ Cần sửa tên thành Step01Initialize (CA1707)**
- `Steps/Step02_Inspect.cs` — **⚠️ Cần sửa tên thành Step02Inspect (CA1707)**

#### AM.Application.Shell
- `App.xaml` + `App.xaml.cs` — WPF entry point
- `Bootstrapper.cs` — Prism + DryIoc DI setup, đăng ký tất cả services/hardware
- `MainWindow.xaml` — Shell window với Prism regions

### 🔧 Quyết định kiến trúc

1. **DryIoc thay vì Unity**: Prism 9 chính thức support DryIoc, type-safe hơn, performance tốt hơn.
2. **SQLite thay vì SQL Server**: Embedded DB, không cần cài đặt riêng, phù hợp máy standalone.
3. **Recipe lưu JSON thay vì DB**: Recipe là file, dễ backup/copy giữa các máy, không cần migration.
4. **Simulator cùng folder với driver thật**: Dễ swap qua appsettings, không cần project riêng.

### 🐛 Bugs đã fix trong session này
- CA1707: AlarmCodes đổi từ `UPPER_SNAKE_CASE` → `PascalCase`
- CA1032: AlarmException thiếu 3 constructors chuẩn
- CA1716: Interface params dùng reserved keywords (`to` → `endDate`, `Get` → `GetValue`)
- CA1003: EventHandler dùng `AlarmModel` trực tiếp → tạo `AlarmEventArgs`
- CA2000: `CancellationTokenSource` không có `using var`
- CA5394: `Random` trong simulators thiếu `[SuppressMessage]`
- RSPEC-6602/6605: LINQ `FirstOrDefault`/`Any` → `List<T>.Find`/`Exists`
- RSPEC-6667: Logger không nhận exception làm tham số đầu tiên
- CA1869: `JsonSerializerOptions` không phải `static readonly`

---

## 📝 Template cho session tiếp theo

```markdown
## [Session N] YYYY-MM-DD — Tiêu đề

**Commit:** `hash`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới
- `File.cs` — Mô tả

### 🔧 Sửa đổi
- `File.cs` — Thay đổi gì, lý do gì

### 🐛 Bugs đã fix
- CA1234: Mô tả fix

### 🔧 Quyết định kiến trúc
1. **Vấn đề**: lý do → giải pháp chọn
```
