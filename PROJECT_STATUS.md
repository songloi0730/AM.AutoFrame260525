# PROJECT_STATUS.md — AM.AutoFrame
> **⚡ Claude: Đọc file này TRƯỚC khi bắt đầu bất kỳ thay đổi nào.**
> File này là snapshot trạng thái dự án. Cập nhật cuối cùng sau mỗi session làm việc.
>
> 📌 **Tiếp tục mạch HMI v2 + Guard Engine (S44–S57)?** Đọc **`docs/SESSION_HANDOFF.md`** — bàn giao chi tiết:
> trạng thái, các BẪY đã gặp (cross-thread UserChanged, users.json migration, analyzer), workflow, và roadmap
> phần còn hoãn (Force IO, HardwareInputEventBus, thao tác trạm, Override...).

---

## 🗓️ Cập nhật lần cuối
**Ngày:** 2026-07-04
**Session:** #79 — **Đánh giá toàn diện + ROADMAP hoàn thiện** (`docs/ROADMAP_HOAN_THIEN.md`): rà 6 trục (an toàn/bảo mật/chức năng/hiệu chỉnh/UI/tích hợp), kiểm chứng gap trực tiếp trong code — nổi bật: **E-Stop không đổi state machine** (EmergencyStop không fire trigger — máy vẫn hiện "Đang chạy"), **DataRetentionDays không được thực thi** (DeleteOlderThanAsync 0 caller — DB phình vô hạn), **users.json re-seed ghi đè không backup**, **nút vật lý DI.Btn.* chưa wire**, **không lockout/password-policy/auto-logout**, calibration = trục trắng (tài liệu tham chiếu treo). Kế hoạch P0–P5 (~17 phiên P0–P4) kèm DoD từng mục + 7 câu hỏi cần chủ dự án chốt (§5) + hợp đồng vision app riêng (§6). Việc tiếp: **P0.1 E-Stop state machine** (ưu tiên 🔴 số 1).
**Commit:** `35c75cc`  ·  (S78: Prompt D `6c71301` · S77: engine `4789c51` · S76: ẩn danh+ADR `798e6c9`)

---

## 🗓️ Session #78
**Session:** #78 — **Prompt D: máy mẫu DemoPickPlace end-to-end trên mô phỏng**. `SimIoService` (IIoService+IMotionService, delay+xác suất lỗi cấu hình `DemoSimOptions`) + 6 station (Scanner/Feed/Pick/Vision/Place/Report — homing Z→X→Y, Abort GIỮ vacuum khi đang giữ hàng, kiểm liệu sót đầu cycle+init) + `recipes/DemoPickPlace.sequence.json` (spec §2) + `DemoMasterController` nối engine (mỗi cycle=1 sản phẩm; Pause/Resume override→RequestPause/Resume dừng giữa cycle ở ranh giới bước; Abort→alarm 60006, sequence hỏng→60005). Dashboard mini-log ăn TRỰC TIẾP sự kiện engine (StepCompleted lỗi/NG + ProductCompleted); KPI/bảng SP/card KQ đi đường IProductionService (ReportStation ghi record thật: SN scanner, OK/NG, vision score — không đường dữ liệu riêng cho UI). **Nút mới**: banner Shell 3 nút trả lời operator prompt (Thử lại / Bỏ qua-Engineer+ / Dừng máy) thay popup chặn thread. **4 kịch bản nghiệm thu (test tự động, engine+station+SimIoService thật trên file sequence thật)**: (a) 20 sản phẩm liên tục — 20 record PASS, SN không trùng, KPI khớp log; (b) vacuum fail 100% → retry đúng 2 lần (1 đầu + retry=1) → prompt → operator Abort → 0 record; (c) Pause giữa cycle dừng ở ranh giới bước (vision CHƯA chạy) → Resume chạy nốt; (d) Stop khi đang giữ hàng → vacuum GIỮ + sản phẩm Aborted → Reset+Init tự thoát liệu sót → chạy lại 1 sản phẩm sạch. **258 test pass** (20 engine + 5 demo + 233 cũ), build 0 warning, app boot sạch với DI graph mới (keyed stations + engine + resolver). Việc tiếp (tuỳ chọn): vòng review phản biện ADR+engine; đấu ảnh cycle thật vào card KQ khi vision IPC (ADR 0008) xong.
**Commit:** `6c71301`  ·  (S77: engine+test `4789c51` · S76: ẩn danh+ADR `798e6c9` · S74: Home v2.1 `970f078`)

---

## 🗓️ Session #77
**Session:** #77 — **AM.Core.Sequencing (Prompt C, theo ADR 0011 đã duyệt + 2 hiệu chỉnh)**: project mới standalone — contracts spec §1 nguyên văn (`IStation`/`StepContext`/`StationResult`) + `IStationResolver` (engine không thấy DryIoc) + `IResumeVerifiable`/`IOperatorPrompt`; `SequenceLoader` 2 pha gom TOÀN BỘ lỗi (tên station chết LÚC NẠP + gợi ý tên đã đăng ký); `SequenceEngine`: nhóm `order` song song, timeout linked-CTS, onError/retry/onRetryExhausted, prompt operator không-chặn-thread (Respond trong args), NG bypass trừ `runOnNg`, pause ranh giới bước + resume-check, Stop sạch + sản phẩm dở Aborted. **20/20 test** (đủ 6 case spec §4 + validator + prompt/resume/blackboard) — coverage engine core **92.7% line** (package 85.5%). Commit `4789c51`.

---

## 🗓️ Session trước
**Session:** #75 — **Sequence Requirements (khảo sát máy tham khảo RefSeq-A)**: đọc dự án tham khảo RefSeq-A (C# WinForms, 8 trạm thread-per-station + bit bắt tay), điền `docs/private/Sequence_Requirements_RefSeqA.md` *(local, không commit)* theo template — 10 mục: vai trò 8 trạm, vòng đời init phụ thuộc chéo, ngữ nghĩa Pause (giữa bước + resume-check vị trí)/Stop (hủy ngay + Thread.Abort)/EMG (mọi Error-warning → EMG toàn máy)/Reset (xóa bit + re-init), chính sách lỗi popup-operator (không auto-retry, timeout mặc định 600s), song song giả (bit handshake), traceability MES + data-host + CSV, 4 mode chạy, anti-pattern KHÔNG bắt chước + 7 hành vi đáng học. Nhập bộ spec sequence vào docs/: `SequenceEngine_Spec.md` (chuẩn thiết kế), `DemoMachine_IO_Map.md`, `Sequence_Requirements_Template.md`. Việc tiếp: thiết kế `AM.Core.Sequencing` CHỈ từ 3 file này.
**Commit:** `8be4ef0` *(S75 được gộp + ẩn danh hoá ở S76 — hash gốc đã bị viết lại)*  ·  (S74: Home v2.1 `970f078` · S73: Shell v3 `991f34b` · S72: ADR 0008 Vision IPC `b50e22b`)

---

## 📊 Trạng thái tổng quan

| Hạng mục | Trạng thái | Ghi chú |
|----------|-----------|---------|
| Solution structure | ✅ Hoàn thành | **28 projects** (CPM), production 0 warning, **258 tests pass** · light theme + i18n toàn module (AM.UI.Localization) + cửa sổ cố định |
| AM.Core | ✅ Hoàn thành | Enums (+PixelFormat) + 5 Attributes + Models (+RobotPose +FrameData +MotionStatus) + EventArgs |
| AM.Core.Abstractions | ✅ Hoàn thành | Hardware (16 ifaces, **+IHardwareDevice base**: mọi device kế thừa → ConnectAll generic) + Machine + Services |
| AM.Core.Sequencing | ✅ Hoàn thành | **Mới (S77, ADR 0011)** — sequence engine khai báo: contracts (`IStation`/`StepContext`/`StationResult`/`IStationResolver`/`IResumeVerifiable`/`IOperatorPrompt`), `SequenceLoader` 2 pha gom lỗi, `SequenceEngine` (order song song, timeout linked-CTS, onError/retry/prompt, pause ranh giới bước + resume-check). Standalone — không reference DryIoc/hardware/UI |
| AM.Core.Sequencing.Tests | ✅ Hoàn thành | **Mới (S77)** — 20 tests: 6 case spec §4 + validator + prompt/resume-check/blackboard; station = fake thuần; coverage engine core 92.7% |
| AM.Hardware.Scanner | ✅ Hoàn thành | **Keyence + Cognex (TCP line) + Simulated** — IBarcodeScanner |
| AM.Hardware.Motion | ✅ Hoàn thành | Sim (+**IAxisDiagnostics**: 8 tín hiệu/servo/phản hồi) + **GtsMotionController (固高, P/Invoke)** + **AdvantechMotionController (P/Invoke)** |
| AM.Hardware.Vision | ✅ Hoàn thành | SimulatedCameraDevice (+**GrabFrameAsync sinh frame Bgr24 live, S67**) + SimulatedVisionProcessor (IVisionProcessor) |
| AM.Hardware.IO | ✅ Hoàn thành | Sim + AdvantechAdamIoModule (+**force/unforce/ReadAllDo** — kênh forced bỏ qua write của logic, S59) + SimulatedSafetyInput + JsonIoTagMap + IoTagExtensions |
| AM.Hardware.Comm | ✅ Hoàn thành | **Modbus TCP thật (raw MBAP)**, Inovance PLC+servo, Mitsubishi MC 3E, Siemens S7, Robot socket+sim, PLC sim |
| AM.Services | ✅ Hoàn thành | Alarm, Recipe, Parameter, HardwareManager, StationSync, Watchdog, Production, UserService, **GuardService (3 tầng: state→role→condition), HardwareSignalBus + SafetySignalPublisher (event-push)** |
| AM.Services.Tests | ✅ Hoàn thành | **122 tests** (Alarm, Recipe, StationSync, HardwareManager, Watchdog, Production, UserService +**CRUD/last-admin**, PointTable, Guard 3 tầng, SignalBus, SafetyPublisher, RecoveryActions, Override provider) |
| AM.Infrastructure (i18n) | ✅ Hoàn thành | **JsonAlarmCatalogService** — Alarms.{vi,en,zh}.json (44 mã), dịch tên/remedy theo culture |
| AM.Hardware.Tests | ✅ Hoàn thành | **36 tests**: Modbus MBAP, Inovance/ADAM, Robot+Scanner loopback, SimVision/SimSafety, SimAxisDiagnostics, IO force semantics, IoTagMap schema mảng, **SimCamera GrabFrame live-view (S67)** |
| AM.Data | ✅ Hoàn thành | EF Core SQLite, AlarmRepository, ProductionRepository |
| AM.Infrastructure | ✅ Hoàn thành | BaseMechanism, StationBase\<T\>, BaseMasterController, **JsonLocalizationService (i18n runtime)** |
| AM.CommonTools | ✅ Hoàn thành | Guard, RetryHelper |
| AM.WorkStation.Demo | ✅ Hoàn thành | Full 3-tier: DemoPick/InspectMechanism → DemoStation → DemoMasterController; **+Sequencing (S78)**: SimIoService + 6 station (Scanner/Feed/Pick/Vision/Place/Report) + adapters, master nối SequenceEngine (mỗi cycle=1 sản phẩm, Pause/Resume→ranh giới bước, Abort→60006) |
| AM.WorkStation.Demo.Tests | ✅ Hoàn thành | **Mới (S78)** — 5 tests: 4 kịch bản nghiệm thu Prompt D (20 sản phẩm/KPI, vacuum-fail retry+prompt+Abort, Pause-giữa-cycle+Resume, Stop-giữ-hàng+Reset+chạy-lại) + vòng đời ISA-88 master nối engine; chạy engine+station+SimIoService thật trên file sequence thật |
| AM.Modules.Dashboard | ✅ Hoàn thành | **Home v2.1** (S74, ADR 0010): work area (card "Kết quả gần nhất" + bảng truy vết SN empty-state, KQ chip màu) + right rail 560px (KPI ca 8h số 26px màu-khi-có-nghĩa, **quick actions đủ HAL — S65** + tooltip lý do + Andon, trạm & an toàn ISafetyInput event, nhật ký) — spec: `docs/HMI_Dashboard_Spec.md` v2 (cần nâng v2.1) |
| AM.Modules.Alarm | ✅ Hoàn thành | active alarms + acknowledge/clear, đồng bộ realtime |
| AM.Modules.IoMonitor | ✅ Hoàn thành | Danh sách "địa chỉ·tên" (IOMap) + ô lọc + chỉ báo Off/On/Pending/Forced + nhóm Xi lanh ▲giữa (S60); set/reset thường (Engineer; **có hậu quả → chạm-2-bước**) + Chế độ Force (Admin) + **alarm 70010 "còn IO forced"** (S61); nav tự sinh từ [ModuleNavigation] |
| AM.Modules.Identity | ✅ Hoàn thành | **Mới** — login/logout/RBAC (IUserService); password ở code-behind; nav order 90 |
| AM.Modules.Motion | ✅ Hoàn thành | **Màn điều khiển trục v2** (S46): bảng đèn 8 tín hiệu + servo/home/clear/move từng trục + jog pad/inching + phản hồi servo + bảng điểm Set/Confirm 2-chạm + **Thao tác trạm (RecoveryActions, S63) + Supervised Override (xác nhận 1 người, S64)**. Bám `IMotionController` + `IAxisDiagnostics` (tuỳ chọn); nav order 40 |
| AM.Modules.Parameter | ✅ Hoàn thành | **Mới** — recipe editor attribute-driven ([ParamView] reflection); Save gate Engineer; nav order 50 |
| AM.Application.Shell | ✅ Hoàn thành | Bootstrapper + HardwareFactory + **Shell v3 — 4 vùng Persistent Frame** (S73, ADR 0009): header+nav gộp 56px (chip AUTO/LOCAL/state + tab RadioButton), alarm banner co giãn 36→52 + ACK 40px + chip "+N", action bar 76px (lệnh máy 64px + Dry run + chip kết nối n/m + popup Thiết bị│Host), kiosk config-driven (Ctrl+Shift+F11 Engineer+) |
| AM.UI.Localization | ✅ Hoàn thành | Proxy i18n dùng chung `Loc.Strings` (module bind `{x:Static loc:Loc.Strings}`) |
| .claude/ (AI config) | ✅ Hoàn thành | rules(2) + commands(9) + skills(8) + hooks(4) |
| PROJECT_STATUS.md + CHANGELOG.md | ✅ Hoàn thành | Tracking system, auto-commit workflow |
| scripts/am-commit.sh | ✅ Hoàn thành | Git wrapper xử lý Windows index.lock |
| `libs/` vendor DLLs | ✅ Structure tạo xong | Placeholder + README; DLL do developer tự copy từ SDK |
| AM.Infrastructure.Tests | ✅ Hoàn thành | **55 tests**: ISA-88 + busy-guard + StationBase + e2e + i18n + alarm catalog + **StepSequence (4) + AxisMap (5)** |
| AM.Modules.Engineering | ✅ Hoàn thành | **Mới** — auto-discovery [StationUI]/[MechanismUI] + chạy SubRoutine + E-Stop từng cụm; nav order 80 |
| AM.Modules.Production | ✅ Hoàn thành | **Mới** — KPI UPH/yield/cycle-time (IProductionService), tự refresh khi CycleCompleted; nav order 15 |
| AM.Modules.Diagnostics | ✅ Hoàn thành | **Mới** — device health + system info + Reconnect All; nav order 70 |
| AM.Modules.Logging | ✅ Hoàn thành | **Mới** — tail file Serilog + lọc level/search + mở thư mục; nav order 75 |
| AM.Modules.Vision | ✅ Hoàn thành | **V1–V2 (S68–69)**: camera toolbar + sub-tab **Kết quả·Lịch sử·Công cụ**; tab Kết quả có **lưới phép đo** (`VisionResult.Checks`) + **stats ca** + trend; live-view + Grab/Inspect/Light/Calibrate (S67). **V3 (S70)**: tab Công cụ = **VisionTeachView** (gate Engineer, phủ toàn vùng) — chụp ảnh tham chiếu + ROI editor (Canvas/`Thumb`) + ngưỡng + calib px→mm (form+lịch sử) + Lưu/Nạp JSON (`VisionTeachConfig`/`IVisionTeachStore`). Roadmap V4–V5 (ILightController per-channel · VisionRecipe) ở ADR `docs/design-notes/0007` |
| AM.Modules.Vision.Tests | ✅ Hoàn thành | **Mới (S70)** — 10 test: `VisionTeachStore` round-trip JSON (ROI+calib) + thiếu file→rỗng + per-camera; `CalibrationMath` mm/px |
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
| `IPointTableService` | `AM.Services/PointTableService.cs` ✅ (Point Table — toạ độ đặt tên JSON) |
| `IAxisMap` | `AM.Infrastructure/Motion/JsonAxisMap.cs` ✅ (trục logic→IAxis qua `MotionAxisAdapter` — concrete IAxis đầu tiên) |
| `IMachineConfigProvider` | `AM.Infrastructure/Configuration/JsonMachineConfigProvider.cs` ✅ (layout máy machine.json) |

### Enums (AM.Core/Enums/)
| Enum | Values |
|------|--------|
| `MachineState` | Uninitialized, Initializing, Idle, Running, Paused, InitAlarm, RunAlarm, Resetting |
| `MachineTrigger` | Initialize, InitializeDone, Start, Pause, Resume, Stop, Error, Reset, ResetDone, ResetDoneUninitialized |
| `HardwareCategory` | General=0, Axis=1, IOController=2, Camera=3, Robot=4, Scanner=5, Instrument=6, MotionCard=7, LightController=8, ModbusTcp=9, SerialPort=10, OpcUaClient=11, EthernetIp=12, TcpDevice=13 |
| `UserLevel` | Null=-1, Operator=0, **LineLead=1**, Engineer=2, Administrator=3, SuperUser=4 (4 role vận hành + SuperUser OEM) |
| `OperationMode` | Normal, DryRun |
| `AlarmLevel` | Info, Warning, Error, Critical |

### EventArgs (AM.Core/Models/EventArgs/)
| Class | Dùng cho |
|-------|---------|
| `AlarmEventArgs` | IAlarmService.AlarmRaised/AlarmCleared |
| `RecipeEventArgs` | IRecipeService.RecipeChanged |
| `MachineStateChangedEventArgs` | IMasterController/IStation.StateChanged |
| `CycleCompletedEventArgs` | IMasterController.CycleCompleted (`CycleCount`, `CompletedAt`, **`CycleDurationMs`**) |
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
*(Không có bug nào đang mở)*

### TODO tiếp theo
> ⚡ **Nguồn TODO chính từ S79: `docs/ROADMAP_HOAN_THIEN.md`** — bảng ưu tiên §4: P0.1 E-Stop state machine (🔴 số 1) → P0.2 Retention job → P0.3 users.json backup → P0.4 sync docs → P1 Vận hành tay (cần chốt §5 Q1–Q7). Các dòng dưới đã gộp vào roadmap, giữ để truy vết:
- [x] **Prompt D — máy mẫu DemoPickPlace end-to-end** ✅ HOÀN THÀNH (S78): SimIoService + 6 station + sequence JSON + master nối engine + dashboard bridge + banner prompt 3 nút; 4 kịch bản nghiệm thu đạt (test tự động)
- [ ] (Tuỳ chọn) Vòng review phản biện ADR 0011 + engine (ChatGPT/Gemini → lọc bằng chứng theo SequenceEngine_Spec + requirements local) như quy trình chương sách
- [ ] Đấu ảnh cycle thật vào card "Kết quả gần nhất" khi vision service IPC (ADR 0008) sẵn sàng — hiện dùng placeholder tối
- [ ] (Giai đoạn 2 sequence) single-step mode · pipeline maxProductsInFlight>1 · resources chống tranh chấp · resume-from-crash (đã ghi lý do hoãn ở ADR 0011 §6)
- [ ] Sync `HMI_UI_Architecture_Template` + Master Index §3 lên **v3** — Shell đã đổi 7 vùng → 4 vùng (ADR 0009), tài liệu đang mô tả bố cục cũ; cùng đợt nâng `HMI_Dashboard_Spec` lên v2.1 (card KQ gần nhất — ADR 0010) + ghi 3 nguyên tắc: màu-khi-có-nghĩa, empty-state-có-hướng-dẫn, xếp-theo-tần-suất-liếc
- [ ] Màn Cài đặt: thêm nút vào/thoát kiosk (hiện chỉ có Ctrl+Shift+F11 Engineer+)
- [ ] Vision V4 — `ILightController` per-channel + `SimulatedLightController` + test (ADR 0007 Quyết định 5)
- [ ] Vision V5 — `VisionRecipe` model (promote `VisionTeachConfig` lên Core) + validate attribute-driven + test
- [ ] (Nợ test, ngoài phạm vi V3) S6966/CA2007 trong AM.Services.Tests + AM.Infrastructure.Tests (pre-existing)
- [ ] Dựng 1 máy reference để nghiệm thu nền framework (đề xuất từ S43)