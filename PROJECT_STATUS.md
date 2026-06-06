# PROJECT_STATUS.md — AM.AutoFrame
> **⚡ Claude: Đọc file này TRƯỚC khi bắt đầu bất kỳ thay đổi nào.**
> File này là snapshot trạng thái dự án. Cập nhật cuối cùng sau mỗi session làm việc.

---

## 🗓️ Cập nhật lần cuối
**Ngày:** 2026-06-04
**Session:** #22 — G0 Nav auto-discovery ([ModuleNavigation]) + AM.Modules.IoMonitor
**Commit:** *(pending)*

---

## 📊 Trạng thái tổng quan

| Hạng mục | Trạng thái | Ghi chú |
|----------|-----------|---------|
| Solution structure | ✅ Hoàn thành | **20 projects** (CPM), 0 warning, **124 tests pass** · README + CI/CD + arch-test + i18n |
| AM.Core | ✅ Hoàn thành | Enums (+PixelFormat) + 5 Attributes + Models (+RobotPose +FrameData +MotionStatus) + EventArgs |
| AM.Core.Abstractions | ✅ Hoàn thành | Hardware (16 ifaces, **+IHardwareDevice base**: mọi device kế thừa → ConnectAll generic) + Machine + Services |
| AM.Hardware.Scanner | ✅ Hoàn thành | **Keyence + Cognex (TCP line) + Simulated** — IBarcodeScanner |
| AM.Hardware.Motion | ✅ Hoàn thành | Sim + **GtsMotionController (固高, P/Invoke)** + **AdvantechMotionController (P/Invoke)** |
| AM.Hardware.Vision | ✅ Hoàn thành | SimulatedCameraDevice + **SimulatedVisionProcessor (IVisionProcessor)** |
| AM.Hardware.IO | ✅ Hoàn thành | Sim + AdvantechAdamIoModule + **SimulatedSafetyInput + JsonIoTagMap + IoTagExtensions** |
| AM.Hardware.Comm | ✅ Hoàn thành | **Modbus TCP thật (raw MBAP)**, Inovance PLC+servo, Mitsubishi MC 3E, Siemens S7, Robot socket+sim, PLC sim |
| AM.Services | ✅ Hoàn thành | Alarm, Recipe, Parameter, HardwareManager, StationSync, HardwareWatchdog, **Production** (UPH/yield) |
| AM.Services.Tests | ✅ Hoàn thành | 50 tests (Alarm, Recipe, StationSync, HardwareManager, **Watchdog**, **Production**) |
| AM.Hardware.Tests | ✅ Hoàn thành | **27 tests**: Modbus MBAP, Inovance/ADAM, Robot+Scanner loopback, SimVision/SimSafety/IoTagMap |
| AM.Data | ✅ Hoàn thành | EF Core SQLite, AlarmRepository, ProductionRepository |
| AM.Infrastructure | ✅ Hoàn thành | BaseMechanism, StationBase\<T\>, BaseMasterController, **JsonLocalizationService (i18n runtime)** |
| AM.CommonTools | ✅ Hoàn thành | Guard, RetryHelper |
| AM.WorkStation.Demo | ✅ Hoàn thành | Full 3-tier: DemoPick/InspectMechanism → DemoStation → DemoMasterController |
| AM.Modules.Dashboard | ✅ Hoàn thành | **Đã wire vào Shell** (side-nav, resolve từ DI) |
| AM.Modules.Alarm | ✅ Hoàn thành | active alarms + acknowledge/clear, đồng bộ realtime |
| AM.Modules.IoMonitor | ✅ Hoàn thành | **Mới** — DI realtime (poll) + toggle DO; nav tự sinh từ [ModuleNavigation] |
| AM.Application.Shell | ✅ Hoàn thành | Bootstrapper + RegisterRealHardware + **HardwareFactory** (vision/scanner/safety/io-tagmap) + io.map.json |
| .claude/ (AI config) | ✅ Hoàn thành | rules(2) + commands(9) + skills(8) + hooks(4) |
| PROJECT_STATUS.md + CHANGELOG.md | ✅ Hoàn thành | Tracking system, auto-commit workflow |
| scripts/am-commit.sh | ✅ Hoàn thành | Git wrapper xử lý Windows index.lock |
| `libs/` vendor DLLs | ✅ Structure tạo xong | Placeholder + README; DLL do developer tự copy từ SDK |
| AM.Infrastructure.Tests | ✅ Hoàn thành | **35 tests**: 13 ISA-88 transitions + BaseMechanism busy-guard + StationBase + end-to-end (pause/resume/safety-trip) |
| AM.Modules.* (còn lại) | ❌ Chưa có | Motion, Parameter, Vision, Identity, Logging, Diagnostics |
| CI/CD + README | ✅ Hoàn thành | `.github/workflows/ci.yml` (windows, build+test) + README.md |

---

## 🏗️ Kiến trúc thực tế — 14 projects

```
AM.Core                  — Enums, Models, 5 Attributes, AlarmCodes, AlarmException, EventArgs
AM.Core.Abstractions     — Interfaces: Hardware(8) + Machine(3) + Services(5) + Repos(2) + IStep
AM.CommonTools           — Guard, RetryHelper
AM.Hardware.Motion       — SimulatedMotionController
AM.Hardware.Vision       — SimulatedCameraDevice
AM.Hardware.IO           — SimulatedIoModule
AM.Hardware.Comm         — Modbus/Serial/TCP (real+sim), OpcUa/EthernetIP (sim only)
AM.Services              — AlarmService, RecipeService, ParameterService,
                           HardwareManagerService, StationSyncService
AM.Services.Tests        — 32 unit tests (xUnit + Moq + FluentAssertions)
AM.Data                  — AutoMachineDbContext, AlarmRepository, ProductionRepository
AM.Infrastructure        — BaseMechanism, StationBase<T>, BaseMasterController, DispatcherHelper
AM.WorkStation.Demo      — DemoPickMechanism, DemoInspectMechanism, DemoStation,
                           DemoMasterController, Step01Initialize, Step02Inspect
AM.Modules.Dashboard     — DashboardViewModel, DashboardView [⚠️ chưa wire vào Shell]
AM.Application.Shell     — WPF entry, Prism+DryIoc Bootstrapper, 8 hw devices registered
```

### 3-Tier Machine Hierarchy — ✅ Đầy đủ cả interface + base + demo

```
[✅ Interface]  IMasterController       AM.Core.Abstractions/Interfaces/Machine/
[✅ Interface]  IStation                AM.Core.Abstractions/Interfaces/Machine/
[✅ Interface]  IMechanism              AM.Core.Abstractions/Interfaces/Machine/
[✅ Base]       BaseMasterController    AM.Infrastructure/ (ISA-88 13 transitions, FireTrigger, CheckPauseAsync)
[✅ Base]       StationBase<T>          AM.Infrastructure/ (RegisterMechanism, SetState, RunCycle template)
[✅ Base]       BaseMechanism           AM.Infrastructure/ (IsBusy guard, EmergencyStop wrapper)
[✅ Demo]       DemoMasterController    AM.WorkStation.Demo/Controllers/
[✅ Demo]       DemoStation             AM.WorkStation.Demo/Stations/
[✅ Demo]       DemoPickMechanism       AM.WorkStation.Demo/Mechanisms/
[✅ Demo]       DemoInspectMechanism    AM.WorkStation.Demo/Mechanisms/
```

### ISA-88 State Machine (8 states, 10 triggers)
```
States:   Uninitialized → Initializing → Idle → Running ⇄ Paused
                              ↓                    ↓
                          InitAlarm            RunAlarm → Resetting → Idle/Uninitialized
Triggers: Initialize, InitializeDone, Start, Pause, Resume, Stop,
          Error, Reset, ResetDone, ResetDoneUninitialized
```

---

## 📁 Key files — vị trí và nội dung

### Build & Config
| File | Nội dung |
|------|---------|
| `Directory.Build.props` | TreatWarningsAsErrors=true, AnalysisMode=All, .NET 9, CA suppressions |
| `.editorconfig` | Code style |
| `.cursorrules` | AI coding rules (Cursor/Copilot) |
| `AM.AutoFrame.sln` | 15 projects |

### AI Instructions (đọc theo thứ tự)
| File | Nội dung | Đọc khi nào |
|------|---------|------------|
| `PROJECT_STATUS.md` | **File này** — snapshot thực tế | ✅ Luôn đọc TRƯỚC |
| `CLAUDE.md` | Kiến trúc, build rules, behavior | ✅ Luôn đọc |
| `CHANGELOG.md` | Lịch sử session, quyết định kiến trúc | Khi cần hiểu lý do |
| `.claude/rules/common/coding-standards.md` | R01–R17 | Auto-load Claude Code |
| `.claude/rules/csharp/csharp-patterns.md` | CS01–CS15 | Auto-load Claude Code |
| `docs/AGENTS.md` | 9 agents + ECC routing table | Khi cần routing |
| `docs/QUICK_REFERENCE.md` | Quick ref (in ra dán màn hình) | Tra cứu nhanh |
| `docs/PROMPT_TEMPLATES.md` | PT-00 đến PT-14 | Khi tạo component mới |

### Hardware Interfaces thực tế (AM.Core.Abstractions/Interfaces/Hardware/)
| Interface | Mô tả |
|-----------|-------|
| `IMotionController` | Connect, MoveAbs, MoveRel, Home, GetPosition |
| `ICameraDevice` | Connect, Grab, RunTool, GetResult |
| `IIoModule` | Connect, ReadDI, WriteDO, ReadAI, WriteAO |
| `IModbusClient` | Connect, ReadCoils, ReadHolding, WriteCoil, WriteRegister |
| `ISerialDevice` | Connect, SendAsync, DataReceived event |
| `ITcpDevice` | Connect, SendAsync, ReceiveAsync |
| `IOpcUaClient` | Connect, ReadNode, WriteNode, Subscribe |
| `IEthernetIpClient` | Connect, ReadTag, WriteTag |

### Machine Interfaces (AM.Core.Abstractions/Interfaces/Machine/)
| Interface | Mô tả |
|-----------|-------|
| `IMechanism` | Name, IsReady, IsBusy, InitializeAsync, HomeAsync, EmergencyStop |
| `IStation` | Name, State, Mechanisms, RunCycleAsync, StateChanged event |
| `IMasterController` | ISA-88 full state machine, Initialize/Start/Stop/Reset/EmergencyStop |

### Service Interfaces (AM.Core.Abstractions/Interfaces/Services/)
| Interface | Implemented by |
|-----------|---------------|
| `IAlarmService` | `AM.Services/AlarmService.cs` ✅ |
| `IRecipeService` | `AM.Services/RecipeService.cs` ✅ |
| `IParameterService` | `AM.Services/ParameterService.cs` ✅ |
| `IHardwareManagerService` | `AM.Services/HardwareManagerService.cs` ✅ |
| `IStationSyncService` | `AM.Services/StationSyncService.cs` ✅ |

### Enums (AM.Core/Enums/)
| Enum | Values |
|------|--------|
| `MachineState` | Uninitialized, Initializing, Idle, Running, Paused, InitAlarm, RunAlarm, Resetting |
| `MachineTrigger` | Initialize, InitializeDone, Start, Pause, Resume, Stop, Error, Reset, ResetDone, ResetDoneUninitialized |
| `HardwareCategory` | General=0, Axis=1, IOController=2, Camera=3, Robot=4, Scanner=5, Instrument=6, MotionCard=7, LightController=8, ModbusTcp=9, SerialPort=10, OpcUaClient=11, EthernetIp=12, TcpDevice=13 |
| `UserLevel` | Null=-1, Operator=0, Engineer=1, Administrator=2, SuperUser=3 |
| `OperationMode` | Normal, DryRun |
| `AlarmLevel` | Info, Warning, Error, Critical |

### EventArgs (AM.Core/Models/EventArgs/)
| Class | Dùng cho |
|-------|---------|
| `AlarmEventArgs` | IAlarmService.AlarmRaised/AlarmCleared |
| `RecipeEventArgs` | IRecipeService.RecipeChanged |
| `MachineStateChangedEventArgs` | IMasterController/IStation.StateChanged |
| `CycleCompletedEventArgs` | IMasterController.CycleCompleted |
| `SerialDataReceivedEventArgs` | ISerialDevice.DataReceived |
| `OpcUaValueChangedEventArgs` | IOpcUaClient.ValueChanged |

### Attributes (AM.Core/Attributes/)
| Attribute | Target | Params |
|-----------|--------|--------|
| `[AlarmInfo]` | AlarmCodes fields | displayName, remedy, isStoppable |
| `[MechanismUI]` | Mechanism classes | displayName, group, order |
| `[StationUI]` | Station classes | displayName, icon, order |
| `[ModuleNavigation]` | Prism View classes | displayName, icon, region, order |
| `[ParamView]` | Recipe properties | label, unit, min, max, group, order |

### Alarm Code Ranges
```
10000–10999  Motion / Axis
20000–20999  Vision / Camera
30000–30999  I/O / Sensor
40000–40999  System / Application
50000–50999  Communication / Network
60000–60999  Production / Recipe
70000–70999  Safety / Interlock
```

### .claude/ Skills (8 skills)
| Skill | Lazy-load khi |
|-------|--------------|
| `am-hardware-patterns` | Tạo driver mới |
| `am-sequence-patterns` | Tạo Step / Sequence |
| `am-mechanism-patterns` | Tạo Mechanism |
| `am-station-patterns` | Tạo Station |
| `am-testing` | Viết unit tests |
| `am-wpf-mvvm` | Tạo WPF screen + ISA-101 rules |
| `am-alarm-dictionary` | Thêm alarm code mới |
| `am-hmi-design` | Thiết kế HMI/UI |

---

## ⚠️ Known Issues & TODO

### BUGS hiện tại
*(Không có bug nào đang m�