# AutoMachine — Quick Reference Card
# In ra, dán cạnh màn hình. Kiểm tra trước mỗi lần commit.

## ⚡ SNIPPET SHORTCUTS (gõ + Tab Tab trong VS)

| Shortcut        | Tạo ra                                |
|-----------------|---------------------------------------|
| `am-fileheader` | File header comment                   |
| `am-service`    | Service class với DI + logging        |
| `am-hwdriver`   | Hardware driver + Dispose             |
| `am-step`       | Machine sequence step                 |
| `am-viewmodel`  | WPF ViewModel (CommunityToolkit)      |
| `am-hwcall`     | Hardware call với timeout             |
| `am-seqloop`    | Sequence main loop + exception handling|
| `am-dispatchprop`| Property update từ background thread |
| `am-unittest`   | xUnit test với AAA pattern            |
| `am-alarmex`    | Throw AlarmException                  |
| `am-prismmodule`| Prism IModule registration            |

---

## 🚨 10 ĐIỀU KHÔNG ĐƯỢC LÀM (nhớ thuộc lòng)

```
1. ❌ new LtdmcController()      → ✅ inject IMotionController
2. ❌ Thread.Sleep(1000)         → ✅ await Task.Delay(1000, ct)
3. ❌ .Result / .Wait()          → ✅ await
4. ❌ catch (Exception) {}       → ✅ log + rethrow hoặc AlarmException
5. ❌ Background="#252525"       → ✅ {DynamicResource Panel.BackgroundBrush}
6. ❌ Text="Alarm List"          → ✅ {lang:Text Key='Alarm.List.Title'}
7. ❌ Console.WriteLine(...)     → ✅ _logger.LogDebug(...)
8. ❌ Bỏ CancellationToken       → ✅ async Task Xxx(CancellationToken ct)
9. ❌ Lưu password plaintext     → ✅ BCrypt.HashPassword(pwd, 12)
10.❌ Import System.Windows.* trong VM → ✅ ViewModel không biết View
```

---

## 📋 CHECKLIST TRƯỚC KHI COMMIT

```
□ Build: không warning, không error
□ Test: dotnet test — tất cả pass
□ File header có trong file mới
□ XML doc cho tất cả public method mới
□ Không có hardcode string trong XAML
□ Không có hardcode màu trong XAML
□ CancellationToken có trong mọi async method mới
□ Không có Console.WriteLine()
□ Không có TODO/FIXME chưa xử lý
□ git diff — không commit file: *.db, *.log, hardware.json
```

---

## 🏗️ PROJECT → LAYER MAP (khi tạo file mới)

```
Loại file cần tạo              → Đặt vào project
─────────────────────────────────────────────────────
Interface hardware/service     → AM.Core.Abstractions
Enum, Model, AlarmCode         → AM.Core
Hardware driver thật           → AM.Hardware.{Motion/Vision/IO/Communication}
Hardware simulator             → AM.Hardware.{...} (cùng folder với driver thật)
Business logic service         → AM.Services
EF Entity + Repository         → AM.Data
Logging, I18n, Security        → AM.Infrastructure
Pure utility helper            → AM.CommonTools
Machine sequence Step          → AM.WorkStation.{MachineName}/Steps/
WPF View + ViewModel           → AM.Modules.{FeatureName}
Prism Module registration      → AM.Modules.{FeatureName}/{Name}Module.cs
DI registration                → AM.Application.Shell/Bootstrapper.cs
```

---

## 🔔 ALARM CODE RANGES

```
10000–10999  Motion / Axis        → 10001=TIMEOUT, 10002=NOT_HOMED, 10003=ESTOP
20000–20999  Vision / Camera      → 20001=GRAB_FAIL, 20002=TOOL_FAIL, 20003=NG_DETECTED
30000–30999  I/O / Sensor         → 30001=PART_MISSING, 30002=CLAMP_FAIL
40000–40999  System               → 40001=DB_ERROR, 40002=CRITICAL, 40003=CONFIG_INVALID
50000–50999  Communication        → 50001=CONN_FAIL, 50002=TIMEOUT, 50003=CRC_ERROR
60000–60999  Production / Recipe  → 60001=RECIPE_INVALID, 60002=SN_DUPLICATE
70000–70999  Safety / Interlock   → 70001=ESTOP, 70002=DOOR_OPEN, 70003=LIGHT_CURTAIN
```

---

## 🎨 COLOR TOKENS (DynamicResource)

```
Nền                    Text                    Status
──────────────────     ─────────────────────   ──────────────────────
Screen.Background      Text.Primary            Status.Normal    #4CAF50
Panel.Background       Text.Secondary          Status.Warning   #FFC107
Panel.Background.Alt   Text.Heading            Status.Alarm     #F44336
Header.Background      Text.LiveValue          Status.Critical  #B71C1C
Equipment.Normal       Text.Disabled           Status.Disabled  #9E9E9E
Border.Default                                 Status.Manual    #1E88E5
Border.Strong                                  Status.Interlock #7B1FA2
```

---

## 🤖 AGENT NHANH

```
Tạo hardware driver   → paste AGENT: HardwareDriver  + mô tả thiết bị
Tạo sequence step     → paste AGENT: MachineSequence + mô tả bước
Tạo màn hình WPF      → paste AGENT: UIModule        + mô tả màn hình
Tạo service + test    → paste AGENT: ServiceLayer     + mô tả service
Review code           → paste AGENT: CodeReview       + paste code
```

---

## ⚡ GIT WORKFLOW

```bash
git checkout -b feature/ten-tinh-nang    # Tạo branch
# ... code ...
dotnet test                               # Test trước khi commit
git add .
git commit -m "feat: mô tả rõ ràng"
git push origin feature/ten-tinh-nang

# Khi xong → tạo Pull Request → review → merge vào develop
```

**Commit prefix:**
`feat:` `fix:` `refactor:` `test:` `docs:` `chore:`

---

## 🏭 KIẾN TRÚC 3 TẦNG — LUẬT KHÔNG VI PHẠM

```
MasterController ← nơi DUY NHẤT fire MachineTrigger / thay đổi State
   └── Station   ← KHÔNG gọi hardware trực tiếp, chỉ gọi Mechanism methods
         └── Mechanism ← bao bọc 1–N hardware devices, expose domain methods
               └── Hardware (IMotionController / ICameraDevice / IIoModule)

✅ Mechanism.PickAsync()          ← Station gọi đây
❌ Station → _motion.MoveAbsAsync() ← Station KHÔNG gọi trực tiếp hardware
✅ _hwManager.Resolve<T>("name")  ← Mechanism lấy hardware qua manager
❌ new LtdmcController()          ← KHÔNG new hardware trực tiếp bao giờ
```

### Khi nào dùng gì:
```
Thêm cụm cơ học mới     → Mechanism  [MechanismUI]  BaseMechanism
Thêm công đoạn mới      → Station    [StationUI]    StationBase<T>
Thêm máy mới            → MasterController           BaseMasterController
Pipeline giữa stations  → IStationSyncService.Signal/WaitAsync
```

---

## ⌨️ SLASH COMMANDS (Claude Code / .claude/commands)

```
/am-new-driver     → Tạo hardware driver (interface + real + simulator)
/am-new-step       → Tạo machine sequence step (Step{NN}{Name}, no underscore)
/am-new-mechanism  → Tạo Mechanism [MechanismUI], dùng IHardwareManagerService
/am-new-station    → Tạo Station [StationUI], inject Mechanisms, dùng IStationSyncService
/am-new-screen     → Tạo màn hình WPF (View + ViewModel + Module ISA-101 compliant)
/am-alarm          → Thêm alarm code với [AlarmInfo] attribute
/am-review         → Review code (10 categories: arch, async, roslyn, alarm...)
/am-test           → Tạo unit tests (xUnit + Moq + FluentAssertions)
```

### Dùng slash command:
```
# Trong Claude Code terminal hoặc chat
/am-new-mechanism
> Tên: PickMechanism, Hardware: IMotionController "AxisXY" + IIoModule "Vacuum"
> Alarm range: 10100–10109, Timeout: 5s

/am-new-station
> Tên: PickStation, Mechanisms: PickMechanism + InspectMechanism
> Pipeline: wait "Feed→Pick", signal "Pick→Place", Timeout: 8s/cycle
```

---

## 🏷️ ATTRIBUTES NHANH

```csharp
[MechanismUI("Cụm gắp", group: "Station A", order: 0)]
class PickMechanism : BaseMechanism { }

[StationUI("Trạm gắp", icon: "robot-arm", order: 1)]
class PickStation : StationBase<PickStation> { }

[AlarmInfo("Axis timeout", "Kiểm tra cơ học", isStoppable: true)]
public const int AXIS_TIMEOUT = 10001;

[ParamView("Pick speed", "mm/s", min: 10, max: 500, group: "Motion", order: 0)]
public double PickSpeed { get; set; }
```

---

## 🤖 AGENT NHANH (cập nhật)

```
Tạo hardware driver      → paste AGENT: HardwareDriver      + mô tả thiết bị
Tạo sequence step        → paste AGENT: MachineSequence     + mô tả bước
Tạo màn hình WPF         → paste AGENT: UIModule            + mô tả màn hình
Tạo service + test       → paste AGENT: ServiceLayer        + mô tả service
Tạo Mechanism            → paste AGENT: MechanismDeveloper  + tên + hardware + alarm range
Tạo Station              → paste AGENT: StationDeveloper    + tên + mechanisms + pipeline
Tạo MasterController     → paste AGENT: MasterControllerDeveloper + stations + pipeline
Review code              → paste AGENT: CodeReview          + paste code
Refactor code            → paste AGENT: Refactor            + paste code
```

---

## 📐 NAMING CONVENTIONS (quan trọng — vi phạm = build fail)

```
Step class:     Step01Initialize   ✅  Step01_Initialize  ❌ (CA1707)
Mechanism:      PickMechanism      ✅  Pick_Mechanism     ❌
Station:        PickStation        ✅  Pick_Station       ❌
Controller:     DemoMasterController ✅

Event handler:  AlarmEventArgs     ✅  AlarmModel trực tiếp ❌ (CA1003)
Param name:     endDate, getValue  ✅  to, Get            ❌ (CA1716)
```

---

## 🔄 ISA-88 STATE MACHINE

```
Uninitialized ──[Initialize]──► Initializing ──[InitializeDone]──► Idle
                                     │[Error]                       │[Start]
                                     ▼                              ▼
                                 InitAlarm       Paused ◄──[Pause]── Running
                                     │[Reset]      │[Resume]─────────►│
                                     ▼             │[Stop]            │[Error]
                                 Resetting ◄───────┤                  ▼
                                     │          RunAlarm ──[Reset]──► Resetting
                              [ResetDone]▼
                                     Idle
                    [ResetDoneUninitialized]▼
                                 Uninitialized
```
