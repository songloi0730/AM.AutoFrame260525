# CLAUDE.md — AM.AutoFrame Project

> Hướng dẫn dành riêng cho Claude (và các AI assistant) khi làm việc với dự án này.

## ⚡ BẮT BUỘC — Đọc theo thứ tự này trước khi làm bất cứ thứ gì:

1. **`PROJECT_STATUS.md`** — Trạng thái hiện tại, TODO list, known bugs → đọc trước nhất
2. **`CLAUDE.md`** (file này) — Kiến trúc, build rules, conventions
3. **`CHANGELOG.md`** — Khi cần hiểu lý do của một quyết định kiến trúc

> Không cần đọc toàn bộ source files — PROJECT_STATUS.md đã tóm tắt đủ để bắt đầu.

## ⚡ BẮT BUỘC — Cuối mỗi session (trước khi kết thúc):

```
1. Cập nhật PROJECT_STATUS.md  — đổi trạng thái, thêm/xoá TODO
2. Thêm entry vào CHANGELOG.md — ghi rõ file nào thay đổi, lý do gì
3. Commit + push:  bash scripts/am-commit.sh "loại: mô tả"
```

> **Nếu không làm được push** (sandbox bị chặn): vẫn commit, báo user chạy `git push origin main`.
> **Slash command nhanh**: `/am-done` — tự động làm cả 3 bước trên.

---

## Dự án là gì?

**AM.AutoFrame** là C# framework cho phần mềm điều khiển máy tự động hoá công nghiệp.
- Nền tảng: .NET 9 / WPF / Prism 9 / DryIoc / EF Core + SQLite
- Build: `TreatWarningsAsErrors=true` + `AnalysisMode=All` — mọi warning CA/Sonar là lỗi build
- Kiến trúc: 3 tầng máy (MasterController → Station → Mechanism) + ISA-88 state machine 8 trạng thái

---

## Cấu trúc solution quan trọng

```
AM.Core/                         — Enums, Models, Constants, Exceptions
  Enums/                         — MachineState, MachineTrigger, OperationMode,
                                    HardwareCategory, UserLevel, AlarmLevel
  Attributes/                    — AlarmInfoAttribute, MechanismUIAttribute,
                                    StationUIAttribute, ModuleNavigationAttribute, ParamViewAttribute
  Models/                        — AlarmModel, Recipe, ProductionRecord
  Constants/                     — AlarmCodes
  Exceptions/                    — AlarmException

AM.Core.Abstractions/            — Interfaces only, no implementation
  Interfaces/Hardware/           — IMotionController, ICameraDevice, IIoModule
  Interfaces/Machine/            — IMechanism, IStation, IMasterController
  Interfaces/Services/           — IAlarmService, IRecipeService, IParameterService,
                                    IHardwareManagerService, IStationSyncService
  Interfaces/Repositories/       — IAlarmRepository, IProductionRepository
  Interfaces/                    — IStep

AM.Hardware.Motion/              — SimulatedMotionController
AM.Hardware.Vision/              — SimulatedCameraDevice
AM.Hardware.IO/                  — SimulatedIoModule

AM.Services/                     — AlarmService, RecipeService, ParameterService
AM.Data/                         — AutoMachineDbContext, Repositories
AM.Infrastructure/               — (TODO: BaseMechanism, StationBase, BaseMasterController)

AM.WorkStation.Demo/             — Demo machine: Steps, DemoMachineSequence
AM.Application.Shell/            — WPF entry point, Bootstrapper (DI)
```

---

## Luật build cứng — vi phạm = không build được

| Rule | Nội dung |
|------|----------|
| CA1707 | Không dùng underscore trong tên class/method (Step01**_**Init → Step01Init) |
| CA1003 | EventHandler phải dùng `EventArgs` subclass (AlarmEventArgs, không AlarmModel) |
| CA1716 | Không dùng reserved keyword làm param name (`to` → `endDate`, `Get` → `GetValue`) |
| CA1031 | Không bắt `Exception` chung — dùng `#pragma warning disable CA1031` với justification |
| CA2000 | `CancellationTokenSource.CreateLinkedTokenSource(ct)` phải `using var` |
| RSPEC-2139 | Double-catch pattern → dùng exception filter `when (ex is not AlarmException)` |
| RSPEC-6667 | `catch (Exception ex)` → logger phải nhận `ex` làm tham số đầu tiên |
| RSPEC-6602 | Dùng `List<T>.Find()` thay vì LINQ `FirstOrDefault()` |
| RSPEC-6605 | Dùng `List<T>.Exists()` thay vì LINQ `Any()` |
| CA1869 | `JsonSerializerOptions` phải là `static readonly` field |
| CA5394 | `Random` trong simulator → `[SuppressMessage("Security","CA5394",...)]` |
| CA1512 | Dùng `ArgumentOutOfRangeException.ThrowIfNegativeOrZero()` |
| S2365 | Property trả copy collection → `[SuppressMessage("Major Code Smell","S2365",...)]` |

---

## Kiến trúc 3 tầng — không vi phạm

```
MasterController (BaseMasterController)
   ├── Station A (StationBase<T>)
   │     ├── PickMechanism (BaseMechanism)   ← gọi IMotionController
   │     └── InspectMechanism (BaseMechanism) ← gọi ICameraDevice
   └── Station B (StationBase<T>)
         └── PlaceMechanism (BaseMechanism)  ← gọi IMotionController + IIoModule
```

**Nguyên tắc:**
- Station KHÔNG gọi hardware trực tiếp — chỉ gọi methods của Mechanisms
- MasterController là nơi DUY NHẤT fire MachineTrigger / thay đổi State
- Pipeline sync giữa stations: dùng `IStationSyncService`, không busy-wait

---

## State machine 8 trạng thái

```
Uninitialized ──[Initialize]──► Initializing ──[InitializeDone]──► Idle
                                     │[Error]                       │[Start]
                                     ▼                              ▼
                                 InitAlarm      Paused ◄──[Pause]── Running
                                     │[Reset]     │[Resume]──────────►│
                                     ▼            │[Stop]             │[Error]
                                 Resetting ◄──────┤                   ▼
                                     │         RunAlarm ──[Reset]──► Resetting
                              [ResetDone]▼
                                     Idle
                        [ResetDoneUninitialized]▼
                                 Uninitialized
```

Triggers: `Initialize`, `InitializeDone`, `Start`, `Pause`, `Resume`, `Stop`, `Error`, `Reset`, `ResetDone`, `ResetDoneUninitialized`

---

## Attributes — khi nào dùng

| Attribute | Đặt trên | Mục đích |
|-----------|----------|----------|
| `[AlarmInfo("...", "...", isStoppable)]` | AlarmCodes constant fields | UI tự load metadata alarm |
| `[MechanismUI("...", group, order)]` | Mechanism classes | Debug UI tự đăng ký panel |
| `[StationUI("...", icon, order)]` | Station classes | Debug UI tự đăng ký tab |
| `[ModuleNavigation("...", icon, region, order)]` | Prism View classes | Sidebar tự tạo menu item |
| `[ParamView("...", unit, min, max, group, order)]` | Recipe/Parameter properties | UI tự render input field |

---

## Phân quyền UserLevel

```
Null(-1)        — chưa đăng nhập
Operator(0)     — Start/Stop, xem alarm/recipe
Engineer(1)     — chỉnh recipe, parameter, manual jog
Administrator(2)— cấu hình hệ thống, quản lý user
SuperUser(3)    — override safety, debug hardware
```

Luôn check `_userService.CurrentLevel >= UserLevel.X` trước thao tác quan trọng.

---

## Alarm codes

```
10000–10999  Motion / Axis
20000–20999  Vision / Camera
30000–30999  I/O / Sensor
40000–40999  System / Application
50000–50999  Communication / Network
60000–60999  Production / Recipe
70000–70999  Safety / Interlock
```

---

## Quy tắc async

- Mọi hardware call: `await xxx.ConfigureAwait(false)` (Service/Hardware layer)
- Mọi async method: có `CancellationToken ct = default`
- `using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct)` — không để CA2000
- Không bao giờ `.Result`, `.Wait()`, `Thread.Sleep()`

---

## Khi tạo file mới

1. Thêm file header:
   ```csharp
   // -------------------------------------------------------
   // File:    {FileName}.cs
   // Project: {ProjectName}
   // Purpose: {Mô tả ngắn gọn}
   // -------------------------------------------------------
   ```
2. XML doc cho mọi `public` member
3. `ArgumentNullException.ThrowIfNull(x)` trong constructor cho mọi tham số
4. `_logger.LogDebug("Starting {Method}", nameof(MethodName))` đầu mỗi public method quan trọng

---

## Workflow chuẩn khi thêm máy mới

```
1. Tạo AM.WorkStation.{MachineName} project
2. Viết Hardware drivers (nếu chưa có) → AM.Hardware.{Category}/
3. Viết Mechanisms → Mechanisms/{Name}Mechanism.cs  [MechanismUI]
4. Viết Stations → Stations/{Name}Station.cs         [StationUI]
5. Viết MasterController → Controllers/{Name}MasterController.cs
6. Đăng ký DI trong Bootstrapper.cs
7. Tạo Prism module UI → AM.Modules.{MachineName}/ hoặc AM.WorkStation.{MachineName}.UI/
8. Viết unit tests → tests/AM.WorkStation.{MachineName}.Tests/
```

---

## Claude Code — .claude/ folder

```
.claude/
  rules/
    common/coding-standards.md   — 17 rules (safety, async, alarm, logging...)
    csharp/csharp-patterns.md    — 15 patterns (Step naming, timeout, exception filter...)
  commands/
    am-new-driver.md             — /am-new-driver   : tạo hardware driver
    am-new-step.md               — /am-new-step     : tạo sequence step (no underscore)
    am-new-mechanism.md          — /am-new-mechanism: tạo Mechanism [MechanismUI]
    am-new-station.md            — /am-new-station  : tạo Station [StationUI]
    am-new-screen.md             — /am-new-screen   : tạo WPF screen ISA-101
    am-alarm.md                  — /am-alarm        : thêm alarm code [AlarmInfo]
    am-review.md                 — /am-review       : review code (10 categories)
    am-test.md                   — /am-test         : tạo unit tests xUnit+Moq
  skills/
    am-hardware-patterns/        — interface, driver, simulator templates
    am-sequence-patterns/        — Step, MachineSequence, exception filter
    am-mechanism-patterns/       — [MechanismUI], hwManager.Resolve, EmergencyStop
    am-station-patterns/         — [StationUI], pipeline, WaitAsync/Signal
    am-testing/                  — xUnit+Moq+FluentAssertions templates
    am-wpf-mvvm/                 — ViewModel, XAML, Prism Module, ISA-101 checklist
```

---

## Tài liệu tham khảo trong dự án

| File | Nội dung |
|------|----------|
| `.cursorrules` | Toàn bộ coding rules (AI coding assistant rules) |
| `.claude/rules/` | Claude Code rules — áp dụng tự động cho mọi session |
| `.claude/commands/` | Slash commands `/am-*` |
| `.claude/skills/` | Skill templates — code patterns tham khảo |
| `file hướng dẫn code/AGENTS.md` | Agent definitions + ECC routing table |
| `file hướng dẫn code/PROMPT_TEMPLATES.md` | PT-01 đến PT-14 — copy & fill |
| `file hướng dẫn code/QUICK_REFERENCE.md` | In ra dán cạnh màn hình |
| `CLAUDE.md` | File này — project instructions cho Claude |
| `README.md` | *(chưa có — TODO)* Tổng quan kiến trúc solution |
