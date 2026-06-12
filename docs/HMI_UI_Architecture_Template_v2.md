# HMI_UI_Architecture_Template — v2.0

Tài liệu chuẩn cho giao diện AM.AutoFrame trên IPC 24" 1920×1080 (cảm ứng + chuột), ngành điện tử/bán dẫn.
Dùng làm tham chiếu gốc khi sinh màn hình bằng Claude/Claude Code. Thay thế bố cục Home của v1.1;
các phần template điều khiển (ManualControlView, AxisControlView, VisionTeachView…) của v1.1 vẫn hiệu lực.

Mockup tham chiếu: `hmi_home_v2.html` (tỷ lệ thật, sub-tab bấm được).

---

## 1. Nguyên tắc nền

1. **ISA-101**: nền xám trung tính, màu CHỈ dành cho trạng thái và cảnh báo. Không nút màu rực, không trang trí.
2. **EEMUA 201**: alarm có vị trí cố định, phân ưu tiên, ACK tách biệt với tắt còi.
3. **SEMI E95-1101**: phân vùng màn hình cố định (tiêu đề / dòng alarm / vùng làm việc / vùng lệnh). Lưu ý: E95 KHÔNG quy định vị trí bộ chọn ngôn ngữ.
4. **Mô hình vận hành**: 1 kỹ thuật viên / nhiều máy — ghé máy 30 giây để cấp liệu, xử lý lỗi nhỏ. Tối ưu cho can thiệp nhanh, không phải ngồi giám sát.
5. **Phân tầng nhận thức → hành động**: trên màn hình từ trên xuống = trạng thái → nội dung → lệnh. Lệnh máy không bao giờ ở nửa trên màn hình.
6. **Một lệnh một chỗ**: không trùng lặp nút giữa các vùng (vd: Tắt còi chỉ ở Thao tác nhanh, không ở action bar).
7. **Giải thích thay vì giấu**: nút không khả dụng thì làm mờ; bấm vào hiện toast một dòng nêu lý do. Không ẩn nút.
8. **Cảm ứng — spec theo mm, không chỉ px**: thiết kế gốc 1920×1080 @ 24" (~92 PPI); cùng độ phân giải trên 21.5" (~102 PPI) mọi thứ co ~12%, và operator điện tử thường đeo găng ESD. Tối thiểu: nút thường ≥48 px (≥10.5 mm trên 21.5"), lệnh chính và thao tác nhanh ≥64 px, dòng bảng/danh sách ≥44 px, nút ACK ≥40 px, khoảng cách giữa hai vùng chạm liền kề ≥8 px. Mỗi máy khai `UiScale` trong machine config (24"→1.0, 21.5"→1.1) áp vào LayoutTransform của Shell; nút có hậu quả vật lý dùng nhấn-giữ 1 giây.
9. **Hình thái nút**: nút dạng lưới (Thao tác nhanh, GridMenuView) dùng icon 24–28 px phía trên + nhãn dưới; nút dạng thanh (tab điều hướng, action bar) dùng icon trái + chữ phải. Không áp một kiểu cho cả hai.

---

## 2. Bố cục màn Home (Level 1)

Tổng chiều cao 1080 px, chia 7 vùng cố định (Persistent Frame — vùng 1, 2, 3, 6, 7 lặp lại trên mọi màn):

| # | Vùng | Cao (px) | Nội dung |
|---|------|----------|----------|
| 1 | Header | 64 | Logo + tên máy · badge AUTO/MANUAL · badge LOCAL/REMOTE · badge trạng thái PackML · tiến độ lô · recipe · đồng hồ + heartbeat · nút ngôn ngữ · nút đăng nhập |
| 2 | Tab điều hướng | 48 | 8 tab module: Home, Vision, Motion/IO, Recipe, Dữ liệu, Alarm, Log, Cài đặt — icon + chữ |
| 3 | Banner alarm | 40 | Alarm ưu tiên cao nhất chưa ACK + nút ACK + chip "+N khác". Xám khi không có alarm |
| 4 | Vùng làm việc | 664 | Sub-tab theo máy (xem §4). Bề ngang ≈ 1310 px |
| 5 | Right rail | 664 | Rộng 560 px: KPI ca → Thao tác nhanh → Trạm & an toàn → Nhật ký |
| 6 | Action bar | 84 | Start · Pause/Resume · Stop · Reset │ Dry run · Manual — icon trên, chữ dưới |
| 7 | Thanh kết nối | 40 | Thiết bị │ Host │ phiên bản phần mềm (góc phải) |

Vùng 4 + 5 dùng grid `1fr 560px`, gap 12, padding ngang 20.

---

## 3. Đặc tả từng vùng

### 3.1 Header (64 px)

- Trái: logo 36×36 + `AM.AutoFrame · {MachineId}` (18px, đậm).
- Badge nhóm trạng thái (cao 30, chữ 14 đậm):
  - **AUTO / MANUAL / DRY** — chế độ chạy. DRY dùng màu riêng để không nhầm sản xuất thật.
  - **LOCAL / REMOTE** — quyền điều khiển (SECS/GEM control state). REMOTE = host điều khiển, các nút lệnh local khoá tương ứng. Bấm badge mở popup trạng thái GEM (communication state + control state).
  - **Trạng thái PackML** (IDLE/EXECUTE/PAUSED/STOPPED/ABORTED…) — bấm mở popup sơ đồ state machine, tô sáng trạng thái hiện tại + liệt kê điều kiện đang chặn transition.
- Giữa: tiến độ lô — `LOT-xxx · {done}/{total} · còn {n} sp · dự kiến xong {hh:mm}` (thông tin thụ động, KHÔNG đặt nút lệnh ở đây — vùng trên là vùng nhận thức, tay với lên che màn hình, và cạnh tab điều hướng dễ chạm trượt).
- Phải: `Recipe {name} · v{ver}` (bấm → tab Recipe, recipe đang nạp được chọn sẵn) · đồng hồ + **heartbeat** (chấm 9 px nhấp nháy 1 Hz theo chu kỳ cập nhật dữ liệu; ngừng nháy = UI/đường dữ liệu treo) · nút ngôn ngữ · nút đăng nhập.
- **Nút ngôn ngữ**: cao 44, icon quả địa cầu + tên đầy đủ `Tiếng Việt ▾`. Dropdown liệt kê mỗi ngôn ngữ bằng chính ngôn ngữ đó ("Tiếng Việt", "English"). Không dùng cờ quốc gia.
- **Nút đăng nhập**: cao 44, avatar chữ cái tròn + `{Tên} · {Role} ▾`. Mở dialog Identity (PIN / thẻ RFID, đổi ca, đăng xuất). Đặt góc ngoài cùng bên phải (quy ước danh tính phiên).

### 3.2 Tab điều hướng (48 px)

- Icon + chữ, KHÔNG BAO GIỜ icon-only. Icon từ `Icons.xaml` (Material Design Icons):
  Home→`HomeOutline`, Vision→`CameraOutline`, Motion/IO→`TuneVertical`, Recipe→`ClipboardTextOutline`,
  Dữ liệu→`ChartBar`, Alarm→`BellOutline`, Log→`TextBoxOutline`, Cài đặt→`CogOutline`.
- Tab active: nền xám đậm hơn + chữ đậm. Tab có sự kiện chưa xem: chấm đỏ 8 px (không tự nhảy tab).
- Module ẩn theo máy (vd máy không vision → ẩn tab Vision) qua config catalog. Tab ẩn theo role: **Motion/IO chỉ hiện với Engineer trở lên** — Operator không dùng màn này, ẩn để nav bớt nhiễu (khác với nút bị khoá: tab nguyên module không liên quan tới Operator thì ẩn hẳn, không làm mờ).
- Phân biệt Manual vs Motion/IO (không gộp): **Manual là chế độ** thao tác tay theo trạm — các thao tác đóng gói có interlock (hút, nhả, lên, xuống), vào qua nút Manual ở action bar. **Motion/IO là màn giám sát/kỹ thuật** mức trục và tín hiệu thô — DRO, servo, position table, IO sống, force output. Panel jog trong ManualControlView nhúng lại từ AxisControlView, không viết trùng.

### 3.3 Banner alarm (40 px) — quy tắc nhiều alarm

- Hiển thị **duy nhất alarm ưu tiên cao nhất chưa ACK**: `⚠ {Mã}` + thông điệp + thời gian + trạm + nút **ACK** + chip `+N cảnh báo khác ▾`.
- ACK xong → alarm ưu tiên kế tiếp tự trồi lên. Bấm chip hoặc text → tab Alarm / ErrorDetailView.
- KHÔNG xếp chồng nhiều banner. Màu theo mức: đỏ (lỗi dừng máy), hổ phách (cảnh báo, máy vẫn chạy), xám (không có alarm — banner vẫn chiếm chỗ để vị trí cố định).
- ACK ≠ tắt còi: hai lệnh tách biệt (EEMUA 201).

### 3.4 Vùng làm việc (≈1310 × 676 px) — sub-tab theo máy

Thanh sub-tab (40 px) + content region (Prism). Sub-tab khai báo qua `HomeSubViews` trong machine config, 3–5 tab, nhớ tab chọn lần cuối, badge sự kiện thay vì tự chuyển tab.

**Sub-tab chuẩn "Sản phẩm"** (mặc định mọi máy):
- Dải thumbnail vision (≈200 px): ảnh KẾT QUẢ tĩnh per cycle (vision service push 1 frame JPEG nén + overlay OK/NG qua gRPC), KHÔNG stream live. Bấm thumbnail → phóng to + nút Teach + Lưu ảnh. Live view thật chỉ ở tab Vision.
- Bảng truy vết sản phẩm (~14 dòng, cao dòng ≥40 px cho chạm): SN · Vào · Cycle · Data trạm · Trạm cuối · KQ. Chỉ dòng NG tô màu. Bấm dòng → popup chi tiết sản phẩm (toàn bộ phép đo theo trạm + ảnh vision từng camera + nút gửi lại MES).
- Cột "Data trạm" định nghĩa qua `ProductDataColumns` (mỗi loại máy hiển thị data đặc thù).

**Sub-tab tuỳ máy** (ví dụ đã thiết kế):
- `ScrewForceChart`: bản đồ vị trí vít (điểm tô màu kết quả) + đường mô-men theo góc siết, dải ngưỡng OK, đường chuẩn OK gần nhất để so sánh. Bấm điểm vít → đổi đường lực.
- `WorkPositionMap`: sơ đồ sản phẩm + trình tự vị trí làm việc, màu theo trạng thái (xong-đạt / đang làm / lỗi / chưa tới), giá trị đo hiện cạnh điểm. Toạ độ + trình tự nạp từ recipe.
- Máy không vision: `VisionLayout = None` → ẩn dải thumbnail, bảng nở thêm; sub-tab thay bằng Motion overview / WorkPositionMap.

### 3.5 Right rail (560 × 676 px)

Thứ tự từ trên xuống (gradient nhận thức → hành động: thông tin liếc ở đỉnh, nút bấm tụt xuống gần vùng lệnh — nhưng không sát đáy rail để tránh chạm nhầm khi tay tì bấm action bar):

1. **Sản xuất — ca hiện tại** — lưới 3×2: Tổng, OK, NG, Tỷ lệ OK, UPH, Cycle. Ghi rõ phạm vi (ca, giờ bắt đầu) để không nhầm với số liệu lô trên header. Đặt đỉnh rail để liếc nhanh khi đi ngang.
2. **Thao tác nhanh** — lưới 2 cột, nút ≥64 px (icon 24–28 px trên, nhãn dưới). Khai báo qua `QuickActions` (§6). Phân hai loại:
   - Tiện ích (đèn, tắt còi, thổi ion, gọi kỹ thuật/Andon): chạm thường.
   - Can thiệp vật lý (mở cửa an toàn, cửa cấp liệu): **nhấn-giữ 1 giây**, đi qua interlock, ghi audit log. "Mở cửa an toàn" là YÊU CẦU qua chuỗi dừng-an-toàn (xong bước → PAUSED → nhả khoá solenoid), nút hiển thị tiến trình: Yêu cầu → Đang dừng → Đã mở khoá. Không bao giờ bypass interlock.
3. **Trạm & an toàn** — 2 cột: trạng thái từng trạm + Cửa an toàn + E-Stop. Tín hiệu an toàn ĐI QUA `HardwareInputEventBus` (event push từ HAL, không polling). Khi EMG/cửa mở giữa chừng: máy → ABORTED, badge header đổi đỏ + banner đỏ + dòng rail đổi đỏ (ba nơi cùng đổi — hiển thị an toàn theo phân tầng, không cần phóng to thường trực).
4. **Nhật ký** — chiếm phần còn lại, mỗi entry MỘT dòng (giờ · mức · message, cắt bằng ellipsis) để tối đa số dòng nhìn thấy; bấm dòng bung toàn văn, link "xem tất cả →" sang tab Log. Không dùng entry 2 hàng.

### 3.6 Action bar (84 px)

`Start · Pause/Resume · Stop · Reset` (trái) — `Dry run · Manual` (phải). Nút 64 px cao ≥104 px rộng, **icon một màu 20–24 px phía trên + nhãn dưới** (nhận diện nhanh với găng tay và đa ngôn ngữ). Phong cách phẳng theo ISA-101 — KHÔNG bóng 3D/gradient, KHÔNG màu nền bão hòa thường trực (chỉ Start có viền nhấn), KHÔNG nút tròn (mất ~21% diện tích chạm ở góc so với chữ nhật bo).
Enable/disable bind trực tiếp `stateMachine.CanFire(trigger)` (thư viện Stateless — không tự viết state machine):

| Nút | Khả dụng từ | Hành vi |
|-----|-------------|---------|
| Start | IDLE | Pre-check (cửa đóng, EMG nhả, đã Home, recipe nạp, không alarm chưa ACK) → fail thì popup liệt kê điều kiện thiếu + nút nhảy tới chỗ xử lý. Đạt thì chạy luôn, không hỏi xác nhận |
| Pause/Resume | EXECUTE / PAUSED | Dừng ở điểm an toàn gần nhất, giữ chân không + vị trí. Nhãn tự đổi |
| Stop | EXECUTE, PAUSED | Popup 2 lựa chọn: "Dừng hết chu kỳ" / "Dừng ngay". Stop mềm ≠ E-Stop phần cứng |
| Reset | STOPPED, ABORTED, COMPLETE (+ mọi alarm đã ACK) | Về IDLE, xoá cờ lỗi, tư thế an toàn. Còn alarm chưa ACK → toast + nhảy tab Alarm |
| Dry run | toggle khi IDLE | Bỏ qua check vật liệu, vision giả lập (SimulatedXxx), badge header → DRY |
| Manual | không ở EXECUTE, role ≥ Engineer | Overlay ManualControlView toàn màn; thoát bắt buộc qua Reset |

### 3.7 Thanh kết nối (40 px)

- Hai nhóm: **Thiết bị** (PLC, EtherCAT, driver, CAM, RFID…) │ **Host** (MES, SECS/GEM, OPC-UA, DB). Danh sách theo `hardware.config.json`, thiết bị không cấu hình tự ẩn.
- Ký hiệu hình + màu (an toàn mù màu): ● kết nối · ▲ cảnh báo · ✕ mất · ○ tắt. Chú giải để trong tài liệu/tooltip, không chiếm chỗ thường trực.
- Bấm chip → popup chẩn đoán: trạng thái, địa chỉ, thống kê truyền thông, nút Reconnect (Engineer) + Test. SECS/GEM thêm communication state + control state.
- Góc phải: `AM.AutoFrame v{x.y.z} · HMI build {date} · máy {serial}` — phiên bản thường trực để gọi support/audit; chi tiết đầy đủ ở Cài đặt → Giới thiệu.
- Thanh này là vùng ưu tiên thấp nhất: chỉ thông tin thụ động. KHÔNG đặt tín hiệu an toàn ở đây.

---

## 4. Quy tắc trạng thái toàn cục

1. **Cold start / empty state**: mỗi vùng phải định nghĩa hiển thị khi chưa có dữ liệu. Bảng sản phẩm trống → "Nạp recipe để bắt đầu →" + nút hành động; thumbnail → "Chưa kết nối camera"; KPI → gạch ngang. Empty state luôn kèm chỉ dẫn hành động kế tiếp.
2. **Heartbeat**: mọi màn hình kế thừa heartbeat từ Shell. Mất cập nhật > 3 s → chấm chuyển hổ phách; > 10 s → overlay cảnh báo "Mất cập nhật dữ liệu".
3. **REMOTE**: khi control state = Online-Remote, các nút lệnh local bị khoá (mờ + toast "Máy đang do host điều khiển"), trừ Stop và thao tác an toàn.
4. **Role**: Operator (vận hành, ACK, quick actions tiện ích) < Engineer (teach, manual, recipe, reconnect, quick actions can thiệp) < Admin (force IO, hardware config, user). Nút thiếu quyền: mờ + toast nêu role yêu cầu.
5. **Billboard mode (tuỳ chọn)**: không tương tác sau N phút → overlay phóng to 3 thông tin (trạng thái, còn bao nhiêu sp, dự kiến xong) cỡ chữ 60–80 px cho người đi ngang 2–3 m; chạm bất kỳ để về giao diện đầy đủ.

---

## 5. Màu, chữ, icon

| Vai trò | Light (mặc định) |
|---------|------------------|
| Nền màn hình | #DCDCDC |
| Panel | #F2F2F2 / #FAFAFA |
| Viền | #C8C8C8 |
| Chữ chính / phụ / mờ | #2B2B2B / #6A6A6A / #9A9A9A |
| OK / nền OK | #1E7E46 / #E2F1E8 |
| NG-lỗi / nền lỗi | #C0392B / #F9E6E3 |
| Cảnh báo | #B26A00 / #FBF0DC |
| Thông tin-active | #1565C0 / #E3EDF8 |
| Nền ảnh camera | #1F1F1F |

- Chữ: Segoe UI. Nhỏ nhất 13 px (chú thích), nội dung 14–15 px, giá trị KPI 16–18 px, badge trạng thái 14 px đậm.
- Icon — hai lớp, đều MỘT MÀU (fill theo `Foreground`, không icon nhiều màu):
  - **Icon UI** (điều hướng, hành động phần mềm): Material Design Icons / Pictogrammers (Apache 2.0) — một bộ duy nhất, không trộn bộ khác. KHÔNG dùng Apple SF Symbols (giấy phép chỉ cho nền tảng Apple, cấm dùng trên Windows).
  - **Ký hiệu thiết bị vật lý** (ionizer, chân không, tower lamp, tiếp đất, ESD…): lấy theo IEC 60417 / ISO 7000 dưới dạng PathGeometry — operator đã quen các ký hiệu này từ tem nhãn trên máy.
  - Tất cả tập trung trong `Icons.xaml`; emoji chỉ dùng trong mockup, không vào sản phẩm.
- Màu chỉ mang nghĩa trạng thái. Một bảng 15 dòng xám với 1 dòng đỏ — đó là chuẩn.

---

## 6. Config schemas (machine config / config catalog)

```json
{
  "MachineId": "AM-SCR-02",
  "VisionLayout": "Dual",            // None | Single | Dual | Quad | SingleLive
  "HomeSubViews": [
    "ProductTracking",                // bắt buộc, mặc định
    "ScrewForceChart",
    "WorkPositionMap"
  ],
  "ProductDataColumns": [
    { "key": "ScrewSummary", "header": "Data trạm", "format": "{okCount}/{total} vít · max {maxTorque} N·m" }
  ],
  "QuickActions": [
    { "id": "WorkLight",  "icon": "LightbulbOutline", "type": "Toggle",
      "halCommand": "IO.WorkLight", "roles": ["Operator+"] },
    { "id": "BuzzerOff",  "icon": "BellOffOutline",   "type": "Momentary",
      "halCommand": "IO.BuzzerOff", "roles": ["Operator+"] },
    { "id": "SafetyDoor", "icon": "LockOpenOutline",  "type": "HoldToConfirm",
      "halCommand": "Safety.RequestDoorUnlock",
      "interlock": "StateIn(IDLE,PAUSED,STOPPED)", "audit": true, "roles": ["Operator+"] },
    { "id": "FeedDoor",   "icon": "PackageVariant",   "type": "HoldToConfirm",
      "halCommand": "IO.FeedDoorUnlock",
      "interlock": "StationIdle(Loader)", "audit": true, "roles": ["Operator+"] }
  ],
  "ConnectionBar": {
    "devices": ["PLC", "EtherCAT", "ScrewDriver1", "ScrewDriver2", "Cam1", "Cam2"],
    "hosts":   ["MES", "OPC-UA", "DB"]
  }
}
```

Quy tắc: Shell và Home KHÔNG chứa code riêng theo máy — mọi khác biệt đi qua schema này.
Nút `interlock` không đạt → mờ + toast lý do. `audit: true` → ghi user, thời gian, kết quả vào audit log.

---

## 7. Tóm tắt chức năng tab (màn Level 2 — chi tiết ở tài liệu v1.1)

| Tab | Nội dung chính | Role |
|-----|----------------|------|
| Vision | Live view, VisionTeachView (ROI, model, hiệu chuẩn px→mm, test grab, lịch sử offset) | Xem: Operator · Sửa: Engineer |
| Motion/IO | AxisControlView (jog 3 cấp + inching, home, servo, position table) + SensorVacuumMonitor (force output: Admin, ngoài EXECUTE, audit) | Engineer |
| Recipe | Danh sách JSON, nạp/lưu/sao chép/so sánh, editor min-max | Engineer |
| Dữ liệu | Sản lượng ca/lô, Pareto NG, trend UPH/cycle, xuất CSV | Operator |
| Alarm | Active + History, ACK, ErrorDetailView (nguyên nhân, bước xử lý, snapshot IO, retry) | Operator |
| Log | Bảng đầy đủ, lọc mức/trạm/thời gian, tìm kiếm, xuất | Operator |
| Cài đặt | GridMenuView: hardware config, hiệu chuẩn, user, host (GEM/MES/OPC-UA), backup, Giới thiệu (phiên bản đầy đủ) | Engineer/Admin |

---

## 8. Checklist sinh màn hình mới (dùng với Claude/Claude Code)

1. Màn thuộc Level nào (1 overview / 2 thao tác nhóm / 3 chi tiết / 4 chẩn đoán)? Kế thừa Persistent Frame (vùng 1,2,3,6,7).
2. Vùng nội dung dùng template nào của v1.1 (ManualControlView, AxisControlView…)? Nếu mới → định nghĩa empty state + role + interlock trước khi vẽ.
3. Mọi nút: khai precondition theo state machine, role, target view, audit hay không.
4. Không thêm màu mới ngoài bảng §5. Không icon-only. Vùng chạm ≥44 px.
5. Chuỗi hiển thị qua `ILocalizationService` (vi/en), không hardcode.
6. Dữ liệu an toàn và trạng thái: event push qua `HardwareInputEventBus`, không polling.

---

*Lịch sử: v2.0 (06/2026) — bố cục Home work-area + right rail, sub-tab theo máy, quick actions, quy tắc multi-alarm, LOCAL/REMOTE, heartbeat, billboard mode. v1.1 — template điều khiển, ISA-101 cơ sở, localization, GridMenuView.*


---

## 9. Quyết định adoption — AM.AutoFrame (Session 45, 12/06/2026)

> Mục này do team AM.AutoFrame thêm sau khi phản biện spec gốc. Spec gốc giữ nguyên ở trên.

### Áp ngay (đã code)
- Bố cục 7 vùng Persistent Frame (§2) — Shell `MainWindow.xaml`.
- Palette §5 — thay toàn bộ value token trong `App.xaml` (GIỮ TÊN token cũ để module không phải sửa).
- Action bar trắng phẳng, icon trên + nhãn dưới, Start chỉ viền xanh; Pause/Resume một nút tự đổi nhãn.
- Banner alarm 1 dòng: chỉ alarm ưu tiên cao nhất CHƯA ACK + nút ACK (40px — spec §1.8 thắng mockup 32px) + chip "+N khác"; xám khi sạch.
- Right rail 560px: KPI ca → Thao tác nhanh → Trạm & an toàn → Nhật ký 1 dòng/entry.
- Connection bar 2 nhóm Thiết bị│Host + chuỗi phiên bản; chú giải ●▲✕○ để tooltip (theo §3.7, mockup cũ có legend thường trực — bỏ).
- "Một lệnh một chỗ", "mờ + nêu lý do (tooltip), không ẩn nút".

### Map sang cái đã có (KHÔNG đổi core)
| Spec v2 | AM.AutoFrame |
|---------|--------------|
| PackML IDLE/EXECUTE/PAUSED/STOPPED/ABORTED | ISA-88 8 trạng thái hiện có (`MachineState`) — badge hiển thị nhãn i18n `State.*`. Đổi sang PackML là việc tầng máy, làm riêng nếu cần |
| Thư viện Stateless `CanFire(trigger)` | `BaseMasterController` hiện có (55 tests) + `CanExecute` của RelayCommand — tương đương chức năng |
| Material Design Icons | **Segoe MDL2 Assets** (sẵn trên Windows, một màu, đang dùng toàn codebase) — không thêm package |
| Vision push gRPC 1 frame/cycle | Chưa có vision service → tile camera hiển thị tên + trạng thái kết nối (empty-state đúng §4.1) |

### Hoãn có chủ đích (TODO — cần hạ tầng riêng, không làm nửa vời)
- LOCAL/REMOTE thật + popup GEM (module SecsGem chưa build — badge LOCAL tĩnh).
- Tiến độ lô trên header (chưa có MES/lot service → ẩn theo cold-start §4.1).
- QuickActions `HoldToConfirm` + audit log + `HardwareInputEventBus` (mới có: Momentary "Tắt còi" qua `ILightController`; còn lại disabled + lý do).
- Stop popup 2 lựa chọn · Start pre-check popup · Manual overlay (`ManualControlView`) · Billboard mode · `UiScale` theo machine config · heartbeat đổi màu khi mất cập nhật >3s.

### Phản biện spec gốc (điểm cần sửa khi ra v2.1)
1. Mockup vẽ nút ACK 32px, spec §1.8 yêu cầu ≥40px — mockup phải theo spec.
2. Mockup có chú giải ●▲✕○ thường trực ở conn bar, §3.7 nói để tooltip — thống nhất một đằng.
3. §3.6 bỏ quy tắc "nút Stop cách nhóm thường ≥48px" của v1 mà không nêu lý do. Chấp nhận được vì Stop là soft-stop có popup xác nhận, nhưng cần ghi rõ trade-off này vào spec.
4. Emoji trong mockup dễ bị copy thẳng vào code — spec nên kèm bảng map emoji→icon chuẩn ngay trong mockup.
