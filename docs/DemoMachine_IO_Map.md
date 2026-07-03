# DemoMachine_IO_Map v1.0 — Máy mẫu Pick & Place (DemoPickPlace)

> IO tối thiểu đủ để sequence mẫu 6 bước (Scan → Feed → Pick → Vision → Place → Report)
> chạy được cả trên phần cứng thật lẫn chế độ mô phỏng.
> Quy tắc HAL: code chỉ dùng **tên logic** (hằng số trong `IoMap`), không dùng địa chỉ
> vật lý. Địa chỉ vật lý map trong `hardware.config.json` theo vendor đang chọn.

## 1. Digital Input

| Tên logic | Mô tả | Trạm dùng | Ghi chú |
|---|---|---|---|
| `DI.EStop.Ok` | Trạng thái mạch E-Stop (qua safety relay) | Shell, mọi trạm | Đọc để hiển thị; cắt cứng do relay, không do software |
| `DI.SafetyDoor.Closed` | Cửa an toàn đóng | Interlock Start | |
| `DI.DoorLock.Locked` | Phản hồi khóa cửa | Interlock | |
| `DI.AirPressure.Ok` | Áp khí nguồn đạt ngưỡng | Interlock Start | Công tắc áp; AI chi tiết xem mục 3 |
| `DI.Btn.Start` | Nút Start vật lý | Master controller | Song song với nút UI |
| `DI.Btn.Stop` | Nút Stop vật lý | Master controller | |
| `DI.Btn.Reset` | Nút Reset vật lý | Master controller | |
| `DI.Feeder.TrayPresent` | Có khay liệu vào | FeedStation | |
| `DI.Feeder.PartAtPick` | Sản phẩm đã ở vị trí gắp | FeedStation → PickStation | |
| `DI.Nozzle.VacuumOn` | Phản hồi vacuum (đã hút được hàng) | PickStation | Kiểm sau khi bật van hút |
| `DI.OutTray.Present` | Có khay ra (OK) | PlaceStation | |
| `DI.OutTray.Full` | Khay ra đầy | PlaceStation, điều kiện tự dừng | |
| `DI.NgTray.Present` | Có khay NG | PlaceStation (`runOnNg`) | |
| `DI.Axis.X.Home` / `DI.Axis.Y.Home` / `DI.Axis.Z.Home` | Cảm biến home 3 trục | InitializeAsync | Nếu dùng homing theo encoder tuyệt đối thì bỏ |

## 2. Digital Output

| Tên logic | Mô tả | Trạm dùng | Trạng thái an toàn khi Abort |
|---|---|---|---|
| `DO.Tower.Red` / `DO.Tower.Yellow` / `DO.Tower.Green` | Đèn tháp 3 tầng | Shell (theo PackML state) | Red ON |
| `DO.Buzzer` | Còi | Alarm service; nút "Tắt còi" | OFF |
| `DO.Vacuum.On` | Van hút | PickStation | OFF (nhả hàng có kiểm soát trước khi tắt — xem §5) |
| `DO.Vacuum.Blow` | Van thổi nhả | PlaceStation | OFF |
| `DO.DoorLock` | Khóa cửa an toàn | Interlock | Nhả khóa khi máy dừng hẳn |
| `DO.WorkLight` | Đèn máy | Thao tác nhanh | Giữ nguyên |
| `DO.Ionizer` | Thổi ion | Thao tác nhanh | OFF |
| `DO.Camera.Trigger` | Trigger chụp | VisionStation | OFF |
| `DO.Feeder.Advance` | Cấp liệu tiến 1 nhịp | FeedStation | OFF |

## 3. Analog Input

| Tên logic | Mô tả | Đơn vị | Dùng để |
|---|---|---|---|
| `AI.Vacuum.Pressure` | Áp chân không tại nozzle | kPa | Ngưỡng phán định hút thành công (recipe: `VacuumThresholdKpa`) |
| `AI.Main.Pressure` | Áp khí nguồn | MPa | Cảnh báo sớm trước khi `DI.AirPressure.Ok` rớt |

## 4. Trục (EtherCAT / IMotionService)

| Tên logic | Vai trò | Homing | Tham số recipe liên quan |
|---|---|---|---|
| `Axis.X` | Gantry ngang | Home sensor + index | Vị trí Pick/Place theo tọa độ khay (tray coordinate) |
| `Axis.Y` | Gantry dọc | Home sensor + index | như trên |
| `Axis.Z` | Nâng hạ đầu hút | Home về đỉnh trước X/Y | `PickHeightMm`, `PlaceHeightMm`, `SafeZMm` |

Thứ tự homing bắt buộc: **Z lên đỉnh → X → Y** (Z chưa an toàn thì cấm X/Y chạy).
Soft limit ba trục khai báo trong `hardware.config.json`, engine không hard-code.

## 5. Chuỗi liên động cần nhớ khi viết station

- Pick: Z xuống `PickHeightMm` → `DO.Vacuum.On` → chờ `AI.Vacuum.Pressure ≥ ngưỡng`
  (timeout trong bước) → Z lên `SafeZMm`. Dry-run: bỏ vacuum, vẫn chạy quỹ đạo.
- Place: Z xuống → `DO.Vacuum.Blow` nhịp ngắn → tắt vacuum → xác nhận
  `DI.Nozzle.VacuumOn` = false → Z lên. Sản phẩm NG đặt sang tọa độ khay NG.
- Abort khi đang giữ hàng: Z giữ nguyên, vacuum GIỮ ON cho tới khi operator xử lý
  qua chế độ Manual — thả hàng giữa hành trình nguy hiểm hơn giữ nguyên.

## 6. Thiết bị truyền thông (không phải IO nhưng station cần)

| Tên logic | Giao thức | Trạm dùng |
|---|---|---|
| `Dev.Scanner` | COM/TCP | ScannerStation — đọc SN |
| `Dev.Camera` | GigE Vision | VisionStation |
| `Dev.SafetyPlc` | Modbus TCP | Đọc khối an toàn (nếu dùng safety PLC thay relay) |
| `Dev.Host` | OPC-UA / DB | ReportStation — upload kết quả |

## 7. Hằng số IoMap (chống magic string)

```csharp
namespace AM.Core.Hardware;

public static class IoMap
{
    public static class Di
    {
        public const string EStopOk          = "DI.EStop.Ok";
        public const string SafetyDoorClosed = "DI.SafetyDoor.Closed";
        public const string DoorLockLocked   = "DI.DoorLock.Locked";
        public const string AirPressureOk    = "DI.AirPressure.Ok";
        public const string BtnStart         = "DI.Btn.Start";
        public const string BtnStop          = "DI.Btn.Stop";
        public const string BtnReset         = "DI.Btn.Reset";
        public const string FeederTrayPresent= "DI.Feeder.TrayPresent";
        public const string FeederPartAtPick = "DI.Feeder.PartAtPick";
        public const string NozzleVacuumOn   = "DI.Nozzle.VacuumOn";
        public const string OutTrayPresent   = "DI.OutTray.Present";
        public const string OutTrayFull      = "DI.OutTray.Full";
        public const string NgTrayPresent    = "DI.NgTray.Present";
    }

    public static class Do
    {
        public const string TowerRed    = "DO.Tower.Red";
        public const string TowerYellow = "DO.Tower.Yellow";
        public const string TowerGreen  = "DO.Tower.Green";
        public const string Buzzer      = "DO.Buzzer";
        public const string VacuumOn    = "DO.Vacuum.On";
        public const string VacuumBlow  = "DO.Vacuum.Blow";
        public const string DoorLock    = "DO.DoorLock";
        public const string WorkLight   = "DO.WorkLight";
        public const string Ionizer     = "DO.Ionizer";
        public const string CameraTrigger = "DO.Camera.Trigger";
        public const string FeederAdvance = "DO.Feeder.Advance";
    }

    public static class Ai
    {
        public const string VacuumPressure = "AI.Vacuum.Pressure";
        public const string MainPressure   = "AI.Main.Pressure";
    }

    public static class Axis
    {
        public const string X = "Axis.X";
        public const string Y = "Axis.Y";
        public const string Z = "Axis.Z";
    }
}
```

## 8. Chế độ mô phỏng (SimIoService)

Để demo chạy không cần phần cứng, `IIoService` có bản `SimIoService`:

- Mọi DI có thể force qua tab VH tay (đúng contract `data-*`-style: tên logic hiển thị thẳng).
- Hành vi tự động tối thiểu để cycle tự chạy: bật `DO.Vacuum.On` → sau 80 ms
  `DI.Nozzle.VacuumOn` = true và `AI.Vacuum.Pressure` nhảy lên trên ngưỡng;
  `DO.Feeder.Advance` nhịp → 150 ms sau `DI.Feeder.PartAtPick` = true;
  `DO.Vacuum.Blow` → `DI.Nozzle.VacuumOn` = false.
- Tham số cấu hình được: delay từng phản hồi + xác suất lỗi (vacuum không đạt,
  scan fail) để test nhánh `onError` của engine bằng mắt thường trên dashboard.
