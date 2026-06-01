---
name: am-hmi-design
description: >
  Quy tắc thiết kế giao diện HMI ISA-101 cho máy tự động hoá.
  Dùng khi: thiết kế màn hình mới, review UI, hỏi về màu sắc/layout/alarm display.
  Cung cấp: color tokens, screen hierarchy, alarm rules, layout rules, WPF patterns.
---

# AM HMI Design — ISA-101 Reference

## Triết lý: 90% xám, 10% màu có ý nghĩa

Giao diện phục vụ vận hành, không phải trưng bày. 3 ưu tiên theo thứ tự:
1. **Situational Awareness** — operator biết trạng thái máy mà không cần đọc nhiều
2. **Phòng ngừa lỗi** — giao diện ngăn thao tác sai
3. **Nhất quán** — học một lần dùng mãi

---

## Phân cấp màn hình (4 Levels)

| Level | Tên | Mục đích | Quyền truy cập |
|-------|-----|---------|----------------|
| L1 | Overview | Toàn bộ máy: state, alarm count, UPH, tên recipe | Operator+ |
| L2 | Process Area | Chi tiết 1 station: live values, I/O chính, controls | Operator+ |
| L3 | Faceplate | 1 thiết bị cụ thể: popup/flyout, jog, set | Technician+ |
| L4 | Engineering | Cấu hình, debug, calibration | Engineer+ |

**Nguyên tắc:**
- Tối đa 3 click từ L1 đến bất kỳ màn hình nào
- L1: KHÔNG hiển thị giá trị chi tiết — chỉ trạng thái tổng quan
- L3: xuất hiện dạng Popup/Flyout, không che Status Bar
- L4: không dùng trong sản xuất bình thường

---

## Layout Shell cố định

```
Top Bar (48px):    Logo | Machine Name | [State Chip] | User | Clock | Language
Side Menu (200px): Navigation — có thể collapse còn 60px (chỉ icon)
Content Area:      Thay đổi theo màn hình Level 1-4
Status Bar (32px): [🔴 Critical:N] [🔴 High:N] [🟡 Med:N] | Cycle Time | UPH | Recipe
```

**Nguyên tắc bố cục:**
- Thông tin quan trọng nhất: góc trên-trái (natural reading pattern)
- E-Stop: góc trên phải, luôn accessible, KHÔNG bị che
- Padding: 8px (nhỏ) / 16px (thông thường) / 24px (section)
- Grid 8px: mọi kích thước là bội số của 8
- Tối đa 7±2 elements chính trên một màn hình (Miller's Law)

---

## Alarm Display — ISA-18.2 / EEMUA 191

### Mức độ và màu

| Level | Màu | Hex | Nhấp nháy | Phản hồi yêu cầu |
|-------|-----|-----|-----------|------------------|
| Critical | Đỏ đậm | `#B71C1C` | 1 Hz | Ngay lập tức |
| High | Đỏ | `#F44336` | Không | < 5 phút |
| Medium | Vàng | `#FFC107` | Không | < 30 phút |
| Low | Xanh dương nhạt | `#64B5F6` | Không | Cuối ca |

**Alarm message format:** `[Thiết bị] [Vấn đề] — [Hành động]`
```
✅ "Axis X: Home timeout sau 5000ms — Kiểm tra cơ học và home lại"
❌ "Error 10001"
❌ "Motion error occurred"
```

**Alarm Bar (Status Bar)** — LUÔN visible, không che:
- Hiển thị số lượng theo level: `🔴 Critical:1 | 🔴 High:3 | 🟡 Med:5`
- Critical alarm: nền Status Bar chuyển đỏ nhạt
- Click → mở Alarm List screen

**Alarm List** — Cột bắt buộc: Priority | Timestamp | Code | Description | Station | State | Duration | [Acknowledge]

**Alarm rate (EEMUA 191):** ≤ 6 alarm / 10 phút là bình thường. >10 alarm đồng thời = cần review.

---

## Hiển thị thiết bị (Equipment State)

Mỗi thiết bị cần **ít nhất 2 cách** biểu thị trạng thái (màu + icon/text):

| Trạng thái | Màu fill | Symbol | Text |
|-----------|---------|--------|------|
| Off/Stopped | Xám `#616161` | ○ | OFF |
| Running/On | Xanh lá `#4CAF50` | ● | RUN |
| Fault/Error | Đỏ `#F44336` | ✕ | ERR |
| Warning | Vàng `#FFC107` | △ | WARN |
| Manual | Xanh dương `#1E88E5` | M | MAN |
| Interlock | Tím `#7B1FA2` | 🔒 | ILK |

> **Quan trọng:** Equipment ở trạng thái **bình thường** hiển thị màu **XÁM** — KHÔNG phải xanh lá. Xanh lá chỉ khi có trạng thái cần confirm "permissive met".

---

## I/O Display Rules

```
DI (Input):  ○ xám = OFF   → ● xanh = ON
DO (Output): □ xám = OFF   → ■ xanh = ON  (khác hình với DI)
AI/AO: Thanh ngang với min/max/setpoint + giá trị số
```
- Group theo chức năng, không theo địa chỉ vật lý
- Tên có nghĩa: `PART_DETECT_SENSOR` không phải `DI_00_03`

---

## Navigation Rules

- Tối đa **3 click** từ Overview đến bất kỳ màn hình nào
- Breadcrumb hoặc chỉ thị màn hình hiện tại luôn hiển thị
- Không dùng browser-style back/forward
- Side menu: luôn có [Home/Overview] ở đầu, [Alarm] với badge số
- Popup: có thể drag, nút đóng rõ ràng (✕), không che Status Bar

---

## Số liệu và đơn vị

| Loại | Thập phân | Ví dụ |
|------|-----------|-------|
| Position (mm) | 2 | `123.45 mm` |
| Velocity (mm/s) | 1 | `50.0 mm/s` |
| Temperature | 1 | `25.3 °C` |
| Pressure (kPa) | 2 | `101.32 kPa` |
| Counter | 0 | `1234` |

- **Luôn hiển thị đơn vị** kế bên: `42.3 °C` (có 1 space giữa số và đơn vị)
- Format thời gian: `DD/MM/YYYY HH:mm:ss` (24h, không AM/PM)
- Alarm timestamp: thêm milliseconds `HH:mm:ss.fff`

---

## Theme System

**Khuyến nghị chọn theme:**
| Môi trường | Theme |
|-----------|-------|
| Control room, clean room | Dark |
| Sàn xưởng ánh sáng mạnh | Light |
| Màn hình báo cáo | Light |
| Tablet ngoài trời | Light |

**Quy tắc ResourceDictionary:**
```xml
<!-- App.xaml load order -->
<ResourceDictionary Source="Themes/Colors.Dark.xaml"/>   <!-- hoặc Colors.Light.xaml -->
<ResourceDictionary Source="Themes/Typography.xaml"/>
<ResourceDictionary Source="Themes/Controls.xaml"/>
<ResourceDictionary Source="Themes/StatusStyles.xaml"/>
```

- `Controls.xaml` và `StatusStyles.xaml`: KHÔNG hardcode màu hex
- Dùng `{DynamicResource TokenBrush}` để switch theme runtime
- Semantic colors (Status.*): dùng `{StaticResource}` — không đổi theo theme

---

## Performance Rules

| Thao tác | Mục tiêu | Xử lý |
|---------|---------|-------|
| Điều hướng màn hình | < 200 ms | — |
| Lệnh hardware (UI response) | < 100 ms | Disable button + spinner ngay |
| Load dữ liệu lớn | > 2 giây | Progress bar + có thể cancel |
| Khởi động app | < 10 giây | Splash screen với progress |

**Update rate data:**
- Machine state, alarm: 100 ms
- Position, velocity: 100–200 ms
- Sensor (nhiệt độ, áp suất): 500 ms
- Production counter: 1 giây
- Chart history: 5 giây

---

## Release Checklist — HMI

```
Màu sắc:
□ Screenshot grayscale → vẫn đọc được thông tin
□ Màu đỏ/vàng chỉ cho alarm/warning
□ Equipment normal = màu xám (không xanh lá)
□ Không hardcode hex — dùng DynamicResource
□ Đổi theme Dark/Light → không mất thông tin

Alarm:
□ Alarm bar visible trên mọi màn hình
□ Mọi alarm có Code + Description + Action
□ Critical alarm nhấp nháy đúng 1 Hz

Navigation:
□ Mọi màn hình đến được trong ≤ 3 click
□ Popup không che Status Bar

Data:
□ Mọi giá trị số có đơn vị
□ Giá trị live update đúng tần suất
□ Không hiển thị NaN/Infinity/exception text

Ergonomics:
□ Font ≥ 12 pt, live value > label ≥ 2 pt
□ Touch target ≥ 44×44 px (60×60 khi đeo găng tay)
□ E-Stop: lớn nhất, đỏ đậm, cố định góc trên phải
□ Khoảng cách nút nguy hiểm ≥ 48 px

i18n:
□ Không hardcode string trong XAML/code
□ Đổi ngôn ngữ không cần restart
□ Text không bị cắt khi chuyển ngôn ngữ
```
