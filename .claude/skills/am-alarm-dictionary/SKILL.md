---
name: am-alarm-dictionary
description: >
  Thêm alarm code mới vào AM.AutoFrame. Dùng khi: tạo AlarmException mới,
  thêm alarm code constant, cần biết range của từng loại alarm.
  Cung cấp: code ranges, format message chuẩn, template [AlarmInfo] attribute.
---

# AM Alarm Dictionary Patterns

## Code Ranges

```
10000–10999  Motion / Axis        → 10001=MotionTimeout, 10002=AxisNotHomed, 10003=EStop
20000–20999  Vision / Camera      → 20001=GrabFail, 20002=ToolFail, 20003=InspectNg
30000–30999  I/O / Sensor         → 30001=PartMissing, 30002=ClampFail, 30003=SensorFail
40000–40999  System / Application → 40001=DbError, 40002=SystemCritical, 40003=ConfigInvalid
50000–50999  Communication        → 50001=ConnFail, 50002=CommTimeout, 50003=CrcError
60000–60999  Production / Recipe  → 60001=RecipeInvalid, 60002=SnDuplicate, 60003=BatchFull
70000–70999  Safety / Interlock   → 70001=EStopPressed, 70002=DoorOpen, 70003=LightCurtain
```

## Format Message Chuẩn

```
Pattern: [Thiết bị/Vị trí] [Vấn đề] — [Hành động operator cần làm]

✅ "Axis X: Home timeout sau 5000ms — Kiểm tra cơ học và thử lại"
✅ "Camera1: Grab thất bại lần 3 — Kiểm tra kết nối GigE và ánh sáng"
✅ "Station A: Part missing tại pickup position — Đặt lại phôi và reset"

❌ "Error 10001"               ← không đủ thông tin
❌ "Motion error occurred"     ← quá chung chung
❌ "Axis timeout"              ← thiếu hành động
```

## Template AlarmCodes.cs

```csharp
// Trong AM.Core/Constants/AlarmCodes.cs
public static class AlarmCodes
{
    // ── Motion (10000–10999) ──────────────────────────────
    [AlarmInfo("Axis timeout", "Kiểm tra cơ học trục, servo drive enable", isStoppable: true)]
    public const int MotionTimeout = 10001;

    [AlarmInfo("Trục chưa home", "Chạy lại Initialize hoặc Home thủ công", isStoppable: true)]
    public const int AxisNotHomed = 10002;

    [AlarmInfo("E-Stop kích hoạt", "Xác nhận an toàn, reset E-Stop rồi Initialize lại", isStoppable: true)]
    public const int EStopTriggered = 10003;

    // ── Vision (20000–20999) ─────────────────────────────
    [AlarmInfo("Camera grab thất bại", "Kiểm tra kết nối camera, tăng exposure nếu ảnh tối", isStoppable: true)]
    public const int CameraGrabFail = 20001;

    [AlarmInfo("Vision tool lỗi", "Kiểm tra vùng ROI, ánh sáng, vị trí phôi", isStoppable: false)]
    public const int VisionToolFail = 20002;

    [AlarmInfo("Sản phẩm NG", "Loại bỏ phôi NG, xác nhận để tiếp tục", isStoppable: false)]
    public const int InspectNg = 20003;
}
```

## Cách throw AlarmException đúng

```csharp
// Ném AlarmException với context đầy đủ
throw new AlarmException(
    AlarmCodes.MotionTimeout,
    stationName,                    // "StationA" hoặc nameof(PickMechanism)
    $"Axis {axisName}: timeout sau {timeoutMs}ms");

// Trong catch — log exception làm tham số đầu tiên (RSPEC-6667)
catch (OperationCanceledException) when (!ct.IsCancellationRequested)
{
    throw new AlarmException(AlarmCodes.MotionTimeout, _name,
        $"{deviceName}: timeout sau {_timeoutMs}ms");
}
```

## Checklist khi thêm alarm mới

```
□ Chọn đúng range (10xxx-70xxx) theo loại thiết bị
□ Thêm [AlarmInfo(displayName, remedy, isStoppable)] attribute
□ Message format: [Thiết bị] [Vấn đề] — [Hành động]
□ isStoppable: true nếu cần dừng máy, false nếu chỉ log
□ Throw đúng vị trí: Mechanism (không phải Station/Controller)
□ Không trùng code với alarm đã có
```

## isStoppable — khi nào dùng gì

```
isStoppable: true  → Máy phải dừng, operator phải xử lý trước khi tiếp
  Ví dụ: axis timeout, E-Stop, hardware error, communication fail

isStoppable: false → Ghi nhận nhưng máy có thể tiếp tục (hoặc tự retry)
  Ví dụ: sản phẩm NG (loại ra và tiếp tục), soft warning, info log
```
