# 0006 — Vision live-view — §6.7

**Bối cảnh.** Vùng ảnh màn Vision là placeholder: sim `GrabImageAsync` trả `Array.Empty<byte>()` và `ICameraDevice` chỉ trả
`byte[]` trần (không W/H/Format) nên không dựng được ảnh. Cần sim sinh frame thật + live-view (FrameData → BitmapSource).

## Phương án

### 1) Nơi convert FrameData → ảnh
- **A — Converter ở tầng View  ✅ CHỌN:** VM expose `FrameData?` (model thuần); `IValueConverter` dựng `BitmapSource`.
  *+* giữ **R-UI-01** (ViewModel KHÔNG import System.Windows — bất biến, để test được + tách UI). *−* thêm 1 converter nhỏ.
- **B — VM expose `ImageSource`:** binding 1 dòng nhưng VM kéo `System.Windows.Media` → **phá R-UI**. Loại.
- **C — code-behind dựng ảnh:** khó test, logic UI tản mát. Loại.

### 2) Nguồn frame của simulator
- **A — pattern tổng hợp động  ✅ CHỌN:** gradient + thanh dọc chạy theo `Environment.TickCount` → nhìn thấy "live"; rẻ,
  không cần file asset, không Random (tránh CA5394).
- **B — ảnh mẫu bundled:** cần file + xử lý decode; tĩnh. **C — gradient tĩnh:** không thể hiện "live". Loại.

### 3) Vòng live-view
- **Toggle Start/Stop  ✅ CHỌN:** chỉ grab khi người dùng bật (đỡ tốn CPU khi không xem). (always-on đơn giản hơn nhưng lãng phí.)

## Hiện thực (tóm tắt)
- `ICameraDevice` +`GrabFrameAsync` → `FrameData`; giữ `GrabImageAsync` (byte[]) cho call site cũ (sim trả `frame.Pixels`).
- `SimulatedCameraDevice` sinh Bgr24 640×480 (gradient + thanh chạy + thập tâm).
- `VisionViewModel`: `FrameData? LiveFrame` + `IsLive` + `ToggleLive` + `LiveLoopAsync` (poll 100ms, marshal UI). Giữ R-UI.
- `FrameToImageSourceConverter` (View): map PixelFormat→`PixelFormats`, `BitmapSource.Create(...)` + `Freeze()`.

## Hệ quả / đánh đổi
- Format ngoài Bgr24/Mono/Rgb24 → converter trả null (an toàn). Bayer chưa debayer.
- Chưa vẽ overlay kết quả vision (offset/box) lên ảnh — để sau.
- Camera thật (Basler/HIK…) khi có driver chỉ cần implement `GrabFrameAsync` trả FrameData đúng format — UI/VM không đổi.

## Liên kết
- Triển khai: Session 67 (`CHANGELOG.md`). Nền: [0001 §4 simulation parity, §8 i18n](0001-am-autoframe-design-decisions.md) + R-UI-01.
