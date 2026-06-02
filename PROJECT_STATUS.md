# PROJECT_STATUS.md — AM.AutoFrame
> **⚡ Claude: Đọc file này TRƯỚC khi bắt đầu bất kỳ thay đổi nào.**
> File này là snapshot trạng thái dự án. Cập nhật cuối cùng sau mỗi session làm việc.

---

## 🗓️ Cập nhật lần cuối
**Ngày:** 2026-06-02
**Session:** #8 — DI wiring + Demo 3-tier + Unit Tests + Dashboard module
**Commit:** *(pending)*

---

## 📊 Trạng thái tổng quan

| Hạng mục | Trạng thái | Ghi chú |
|----------|-----------|---------|
| Solution structure | ✅ Hoàn thành | 11 projects, build clean |
| AM.Core (Enums, Models, Exceptions) | ✅ Hoàn thành | 8-state ISA-88 + 5 attributes |
| AM.Core.Abstractions (Interfaces) | ✅ Hoàn thành | Hardware + Machine + Services |
| AM.Hardware.* (Simulators) | ✅ Hoàn thành | Motion, Vision, IO — chỉ simulated |
| AM.Services | ✅ Hoàn thành | Alarm, Recipe, Parameter |
| AM.Data (EF Core + SQLite) | ✅ Hoàn thành | DbContext, 2 repositories |
| AM.Infrastructure | ✅ Hoàn thành | DispatcherHelper + BaseMechanism + StationBase<T> + BaseMasterController |
| AM.WorkStation.Demo | ✅ Hoàn thành | 3-tier đầy đủ: DemoPickMechanism/DemoInspectMechanism → DemoStation → DemoMasterController |
| AM.Application.Shell | ✅ Hoàn thành | Bootstrapper DI đầy đủ (HardwareManager + StationSync + Demo machine + named hw registry) |
| AM.CommonTools | ✅ Hoàn thành | Guard, RetryHelper |
| .claude/ (AI config) | ✅ Hoàn thành | rules + commands (9) + skills (8) + 4 hooks — am-alarm-dictionary + am-hmi-design |
| PROJECT_STATUS.md + CHANGELOG.md | ✅ Hoàn thành | Tracking system + auto-commit workflow |
| scripts/am-commit.sh | ✅ Hoàn thành | Git wrapper xử lý Windows lock file |
| Unit Tests | ✅ Hoàn thành | AM.Services.Tests — 38 tests pass (Alarm + Recipe + StationSync) |
| AM.Modules.* (WPF UI modules) | 🟡 Một phần | AM.Modules.Dashboard (state + alarm list + controls) — các module khác chưa có |

---

## 🏗️ Kiến trúc hiện tại

### Solution Projects (11 projects)
```
AM.Core                    — Enums, Models, Attributes, AlarmCodes, Exceptions
AM.Core.Abstractions       — Interfaces only (Hardware + Machine + Services + Repos)
AM.CommonTools             — Guard, RetryHelper
AM.Hardware.Motion         — SimulatedMotionController
AM.Hardware.Vision         — SimulatedCameraDevice
AM.Hardware.IO             — SimulatedIoModule
AM.Services                — AlarmService, RecipeService, ParameterService
AM.Data                    — AutoMachineDbContext, AlarmRepository, ProductionRepository
AM.Infrastructure          — DispatcherHelper, BaseMechanism, StationBase<T>, BaseMasterController
AM.WorkStation.Demo        — DemoMasterController → DemoStation → DemoPick/DemoInspect Mechanism (3-tier)
AM.Application.Shell       — WPF entry point, Bootstrapper (DI đầy đủ + named hw registry)
AM.Modules.Dashboard       — WPF module: MachineState indicator + AlarmList + control commands
AM.Services.Tests          — xUnit + Moq + FluentAssertions, 38 tests
```

### 3-Tier Machine Hierarchy (✅ Đầy đủ)
```
[✅ Interface]  IMasterController     — AM.Core.Abstractions/Interfaces/Machine/
[✅ Interface]  IStation              — AM.Core.Abstractions/Interfaces/Machine/
[✅ Interface]  IMechanism            — AM.Core.Abstractions/Interfaces/Machine/
[✅ Base class] BaseMasterController  — AM.Infrastructure/BaseMasterController.cs
[✅ Base class] StationBase<T>        — AM.Infrastructure/StationBase.cs
[✅ Base class] BaseMechanism         — AM.Infrastructure/BaseMechanism.cs
```

### ISA-88 State Machine (8 states, 10 triggers)
```
States:   Uninitialized, Initializing, Idle, Running, Paused, InitAlarm, RunAlarm, Resetting
Triggers: Initialize, InitializeDone, Start, Pause, Resume, Stop, Error, Reset,
          ResetDone, ResetDoneUninitialized
```

---

## 📁 Key files — vị trí và nội dung

### Build & Config
| File | Mô tả |
|------|-------|
| `Directory.Build.props` | TreatWarningsAsErrors=true, AnalysisMode=All, .NET 9, CA suppressions |
| `.editorconfig` | Code style rules |
| `.cursorrules` | AI coding rules (Cursor/Copilot) |

### AI Instructions
| File | Mô tả | Claude cần đọc? |
|------|-------|-----------------|
| `CLAUDE.md` | Project instructions + kiến trúc tổng quan | ✅ Luôn đọc |
| `PROJECT_STATUS.md` | **File này** — snapshot tiến độ | ✅ Luôn đọc trước |
| `CHANGELOG.md` | Lịch sử thay đổi chi tiết | Khi cần biết lý do quyết định |
| `.claude/rules/common/coding-standards.md` | 17 coding rules R01–R17 | Auto-load bởi Claude Code |
| `.claude/rules/csharp/csharp-patterns.md` | 15 C# patterns CS01–CS15 | Auto-load bởi Claude Code |
| `file hướng dẫn code/AGENTS.md` | 9 agents + ECC routing table | Khi cần agent-specific context |
| `file hướng dẫn code/QUICK_REFERENCE.md` | Quick ref card (in ra dán màn hình) | Khi cần tra cứu nhanh |
| `file hướng dẫn code/PROMPT_TEMPLATES.md` | PT-01 đến PT-14 — prompt templates | Khi tạo component mới |

### Core Interfaces (AM.Core.Abstractions)
| Interface | File | Mô tả |
|-----------|------|-------|
| `IMotionController` | Interfaces/Hardware/ | Connect, MoveAbs, MoveRel, Home, GetPosition |
| `ICameraDevice` | Interfaces/Hardware/ | Connect, Grab, RunTool, GetResult |
| `IIoModule` | Interfaces/Hardware/ | Connect, ReadDI, WriteDO, ReadAI |
| `IMechanism` | Interfaces/Machine/ | Name, IsReady, IsBusy, Initialize, Home, EmergencyStop |
| `IStation` | Interfaces/Machine/ | Name, State, Mechanisms, RunCycle, EmergencyStop |
| `IMasterController` | Interfaces/Machine/ | ISA-88 state machine, Initialize/Start/Stop/Reset |
| `IAlarmService` | Interfaces/Services/ | Raise, Clear, GetActive, AlarmRaised event |
| `IRecipeService` | Interfaces/Services/ | Load, Save, GetAll, CurrentRecipe |
| `IParameterService` | Interfaces/Services/ | Get/Set/Save parameters |
| `IHardwareManagerService` | Interfaces/Services/ | Register, Resolve&lt;T&gt;, ConnectAll |
| `IStationSyncService` | Interfaces/Services/ | RegisterSlot, Signal, WaitAsync, ResetAll |
| `HardwareManagerService` | AM.Services/ | ✅ Implemented — pattern matching ConnectAll |
| `StationSyncService` | AM.Services/ | ✅ Implemented — SemaphoreSlim pipeline sync |
| `BaseMechanism` | AM.Infrastructure/ | ✅ IsBusy guard (Interlocked), EmergencyStop wrapper, template methods |
| `StationBase<T>` | AM.Infrastructure/ | ✅ RegisterMechanism, SetState, RunCycle template |
| `BaseMasterController` | AM.Infrastructure/ | ✅ ISA-88 transition table, FireTrigger, CheckPauseAsync |

### Enums (AM.Core/Enums)
| Enum | Values |
|------|--------|
| `MachineState` | Uninitialized, Initializing, Idle, Running, Paused, InitAlarm, RunAlarm, Resetting |
| `MachineTrigger` | Initialize, InitializeDone, Start, Pause, Resume, Stop, Error, Reset, ResetDone, ResetDoneUninitialized |
| `HardwareCategory` | General=0, Axis=1, IOController=2, Camera=3, Robot=4, Scanner=5, Instrument=6, MotionCard=7, LightController=8 |
| `UserLevel` | Null=-1, Operator=0, Engineer=1, Administrator=2, SuperUser=3 |
| `OperationMode` | Normal, DryRun |
| `AlarmLevel` | Info, Warning, Error, Critical |

### Attributes (AM.Core/Attributes)
| Attribute | Target | Params |
|-----------|--------|--------|
| `[AlarmInfo]` | AlarmCodes fields | displayName, remedy, isStoppable |
| `[MechanismUI]` | Mechanism classes | displayName, group, order |
| `[StationUI]` | Station classes | displayName, icon, order |
| `[ModuleNavigation]` | Prism View classes | displayName, icon, region, order |
| `[ParamView]` | Recipe properties | label, unit, min, max, group, order |

---

## ⚠️ Known Issues & TODO

### BUGS / VI PHẠM CẦN SỬA
*(Không có bug nào đang mở — tất cả đã fix)*

### TODO — Việc cần làm tiếp
| # | Hạng mục | Ưu tiên | Ghi chú |
|---|----------|---------|---------|
| ~~T1~~ | ~~Refactor Demo machine 3-tier~~ | ✅ Done (S8) | DemoPick/DemoInspect → DemoStation → DemoMasterController |
| ~~T2~~ | ~~Đăng ký HardwareManager + StationSync vào Bootstrapper~~ | ✅ Done (S8) | + named hw registry trong RegisterHardwareDevices |
| ~~T3~~ | ~~Tạo unit test project~~ | ✅ Done (S8) | AM.Services.Tests — 38 tests pass |
| ~~T4~~ | ~~Tạo WPF Dashboard module~~ | ✅ Done (S8) | AM.Modules.Dashboard |
| T5 | Thêm drivers thật (nếu có hardware SDK) | 🟢 Thấp | Tùy theo yêu cầu khách hàng |
| T6 | AM.Infrastructure.Tests — test BaseMasterController state transitions | 🟡 Trung bình | 13 transitions chưa có test coverage |
| T7 | Đăng ký Dashboard vào Shell (region/Prism) để hiển thị runtime | 🟡 Trung bình | Module đã build nhưng chưa wire vào MainWindow |
| T8 | Các WPF module còn lại (Alarm, Parameter, Motion, IO, Vision...) | 🟢 Thấp | Theo danh sách AM.Modules.* trong CLAUDE.md |

---

## 🔑 Alarm Code Ranges (đã định nghĩa)
```
10000–10999  Motion / Axis
20000–20999  Vision / Camera
30000–30999  I/O / Sensor
40000–40999  System / Application
50000–50999  Communication / Network
60000–60999  Production / Recipe
70000–70999  Safety / Interlock
```
Chi tiết xem: `AM.Core/Constants/AlarmCodes.cs`

---

## 🛠️ Build Rules (nhắc lại quan trọng nhất)
```
TreatWarningsAsErrors=true — mọi CA/Sonar warning = build error
CA1707  — không underscore trong tên (Step01Initialize ✅, Step01_Initialize ❌)
CA1031  — không catch Exception chung (dùng pragma + when filter)
CA2000  — CancellationTokenSource phải 'using var'
RSPEC-2139 — dùng exception filter: catch (Exception ex) when (ex is not AlarmException)
RSPEC-6602 — List<T>.Find() thay LINQ FirstOrDefault()
RSPEC-6605 — List<T>.Exists() thay LINQ Any()
CA1869  — JsonSerializerOptions phải static readonly
```

---

## 📋 Hướng dẫn cập nhật file này

**Sau mỗi session làm việc với Claude, cập nhật:**
1. `Cập nhật lần cuối` — ngày + mô tả session
2. `Commit` — hash + message
3. `Trạng thái tổng quan` — đổi ⚠️ → ✅ hoặc thêm hạng mục mới
4. `Known Issues & TODO` — thêm/xoá items
5. Bất kỳ interface/enum/file quan trọng mới nào

**Claude cập nhật file này bằng cách:** Đọc file, chỉnh sửa trực tiếp các bảng, commit cùng với cod