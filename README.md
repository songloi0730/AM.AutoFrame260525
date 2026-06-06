# AM.AutoFrame

Framework C# cho phần mềm điều khiển máy tự động hoá công nghiệp — **đổi hãng phần cứng chỉ sửa một project driver, không động vào logic máy**.

> Nền tảng: .NET 9 · WPF · Prism/DryIoc · EF Core + SQLite · ISA-88 state machine

---

## Tổng quan

AM.AutoFrame tách bạch **logic máy** (Station / Mechanism / Sequence) khỏi **driver phần cứng** qua một tầng abstraction trung lập (HAL). Toàn hệ thống chỉ làm việc với interface (`IMotionController`, `IIoModule`, `IVisionProcessor`...); driver hãng được chọn tại runtime qua cấu hình.

**Nguyên tắc cốt lõi:**
- Tầng trên phụ thuộc **interface**, không phụ thuộc driver hãng.
- Mỗi hãng (SDK-nặng) = một project riêng; đổi hãng = đổi 1 trường JSON.
- Đơn vị kỹ thuật trung lập (mm, độ, mm/s) — quy đổi pulse↔mm nằm trong driver.
- Luôn có bản **Simulated** để chạy/test không cần phần cứng.
- An toàn (E-Stop/Light Curtain) do **mạch an toàn phần cứng** đảm nhiệm; phần mềm chỉ ĐỌC trạng thái.

---

## Kiến trúc 3 tầng

```
MasterController (BaseMasterController — ISA-88 8 trạng thái, 13 transitions)
   ├── Station A (StationBase<T>)
   │     ├── PickMechanism (BaseMechanism) → IMotionController
   │     └── InspectMechanism (BaseMechanism) → ICameraDevice / IVisionProcessor
   └── Station B (StationBase<T>)
         └── PlaceMechanism (BaseMechanism) → IMotionController + IIoModule
```

- **Mechanism** bọc 1-N device, expose domain method (PickAsync, InspectAsync).
- **Station** điều phối Mechanism cho một công đoạn; KHÔNG gọi hardware trực tiếp.
- **MasterController** là nơi DUY NHẤT đổi State / fire trigger; điều phối pipeline qua `IStationSyncService`.

**State machine (ISA-88):** Uninitialized → Initializing → Idle → Running ⇄ Paused, nhánh alarm InitAlarm/RunAlarm → Resetting.

---

## Cấu trúc solution (17 projects)

| Project | Vai trò |
|---|---|
| `AM.Core` | Enums, Models, Attributes, AlarmCodes, Exceptions, EventArgs |
| `AM.Core.Abstractions` | Interface (Hardware/Machine/Service/Repo) — **không** reference SDK hãng |
| `AM.CommonTools` | Guard, RetryHelper |
| `AM.Hardware.Motion` | Simulated + GTS (固高) + Advantech (P/Invoke) |
| `AM.Hardware.Vision` | SimulatedCameraDevice, SimulatedVisionProcessor |
| `AM.Hardware.IO` | Simulated + Advantech ADAM + SafetyInput + IoTagMap |
| `AM.Hardware.Comm` | Modbus/Serial/TCP (real+sim), OPC UA/EtherNet-IP, Inovance PLC+servo, Mitsubishi MC, Siemens S7, Robot socket |
| `AM.Hardware.Scanner` | Keyence / Cognex / Simulated barcode scanner |
| `AM.Services` | Alarm, Recipe, Parameter, HardwareManager, StationSync, **HardwareWatchdog**, Production |
| `AM.Data` | EF Core + SQLite (Alarm history, Production records) |
| `AM.Infrastructure` | BaseMechanism, StationBase<T>, BaseMasterController |
| `AM.WorkStation.Demo` | Máy mẫu 3-tier đầy đủ |
| `AM.Modules.Dashboard` | WPF: machine state + alarm list + controls |
| `AM.Application.Shell` | WPF entry, Bootstrapper (DI), HardwareFactory |
| `AM.Services.Tests` / `AM.Infrastructure.Tests` / `AM.Hardware.Tests` | Unit + integration tests |

---

## Bắt đầu nhanh

```bash
# Build (TreatWarningsAsErrors=true — mọi CA/Sonar warning là lỗi build)
dotnet build AM.AutoFrame.sln

# Chạy toàn bộ test
dotnet test AM.AutoFrame.sln

# Chạy app (chế độ Simulated mặc định)
dotnet run --project AM.Application.Shell
```

Yêu cầu: **.NET 9 SDK**, Windows (do WPF). Mặc định chạy **Simulated** — không cần phần cứng.

---

## Đổi hãng phần cứng (HAL)

Cấu hình trong `AM.Application.Shell/appsettings.json`:

```jsonc
"AutoMachine": {
  "UseSimulation": false,           // true = toàn bộ Simulated
  "Motion": { "Vendor": "Gts" },    // Simulated | Gts | Advantech | InovanceServo
  "Plc":    { "Vendor": "Siemens" },// Simulated | Inovance | Mitsubishi | Siemens
  "Io":     { "Vendor": "AdvantechAdam" },
  "Scanner":{ "Vendor": "Keyence" },// Simulated | Keyence | Cognex
  "Vision": { "Vendor": "Simulated" }
}
```

| Thành phần | Interface | Project hãng | Đổi hãng cần sửa |
|---|---|---|---|
| Motion | `IMotionController` / `IAxis` | AM.Hardware.Motion(.Gts/.Advantech) | config |
| IO | `IIoModule` / `ISafetyInput` | AM.Hardware.IO | config + `io.map.json` |
| PLC | `IPlcDevice` | AM.Hardware.Comm | config |
| Camera/Vision | `ICameraDevice` / `IVisionProcessor` | AM.Hardware.Vision | config + file .vpp |
| Scanner | `IBarcodeScanner` | AM.Hardware.Scanner | config |

> Driver SDK-native (固高 `gts.dll`, Advantech `ADVMOT.dll`, Cognex VisionPro, Basler pylon) cần DLL của hãng — xem `libs/README.md`. Build vẫn pass khi chưa có DLL (chỉ Simulated chạy).

IO gọi theo **tag** (không phải số kênh) qua `io.map.json` — đổi đấu dây chỉ sửa file map.

---

## Độ tin cậy

- **HardwareWatchdog**: poll `IsConnected` mọi device; mất kết nối → raise alarm + EmergencyStop + auto-reconnect (back-off).
- **Alarm code ranges**: Motion 10xxx · Vision 20xxx · IO 30xxx · System 40xxx · Comm 50xxx · Production 60xxx · Safety 70xxx.
- Logging: Serilog (console + file rolling, retain 30 ngày).

---

## Quy ước phát triển

- Build cứng: `TreatWarningsAsErrors=true` + `AnalysisMode=All`.
- NuGet version tập trung tại `Directory.Packages.props` (Central Package Management).
- Coding rules: `.claude/rules/`, `.cursorrules`. Kiến trúc & trạng thái: `CLAUDE.md`, `PROJECT_STATUS.md`, `CHANGELOG.md`.
- Commit: `bash scripts/am-commit.sh "loại: mô tả"` (xử lý lock file Windows/OneDrive).

---

## Giấy phép

Nội bộ — AM.AutoFrame.
