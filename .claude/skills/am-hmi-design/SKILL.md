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

> ⚠️ Vì là màn LỚN: layout tổng dùng full 1920px (work area + right rail), nhưng **dòng dữ liệu
> trong bảng không kéo dài vô hạn** — bảng nằm trong card có bề rộng xác định, chia cột rõ.
> Tài liệu master (đọc theo thứ tự): **`docs/HMI_UI_Architecture_Template_v2.md`** (bố cục Home 7 vùng
> + quyết định adoption) → `docs/HMI_Button_Spec.md` (đặc tả từng nút: precondition/role/audit) →
> `docs/HMI_UI_Architecture_Template.md` (v1.1 — CHỈ còn hiệu lực phần template điều khiển
> ManualControlView/AxisControlView/VisionTeachView) → `docs/HMI_Components_Catalog.md` +
> `docs/HMI_Advanced_Standards.md`.
> **Mô hình vận hành:** 1 kỹ thuật viên / nhiều máy — ghé máy ~30 giây. Tối ưu cho can thiệp nhanh.

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

## Layout Shell 1920×1080 — 7 vùng cố định (v2, Persistent Frame)

```
┌─ 1 HEADER (64px) ─────────────────────────────────────────────────────────────┐
│ Logo | AM.AutoFrame · {MachineId} | [AUTO/DRY] [LOCAL/REMOTE] [State badge]    │
│ (tiến độ lô nếu có MES) … Recipe · Clock+♥heartbeat · [🌐 Ngôn ngữ] [User ▾]  │
├─ 2 NAV TABS (48px) — tab NGANG icon+chữ, KHÔNG BAO GIỜ icon-only ─────────────┤
├─ 3 ALARM BANNER (40px) — DUY NHẤT alarm ưu tiên cao nhất CHƯA ACK + [ACK ≥40px]│
│    + chip "+N khác ▾" · xám khi sạch (vẫn chiếm chỗ) · hổ phách=warn, đỏ=lỗi   │
├─ 4 WORK AREA (≈1310px) ──────────────────────┬─ 5 RIGHT RAIL (560px) ─────────┤
│   Sub-tab theo máy (HomeSubViews config):     │  KPI ca (3×2) → Thao tác nhanh │
│   "Sản phẩm" mặc định: thumbnail vision        │  → Trạm & an toàn (2 cột)      │
│   (ảnh KẾT QUẢ per cycle, không stream live)   │  → Nhật ký (1 dòng/entry,      │
│   + bảng truy vết SN (dòng ≥40px, NG tô màu)   │     ellipsis, bấm bung)        │
├─ 6 ACTION BAR (84px) — Start·Pause/Resume·Stop·Reset │ Dry run·Manual ─────────┤
│    Nút TRẮNG phẳng 64px, icon 1 màu trên + nhãn dưới; CHỈ Start có viền nhấn   │
├─ 7 CONNECTION BAR (40px): Thiết bị │ Host │ … version (góc phải) ──────────────┤
└────────────────────────────────────────────────────────────────────────────────┘
```

**Quy tắc bố cục (v2):**
- **Phân tầng nhận thức → hành động**: trên xuống = trạng thái → nội dung → lệnh.
  **Lệnh máy KHÔNG BAO GIỜ ở nửa trên màn hình** — action bar nằm ĐÁY, không ở header.
- **Một lệnh một chỗ** — không trùng nút giữa các vùng (Tắt còi chỉ ở Thao tác nhanh…).
- **Giải thích thay vì giấu**: nút không khả dụng → MỜ + nêu lý do (toast/tooltip). KHÔNG ẩn.
  Ngoại lệ: tab nguyên module không liên quan role (Motion/IO với Operator) thì ẩn hẳn.
- **Hai hình thái nút**: dạng lưới (quick actions) = icon 24–28px TRÊN + nhãn DƯỚI;
  dạng thanh (tab, action bar ngang chữ) = icon TRÁI + chữ PHẢI. Không trộn.
- E-Stop **vật lý** là chính; màn hình chỉ phản ánh trạng thái, KHÔNG thay nút cứng.
- ACK ≠ tắt còi — hai lệnh tách biệt (EEMUA 201).
- Padding 8/16/20px; vùng 4+5 grid `1fr 560px` gap 12.

**Kích thước chạm — spec theo mm, không chỉ px** (1920×1080 @24"≈92PPI; 21.5" co ~12%, đeo găng ESD):
| Loại | Tối thiểu |
|------|-----------|
| Lệnh chính (action bar) + thao tác nhanh | **≥ 64 px cao** (icon trên + nhãn dưới) |
| Nút thường | **≥ 48 px** (≈10.5mm trên 21.5") |
| Dòng bảng / danh sách chạm được | **≥ 40–44 px** |
| Nút ACK | **≥ 40 px** |
| Khoảng cách 2 vùng chạm liền kề | **≥ 8 px** |
| Nút có hậu quả vật lý (mở cửa…) | **nhấn-giữ 1 giây** + audit log |

> Mỗi máy khai `UiScale` trong machine config (24"→1.0, 21.5"→1.1) áp vào LayoutTransform Shell *(TODO)*.

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

### Palette v2 (bảng màu DUY NHẤT — không thêm màu ngoài bảng này)

| Vai trò | Hex (light, mặc định) |
|---------|----------------------|
| Nền màn hình | `#DCDCDC` |
| Panel | `#F2F2F2` / `#FAFAFA` |
| Viền | `#C8C8C8` |
| Chữ chính / phụ / mờ | `#2B2B2B` / `#6A6A6A` / `#9A9A9A` |
| OK / nền OK | `#1E7E46` / `#E2F1E8` |
| NG-lỗi / nền lỗi | `#C0392B` / `#F9E6E3` |
| Cảnh báo / nền cảnh báo | `#B26A00` / `#FBF0DC` |
| Thông tin-active / nền | `#1565C0` / `#E3EDF8` |
| Nền ảnh camera | `#1F1F1F` |

### Banner alarm — quy tắc nhiều alarm (EEMUA 201)

- Hiển thị **duy nhất alarm ưu tiên cao nhất chưa ACK** + nút ACK + chip `+N cảnh báo khác ▾`.
- ACK xong → alarm kế tiếp tự trồi lên. KHÔNG xếp chồng nhiều banner.
- Màu: **đỏ** = lỗi dừng máy · **hổ phách** = cảnh báo (máy vẫn chạy) · **xám** = sạch (banner vẫn chiếm chỗ).

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

## Connection bar — chip kết nối (dải dưới cùng, 40px)

Hai nhóm: **Thiết bị** (PLC, EtherCAT, driver, CAM, RFID…) │ **Host** (MES, SECS/GEM, OPC-UA, DB).
Danh sách theo hardware config — thiết bị không cấu hình tự ẩn. Góc phải: chuỗi phiên bản
`AM.AutoFrame v{x.y.z} · HMI build {date} · máy {serial}` (gọi support/audit).
Đây là vùng ưu tiên THẤP NHẤT — chỉ thông tin thụ động, KHÔNG đặt tín hiệu an toàn ở đây.

| Ký hiệu + màu (an toàn mù màu) | Trạng thái |
|--------------------------------|-----------|
| ● xanh `#1E7E46` | Kết nối |
| ▲ hổ phách `#B26A00` | Cảnh báo / chậm |
| ✕ đỏ `#C0392B` | Mất kết nối |
| ○ viền xám | Chưa cấu hình / tắt |

Chú giải để trong tooltip, KHÔNG chiếm chỗ thường trực. Click chip → popup chẩn đoán
(địa chỉ, thống kê truyền thông, Reconnect [Engineer]; SECS/GEM thêm comm state + control state).

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

## Bổ sung nâng cao (SEMI E95 / EEMUA 201 / định lượng)

> Chi tiết + **quyết định cái gì áp / không áp**: `docs/HMI_Advanced_Standards.md`.

**⚠️ Đọc tài liệu HMI có phê phán — luôn hỏi "viết cho cỡ màn nào?"** Phần lớn best-practice ("nút thật to",
"1 chức năng/màn", "ẩn bớt thông tin", "<30% mật độ") viết cho **panel 7–12"**. Trên **IPC 21–24" mouse+touch** thì ngược lại:
nhiều cột, hiện nhiều thông tin có tổ chức, nút theo chuẩn chạm (≥44/≥60) nhưng không cần to như panel. KHÔNG bê nguyên.

**Định lượng nên áp:**
- Tương phản chữ/nền **≥ 4.5:1**; alarm critical + thông tin an toàn **≥ 7:1**.
- **Demand vs Status:** luôn phân biệt giá trị *thực tế* với *đặt/setpoint* (lẫn 2 thứ này nguy hiểm).

**EEMUA 201 (đáng theo cho máy đơn):**
- Ưu tiên thiết kế cho **tình huống bất thường + start/stop**, không chỉ lúc bình thường.
- **Overview thường trực** + **alarm truy cập liên tục**; **không "blank-screen"** (đừng chạy ngầm chỉ báo khi lỗi).
- **Task-oriented display:** gom đủ tham số 1 tác vụ vào 1 màn (wizard calib, setup recipe).
- Animation/flashing **tiết kiệm**; mimic tối thiểu chi tiết.

**SEMI E95 — chọn lọc (KHÔNG cần bố cục 4-panel trừ khi bán cho fab bán dẫn):**
- **Salience** (viền tín hiệu): không viền = bình thường; đỏ=alarm · vàng=caution · xanh dương=đang xử lý/đang xem · xanh lá=cần chú ý.
- **Nhãn nút Title-Case** ("Home All"), KHÔNG TOÀN HOA.
- **Dialog:** nút OK/Cancel/Yes-No/Close/Apply đúng ngữ nghĩa; **disable trước** nút sẽ gây lỗi; bàn phím ảo nếu thiếu phím vật lý.
- **Alarm/nav luôn truy cập được kể cả khi mở dialog** — dialog KHÔNG che alarm bar/nav.

**Process (Siemens):** liệt kê **use case theo vai trò** + pain point đời máy trước → quyết định cái gì lên Dashboard & thứ tự nav;
sketch trước; **ngăn lỗi** (disable nút sẽ gây lỗi, xác nhận thao tác hậu quả lớn) thay vì chỉ báo lỗi; component tái dùng.

> Triển khai production trên IPC: cân nhắc **kiosk** (`WindowState=Maximized` + chặn minimize/close) để không "mất" cửa sổ —
> nhưng để dev/laptop ở chế độ resize được (đã sửa ở S32).

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
□ Tương phản chữ/nền ≥ 4.5:1 (≥ 7:1 cho alarm/an toàn); không đỏ thuần #FF0000

Định lượng & nguyên tắc hệ thống (EEMUA/SEMI E95 chọn lọc):
□ Phân biệt rõ Demand (setpoint) vs Status (thực tế)
□ Overview thường trực + alarm truy cập liên tục; không "blank-screen"
□ Dialog không che alarm bar/nav; nút dialog đúng quy ước; nhãn nút Title-Case (không TOÀN HOA)
□ KHÔNG bê khuyến nghị panel 7–12" vào IPC 24" (hỏi "viết cho cỡ màn nào?")

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

Interaction & feedback (đối chiếu RefUX-A — HMI_Advanced_Standards §7, S87):
□ Lệnh chạy >300ms → hiện trạng thái bận; xong việc có xác nhận nhìn thấy (không im lặng)
□ Nút lệnh async DISABLE khi đang chạy (chống double-fire — pattern IsBusy)
□ Mỗi nút đủ trạng thái: enabled / pressed / disabled KÈM LÝ DO (tooltip/blockReason)
□ Animation micro 150–300ms ease-out; KHÔNG animation liên tục/trang trí (trừ chỉ báo bận)
□ Thông báo tiện ích tự tắt 3–5s; lỗi cần xử lý đi đường alarm/ACK — không dùng toast
□ Nội dung async có chỗ chờ (không layout shift); chữ cắt có ellipsis + tooltip
□ Form: label luôn hiện (không chỉ watermark); lỗi validate cạnh ô sai, hiện khi rời ô
□ Số lớn có ngăn cách nghìn; ngày giờ MỘT định dạng HH:mm:ss dd/MM/yyyy toàn app
□ Danh sách/màn rỗng nói VÌ SAO rỗng + làm gì tiếp (không blank)
□ Hành động không đảo ngược: xác nhận 2 bước (pattern Override/Restore)
```
