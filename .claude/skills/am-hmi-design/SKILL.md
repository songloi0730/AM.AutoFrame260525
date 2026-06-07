---
name: am-hmi-design
description: >
  Quy tắc thiết kế giao diện HMI ISA-101 / SEMI E95 cho máy tự động hoá chạy trên
  MÁY TÍNH CÔNG NGHIỆP (IPC) màn hình 21–24" / 1920×1080, dùng CHUỘT + CẢM ỨNG.
  Dùng khi: thiết kế màn hình mới, review UI, hỏi về màu sắc/layout/alarm/connection status.
  Cung cấp: target hardware, layout 1920×1080, color tokens 4-level, alarm rules,
  connection status chips, screen sitemap, touch sizing, WPF patterns.
---

# AM HMI Design — ISA-101 / SEMI E95 cho IPC 21–24"

## 🎯 Target hardware (BẮT BUỘC đọc trước khi thiết kế)

| Hạng mục | Giá trị |
|----------|---------|
| Thiết bị | **Máy tính công nghiệp (IPC)** — KHÔNG phải HMI panel nhỏ 7–10" |
| Màn hình | **1920×1080 (Full HD), 21–24"** (tham chiếu tốt tới 27") |
| Nhập liệu | **Chuột + cảm ứng + bàn phím** (thiết kế cho cả 3) |
| Khoảng cách nhìn | Thao tác đứng/ngồi cạnh máy (~50–80cm) |
| Môi trường | Sàn xưởng / phòng sạch — có thể đeo **găng tay** (SEMI S8: giảm ~15% lực, vùng chạm phải to) |
| Chuẩn | **ANSI/ISA-101.01-2015**, **SEMI E95**, ISA-88/PackML, ISA-18.2 (alarm) |

> ⚠️ Vì là màn LỚN: **đừng giãn nội dung hết 1920px** — dòng dữ liệu quá dài khó đọc.
> Giới hạn bề rộng khối dữ liệu **~1200–1400px**, chia **lưới nhiều cột**, chừa lề hai bên.
> Tài liệu master: `docs/HMI_UI_Architecture_Template.md` + `docs/HMI_Components_Catalog.md`.

---

## Triết lý: High-Performance HMI — "yên tĩnh khi bình thường"

1. **Tình huống bình thường phải trông yên tĩnh** — nền **xám trung tính**, ít màu. Operator không phải căng mắt.
2. **Màu là tín hiệu, không phải trang trí** — xám = bình thường; vàng/cam = lệch chuẩn; đỏ = nghiêm trọng. Nghĩa cố định, nhất quán toàn hệ.
3. **Phát hiện bất thường** — làm nổi cái lệch chuẩn, làm mờ thiết bị đang chạy bình thường.
4. **Tối giản đồ hoạ** — phẳng, sạch; bỏ 3D/ảnh thực/gradient thừa.
5. **An toàn với mù màu (~8% nam giới)** — phân biệt bằng **màu + hình dạng + vị trí + chữ**, không chỉ màu.
6. **Mỗi màn 1 mục tiêu chính**, tác vụ thường dùng cách Dashboard **≤ 3 click**.

---

## Phân cấp 4 cấp màn hình (ISA-101) + nền theo cấp

| Level | Tên | Vai trò | Nền gợi ý | Ví dụ màn (P&P) |
|-------|-----|---------|-----------|-----------------|
| **L1** | Overview | Tổng quan toàn máy, đánh giá trong vài giây | `#F0F0F0` xám nhạt | Dashboard |
| **L2** | Control / Area | Điều khiển 1 cụm/nhiệm vụ | `#D3D3D3` xám vừa | Auto/Run, Motion, Vision |
| **L3** | Detail | Chi tiết 1 module, chỉnh tham số | `#C0C0C0` xám đậm hơn | IO, Axis Settings, Recipe |
| **L4** | Support / Diagnostic | Chẩn đoán, calib, log | `#F5F5F5` gần trắng | Calibration, Diagnostics, Alarm history |

> Light theme là mặc định cho IPC sàn xưởng sáng. Nền theo cấp giúp operator biết "đang ở độ sâu nào".
> Dùng `{DynamicResource}` cho nền/chữ; semantic alarm dùng `{StaticResource}` (không đổi theo theme).

---

## Layout Shell 1920×1080 (cố định mọi màn hình)

```
┌─ HEADER (80–96px) ───────────────────────────────────────────────────────────┐
│ Logo | Machine Name | [PackML State chip] | Mode(Auto/Man/Maint) | Recipe |   │
│ đèn tháp ảo 🔴🟡🟢 | [Start][Stop][Reset] toàn cục | User | 🌐 Lang | Clock     │
├──────────┬───────────────────────────────────────────────────────────────────┤
│ NAV      │ CONTENT (vùng lớn nhất)                                            │
│ 220–260px│   Lưới nhiều cột, GIỚI HẠN bề rộng khối dữ liệu ~1200–1400px,      │
│ collapse │   chừa lề. Nút cục bộ của riêng màn hình nằm trong đây.            │
│ →64px    │                                                                    │
│ (icon+   │                                                                    │
│  chữ)    │                                                                    │
├──────────┴───────────────────────────────────────────────────────────────────┤
│ ALARM BAR (48–56px): alarm mới nhất (đỏ nếu active) + [Acknowledge] + breadcrumb│
├───────────────────────────────────────────────────────────────────────────────┤
│ STATUS BAR (32–40px): dãy CHIP kết nối — PLC RFID CAM MES HIVE SECS/GEM DB ... │
└───────────────────────────────────────────────────────────────────────────────┘
```

**Quy tắc bố cục:**
- **Nút lệnh toàn cục (Start/Stop/Reset) ở HEADER** — cố định, cùng vị trí mọi màn hình (SEMI E95).
- **Nav ở cột trái** 220–260px, **collapse được về ~64px** (chỉ icon) để nhường chỗ content.
- **Tách ALARM BAR và STATUS BAR thành 2 dải riêng** — cảnh báo không bị loãng bởi thông tin kết nối tĩnh.
- E-Stop **vật lý** là chính; nút trên màn chỉ phản ánh trạng thái, KHÔNG thay nút cứng.
- Padding 8/16/24px; grid 8px (mọi kích thước là bội số 8). Tối đa 7±2 elements chính/màn (Miller's Law).

**Kích thước chạm/nút (tính cả đeo găng — SEMI S8):**
| Loại | Tối thiểu |
|------|-----------|
| Nút lệnh chính (Start/Stop/E-Stop ảo) | **≥ 60×60 px** |
| Nút thường | **≥ 44×44 px** |
| Khoảng cách 2 nút | **≥ 8 px** |
| Khoảng cách nút nguy hiểm ↔ nút thường | **≥ 48 px** |

---

## Typography (màn 1920×1080, khoảng cách thao tác)

| Loại | Cỡ | Ghi chú |
|------|-----|---------|
| Dữ liệu chính (live value) | **16–20pt, đậm** | To hơn nhãn để nổi bật |
| Nhãn / label | **11–13pt** | — |
| Tiêu đề màn | 18–24pt đậm | — |
| Tag/giá trị số canh cột | monospace | Dễ so hàng |

- Font **sans-serif** (Segoe UI / Inter / Arial). **Tương phản cao** — tránh vàng trên trắng, xám nhạt trên xám.
- Live value luôn **lớn hơn label ≥ 2pt** và in đậm.

---

## Alarm Display — ISA-101 / ISA-18.2

### Mức độ và màu (dành riêng — KHÔNG dùng trang trí)

| Level | Màu | Hex | Nhấp nháy | Phản hồi |
|-------|-----|-----|-----------|----------|
| Critical | Đỏ đậm | `#B71C1C` | 1 Hz | Ngay lập tức |
| High | Đỏ | `#F44336` | Không | < 5 phút |
| Medium | Vàng/Cam | `#FFC107` | Không | < 30 phút |
| Low | Xanh dương nhạt | `#64B5F6` | Không | Cuối ca |
| Suppressed | Tím | `#9370DB` | Không | (đã chặn/ẩn) |

**Format message:** `[Thiết bị] [Vấn đề] — [Hành động]`
```
✅ "Axis X: Home timeout sau 5000ms — Kiểm tra cơ học và home lại"
❌ "Error 10001"   ❌ "Motion error occurred"
```

- **Alarm Bar (dải riêng)**: alarm mới nhất + nút Acknowledge; nền chuyển đỏ nhạt khi có Critical. Click → Alarm List.
- **Alarm List** cột: Priority | Timestamp(`HH:mm:ss.fff`) | Code | Description | Station | State | Duration | [Ack].
- Ưu tiên hoá rõ ràng; tránh quá tải. EEMUA 191: ≤ 6 alarm/10 phút là bình thường.

---

## Hiển thị thiết bị (Equipment State)

Mỗi thiết bị ≥ **2 cách** biểu thị (màu + icon/text):

| Trạng thái | Màu | Symbol | Text |
|-----------|-----|--------|------|
| Off/Stopped (bình thường) | Xám `#616161` | ○ | OFF |
| Running/On | Xanh lá `#4CAF50` | ● | RUN |
| Fault | Đỏ `#F44336` | ✕ | ERR |
| Warning | Vàng `#FFC107` | △ | WARN |
| Manual | Xanh dương `#1E88E5` | M | MAN |
| Interlock | Tím `#7B1FA2` | 🔒 | ILK |

> Equipment **bình thường = XÁM**, không phải xanh lá. Xanh lá chỉ khi cần confirm "permissive met".

---

## Connection Status Bar — chip kết nối (dải dưới cùng)

Yêu cầu cốt lõi cho máy IPC tích hợp nhà máy. Mỗi kết nối là một **chip**: `icon + tên + chấm màu (+ ký hiệu hình dạng)`.
Click chip → popup chi tiết (IP/port, last heartbeat, lỗi gần nhất, nút Reconnect).

| Màu | Ký hiệu | Trạng thái |
|-----|---------|-----------|
| Xanh | ✓ | Connected / bình thường |
| Vàng | ! | Connecting / chậm / đồng bộ dở |
| Đỏ | ✕ | Mất kết nối / lỗi / timeout |
| Xám | – | Chưa cấu hình / disabled |

**Nhóm kết nối nên có:**
- **Device:** PLC · Motion controller (servo bus) · Camera/Vision (FPS) · RFID reader · Barcode scanner · IO/fieldbus EtherCAT (slave online) · Printer/Labeler.
- **Host/IT:** MES (đang sync) · HIVE · **SECS/GEM** (hiện CẢ Communication state + Control state: Online-Remote / Online-Local / Offline) · OPC-UA (session) · Database (độ trễ ghi).

**Kiến trúc:** mỗi connector implement `IConnectionMonitor` (Name, Kind, State, Detail, StateChanged, ReconnectAsync).
StatusBar ViewModel bind `ObservableCollection<IConnectionMonitor>` — UI KHÔNG tham chiếu kiểu vendor, chỉ qua interface (đổi vendor chỉ chạm HAL). Mất kết nối → cảnh báo ngay cả trên Dashboard.

---

## Sitemap màn hình (khung đủ rộng cho máy tự động hoá)

```
HOME        ├ Dashboard (L1) · Auto/Run (L2) · Process Flow (L2)
PRODUCTION  ├ Recipe · Traceability · History/Report · OEE
DEVICE      ├ IO Monitor (L3) · Motion Overview · Vision · RFID · Barcode · Printer · Device Monitor
ENGINEERING ├ Manual/Jog · Teach Position · Calibration (wizard) · Parameter/Engineering Mode
CONNECTIVITY├ PLC · MES · HIVE · SECS/GEM · OPC-UA · Database
MAINTENANCE ├ Alarm · Event Log · Service/Checklist
SYSTEM      ├ User/Permission · Backup · Restore · Update · License · System Info
```

**Ưu tiên triển khai:** Bắt buộc = Dashboard, Auto, IO, Alarm, Settings cơ bản, Connectivity, User.
Rất nên có = Manual/Jog, Motion, Calibration, Recipe, Maintenance, History. Nâng cao = OEE, Trend, SECS/GEM dashboard, guided troubleshooting.

---

## I/O Display Rules

```
DI: ○ xám = OFF → ● xanh = ON      DO: □ xám = OFF → ■ xanh = ON (khác hình DI), cam nếu FORCE
AI/AO: thanh ngang min/max/setpoint + giá trị số + đơn vị; cảnh báo quá ngưỡng
```
- Group theo **chức năng/station**, không theo địa chỉ vật lý. Tên có nghĩa: `PART_DETECT_SENSOR` không `DI_00_03`.
- **Force chỉ khi Manual/Maintenance**, hiện cảnh báo "ĐANG CÓ IO BỊ FORCE", ghi log user+thời gian.
- Safety I/O hiển thị **riêng, nổi bật hơn**.

---

## Số liệu, đơn vị, thời gian

| Loại | Thập phân | Ví dụ |
|------|-----------|-------|
| Position (mm) | 2 | `123.45 mm` |
| Velocity (mm/s) | 1 | `50.0 mm/s` |
| Temperature | 1 | `25.3 °C` |
| Counter | 0 | `1234` |

- Luôn hiện **đơn vị** (1 space): `42.3 °C`. Thời gian `DD/MM/YYYY HH:mm:ss` (24h). Alarm thêm `.fff`.

---

## Phân quyền (ẩn/hiện theo vai trò — không rừng nút disabled)

| Vai trò | Được phép |
|---------|-----------|
| Operator | Chạy/dừng, chọn recipe có sẵn, ack alarm, xem trạng thái |
| Technician | + jog trục, sửa tham số, calib, xem chẩn đoán, test IO |
| Engineer/Admin | + cấu hình hardware, recipe gốc, quản lý user, backup, communication |

---

## Performance & update rate

| Thao tác | Mục tiêu |
|---------|---------|
| Điều hướng màn hình | < 200 ms |
| Lệnh hardware (UI phản hồi) | < 100 ms (disable nút + spinner ngay) |
| Khởi động app | < 10 s (splash + progress) |

Update: state/alarm 100ms · position/velocity 100–200ms · sensor 500ms · counter 1s · chart 5s.
Throttle cập nhật UI cho bảng IO lớn để không nghẽn.

---

## Release Checklist — HMI cho IPC 21–24"

```
Target & layout:
□ Thiết kế cho 1920×1080, chuột + cảm ứng (không phải panel nhỏ)
□ Nội dung KHÔNG giãn hết 1920px — khối dữ liệu ≤ ~1400px, chia cột
□ Header 80–96px có nút lệnh toàn cục; Nav 220–260px collapse được
□ Alarm bar (48–56px) và Status bar (32–40px) là 2 dải RIÊNG
□ Nền theo cấp màn (L1 nhạt → L4 gần trắng)

Màu (High-Performance HMI):
□ Screenshot grayscale vẫn đọc được; bình thường trông "yên tĩnh" xám
□ Đỏ/vàng chỉ cho alarm/warning; equipment normal = xám (không xanh lá)
□ Trạng thái phân biệt bằng màu + hình dạng + chữ (mù màu OK)
□ Không hardcode hex — DynamicResource; semantic = StaticResource

Connection status:
□ Có dãy chip kết nối (PLC/RFID/Camera/MES/HIVE/SECS-GEM/DB...) + click xem chi tiết
□ SECS/GEM hiện cả Communication state + Control state
□ Mất kết nối cảnh báo ngay trên Dashboard

Alarm / Navigation / Data:
□ Alarm bar visible mọi màn; mỗi alarm có Code+Description+Action; Critical 1Hz
□ Mọi màn đến được ≤ 3 click; popup không che status bar
□ Mọi giá trị số có đơn vị; live value đậm > label ≥ 2pt; không NaN/exception text

Ergonomics (SEMI S8 — đeo găng):
□ Nút chính ≥ 60×60px, nút thường ≥ 44×44px, gap ≥ 8px, nút nguy hiểm cách ≥ 48px
□ Font ≥ 11pt; live value 16–20pt đậm; tương phản cao

i18n & audit:
□ Không hardcode string; đổi ngôn ngữ không restart; text không bị cắt
□ Thao tác nhạy cảm (force IO, calib, đổi tham số) ghi log user + thời gian
```
