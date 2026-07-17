# CHANGELOG — AM.AutoFrame
> Ghi lại mọi thay đổi có ý nghĩa theo từng session làm việc.
> Format: `## [Session N] YYYY-MM-DD — Tiêu đề ngắn`

---

## [Session 94] 2026-07-17 — Vận hành tay: gộp Bảng điểm vào pane Điều khiển trục → "Trục & Điểm"

**Commit:** `bbdce33`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chủ dự án: "để bảng điểm và điều khiển trục không cùng 1 trang khó chọn khi muốn trục
di chuyển đến điểm đã cài" — rà toàn bộ màn Vận hành tay và đưa hướng sửa.

### 🔍 Đánh giá
- 6 sub-tab hiện tại: Điều khiển trục · Bảng điểm · Giám sát I/O · Thao tác trạm · Override · Hiệu chỉnh.
  4 tab sau hợp lý; 2 tab đầu tách nhau làm hỏng luồng teach kinh điển
  (jog → teach → jog tinh chỉnh → teach lại → thử "Tới") — mỗi vòng phải chuyển tab 2 lần.
- Phát hiện kèm: `StatusMessage` (lý do guard từ chối) chỉ được hiển thị ở pane Bảng điểm —
  jog/move bị chặn ở pane Điều khiển trục KHÔNG thấy lý do (vi phạm RefUX-A "không im lặng").

### ✅ Phương án (chốt qua AskUserQuestion — A)
| PA | Nội dung | Kết luận |
|---|---|---|
| **A. Gộp hẳn 1 tab (CHỌN)** | Bảng điểm chuyển nguyên khối xuống DƯỚI khu điều khiển trục, full-width, cùng ScrollViewer | Jog → Teach → Tới trọn vòng MỘT màn, đúng mẫu màn teach công nghiệp |
| B. Giữ 2 tab + khối "Điểm nhanh" ở cột jog | Ít xáo trộn nhưng chức năng điểm tồn tại 2 nơi, lâu dài lệch nhau | bác |
| C. Hai cột "Trục \| Điểm" trong 1 tab | Nhìn đồng thời không cuộn nhưng cả hai khối bị bóp ngang, jog phải dời chỗ | bác |

### ✅ Thực hiện
- `MotionView.xaml`: khối Card bảng điểm (bảng X/Y/Z/U + chọn 2 chạm + thanh Tới/Teach/Lưu + hint +
  StatusMessage) chuyển từ PANE 1 vào cuối PANE 0; bỏ RadioButton "Bảng điểm" + PANE 1.
  **Không đánh lại số sub-tab** (index 1 bỏ trống, 2..5 giữ nguyên — tránh sửa hàng loạt
  ConverterParameter/CommandParameter dễ sót).
- Nhãn tab đầu: "Điều khiển trục" → **"Trục & Điểm"** (Axes & Points / 轴与点位); xoá key
  `Manual.Tab.Points` ×3; VM chỉ sửa doc-comment `SubTabIndex` — logic (GoToSelection/Teach/Save,
  chọn 2 chạm, guard R2/R3) GIỮ NGUYÊN.
- StatusMessage giờ nằm trên pane thao tác → jog bị guard chặn thấy lý do ngay.

### 🧪 Kiểm chứng
- Build 0 warning; logic VM không đổi → không cần test mới (339 pass giữ nguyên).
- **Smoke UIA với DỮ LIỆU** (tạo `bin/points.json` 3 điểm Home/PickUp/Place — máy demo trước đó
  KHÔNG có file điểm nên bảng luôn trống, bài học S89 test-với-dữ-liệu): login engineer → Manual →
  tab đầu "Trục & Điểm", sub-tab "Bảng điểm" biến mất, bảng 3 điểm hiện NGAY dưới khu điều khiển
  trục, chạm "Home" → thanh chọn "Đang chọn: … Home" + bộ nút Tới/Teach/Lưu hiện; app sống.
- Bẫy UIA ghi thêm: file .ps1 bị Edit sau khi đã thêm BOM sẽ MẤT BOM → chuỗi tiếng Việt/ký tự "→"
  trong script hoá mojibake, match fail im lặng — luôn re-save BOM trước khi chạy.

---

## [Session 93] 2026-07-16 — Trang "Thông số máy" · toàn vẹn cấu hình SHA-256 (không gộp file) · layout Người dùng · fix appsettings không vào bin

**Commit:** `4073c4c`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** 3 yêu cầu của chủ dự án: (1) app chưa có trang cấu hình thông số máy (tên máy/line/IP…);
(2) nhiều file config rời rạc — có nên gộp 1–2 file + SHA-256 kiểm tra file bị chỉnh sửa; (3) màn
Người dùng & phân quyền căn chỉnh chưa hợp lý.

### ✅ (1) Thẻ "Thông số máy" trong Cài đặt (Administrator + audit)
- `MachineConfigView(Model)` mới — 3 khối:
  - **Nhận diện máy**: tên máy · line · vị trí → ghi `machine.json` qua JsonNode (GIỮ NGUYÊN `stations`);
    mã máy (day-code) chỉ hiển thị (đổi trong appsettings vì gắn break-glass).
  - **Kết nối thiết bị**: UseSimulation + host/cổng Modbus · PLC · Robot · Scanner · ADAM + OPC-UA
    endpoint + EtherNet/IP host → ghi thẳng `appsettings.json` (chỉ set key màn này quản, phần khác
    giữ nguyên; ô trống = không ghi); validate cổng 1–65535.
  - **Toàn vẹn file cấu hình** (xem mục 2).
- Lưu = audit `Machine.SaveConfig` (kèm giá trị) + **tự ký lại manifest** + banner vàng
  "KHỞI ĐỘNG LẠI để áp dụng" (DI đọc config lúc boot — trung thực về giới hạn).
- Dưới quyền Administrator: form disable + giải thích (mẫu quen mọi màn gate).

### ✅ (2) Câu hỏi "gộp config?": KHÔNG gộp — thay bằng manifest SHA-256 (design-notes/0014)
- Phân tích 3 phương án trong 0014. Gộp 1–2 file bị BÁC vì file khác nhau về **vòng đời/người ghi**:
  nhóm app-tự-ghi (points/parameters/users/recipes — ghi liên tục) trộn với nhóm chỉnh-khi-deploy
  (machine/axismap/io.map/analog.map/appsettings) → app save đè tay sửa, hỏng 1 file mất tất,
  và hash toàn cục đổi liên tục làm kiểm toàn vẹn vô nghĩa.
- **`IConfigIntegrityService`/`ConfigIntegrityService`**: SHA-256 từng file nhóm cấu-hình-máy (7 file)
  vào `config.manifest.json` (kèm SignedBy/SignedAt); **boot đối chiếu → Modified/Missing = alarm
  40013** liệt kê đúng file (phát hiện + ồn ào, KHÔNG chặn máy chạy — chính sách 0012); manifest
  hỏng = coi như chưa ký (app vẫn chạy). Bảng trạng thái Khớp/ĐÃ SỬA/MẤT FILE/Chưa ký + nút
  **"Ký lại (chấp nhận thay đổi)"** (Administrator, audit `Config.Resign`).
- Giới hạn ghi thẳng vào doc + XML doc: manifest thường là **tamper-evident với thao tác thường**,
  không chống được kẻ sửa cả manifest — cần chống giả mạo thật sự thì HMAC bằng DayCodeSecret (P5).
- Backup targets + `config.manifest.json`; alarm 40013 catalog ×3.

### ✅ (3) Layout "Người dùng & phân quyền"
- **Bug chính**: `ListBoxItem` mặc định `HorizontalContentAlignment=Left` → cột `*` trong template
  KHÔNG giãn → combo Quyền/nút mỗi dòng co theo độ dài tên user, dạt trái lệch nhau. Sửa
  `HorizontalContentAlignment=Stretch` + padding dòng 12,6.
- **Header cột** (Tài khoản · Quyền) thẳng hàng cột template (150/88/88); tiêu đề khối
  "Thêm tài khoản" và "Đặt lại mật khẩu cho: <user>"; khối reset khi CHƯA chọn tài khoản hiện
  hint "Chọn một tài khoản trước" thay vì form câm; nút Thêm màu primary; spacing 12 thống nhất;
  MaxWidth 820→900.

### 🐛 (4) Phát hiện nhân tiện — appsettings.json KHÔNG vào bin từ trước tới nay
- Shell csproj thiếu `CopyToOutputDirectory` cho `appsettings.json`; App nạp config
  `optional: true` từ `AppContext.BaseDirectory` → **app luôn chạy giá trị DEFAULT trong code,
  appsettings của repo không có tác dụng** (không ai phát hiện vì default trùng config demo).
  Đã thêm copy — từ giờ sửa config mới thật sự ăn; đây cũng là điều kiện để trang Thông số máy
  ghi appsettings có ý nghĩa.

### 🧪 Kiểm chứng
- **+7 test → 339 pass toàn repo** (`ConfigIntegrityServiceTests`: chưa ký/ký-rồi-khớp/sửa file/
  mất file/manifest hỏng/boot-alarm-40013/boot-sạch-không-alarm). Số 339 là đếm lại chuẩn sau khi
  phát hiện dll test trong bin stale làm S91/S92 báo thiếu (bẫy bin dùng chung, lần thứ 5).
- **Smoke UIA end-to-end**: mở thẻ Thông số máy → 7 file "Chưa ký" → form nạp đúng tên máy + IP
  (sau fix appsettings) → sửa tên + line → Lưu → status + banner restart + **7 file "Khớp"** +
  `config.manifest.json` sinh ra + audit `Machine.SaveConfig`+`Config.Resign` + machine.json
  GIỮ stations; màn Người dùng đủ 3/3 (khối Thêm tài khoản · header Tài khoản · hint chọn);
  **tamper `io.map.json` ngoài app → khởi động lại → alarm 40013 nêu đúng file trên banner**.
- Bẫy UIA mới ghi nhận: cửa sổ khác đè lên app làm `GetClickablePoint` ném NoClickablePoint —
  dùng `SetForegroundWindow` + click tâm `BoundingRectangle` thay vì GetClickablePoint.
- i18n +26 key ×3 (Machine.* + UserAdmin.AddSection/ColAccount).

---

## [Session 92] 2026-07-14 — Gói D phanh Z · Sản xuất chi tiết SP · fix quyền không cập nhật · nav gọn 7 tab

**Commit:** `e27ba92`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** 4 yêu cầu của chủ dự án: (1) thực hiện Gói D phanh Z (hybrid đã chốt S90); (2) tab Sản xuất
chỉ có KPI cơ bản trùng Home, thiếu chi tiết từng sản phẩm; (3) đăng nhập thấp → đăng xuất → đăng nhập
admin mà các màn gate vẫn đòi quyền cao hơn; (4) nav quá dài sau khi thêm Analog + 2 nút Recipe trùng +
Manual/Vận hành tay trùng đường vào — "lên phương án phù hợp rồi tự chỉnh sửa".

### 🐛 (1) Quyền không cập nhật sau đổi user — sửa 2 lớp
- **Gốc rễ**: `AuditViewModel`/`BackupViewModel` đăng ký `UserChanged += (_,_) => RefreshGate()` mà
  RefreshGate đụng `ObservableCollection` (Rows/Backups Clear+Add). `UserChanged` bắn trên **thread nền**
  (LoginAsync Task.Run) → cross-thread exception → **các subscriber SAU trong invocation list không
  chạy nữa** (BuildNavigation/gate các màn khác đứng im) → "tab vẫn yêu cầu quyền cao hơn".
- Sửa: (a) 2 VM marshal `RunOnUIThread` qua SynchronizationContext (mẫu UserAdminViewModel đã đúng);
  (b) phòng thủ tận gốc — `UserService.RaiseUserChanged` gọi **từng subscriber cô lập** try/catch + log
  tên VM lỗi: về sau ai quên marshal cũng chỉ hỏng đúng màn đó, không lây cả app.

### ✅ (2) Nav gọn: 9→7 tab, hết trùng đường vào (phương án chốt + lý do)
- **2 nút Recipe** → chip Recipe trên header thành **hiển thị thuần** (Border, tooltip "quản lý trong tab
  Recipe") — header là danh tính phiên (ISA-101), tab nav là đường vào duy nhất.
- **Manual vs tab Vận hành tay** → bỏ tab nav (`MotionView` hết `[ModuleNavigation]`); nút **Manual**
  action bar là cửa vào duy nhất — đứng cùng mạch nút chế độ (Dry run · Từng bước · Manual), đã enable
  theo quyền + tooltip; MainWindow thêm `ShowStandaloneView` (cache view + bỏ chọn mọi tab nav; đổi user
  khi đang đứng màn này → nav rebuild tự về Home — cũng là hành vi an toàn).
- **Tab Nhật ký** → thẻ "Nhật ký" trong Cài đặt (`LoggingView` nhúng như Chẩn đoán/Audit — chất bảo trì,
  không phải màn vận hành hằng ngày). Nav còn: Bảng điều khiển · Sản xuất · Vision · Cảnh báo · Analog ·
  Recipe · Cài đặt cho MỌI role.

### ✅ (3) Tab Sản xuất — sub-tab "Chi tiết sản phẩm"
- Sub-tab **Tổng quan** (KPI + trend giờ, như cũ) / **Chi tiết sản phẩm** (mới): bảng từng SP trong cửa
  sổ thời gian đã chọn — thời gian · SN · recipe · **KQ màu OK/NG** · cycle · điểm vision · lý do NG ·
  người vận hành (virtualized, 500 dòng mới nhất, mọi cột OneWay — bài học S89).
- Lọc **SN contains + kết quả OK/NG** — lọc client-side trên record đã nạp (đổi filter không truy vấn DB);
  dòng đếm "Hiển thị x/y sản phẩm".
- Panel **Pareto NG theo lý do** (top 10, đếm + % + bar — tính trên toàn kết quả lọc SN, không chỉ 500
  dòng hiển thị) — cùng câu trả lời "lỗi nào hay rớt" như Pareto alarm S90.

### ✅ (4) Gói D — phanh trục Z (design-notes/0013)
- `IAxisBrake` (Abstractions/Hardware) — capability tuỳ chọn như `IAxisJog`: `SetBrakeReleasedAsync` /
  `IsBrakeReleased` / `ReleasedBrakes`; controller không implement → UI ẩn hẳn khối phanh.
  `SimulatedMotionController` implement (per-axis, thread-safe).
- UI trong pane Điều khiển trục (Vận hành tay): **nhả = 2 bước** — bước 1 guard R2 (Engineer + máy dừng,
  từ chối có lý do + audit DENIED) → bước 2 cảnh báo đỏ "trục Z có thể RƠI TỰ DO" + Xác nhận/Hủy;
  **đang nhả** = dải đỏ trong màn + **alarm 10009 banner đỏ toàn app** (mẫu forced-IO);
  **đóng = 1 chạm không cần quyền** (về trạng thái an toàn luôn được phép).
- **Bất biến an toàn**: rời màn Vận hành tay (Unloaded) / đổi user / rớt dưới Engineer → phanh **TỰ ĐÓNG**
  + clear alarm; audit `Brake.Release Z` / `Brake.Engage Z` kèm lý do ("tự đóng: rời màn Vận hành tay"…).
- Phương án bị bác đã lưu 0013: giữ-để-nhả (chỉnh Z cần cả 2 tay) và SuperUser (người chỉnh là Engineer).
- Alarm 10009 catalog ×3; `ZAxisIndex=2` demo — máy thật đưa vào machine.json (P5).

### 🧪 Kiểm chứng
- **+3 test → 320 pass toàn repo** (`SimAxisBrakeTests`: per-axis + idempotent + validate axis).
- **Smoke UIA**: nav đúng 7/7 tab, không còn tab Vận hành tay/Nhật ký; login engineer → **Manual** mở màn;
  **nhả phanh 2 bước** → banner đỏ trong màn + alarm toàn app; **về Home → mở lại: phanh ĐÃ TỰ ĐÓNG**;
  audit JSONL ghi cả Release lẫn Engage kèm lý do; Sản xuất sub-tab chi tiết + lọc + Pareto render
  (0/0 khi chưa có record — empty state); thẻ Nhật ký trong Cài đặt mở được; app sống toàn trình.
- i18n +31 key ×3. Lưu ý UIA: nút action bar (content StackPanel) tên UIA rỗng — phải click chuột thật
  vào label; mở màn nặng lần đầu cần chờ + retry.

---

## [Session 91] 2026-07-14 — Gói C: Giám sát analog — ngưỡng 4 mức + time van theo RECIPE, alarm khoảng an toàn, module UI mới

**Commit:** `a42c64e`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Thực hiện **Gói C** đã chốt S90 (đề xuất số 1+2 của chủ dự án, tham khảo màn giám sát khí nén
công nghiệp trong ảnh): quản lý kênh analog (áp suất/chân không/nhiệt độ/lưu lượng) với 4 mức ngưỡng
Lv Pick Up / On Check / Blow Off / Off Check + 3 thời gian trễ van/xilanh (On/Off/Blow ms), **lưu THEO RECIPE**
(chốt AskUserQuestion: đổi sản phẩm là đổi bộ ngưỡng — không phải setting máy).

### ✅ Model + Recipe (`AM.Core`)
- `Models/AnalogModels.cs` **mới**: `AnalogChannelConfig` — kênh máy khai trong **`analog.map.json` cạnh exe**
  (Id ổn định làm khoá ngưỡng, Name/Unit hiển thị, AiChannel vật lý, scale tuyến tính RawMin..RawMax →
  EngMin..EngMax — thang ÂM cho chân không hoạt động đúng, `SafeMin/SafeMax` tuỳ chọn cho alarm);
  `AnalogLimits` — 4 mức Lv* + OnTimeMs/OffTimeMs/BlowTimeMs (ngữ nghĩa màn khí nén điển hình).
- `RecipeBase.AnalogLimits` **mới**: `Dictionary<string, AnalogLimits>` (OrdinalIgnoreCase) — station khi chạy
  sequence đọc `recipe.AnalogLimits["VAC_PP1"]`; kênh chưa có ngưỡng → mặc định 0.
- `AlarmCodes.IoAnalogOutOfRange = 30006` + catalog 3 ngữ.

### ✅ Service giám sát (`AM.Core.Abstractions` + `AM.Services`)
- `IAnalogMonitorService` / `AnalogMonitorService`: nạp map **tolerant** (không có file = máy không có analog —
  hợp lệ, không poll; file hỏng = log lỗi + bỏ qua, app vẫn chạy); poll **200ms** PeriodicTimer; một kênh đọc
  lỗi → giá trị kênh đó null nhưng KHÔNG giết vòng poll (kênh khác vẫn sống).
- **Khoảng an toàn CHỈ xét khi máy Running** (máy đứng thì vacuum về 0 là bình thường — không alarm rác);
  vượt liên tục **5 mẫu (1s) mới alarm** (debounce nhiễu); alarm **một lần cho mỗi đợt vượt**, re-arm khi
  giá trị về trong khoảng; raise fire-and-forget, alarm lỗi không phá vòng poll.
- `Scale(cfg, raw)` public static — tool/test dùng chung; span 0 → EngMin (không chia 0).

### ✅ Module UI mới `AM.Modules.Analog` (project #30)
- Tab **"Analog"** (`[ModuleNavigation("Nav.Analog", icon "io", order 30)]` — sau Cảnh báo, ai cũng xem được).
- Trái: lưới **card kênh** — giá trị live F1 + đơn vị, bar vị trí trong thang Eng, dòng tóm tắt ngưỡng
  "↑x · on y · blow z · ↓w"; mất tín hiệu → "—" xám. Poll UI 250ms (DispatcherTimer) từ service.
- Phải: panel kênh đang chọn — 7 ô nhập **label luôn hiện** (RefUX-A §7), nút **"Ghi vào recipe"**:
  gate **Engineer+** (dưới quyền: ô disable + hint lý do thay vì nút chết câm), chưa có recipe active →
  báo "nạp recipe trước", parse số Invariant→CurrentCulture, ghi `recipe.AnalogLimits[id]` →
  `SaveRecipeAsync` + **audit `Analog.WriteLimits.{id}`** kèm toàn bộ giá trị; đổi recipe (RecipeChanged) →
  panel tự nạp lại ngưỡng của recipe mới.
- Máy không có kênh analog → empty state có lối đi (hướng dẫn khai analog.map.json). Mọi binding hiển thị
  Mode=OneWay (bài học S89).
- i18n +18 key ×3 (417 chuỗi/ngữ).

### ✅ Sim + demo + backup
- `SimulatedIoModule`: AI 16 kênh có **giá trị nền 2–8V random lúc Connect + random-walk ±0.03V mỗi lần đọc**
  (trước là random 0–10V mỗi lần — số nhảy loạn không nhìn được); helper `SetAnalogInput` cho test/demo.
- `analog.map.json` demo 4 kênh: VAC_PP1/VAC_PP2 (0..-100 kPa, không safe-range) · PRESS_MAIN
  (0..1000 kPa, safe 150–900 — nền sim 200–800 nên không alarm rác) · TEMP_HEAD (0..100°C, safe ≤95).
- `BackupService.DefaultTargets` + `analog.map.json`; DI: service ở `AddCoreServices`, VM ở `AddUiViewModels`,
  `Start()` trong `App.OnStartup`; Shell csproj reference + copy map; `dotnet sln add`.

### 🧪 Kiểm chứng
- **+12 test case → 317 pass toàn repo** (`AnalogMonitorServiceTests`: scale thang âm/span 0, map thiếu/hỏng/
  comment + Id rỗng bị bỏ, poll ra đúng giá trị scale, **debounce 1s + chỉ khi Running + alarm đúng MỘT lần**,
  kênh lỗi cô lập kênh lành).
- **Smoke UIA end-to-end**: boot → tab Analog hiện 4 card giá trị live đúng scale (vd −55.1 kPa / 610.9 kPa) →
  login engineer → **nạp recipe Default** (phát hiện đúng thiết kế: chưa nạp recipe thì ghi ngưỡng báo
  "nạp recipe trước") → chọn Vacuum PP1 → nhập 7 giá trị → status "**Đã ghi ngưỡng Vacuum PP1 vào recipe
  Default.**" + card cập nhật "↑-60 · on -55 · blow -5 · ↓-10" + **audit JSONL** đúng actor engineer + detail.
- 2 lần S125 quen thuộc khi build (comment kết thúc bằng `;` / chứa `Start()`) — sửa lời comment.

---

## [Session 90] 2026-07-13 — Lịch sử cảnh báo + Pareto tần suất lỗi · fix danh sách user trống + label form + audit quản trị user

**Commit:** `31a8d47`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chủ dự án rà UI thực tế và nêu 5 đề xuất (kèm ảnh màn giám sát khí nén tham khảo). Chốt qua
AskUserQuestion: đợt này làm **Gói A (User+Audit) + Gói B (Lịch sử cảnh báo)**; Gói C (giám sát analog
áp suất/khí âm/nhiệt/lưu lượng — ngưỡng THEO RECIPE + time settings van/xilanh) và Gói D (phanh trục Z)
để đợt sau. Riêng phanh Z: chủ dự án phản biện cả 2 phương án ban đầu → chốt hướng lai khi làm:
**toggle + confirm 2 bước ở mức Engineer** + banner đỏ thường trực khi phanh đang nhả + tự đóng khi
rời màn/đăng xuất (giữ-để-nhả bất tiện vì chỉnh Z bằng tay cần cả hai tay; SuperUser quá cao).

### 🐛 Gói A — BUG danh sách user TRỐNG + UX + audit
- **Bug thật chủ dự án phát hiện qua ảnh**: `UserAdminView.xaml` ListBox **thiếu hẳn `ItemsSource="{Binding Users}"`**
  từ ngày viết màn — VM nạp đủ user nhưng UI không bind → danh sách luôn trắng, không biết máy có những ai. Đã thêm.
- Label LUÔN HIỆN trên mỗi ô nhập (Tên đăng nhập · Mật khẩu · Quyền · Mật khẩu mới) — trước chỉ có tooltip,
  vi phạm chính quy tắc RefUX-A §7 đã áp S87.
- **Audit thao tác quản trị user**: `User.Create` / `User.Delete` / `User.SetLevel` / `User.ResetPassword`
  giờ ghi `IAuditService` kèm NGƯỜI THỰC HIỆN — hiện trong màn Nhật ký audit (P3.2) → "ai đó mượn phiên admin
  thêm user" là truy được ai-làm-lúc-nào (auto-logout 15' đã chặn bớt từ P3.2).

### ✅ Gói B — Tab Lịch sử cảnh báo + Pareto (dữ liệu DB có sẵn từ P0, chỉ thiếu UI)
- Màn Cảnh báo thêm 2 sub-tab **"Đang active" / "Lịch sử"**. Xoá alarm active KHÔNG mất lịch sử —
  AlarmHistory đã persist mọi alarm từ lúc raise (P0.2, retention 365 ngày).
- Tab Lịch sử: lọc **từ/đến ngày + text** (mã/trạm/nội dung) · bảng tối đa 500 dòng (virtualized,
  CheckBox cột Mode=OneWay theo bài học S89) · **export CSV** · panel **Pareto tần suất theo mã**
  (top 15, đếm + % + thanh bar — tính trên TOÀN BỘ kết quả lọc, không chỉ 500 dòng hiển thị) —
  trả lời đúng "một ngày nhiều lỗi, không biết lỗi nào hay xảy ra".
- `AlarmListViewModel` nhận `IServiceScopeFactory` (IAlarmRepository là Scoped/EF); i18n +11 key ×3.

### 🧪 Kiểm chứng
- UIA: đăng nhập admin → mở Cảnh báo → **click chuột thật** vào tab Lịch sử (phát hiện nhân tiện:
  UIA `SelectionItemPattern.Select()` không kích `Command` của RadioButton — phải click thật) →
  **10 dòng lịch sử thật từ DB + Pareto hiển thị**, app sống; màn Người dùng hiện **đủ 5 user**.
- Bẫy lặp lại lần 3 ghi sổ: sln build EXIT=0 nhưng dll module trong bin/ KHÔNG refresh → chạy exe là bản cũ,
  suýt kết luận sai "UI mới không hoạt động". Luôn `dotnet build AM.Application.Shell` tường minh + kiểm
  timestamp dll trước khi smoke.
- 159/159 Services tests pass (UserService thêm audit không vỡ gì).

---

## [Session 89] 2026-07-12 — HOTFIX: mở Vận hành tay / Cài đặt làm app thoát (Run.Text TwoWay bind vào Loc indexer)

**Commit:** `231ee0e`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chủ dự án báo: chọn tab Vận hành tay hoặc Cài đặt là chương trình thoát. Log app không có gì —
truy ra qua Windows Event Log (.NET Runtime).

### 🐛 Nguyên nhân
- `XamlParseException`: *"A TwoWay or OneWayToSource binding cannot work on the read-only property 'Item'"* —
  trong `CalibrationPanelView.xaml` (S84) có `<Run Text="{Binding [Calib.Threshold], Source=Loc.Strings}"/>`.
  **`Run.Text` là DP mặc định TwoWay** (bẫy WPF kinh điển) → bind vào indexer chỉ-đọc của Loc nổ ngay lúc load XAML.
- View này nhúng ở CẢ hai chỗ (sub-tab Hiệu chỉnh trong Vận hành tay + thẻ Hiệu chuẩn trong Cài đặt; SettingsView
  dựng TẤT CẢ view con ngay khi mở) → mở màn nào cũng sập. Smoke boot không bắt được vì view tạo lười khi điều hướng.

### ✅ Sửa
- Thêm `Mode=OneWay` cho Run đó (rà toàn repo: đúng MỘT chỗ thiếu — các `<Run Text="{Binding...}">` khác đều đã OneWay).
- App.OnStartup: đăng ký `DispatcherUnhandledException` + `AppDomain.UnhandledException` → **Log.Fatal trước khi chết**
  (chỉ log, không nuốt lỗi) — crash UI từ nay để lại dấu vết trong log app, khỏi phải đào Event Log.

### 🐛 Crash thứ 2 cùng lớp (chủ dự án báo tiếp): màn Cảnh báo thoát khi CÓ alarm
- Handler Log.Fatal vừa thêm trả công ngay — stack nằm sẵn trong log app: `DataGridCheckBoxColumn`
  (cột DataGrid cũng **mặc định TwoWay**) bind vào `AlarmModel.IsAcknowledged` có **setter private** →
  InvalidOperationException lúc binding activate KHI LIST CÓ DÒNG. Lần duyệt tab trước không lộ vì list rỗng;
  chủ dự án bấm E-Stop tạo alarm 70001 rồi mở màn Cảnh báo là sập. `IsReadOnly=True` của grid KHÔNG cứu —
  exception nổ lúc activate binding, trước cả chuyện edit.
- Sửa: `Mode=OneWay` (rà repo: chỉ 1 CheckBoxColumn duy nhất; các TextColumn bind property `init` nên WPF
  không coi là read-only — không nổ).

### 🧪 Kiểm chứng (UI Automation thật, không chỉ boot)
- Crash 1: script UIA mở app → duyệt **cả 7 tab** trước đăng nhập → đăng nhập `engineer` qua overlay →
  duyệt **cả 8 tab gồm Vận hành tay** — app sống toàn trình.
- Crash 2: UIA **đăng nhập sai 5 lần để tự tạo alarm 40010 thật** (tính năng P3.1 thành công cụ test) →
  duyệt hết tab gồm màn Cảnh báo ĐANG CÓ DÒNG alarm — app sống, 0 FTL sau bản sửa.
- Bài học ghi vào quy trình: smoke boot KHÔNG đủ cho thay đổi XAML — phải điều hướng tới màn bị sửa, và màn
  danh sách phải test VỚI DỮ LIỆU (binding trong DataTemplate/column chỉ activate khi có item). Quy tắc:
  mọi bind tới property chỉ-đọc trên DP TwoWay-mặc-định (Run.Text, CheckBox.IsChecked, cột DataGrid...)
  PHẢI `Mode=OneWay`.

---

## [Session 88] 2026-07-11 — ROADMAP P4 HOÀN TẤT: single-step + sequence theo recipe + Settings hết placeholder + Production ca/SPC/CSV

**Commit:** `abdcedc`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** "làm p4 đi" — 4 mục cuối trước P5 (máy thật). Xong phiên này: P0–P4 của ROADMAP_HOAN_THIEN sạch bảng.

### ✅ P4.1 — Chế độ từng bước (gap C3a)
- Engine (`AM.Core.Sequencing`): `SingleStep` (bật/tắt cả khi đang chạy) + `IsWaitingStep` + `StepOnce()`.
  Bật → sau MỖI nhóm order engine TỰ cài gate pause ở ranh giới (tái dùng cơ chế WaitIfPausedAsync —
  **bất biến 5: IStation không đổi, station không biết gì**). `StepOnce` chỉ mở gate DO single-step tạo;
  gate của `RequestPause` thật vẫn phải đi `Resume()` (giữ ngữ nghĩa resume-check an toàn). Tắt toggle khi
  đang đứng gate → bấm Bước tiếp một lần nữa là chạy liên tục.
- Shell: toggle **"Từng bước"** trên action bar (Engineer+, hiện ● khi bật) + nút **"Bước tiếp ▶"** trên banner
  khi engine đứng gate (OnTick poll `IsWaitingStep`, banner cao 52 + text `Shell.StepWaiting`).
- 3 test: đứng sau mỗi nhóm + StepOnce chạy tiếp · tắt giữa chừng → chạy liên tục · StepOnce KHÔNG mở gate pause thật.

### ✅ P4.2 — Sequence theo recipe (gap C4)
- `RecipeBase.SequenceFile` (tùy chọn); thứ tự chọn file: khai tường minh → convention
  `recipes/{RecipeName}.sequence.json` (nếu tồn tại) → file mặc định máy (config).
- `SequenceSource` nhận thêm IRecipeService + IAlarmService: RecipeChanged → invalidate cache +
  **validate sớm** — sequence của recipe mới hỏng/thiếu file → **alarm 60005 NGAY lúc đổi recipe**
  (không đợi bấm Chạy); Get() vẫn ném SequenceValidationException để master chặn chạy như cũ.
- 4 test: file tường minh · convention/fallback · đổi recipe nạp lại đúng · recipe hỏng → 60005.

### ✅ P4.3 — Settings HẾT PLACEHOLDER (gap C5)
- Thẻ **"Phần cứng"** (`HardwareView`+VM): bảng thiết bị từ HardwareManager — tên · category · driver
  (Simulated* nhìn là biết sim) · trạng thái ●/✕ poll 1s + nút **kết nối lại TỪNG thiết bị** (Engineer+, audit
  `Hardware.Reconnect.{name}`) — khác màn Chẩn đoán vốn chỉ Reconnect All.
- Thẻ **"Host"** (`HostView`+VM): endpoint OPC-UA/Modbus/PLC/EtherNet-IP/DB đọc từ config (READ-ONLY — ghi rõ
  "đổi endpoint: sửa appsettings + khởi động lại") + trạng thái sống theo category; Shell bơm danh sách
  `HostEndpointInfo` qua DI — module không đụng IConfiguration.
- **Kiosk**: interface `IKioskService` mới (Abstractions) + `KioskService` (Shell, MainWindow attach getter/setter
  lúc Loaded) + nút "Vào/Thoát kiosk" trên landing Cài đặt (Engineer+); Ctrl+Shift+F11 giữ làm dự phòng.
- Cài đặt giờ đủ 9 thẻ chức năng thật: Chẩn đoán · Kỹ thuật · Giới thiệu · Phần cứng · Hiệu chuẩn · Audit ·
  Người dùng · Host · Sao lưu.

### ✅ P4.4 — Production: ca thật + yield màu + CSV + SPC đơn giản (gap C7, C8)
- `ProductionOptions` (config `AutoMachine:Production`): `ShiftStartHour`=8, `ShiftLengthHours`=8 (ca lặp đều,
  `GetShiftStartLocal`), `YieldWarnPercent`=95, `YieldAlarmPercent`=90. **Dashboard + Production cùng MỘT định
  nghĩa "ca hiện tại"** — hết cửa sổ trượt 8h cứng (KPI ca giờ đúng nghĩa "từ đầu ca đến giờ").
- KPI yield **màu-khi-có-nghĩa** cả 2 màn (hoãn từ ADR 0010): thường = màu live-value; <Warn = vàng; <Alarm = đỏ;
  Total=0 = không tô (chưa có nghĩa).
- **Export CSV**: record trong cửa sổ đang chọn (Time/SN/Recipe/OK-NG/Cycle/Score/Reason/Operator, escape chuẩn).
- **Trend theo giờ** (SPC đơn giản): mỗi giờ một dòng — thanh yield màu theo mức + yield% + X̄ cycle + n=…;
  tự hiện "chưa có dữ liệu" khi rỗng. Cửa sổ Production mặc định "Ca hiện tại".
- 10 test `ProductionOptionsTests` (6 case mốc ca 8h/12h qua đêm + 4 case mức màu yield).

### 🧪 Build & test
- **317 tests pass** (+17: 3 single-step + 4 SequenceSource + 10 ProductionOptions) — chạy theo từng project
  (Sequencing 23 · Demo 15 · Services 159 · Hardware 42 · Infra 62 · Architecture 6 · Vision 10).
- Build 0 warning (sửa CA1849/S6966 WriteAllTextAsync, CS0117 ContinueMode.UntilStopped, CS0535 AllNames).
- i18n +27 key ×3 → **399 chuỗi/ngôn ngữ**. Smoke boot sạch: AutoLogout + Auto-backup + Calib routine
  + i18n 399×3 + "started successfully".
- **2 bug thật trong test mới bị bắt và sửa khi chạy**: (a) test single-step kỳ vọng gate ranh giới sản phẩm
  với SingleCycle — sai, SingleCycle kết thúc sau nhóm cuối → chuyển UntilStopped; (b) test 3 DEADLOCK cả
  assembly: station stub hoàn thành đồng bộ + UntilStopped → RunAsync không có điểm yield, chiếm vĩnh viễn
  worker thread xUnit ngay tại lời gọi (tìm ra bằng vstest --diag) → station thêm Task.Delay(20) + chờ engine
  vào Running trước khi RequestPause. Bài học: station stub trong test engine PHẢI có ít nhất một await thật.
- Bẫy môi trường ghi lại: MSBuild node-reuse treo (build với `-nodeReuse:false`), bin/ dùng chung chứa dll test
  cũ (Jun 6/14) — LUÔN `dotnet test <project>`, đừng test dll trong bin/; testhost mồ côi khóa dll sau khi
  kill run (taskkill testhost trước khi build lại).

### ⏭️ Việc tiếp
- **P0–P4 XONG TOÀN BỘ.** Còn **P5 tích hợp máy thật** (axis map thật, ngưỡng calib theo trục, day-code secret
  triển khai, số trục máy thật §9.3) — chờ có phần cứng.

---

## [Session 87] 2026-07-10 — Chắt lọc bộ UX guidelines web/mobile (RefUX-A): 11 quy tắc interaction/feedback vào chuẩn HMI, phần styles/palette từ chối có lý do

**Commit:** `25ec2bf`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chủ dự án đưa link một repo OSS AI-skill sinh design-system UI/UX (bí danh **RefUX-A** —
`docs/private/alias.local.md`) và yêu cầu rà xem áp được gì vào AM.AutoFrame.

### 🔍 Đánh giá (phê phán)
- RefUX-A nhắm **web/mobile marketing UI**: 67 UI styles (Glassmorphism, Claymorphism...), 161 color palettes,
  57 font pairings, reasoning rules theo ngành (fintech/e-commerce), stack React/Next/Tailwind.
- **KHÔNG cài làm skill**: skill trigger tự động cho mọi yêu cầu UI → sẽ đề xuất style/gradient/font web vào HMI,
  ngược trực diện High-Performance HMI (nền yên tĩnh, palette v2 duy nhất, màu-chỉ-khi-có-nghĩa). Từ chối ghi thành
  bảng "KHÔNG áp + lý do" để phiên sau không bê nhầm.
- Phần giá trị thật: nhóm **99 UX guidelines** có các quy tắc interaction/feedback platform-agnostic KÈM ĐỊNH LƯỢNG
  mà bộ HMI docs hiện hành nói rải rác nhưng chưa thành luật.

### ✅ Nội dung thêm vào dự án
- **`docs/HMI_Advanced_Standards.md` +§7** "Đối chiếu RefUX-A — adoption": bảng 5 nhóm KHÔNG áp (styles/palette ·
  font · mobile-first/web-perf · ARIA/HTML · AI-spatial-sustainability) + bảng **11 quy tắc ÁP**: lệnh >300ms hiện
  bận + xong việc không im lặng · disable nút khi lệnh chạy (chống double-fire) · đủ bộ trạng thái nút kèm LÝ DO
  disabled · animation micro 150–300ms ease-out, cấm animation trang trí · thông báo tiện ích tự tắt 3–5s vs lỗi
  phải đi đường alarm/ACK · không layout shift (trừ banner co giãn chủ đích) · truncation có tooltip · form label
  luôn hiện + validate cạnh ô · số ngăn nghìn + ngày một định dạng `HH:mm:ss dd/MM/yyyy` · empty state có lối đi ·
  2 bước cho hành động không đảo ngược. Nhiều mục vốn là pattern có sẵn trong code (IsBusy, blockReason,
  Override/Restore 2 bước, Calib.Empty) — giờ được NÂNG THÀNH LUẬT.
- **`.claude/skills/am-hmi-design/SKILL.md`**: Release Checklist +10 mục nhóm "Interaction & feedback (RefUX-A §7)".
- **CLAUDE.md**: dòng mô tả `HMI_Advanced_Standards.md` trỏ thêm §7.
- **`docs/private/alias.local.md`** (local, không commit): thêm bí danh RefUX-A theo quy ước ẩn danh nguồn tham khảo.

### 🧪 Build & test
- Docs-only — không đổi code/string; 300 tests + build giữ nguyên trạng thái S86.

### ⏭️ Việc tiếp
- **P4.1 Single-step** · **P4.2 Sequence per-recipe** · **P4.3 Settings hoàn thiện** · **P4.4 Production/SPC** · **P5** máy thật.

---

## [Session 86] 2026-07-08 — P3.3 Backup & restore: zip 3 loại bản lưu + auto hàng ngày + phục hồi 2 bước có đường lùi — P3 XONG TOÀN BỘ

**Commit:** `31da608`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** P3.3 (gap B6, C5-một-phần) — thẻ Settings cuối cùng của trục bảo mật còn placeholder.
Xong mục này: P0–P3 của roadmap sạch bảng.

### ✅ IBackupService + BackupService
- Gom dữ liệu vận hành thành zip: db · users/points/parameters.json · io.map/machine/axismap.json ·
  calibration-history.json · recovery/override-actions.json · appsettings.json · recipes/ — chỉ gom mục
  đang tồn tại (máy chưa có file nào thì bỏ qua mục đó).
- **3 loại bản lưu theo prefix**: `am-backup-*` (tay), `am-auto-*` (tự động **mỗi ngày một bản** lúc app chạy —
  khởi động nhiều lần/ngày không nhân bản; giữ `AutoMachine:Backup:KeepCount`=7 bản mới nhất, xoá bản cũ;
  lỗi auto-backup chỉ log — không phá app đang sản xuất), `am-prerestore-*` (**TỰ tạo trước MỌI lần phục hồi** —
  phục hồi nhầm bản vẫn còn đường lùi về trạng thái ngay trước đó).
- Restore: giải nén đè vào thư mục gốc app, **chặn path-traversal** (entry vượt thư mục gốc → InvalidDataException),
  audit `Backup.Restore`, log WARNING nhắc **khởi động lại app** (UserService/RecipeService... đã nạp dữ liệu cũ
  vào RAM). Tên file chống trùng trong cùng giây (hậu tố -1, -2 — bug bắt được nhờ test).

### ✅ Màn "Sao lưu & phục hồi" (Settings — hết placeholder)
- `BackupView`+`BackupViewModel` (gate Administrator): khối "Nội dung sẽ sao lưu" (minh bạch cái gì vào zip) +
  nút **Sao lưu ngay…** (OpenFolderDialog chọn đích) + danh sách bản lưu (tên/thời điểm/KB, mới nhất trước) +
  nút Phục hồi từng dòng → **confirm 2 bước**: cảnh báo đỏ "GHI ĐÈ dữ liệu hiện tại… PHẢI khởi động lại" →
  nút xác nhận lần 2 (pattern giống Override). Sau phục hồi status nhắc khởi động lại.
- i18n +13 key `Backup.*`/`Set.BackupDesc` ×3 ngôn ngữ → 380 chuỗi. DI: `AddCoreServices` đăng ký theo config;
  `App.OnStartup` gọi `Start()` bật auto-backup. Settings giờ chỉ còn 2 placeholder: Phần cứng + Host (P4.3).

### 🧪 Build & test
- **300 tests pass** (+3 `BackupServiceTests`: zip đúng nội dung + bỏ mục không tồn tại · restore khôi phục file
  đã xoá/đè file hỏng + có bản prerestore · zip thiếu throw + danh sách mới-nhất-trước), build 0 warning
  (sửa S1994 vòng for → while), smoke: "[Backup] Auto-backup hàng ngày BẬT — giữ 7 bản" + tạo thật
  `am-auto-20260708-*.zip` ngay lần boot đầu, i18n 380×3.

### ⏭️ Việc tiếp
- **P0–P3 XONG TOÀN BỘ.** Còn theo roadmap §4: **P4.1 Single-step** · **P4.2 Sequence per-recipe** ·
  **P4.3 Settings hoàn thiện** (2 placeholder cuối) · **P4.4 Production/SPC/ca** · **P5** tích hợp máy thật.

---

## [Session 85] 2026-07-08 — P3.2: tự đăng xuất khi idle (máy vẫn chạy) + audit lưu bền JSONL + màn Audit trong Cài đặt

**Commit:** `6f05f0d`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** P3.2 theo roadmap (gap B2, B5). Q6 phần auto-logout: mặc định 15 phút, config
`AutoMachine:Security:AutoLogoutMinutes` (0 = tắt) — chỉnh theo nhà máy lúc triển khai.

### ✅ Auto-logout (an toàn phiên, không đụng sản xuất)
- MỚI `InactivityMonitor` (Shell): hook `InputManager.Current.PreProcessInput` — MỌI input chuột/bàn phím/cảm ứng
  toàn app đều reset đồng hồ idle; DispatcherTimer kiểm mỗi 30s. Idle ≥ ngưỡng và đang có phiên đăng nhập →
  `IUserService.Logout()` — **chỉ hạ quyền về "Chưa đăng nhập", máy đang chạy VẪN chạy** (nguyên tắc 0012:
  biện pháp bảo mật không được gây downtime) + audit `AutoLogout` kèm số phút idle. Đăng nhập xong tính idle
  lại từ đầu (không bị đăng xuất oan ngay sau login). `AutoLogoutMinutes=0` → tắt hẳn, log rõ.
- `SecurityOptions` bind MỘT lần thành singleton — UserService + InactivityMonitor dùng chung (hết bind lặp).

### ✅ Audit lưu bền + màn Audit
- `AuditService`: ngoài structured log `[AUDIT]` giờ append 1 dòng JSON/bản ghi vào `logs/audit-yyyyMMdd.jsonl`
  (một file mỗi ngày; file quá `LogRetentionDays` bị xoá lúc boot; **ghi file lỗi không phá thao tác gốc**).
  `IAuditService` + model `AuditEntry` + `Query(from, to, userFilter, max=500)` — đọc từ ngày mới về cũ,
  dừng sớm khi đủ max, file hỏng bỏ qua ngày đó.
- Settings thẻ MỚI **"Nhật ký audit"** (`AuditView`+`AuditViewModel`, gate Administrator): bảng 5 cột
  (thời gian · user · thao tác · kết quả OK/DENIED-đỏ · chi tiết, virtualized), lọc từ/đến ngày (DatePicker)
  + user (contains), nút Làm mới + **Xuất CSV** (escape ngoặc kép/phẩy/xuống dòng chuẩn, SaveFileDialog).
- i18n +14 key (`Set.Audit*`, `Audit.*`) ×3 ngôn ngữ → 367 chuỗi.

### 🧪 Build & test
- **297 tests pass** (+3 `AuditServiceTests`: Record→Query mới-nhất-trước + lọc user · đọc gộp nhiều ngày +
  sống qua reload · retention xoá file >30 ngày giữ file mới), build 0 warning (sửa S6580 parse có culture),
  smoke boot sạch: "[AutoLogout] Bật — idle 15 phút sẽ tự đăng xuất (máy vẫn chạy)", i18n 367×3.

### ⏭️ Việc tiếp
- **P3.3 Backup & restore** (thẻ Settings cuối còn placeholder) — làm tiếp cùng đợt.

---

## [Session 84] 2026-07-08 — ROADMAP P2 HOÀN TẤT: mô hình calibration + framework wizard 2 nhánh + UI hai chỗ nhúng + demo routine

**Commit:** `a6ff044`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** P2 là trục "Hiệu chỉnh" của roadmap (gap D1–D3 — trước đây calib là trục trắng, tài liệu tham chiếu treo).
Làm trọn 3 phần trong một phiên theo yêu cầu chủ dự án.

### ✅ P2.1 — `docs/HMI_Calibration_Model_v1.0.md` (chuẩn hiện hành)
- Calib ≠ Setting (quy trình động có đo/bù ≠ giá trị tĩnh); `frequency` routine/rare quyết định CHỖ ĐỨNG UI
  (không quyết định quyền — `MinLevel` khai riêng); **wizard 2 nhánh theo `autoThreshold`** với bất biến an toàn:
  không tự áp khi vượt ngưỡng (che giấu vấn đề cơ khí), không áp khi chưa đo; lịch sử + audit;
  `requiresCalibAfterChange` + usage counter để khái niệm (code P5 khi có máy thật).
- 4 quyết định ADR-style: framework đặt Abstractions+Services (không project riêng — engine nhỏ);
  routine đăng ký CODE lúc bootstrap (routine là class có logic đo — config JSON không mô tả được phép đo);
  MỘT module UI nhúng hai chỗ; kết quả bù ghi vào RECIPE qua IRecipeService (một nguồn sự thật).
- Master Index: gạch tham chiếu treo `HMI_Calibration_Model_v1.0.md` (đã có) + mockup calib (bỏ trỏ — UI thật thay).

### ✅ P2.2 — Framework (contracts + service + wizard + 5 test)
- MỚI enum `CalibrationFrequency`/`CalibrationWizardState` (AM.Core), record `CalibrationMeasurement`
  (Offset đại diện + Components dx/dy) / `CalibrationRecord`; interfaces `ICalibrationRoutine` /
  `ICalibrationService` (Register chống trùng Id + CreateWizard + GetHistory) / `ICalibrationWizard`.
- `CalibrationService`: lịch sử `calibration-history.json` (giữ 200 mới nhất, sống qua restart) + audit
  `Calibration.{routineId}` mỗi lần hoàn tất. `CalibrationWizard`: Idle→Measuring→Within/OutOfThreshold→Applying→
  Completed/Failed; Apply sai trạng thái → InvalidOperationException; đo lỗi → Failed (không sập); Reset về Idle;
  bản ghi phân biệt "tự áp" vs "sau chỉnh tay".
- 5 test: nhánh trong ngưỡng 1 chạm · nhánh chỉnh tay đo lại 2 lần rồi đạt · cấm áp khi chưa đo/vượt ngưỡng
  (routine.Apply không bao giờ bị gọi) · đo hỏng → Failed → Reset · trùng Id throw + lịch sử reload.

### ✅ P2.3 — UI hai chỗ nhúng + demo routine end-to-end
- MỚI project **`AM.Modules.Calibration`** (#29): `CalibrationPanelView` + `CalibrationPanelViewModel`
  (danh sách routine theo frequency · wizard card: trạng thái/kết quả đo/ngưỡng/nút Đo–Áp bù–Làm lại ≥44px ·
  hướng dẫn chỉnh tay từng bước khi vượt ngưỡng · lịch sử 10 dòng · gate quyền theo `MinLevel` + `UserChanged`);
  2 subclass mỏng `RoutineCalibrationPanelViewModel`/`RareCalibrationPanelViewModel` chốt frequency để DI thường.
- Vận hành tay: **sub-tab thứ 6 "Hiệu chỉnh"** (pane 5) — RadioButton TỰ ẨN khi `Calibration.HasRoutines=false`.
- Cài đặt: thẻ "Hiệu chuẩn" **hết placeholder** → mở `CalibrationPanelView` (rare).
- Demo: `PickOffsetCalibrationRoutine` (`demo.pick-offset`, routine, LineLead+, **ngưỡng 0.05mm** khớp Set–Confirm §9):
  đo mô phỏng độ trôi ±0.12mm (đo lại co ×0.35 như thể operator đã chỉnh giữa 2 lần đo — cả 2 nhánh demo được);
  Áp bù cộng dX/dY vào `PickPositionX/Y` recipe active + `SaveRecipeAsync` (audit người thực hiện);
  sau áp còn nhiễu dư ±0.01mm — đo kiểm lại thấy trong ngưỡng. Đăng ký: DI `ICalibrationRoutine` (AddDemoMachine)
  + `RegisterCalibrationRoutines` generic ở App.OnStartup (máy mới chỉ thêm 1 dòng AddSingleton).
- i18n +26 key `Calib.*`/`Manual.Tab.Calib`/`Set.CalibDesc` ×3 ngôn ngữ (353 chuỗi).

### 🧪 Build & test
- **294 tests pass** (+5), build 0 warning (sửa CA2017/S6677 template log, CA2007 ConfigureAwait(true) module,
  S125 comment), smoke boot sạch: log "[Calib] Đăng ký routine demo.pick-offset (Routine, ngưỡng 0.05mm)".
- Bẫy lặp lại đã dính lần nữa: sln build KHÔNG build exe WinExe — smoke lần đầu chạy exe cũ (không có [Calib],
  i18n 327) → build `AM.Application.Shell` riêng rồi smoke lại mới đúng (353 ×3).

### ⏭️ Việc tiếp
- **P3.2 Auto-logout + audit UI** và **P3.3 Backup & restore** (đang làm tiếp cùng đợt nếu đủ).

---

## [Session 83] 2026-07-08 — P3.1 chính sách đăng nhập nhà máy: bỏ lockout, break-glass day-code + file recovery, banner mật khẩu mặc định

**Commit:** `f813b91`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chủ dự án phản biện DoD gốc của P3.1 (lockout 5 lần + MustChangePassword) bằng thực tế nhà máy:
tài khoản dùng chung theo vai, kỹ sư vận hành nhiều máy, lockout đúng lúc hỏng máy = downtime, không có khôi phục
online, người giữ mật khẩu admin có thể rời đi vĩnh viễn. Đánh giá + 3 câu hỏi chốt qua AskUserQuestion →
lưu **`docs/design-notes/0012-security-policy-factory.md`** (ADR đầy đủ phương án + đánh đổi) rồi mới thực hiện.

### ✅ Chính sách chốt (0012)
- **Q1 — KHÔNG lockout, không delay**: mọi lần sai → audit log; sai ≥5 lần LIÊN TIẾP (cùng username, reset khi đúng)
  → alarm nhẹ **40010** để ca trưởng biết có người dò. Đăng nhập đúng luôn vào được ngay — zero rủi ro downtime.
- **Q2 — Break-glass kép, nguyên tắc "vào được nhưng không vào lén được"**:
  - **Day-code**: user `service` + mã 8 số = HMAC-SHA256(secret, machineId + yyyyMMdd) — chấp nhận ±1 ngày →
    phiên **SuperUser** + alarm **40011** + audit. Secret rỗng = TẮT (mặc định repo — chỉ đặt trong config triển khai).
    Tool **`scripts/am-daycode.ps1`** cho kỹ sư hãng (1 tool mọi máy) — **kiểm chứng thực nghiệm khớp C#** bằng spike
    console gọi thẳng `UserService.ComputeDayCode` từ DLL build: 2 ngày thử cùng ra `69307252`/`88579376`.
  - **File recovery**: đặt **`am-recovery.key`** cạnh executable → lúc boot file bị **XOÁ NGAY** (một lần dùng) + mở
    cửa sổ **30 phút** đăng nhập `recovery/recovery` = **Administrator tạm** + alarm **40012** + audit.
    KHÔNG đụng users.json — danh sách user giữ nguyên (khác đường xoá-file re-seed vốn có, vẫn giữ làm lớp cuối).
- **Q3 — Banner thay ép đổi**: không MustChangePassword (tài khoản dùng chung — người đổi không báo được ca khác);
  `HasDefaultPasswordsAsync` (BCrypt so nền, cache invalidate khi Save) → **banner vàng thường trực** trên Shell khi
  còn tài khoản seed dùng mật khẩu mặc định; alarm/prompt khẩn hơn đè lên; tắt trong ~1s sau khi đổi hết.
  MinLength giữ lại (config, mặc định 8) — không thể gây downtime.

### 📁 Thay đổi
- MỚI: `AM.Core/Models/SecurityOptions.cs` (bind `AutoMachine:Security`) · `scripts/am-daycode.ps1` ·
  `docs/design-notes/0012-security-policy-factory.md` · `AM.Services.Tests/UserSecurityPolicyTests.cs`
- `AlarmCodes`: +40010/40011/40012 (+ catalog `Alarms.{vi,en,zh}.json`); `IUserService`: +`HasDefaultPasswordsAsync`
- `UserService`: break-glass 2 đường trong LoginAsync (username dành riêng `service`/`recovery` — cấm tạo tài khoản
  trùng), `ComputeDayCode` public static (chung thuật toán với tool), đếm chuỗi sai + audit, MinLength, seed dùng
  bảng `SeedAccounts` chung với kiểm mật khẩu mặc định; ctor thêm 4 tham số optional (tương thích ngược test cũ)
- Shell: `AddCoreServices(config)` + factory UserService (Security options + IAlarmService + IAuditService);
  ShellViewModel `HasSecurityNotice` (poll cache mỗi tick 1s) + banner else-branch; MainWindow.xaml +2 DataTrigger
  (nền vàng + glyph ⚠, đặt đầu để trạng thái khẩn hơn đè); appsettings +`Security`; strings +`Shell.DefaultPwdWarn` 3 ngữ
- Roadmap: hàng 12 ✅ S83 + DoD P3.1 viết lại theo 0012 + Q6 gạch phần lockout

### 🧪 Build & test
- **+8 test → 289 pass**: không-khoá-sau-5-lần + alarm đúng 1 lần · day-code hôm nay/±1 vào, quá hạn/sai/chưa-config
  từ chối · file key bị xoá + cửa sổ recovery vào được + không file thì không · MinLength tạo/đổi · cấm tên dành riêng ·
  banner true khi seed / false sau đổi hết. 2 test cũ nâng mật khẩu test lên ≥8 ký tự theo policy mới.
- Build 0 warning; smoke boot sạch (i18n 327 chuỗi ×3, users.json nạp bình thường, không lỗi).

### ⏭️ Việc tiếp
- **P2.1–P2.3 Calibration** (3 phiên) hoặc **P3.2 Auto-logout + audit UI** (Q6 phần auto-logout còn chờ chốt số phút).

---

## [Session 82] 2026-07-07 — ROADMAP P1 HOÀN TẤT: guard hình học Z-an-toàn (P1.4) + jog giữ-để-chạy deadman 200ms (P1.5)

**Commit:** `4a9d35f`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** 2 mục cuối của P1 — code chuyển động an toàn-trọng-yếu, làm trong 1 phiên riêng đúng kế hoạch S81.

### ✅ P1.4 — Guard hình học: cấm X/Y/U khi Z chưa ở độ cao an toàn (gap A3)
- MỚI `AM.Services/MotionSignalPublisher`: poll vị trí trục Z (mặc định axis 2) mỗi 100ms qua `IMotionController.GetPositionAsync` → publish tín hiệu bool **`Motion.ZAtSafe`** (`|z − safeZ| ≤ 0.5mm`) lên `HardwareSignalBus` — bus dedup nên consumer vẫn event-push. **FAIL-SAFE**: chưa kết nối / lỗi đọc / chưa Start → publish `false` (guard coi như CHƯA an toàn). Đăng ký DI + `Start()` ở App.OnStartup cạnh SafetySignalPublisher.
- `SignalKeys.MotionZAtSafe` mới (AM.Core). `MotionViewModel.GeometricGuardFor(axis)`: trục **X/Y/U** nhận `GuardCondition.RequireAll(Motion.ZAtSafe)` cho jog/nudge/move-abs/jog-hold; **trục Z được miễn** (phải còn đường nâng Z lên). Bị chặn → blockReason **`Manual.ZNotSafe`** (3 ngữ: "Z chưa ở độ cao an toàn — nâng Z lên đỉnh trước khi chạy X/Y/U") + audit DENIED.
- `RunGuardedAsync` thêm overload nhận `GuardCondition?` — đường guard 3 tầng (state → role → hardware-condition) dùng chung cho mọi lệnh trục.
- Test +2 (`MotionSignalPublisherTests`): Z ở 0 → tín hiệu true, đẩy Z −12mm → false · motion chưa kết nối → fail-safe false + KHÔNG gọi GetPosition.

### ✅ P1.5 — Jog giữ-để-chạy với deadman watchdog (gap A4)
- MỚI interface **`IAxisJog`** (Core.Abstractions): `StartJogAsync(axis, velocity có dấu)` / `KeepAlive(axis)` / `StopJogAsync(axis)` + hằng hợp đồng **`WatchdogTimeoutMs = 200`** — HAL nào implement PHẢI tự dừng trục khi mất KeepAlive quá 200ms (UI treo/crash/mất kết nối KHÔNG thể để trục chạy tiếp).
- `SimulatedMotionController : IAxisJog`: vòng tích phân vị trí 25ms/tick dưới lock; mất KeepAlive >200ms → tự dừng + log WARNING "JOG WATCHDOG… TỰ DỪNG (deadman)"; StopJog idempotent; vận tốc 0 → ArgumentOutOfRange.
- MỚI `AM.Modules.Motion/JogHoldBehavior` (attached behavior `local:JogHold.DownCommand/UpCommand/Parameter`): PreviewMouseLeftButtonDown = bắt đầu giữ (CaptureMouse), **Up / MouseLeave / LostMouseCapture = nhả** → không có đường nào giữ nút mà thoát không Stop. 8 nút hướng jog pad MotionView chuyển sang behavior này.
- `MotionViewModel`: `JogHoldPlus/Minus` → guard R3 + hình học → `StartJogAsync` + **vòng nuôi KeepAlive 80ms nền** (CTS liên kết `_cts`); `JogHoldStop` hủy vòng + StopJog; STOP đỏ hủy hold + StopAllAxes; HAL không có `IAxisJog` → **fallback mỗi lần nhấn = 1 bước inching** (hành vi cũ, an toàn). `Axis.JogHint` viết lại 3 ngữ (GIỮ-để-chạy + deadman 200ms).
- Test +4 (`SimJogDeadmanTests`): nuôi KeepAlive → chạy liên tục, nhả → đứng yên · **KHÔNG nuôi → tự dừng trong cửa sổ watchdog** · vận tốc âm chạy chiều âm · vận tốc 0 throw.

### 🧪 Build & test
- **281 tests pass** (+6: Hardware 38→42, Services 128→130), build 0 warning (sửa S1244 so sánh float, CA2016 token, CA1849/S6966 → `CancelHold()` sync helper), smoke boot sạch — log "[MotionSignals] Started — Z(axis 2) an toàn tại 0±0.5 mm", i18n 326 chuỗi ×3 ngôn ngữ.

### 📁 Files
- Mới: `IAxisJog.cs` · `MotionSignalPublisher.cs` · `JogHoldBehavior.cs` · `SimJogDeadmanTests.cs` · `MotionSignalPublisherTests.cs`
- Sửa: `SimulatedMotionController.cs` (+IAxisJog) · `SignalKeys.cs` · `MotionViewModel.cs` · `MotionView.xaml` · `ServiceCollectionExtensions.cs` · `App.xaml.cs` · `strings.{vi,en,zh}.json` · `ROADMAP_HOAN_THIEN.md` (§4 hàng 8/9 ✅)

### ⏭️ Việc tiếp
- **P1 xong 6/6.** Theo roadmap §4: **P2.1–P2.3 Calibration** (3 phiên, doc + framework wizard + demo routine) hoặc chen **P3.1 Password policy + lockout** (🟠, 1 phiên).

---

## [Session 81] 2026-07-07 — ROADMAP P1 (4/6): chốt chính sách §9, nối nút Manual, nút vật lý, prompt liệu sót + resume-check

**Commit:** `00c5367`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Thực hiện P1 của ROADMAP_HOAN_THIEN. P1.4 (guard hình học) + P1.5 (jog deadman) để phiên riêng — code chuyển động an toàn-trọng-yếu, không làm nửa vời.

### ✅ P1.1 — Chốt chính sách §9 (chủ dự án trả lời qua AskUserQuestion)
- **Q1 Override**: 1 người — 2 bước + đếm ngược 3s + lý do bắt buộc + audit nặng (GIỮ NGUYÊN S64). **Q2 R2**: cứng ở Engineer, KHÔNG hạ LineLead. **Q3 ngưỡng Set–Confirm**: 0.05 mm mặc định, khai config theo trục khi có máy thật.
- `HMI_Manual_Operation_and_Safety_v1.0.md` + `HMI_Master_Index.md` §9: từ "chờ xác nhận/default TẠM" → **ĐÃ CHỐT** (mục 3 — số trục máy thật — chờ P5.1).

### ✅ P1.2 — Nối nút Manual (kèm ĐÍNH CHÍNH roadmap)
- **Đính chính gap C2 (S79 ghi quá tay)**: màn Vận hành tay ĐÃ TỒN TẠI từ S48 — `MotionView` = tab `Nav.ManualOp` (minLevel LineLead) đủ 5 sub-tab Trục/Điểm/IO/Thao tác trạm/Override + dải khoá `IsAdjustAllowed`. Gap thật: nút Manual trên action bar disabled vĩnh viễn.
- Shell: nút Manual → `NavigateToView("MotionView")`, `IsEnabled` bind `CanOpenManual` (LineLead+, cập nhật theo UserChanged), tooltip mới 3 ngữ ("Mở màn Vận hành tay...").

### ✅ P1.3 — Nút vật lý Start/Stop/Reset (gap [✓code] A2)
- MỚI `PhysicalButtonMonitor` (Demo/Sequencing): poll 50ms `DI.Btn.*` qua IIoService, **edge-detect sườn lên** (giữ nút không lặp lệnh), ưu tiên Stop trước Start cùng tick; gọi thẳng master Start/Stop/ResetAsync — **master tự kiểm interlock + state** (một nguồn sự thật, monitor không thêm logic). Đăng ký DI + `Start()` ở App.OnStartup.
- Test +3: sườn lên gọi đúng 1 lần khi giữ nút · nhấn-nhả-nhấn = 2 lần · Stop gọi đúng lệnh.

### ✅ P1.6 — Prompt liệu sót khi init + resume-check demo (ADR 0011 §4.1/§4.2 hoàn tất vòng)
- MỚI `BannerOperatorPromptService : IOperatorPrompt` (Shell): station hỏi operator KHÔNG dính UI — ShellViewModel subscribe, banner hiện câu hỏi + **nút ĐỘNG theo Choices** (`RespondServicePromptCommand`); KHÔNG có UI subscriber (headless/test) → tự chọn **lựa chọn ĐẦU TIÊN** (quy ước: an toàn nhất đứng đầu).
- `PickStation.InitializeAsync`: liệu sót → **HỎI operator** "Máy tự thoát liệu / Đã lấy tay — kiểm lại", lặp tới khi cảm biến xác nhận sạch (RefSeq-A req §2.4/§10b.2 — thay hành vi tự-quyết cũ).
- `PickStation : IResumeVerifiable`: kiểm **bất biến hình học** — mọi ranh giới bước Z phải ở độ cao an toàn (±0.5mm); Z bị đẩy khi pause → `VerifyResumeAsync` Fail → engine giữ Paused + prompt. *Quyết định thiết kế: KHÔNG so snapshot per-station (gantry dùng chung Pick/Place làm snapshot stale) — kiểm bất biến tự nhất quán thay thế; ghi chú vào roadmap.*
- Test +3: init liệu sót hỏi đúng + tự thoát · "đã lấy tay" tắt van kiểm lại · Z lệch khi pause → từ chối resume → đưa Z về + Retry → cycle hoàn thành sạch.

### 🧪 Build & test
- **275 tests pass** (+6: Demo 5→11), build 0 warning (sửa CA1515/CA1812 service internal + suppress DI), app boot sạch — log "[PhysBtn] Started".

### ⏭️ Việc tiếp
- **P1.4** guard hình học (publish tín hiệu trục/IO lên HardwareSignalBus + predicate jog) và **P1.5** jog deadman (IAxisJog velocity-mode + watchdog 200ms) — mỗi mục một phiên riêng; sau đó P2 calibration.

---

## [Session 80] 2026-07-06 — ROADMAP P0 hoàn tất: E-Stop state machine + retention job + users.json backup + docs HMI v3

**Commit:** `b72cf8b`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Thực hiện giai đoạn P0 của `docs/ROADMAP_HOAN_THIEN.md` — 4 mục sửa-ngay về đúng đắn & an toàn nền, tất cả gap đã kiểm chứng code ở S79.

### ✅ P0.1 — E-Stop vào state machine (an toàn — gap [✓code] nghiêm trọng nhất)
- `BaseMasterController.EmergencyStop()`: fire trigger `Error` (Running/**Paused**→RunAlarm, Initializing→InitAlarm — **thêm transition (Paused, Error)**, bảng 13→14 cạnh) + `RaiseEstopAlarmSafe()` raise alarm **70001** fire-and-forget (safety path tuyệt đối không throw). Trước đây E-Stop xong máy vẫn hiện "Đang chạy".
- Wire `ISafetyInput.SafetyStateChanged` ngay trong Base ctor: **E-Stop vật lý nhấn → EmergencyStop toàn máy**; cửa/light-curtain mở KHÔNG estop từ software (chỉ cảnh báo — cắt cứng do PLC/relay, HMI spec §8). Unsubscribe trong DisposeAsync.
- Test +5 (`BaseMasterControllerTests` + `FakeSafetyInput` mới): EStop lúc Running→RunAlarm+70001 · lúc Idle giữ Idle nhưng vẫn alarm · tín hiệu E-Stop vật lý → RunAlarm · cửa mở lúc Running → VẪN Running · transition Paused+Error.

### ✅ P0.2 — Retention job (gap [✓code]: DeleteOlderThanAsync có sẵn nhưng 0 caller — DB phình vô hạn)
- MỚI `IRetentionCleanupService` (Abstractions) + `RetentionCleanupService` (AM.Services): dọn alarm history + production record cũ hơn `DataRetentionDays` — 1 lượt ngay lúc `Start()` + `PeriodicTimer` mỗi 24h; scope per-lượt (EF Scoped); lỗi một lượt chỉ log. Đăng ký ở `AddDataAccess`, start ở `App.OnStartup`. Xác nhận runtime: log "[Retention] Started — giữ 365 ngày".
- Test +4: dọn cả 2 repo + trả tổng, cutoff = now−retention (±1'), ctor chặn ngày ≤ 0.

### ✅ P0.3 — users.json backup trước re-seed (gap [✓code]: mất user im lặng, đã xảy ra 02/07)
- `UserService.Load()`: 2 nhánh re-seed (schema sai/cũ + exception) gọi `BackupCorruptStore()` — copy `users.json.bak-{yyyyMMdd-HHmmss}` + LogError rõ đường dẫn TRƯỚC khi `SeedDefaults()` ghi đè; backup lỗi không chặn seed.
- Test +2: store hỏng → có đúng 1 file .bak nội dung NGUYÊN VẸN + store mới seed hợp lệ; lần đầu chạy (chưa có file) → không tạo .bak.

### ✅ P0.4 — Đồng bộ tài liệu HMI (docs mô tả 7 vùng trong khi shell đã 4 vùng từ S73)
- MỚI **`docs/HMI_UI_Architecture_Template_v3.md`** (CHUẨN HIỆN HÀNH): 4 vùng + số đo chạm, banner alarm/operator-prompt, kiosk config-driven, **3 nguyên tắc nội dung** (ADR 0010), bảng đối chiếu v2→v3; v2 giữ hiệu lực phần work-area/palette/schemas.
- `HMI_Master_Index.md`: §1 bảng (+v3, đánh dấu v2), §2 nguyên tắc bất biến +3 (11–13), §3 bố cục 4 vùng, tham chiếu calib treo → trỏ ROADMAP P2.
- `HMI_Dashboard_Spec.md` → **v2.1**: bảng vùng theo shell v3, nguồn dữ liệu cập nhật (record do ReportStation ghi, mini-log ăn sự kiện engine, chip kết nối, prompt banner).
- CLAUDE.md: chuẩn UI trỏ v3, sửa dòng README stale.

### 🧪 Build & test
- **269 tests pass** (+11: Infra 57→62, Services 122→128), build 0 warning; app boot sạch (lưu ý: `dotnet test` KHÔNG build WinExe — phải build Shell riêng trước khi smoke, đã gặp exe cũ 07-04).
- `AM.Services.Tests` +PackageReference `Microsoft.Extensions.DependencyInjection` (BuildServiceProvider cho scope-factory test).

### ⏭️ Việc tiếp
- **P1** roadmap: chủ dự án chốt §5 Q1–Q7 (nhất là Q1 override + Q2 R2) → dựng màn Vận hành tay v1 (P1.2); song song làm được ngay: P1.3 nút vật lý, P1.6 prompt liệu sót + resume-check demo.

---

## [Session 79] 2026-07-04 — Đánh giá toàn diện dự án + ROADMAP hoàn thiện (docs/ROADMAP_HOAN_THIEN.md)

**Commit:** `35c75cc`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chủ dự án yêu cầu rà toàn diện (giao diện/chức năng/an toàn/bảo mật/hiệu chỉnh — vision tạm bỏ, sẽ làm app riêng) + kế hoạch chi tiết hoàn thiện. Gap được KIỂM CHỨNG trực tiếp trong source (grep/đọc code) chứ không chỉ gom TODO cũ.

### 🔍 Phát hiện chính (đánh dấu [✓code] trong roadmap)
1. **An toàn**: `BaseMasterController.EmergencyStop()` KHÔNG fire trigger/alarm — E-Stop xong máy vẫn hiện "Đang chạy"; nút vật lý `DI.Btn.*` chỉ là hằng số chưa wire; guard tầng 3 mới có nguồn Safety.*; jog deadman chưa có.
2. **Dữ liệu**: `DataRetentionDays` chỉ được validate — `DeleteOlderThanAsync` có ở 2 repository nhưng **0 caller** → DB SQLite phình vô hạn.
3. **Bảo mật**: không lockout / không password policy / không bắt đổi mật khẩu seed / không auto-logout; `users.json` schema cũ → re-seed **ghi đè không backup** (đã xảy ra thật trong log 02/07); audit ghi nhưng chưa xem/export được.
4. **Hiệu chỉnh**: trục trắng hoàn toàn — `HMI_Calibration_Model_v1.0.md` là tham chiếu treo, chưa có framework/wizard/routine nào.
5. **UI/docs**: màn Vận hành tay chưa dựng (nút Manual disabled); Settings còn 4 placeholder; template HMI vẫn mô tả 7 vùng (thực tế 4).

### ✅ Thêm mới
- `docs/ROADMAP_HOAN_THIEN.md` — hiện trạng theo 6 trục + bảng gap có bằng chứng + **kế hoạch P0–P5** (mỗi mục: việc cụ thể/DoD/ước lượng, ~17 phiên cho P0–P4) + bảng ưu tiên 20 dòng + 7 câu hỏi cần chủ dự án chốt + hợp đồng để app vision riêng cắm vào (giữ IVisionProcessor/VisionStation/card KQ, không đổi engine).
- CLAUDE.md bảng tài liệu +1 dòng; PROJECT_STATUS trỏ TODO chính về roadmap.

### ⏭️ Việc tiếp
- **P0.1 E-Stop vào state machine** (ưu tiên 🔴 số 1) → P0.2 Retention job → P0.3 users.json backup → P0.4 sync docs; song song chủ dự án trả lời §5 (Q1–Q7) để mở khoá P1 Vận hành tay.

---

## [Session 78] 2026-07-04 — Prompt D: máy mẫu DemoPickPlace end-to-end trên mô phỏng + nối dashboard + banner prompt

**Commit:** `6c71301`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Triển khai Prompt D của Sequencing_NextSteps — máy mẫu chạy end-to-end trên SimIoService, nối `SequenceEngine` (S77) với master controller + dashboard. Chi tiết thiết kế: ADR 0011 (mục "Prompt D — đã triển khai").

### ✅ Thêm mới — `AM.WorkStation.Demo/Sequencing/`
- **`SimIoService`** (IIoService + IMotionService): IO/AI/vị trí in-memory + hành vi tự động (IO map §8) — bật vacuum→cảm biến báo sau delay (xác suất fail cấu hình), nhịp feeder→có hàng, thổi nhả→mất chân không; `DemoSimOptions` (delay + %lỗi vacuum/scan/vision NG) từ `appsettings:AutoMachine:DemoSim`.
- **6 station**: Scanner (SN + %scan-fail), Feed (nhịp feeder + chờ cảm biến), Pick (homing **Z→X→Y**, Z-an-toàn-trước-XY, kiểm liệu sót đầu cycle+init, **Abort khi đang giữ hàng → GIỮ vacuum** không thả — IO map §5), Vision (NG = kết quả nghiệp vụ StationResult.Ng, score→blackboard), Place (khay OK/NG theo runOnNg, nhả có kiểm soát, đọc "đang giữ hàng" từ HAL không tham chiếu Pick), Report (CSV local TRƯỚC + DB + upload — runOnNg, lỗi không giết cycle).
- **Adapters**: `RecipeViewAdapter` (IRecipeView read-only trên IRecipeService qua reflection tên property), `DemoRuntimeContext` (HAL+recipe+dry-run từ OperationMode), `SequenceSource` (nạp+validate+cache, lỗi→SequenceValidationException).
- **`recipes/DemoPickPlace.sequence.json`** (spec §2, gắn theo recipe qua `AutoMachine:Sequence:File`).

### ✅ Nối hệ thống
- **`DemoMasterController`** nối engine: mỗi `RunOneCycleAsync` = `engine.RunAsync(SingleCycle)` (run-loop base lo lặp + CycleCompleted/sản phẩm); InitializeCore nạp sequence (fail→alarm **60005**) + init station theo thứ tự khai báo; **Pause/Resume override** gọi `RequestPause`/`Resume` (dừng GIỮA cycle ở ranh giới bước); Abort→alarm **60006** (mã mới). `BaseMasterController.Pause/ResumeAsync` thành `virtual`.
- **DI** (`ServiceCollectionExtensions.AddDemoSequencing`): SimIoService, 6 station **keyed** theo `StationName`, `KeyedStationResolver` (IStationResolver trên keyed DI — engine không thấy container, tên bắt sai lúc nạp), engine, runtime context, SequenceSource. Gỡ `ProductionRecorder` khỏi máy này (ReportStation ghi record thật — tránh trùng).
- **Dashboard**: mini-log ăn TRỰC TIẾP `StepCompleted`(lỗi/NG)+`ProductCompleted` của engine (một nguồn — ADR 0011 §5); KPI/bảng SP/card KQ vẫn đi đường `IProductionService`.
- **Nút mới — banner Shell**: 3 nút trả lời `OperatorPromptRequired` — **Thử lại · Bỏ qua (Engineer+) · Dừng máy** — thay popup chặn thread; ShellViewModel `PromptRetry/Skip/AbortCommand`, banner co giãn + ẩn ghi chú khi có prompt; +9 key i18n (Shell.Prompt* + Seq.Log*) vi/en/zh.
- Alarm catalog +60005/60006 (vi/en/zh).

### 🧪 4 kịch bản nghiệm thu (`AM.WorkStation.Demo.Tests` — engine+station+SimIoService THẬT trên file sequence THẬT)
- **(a) 20 sản phẩm liên tục**: 20 `ProductCompleted` không NG/Abort, 20 record PASS, SN không trùng, CycleTimeMs>0 → KPI (IProductionService) khớp log (engine events).
- **(b) vacuum fail 100%** (app demo để 30%): bước pick chạy **đúng 2 lần** (1 đầu + retry=1) → `OperatorPromptRequired` (message "Chân không", 3 lựa chọn) → operator **Abort** → SequenceAbortException, 0 record.
- **(c) Pause giữa cycle**: RequestPause khi StepStarted "pick" → dừng ở **ranh giới bước** (vision CHƯA chạy) → Resume → chạy nốt, có record.
- **(d) Stop khi đang giữ hàng**: cancel khi StepStarted "vision" → dừng sạch, sản phẩm **Aborted**, `DO.Vacuum.On` GIỮ true + `DI.Nozzle.VacuumOn` true → Reset+Init **tự thoát liệu sót** (vacuum tắt) → chạy lại 1 sản phẩm PASS sạch.
- +1 test vòng đời ISA-88 master nối engine (Initialize→Start→Pause→Resume→Stop, cycle không đếm sau Stop).

### 🔧 Build & smoke
- **258 test pass** (20 engine + 5 demo + 233 cũ), build **0 warning** (sửa CA1034/CA1716 IoMap→internal, S3358 ternary lồng Dashboard). App boot sạch với DI graph mới (keyed stations + engine + resolver + ctor mới ShellVM/DashboardVM).
- +2 project vào solution (`AM.Core.Sequencing.Tests` S77, `AM.WorkStation.Demo.Tests` S78) → **28 projects**.

### ⏭️ Việc tiếp (tuỳ chọn)
- Vòng review phản biện ADR+engine; đấu ảnh cycle thật vào card KQ khi vision IPC (ADR 0008) xong; giai đoạn 2 sequence (single-step/pipeline/resources/resume-from-crash — lý do hoãn ở ADR 0011 §6).

---

## [Session 77] 2026-07-02 — AM.Core.Sequencing: engine + loader + 20 unit test (Prompt C — theo ADR 0011 đã duyệt)

**Commit:** `4789c51`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Triển khai theo `SequenceEngine_Spec.md` + ADR 0011 (đã duyệt kèm 2 hiệu chỉnh: resume-snapshot do station tự lưu; nhánh Pause chỉ dùng event với kênh trả lời trong args). Phạm vi đúng Prompt C: contracts + loader/validator + engine + events + test. KHÔNG UI, KHÔNG hardware.

### ✅ Thêm mới — project `AM.Core.Sequencing` (net9, standalone: chỉ reference M.E.Logging.Abstractions)
- **Contracts** (`Contracts/`): `IStation`, `StepContext` (required init, Blackboard = ConcurrentDictionary sống 1 cycle), `StationResult`/`StationStatus`/`StepErrorAction` (đúng nguyên văn spec §1), `ProductContext` (thread-safe, NG giữ lý do đầu), `IRecipeView`, `IIoService`/`IMotionService` (HAL theo tên logic — adapter thật để Prompt D), `IStationResolver` (abstraction trên DryIoc keyed — engine không thấy container), `IOperatorPrompt` (cho station init — impl ở D), `IResumeVerifiable` (capability tuỳ chọn), `ISequenceRuntimeContext`.
- **Loader** (`Definition/`): `SequenceLoader` 2 pha (schema qua JsonDocument — bắt được key lạ thành warning; ngữ nghĩa: id trùng, order âm, timeout ≤ 0, retry, **tên station chết NGAY LÚC NẠP** qua `IStationResolver.Contains` + gợi ý `AllNames`), gom TOÀN BỘ lỗi một lần (`SequenceLoadResult` / `LoadOrThrow` → `SequenceValidationException`); `onError=Retry` thiếu `onRetryExhausted` → default `Pause`.
- **Engine** (`Engine/`): `SequenceEngine` — nhóm `order` song song (`Task.WhenAll`), timeout per-step bằng linked CTS (phân biệt Stop bằng exception filter), nhánh Error theo chính sách khai báo (Retry đếm đúng số lần → onRetryExhausted; Pause → `OperatorPromptRequired` với `Respond()` ngay trong args — không chặn thread, không subscriber → Abort an toàn; operator Retry reset đếm), NG không áp onError + bước sau bị bỏ trừ `runOnNg` (vẫn phát sự kiện Skipped để log đủ), pause ở ranh giới bước + **resume-check** (`IResumeVerifiable` — lệch thì giữ Paused + prompt Retry/Abort), Stop = token hủy → sản phẩm dở `IsAborted` + `ProductCompleted` vẫn phát, consumer sự kiện ném lỗi không giết vòng chạy.

### 🧪 Test — project `AM.Core.Sequencing.Tests` (xUnit + FluentAssertions, station = fake thuần)
- **20/20 pass**: đủ 6 case spec §4 (tuần tự theo order · song song cùng order chứng minh bằng barrier chéo · timeout→retry 3 lần→exhausted · Ng bypass trừ runOnNg · pause-ranh-giới-bước + resume · cancel giữa bước dừng sạch + Aborted · station lạ chết lúc nạp) + validator (thiếu timeoutMs, order âm, id trùng, retry=0, key lạ = warning, JSON hỏng, gom 3 lỗi một lần, parse nguyên văn JSON mẫu spec §2) + prompt Skip + resume-check từ chối rồi Retry + blackboard `{stepId}.{field}`.
- **Coverage**: package `AM.Core.Sequencing` **85.5% line / 75.1% branch**; riêng `SequenceEngine` core **92.7% line**.
- Toàn solution: **253 tests pass** (233 cũ + 20 mới), build 0 warning (5 lỗi analyzer trong lúc viết đã sửa: CA1716 `Resume` suppress theo spec, S6667, S3267, S1172, S3458).

### 🔧 File thay đổi
- `AM.Core.Sequencing/` + `AM.Core.Sequencing.Tests/` — MỚI (+ vào `AM.AutoFrame.sln` → 27 projects)
- `docs/design-notes/0011-sequencing-engine.md` — trạng thái ĐÃ DUYỆT + 2 hiệu chỉnh S77 (+index README)

### ⏭️ Việc tiếp (Prompt D — phiên riêng)
- SimIoService + 6 station demo + `recipes/DemoPickPlace.sequence.json` + nối master controller (PackML mapping spec §3) + nối sự kiện engine vào dashboard (bridge — không tạo đường dữ liệu riêng) + 4 kịch bản nghiệm thu tay.

---

## [Session 76] 2026-07-02 — Ẩn danh hoá nguồn tham khảo (viết lại lịch sử) + ADR 0011 AM.Core.Sequencing (chờ duyệt)

**Commit:** `798e6c9` (lịch sử viết lại: S75 gộp thành `8be4ef0`)
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Thực hiện Prompt A + B của kế hoạch Sequencing_NextSteps. Commit S75 đã push chứa tên thật dự án tham khảo trong message + nội dung → phải ẩn danh hoá kể cả lịch sử git, rồi mới thiết kế engine.

### ✅ Prompt A — Ẩn danh hoá (hoàn tất)
1. **CLAUDE.md**: thêm mục "⚡ BẮT BUỘC — Quy ước ẩn danh nguồn tham khảo" (bí danh `RefSeq-A/B/…`; bảng đối chiếu chỉ nằm `docs/private/alias.local.md`; tài liệu điền từ dự án tham khảo không commit; tổng quát hoá dấu vết nhận dạng).
2. **`docs/private/`** (gitignore, xác nhận bằng git status): `alias.local.md` (bảng đối chiếu) + `Sequence_Requirements_RefSeqA.md` — bản requirements S75 đã **tổng quát hoá nội dung**: tên trạm ngôn ngữ gốc → vai trò chức năng tiếng Việt (Bàn chỉnh/Chỉnh chính/Đo kiểm/…), hệ thống upload nội bộ → "MES"/"data-host", đại lượng đo đặc thù → Đ1–Đ4, xoá tên phần mềm/công ty/model + chuỗi thanh ghi nguyên văn; GIỮ NGUYÊN hành vi, bảng 14 anti-pattern + 7 hành vi đáng học. File gốc trong `docs/` đã xoá.
3. **CHANGELOG (S75) + PROJECT_STATUS + CLAUDE.md (bảng tài liệu)**: mọi tham chiếu → `RefSeq-A`, đường dẫn → private "(local, không commit)".
4. **Viết lại lịch sử** (chủ dự án duyệt qua prompt): squash 2 commit đỉnh (S75 + fill-hash) cùng sửa đổi ẩn danh → commit sạch `8be4ef0`, `git push --force-with-lease`. Kiểm tra: `git log --all -S` với mọi tên nhận dạng (tên dự án, tên máy, hệ thống upload, tên trạm ngôn ngữ gốc) = 0 kết quả; grep toàn repo (trừ `docs/private`) = sạch. ⚠ Repo public — commit cũ còn truy cập được qua GitHub cache (HTTP 200); triệt để cần GitHub Support.
5. Ghi chú phạm vi: từ đơn tên 2 nguồn tham khảo HMI cũ còn trong tree các commit S46–S60 (không kèm định danh đầy đủ) — chủ dự án chọn chấp nhận, không rewrite sâu.

### ✅ Prompt B — ADR 0011 (chờ duyệt, CHƯA code)
- `docs/design-notes/0011-sequencing-engine.md` — 7 mục theo yêu cầu: (1) loader/validator 2 pha, bảng lỗi-lúc-nạp vs lúc-chạy; (2) DryIoc keyed qua `IStationResolver` (engine không reference container) + bắt tên station sai NGAY LÚC NẠP; (3) pseudocode vòng lặp sản phẩm — nhóm `order` song song, linked CTS per-step, nhánh onError/retry/onRetryExhausted, Ng + runOnNg, PauseGate ranh giới bước; (4) hành vi học từ RefSeq-A có trích số mục: `IResumeVerifiable` (resume-check), init kiểm liệu sót + `IOperatorPrompt` (thay popup chặn thread); (5) sự kiện một nguồn → ProductionBridge (dashboard ăn đường CycleCompleted cũ) + LogSink (log + persist bước); (6) hoãn giai đoạn 2 (single-step, pipeline >1, resources, resume-from-crash) kèm lý do; (7) bảng 14 anti-pattern → cách tránh từng dòng.

### 🔧 File thay đổi
- `.gitignore` (+`docs/private/`), `CLAUDE.md`, `PROJECT_STATUS.md`, `CHANGELOG.md` (S75 viết lại)
- `docs/Sequence_Requirements_*.md` (bản điền) — XOÁ khỏi repo, chuyển vào private
- `docs/design-notes/0011-sequencing-engine.md` — MỚI (+index README)

### ⏭️ Việc tiếp
- Chủ dự án duyệt ADR 0011 (nhất là §3 pseudocode + §7) → Prompt C (engine + ≥6 test + validator test) → Prompt D (SimIoService + 6 station demo + sequence JSON + nối dashboard + 4 kịch bản nghiệm thu).

---

## [Session 75] 2026-07-02 — Sequence Requirements: khảo sát máy tham khảo RefSeq-A → tài liệu yêu cầu sequence

**Commit:** *(gộp lại ở S76 — xem bên dưới)*
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chuẩn bị thiết kế `AM.Core.Sequencing`. Theo quy trình trong `Sequence_Requirements_Template.md`: mở phiên riêng đọc dự án tham khảo **RefSeq-A** (C# WinForms, framework nội bộ — bí danh theo quy ước ẩn danh trong CLAUDE.md), rút **hành vi và yêu cầu** — không chép code. Sau file này, thiết kế engine KHÔNG mở lại dự án tham khảo.

### ✅ Đã làm
1. **Đọc dự án tham khảo** (chọn lọc): base trạm (thread lifecycle, `CheckContinue`, timeout dialog), manager trạm (start/stop/pause/resume/EMG/reset), lớp mode dispatch (4 run-mode virtual, wait register/TCP), file đăng ký 8 trạm, 8 file trạm (enum bước + handshake bit), guard thao tác tay, trạm upload (MES/data-host/CSV).
2. **Điền requirements RefSeq-A** *(local, không commit — `docs/private/`)* — đủ 10 mục template: thông tin máy (hybrid PC+PLC, 1 sản phẩm/lượt, theo dõi làn tuyến 1/2/3), bảng 8 trạm + vai trò, vòng đời (init phụ thuộc chéo, Z-lên-trước, kiểm liệu sót + hỏi operator), ngữ nghĩa lệnh (Pause giữa bước; **Resume có kiểm tra vị trí trục/xi lanh không đổi**; Stop hủy ngay + `Thread.Abort`; mọi warning mức Error → EMG toàn máy; Reset xóa bit bắt tay + re-init), chính sách lỗi (popup operator chọn — không auto-retry; timeout mặc định 600 s; NG vs lỗi máy phân biệt bằng convention), song song hóa (thread-per-station nhưng tuần tự theo bit; trạm Upload là song song thật duy nhất), traceability (MES lúc xong + data-host sau khi PLC xác nhận trôi + CSV backup trước upload), 4 mode chạy (Normal/DryRun±carrier/Calib/GRR + Simulate), an toàn (cửa = Warn, cắt cứng ở PLC), log (tên bước + duration, persist số bước để resume), **bảng anti-pattern KHÔNG bắt chước** (Thread.Abort, busy-wait, MessageBox trong thread trạm, magic string ngôn ngữ gốc, god-class 3.4k dòng, singleton, bit cứng chéo trạm, switch ~90 bước...) + **7 hành vi đáng học** (resume-check, kiểm liệu sót khi init, guard hình học, persist bước, đo thời gian bước, CSV-trước-upload, simulate auto-pass).
3. **Nhập bộ spec sequence vào `docs/`**: `SequenceEngine_Spec.md` (chuẩn thiết kế IStation/StepContext/sequence JSON/PackML mapping/bất biến/test), `DemoMachine_IO_Map.md` (IO máy mẫu DemoPickPlace + IoMap + SimIoService), `Sequence_Requirements_Template.md` (template trống dùng lại).
4. **CLAUDE.md**: thêm 4 dòng vào bảng tài liệu tham khảo (nhóm **Sequence**).

### 🔧 File thay đổi
- `docs/private/Sequence_Requirements_RefSeqA.md` — MỚI, local không commit (tài liệu chính của session)
- `docs/SequenceEngine_Spec.md`, `docs/DemoMachine_IO_Map.md`, `docs/Sequence_Requirements_Template.md` — MỚI (nhập từ bộ spec ngoài)
- `CLAUDE.md` — bảng tài liệu +4 dòng
- `PROJECT_STATUS.md` — session #75 + TODO thiết kế `AM.Core.Sequencing`

### ⏭️ Việc tiếp
- Thiết kế `AM.Core.Sequencing` (lưu ADR vào `docs/design-notes/`) CHỈ từ `SequenceEngine_Spec.md` + requirements RefSeq-A (local) + `DemoMachine_IO_Map.md` — không mở lại dự án RefSeq-A.

---

## [Session 74] 2026-07-02 — Home v2.1: card "Kết quả gần nhất", empty state, KPI màu-khi-có-nghĩa, quick actions gọn (phản biện ISA-101)

**Commit:** `970f078`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Nhận phản biện 7 điểm cho vùng nội dung Dashboard theo tiêu chí ISA-101 Level 1 ("liếc 3 giây") kèm wireframe. Đánh giá từng điểm, áp 6.5/7, từ chối 1. Chi tiết: **ADR `docs/design-notes/0010-home-content-tinh-chinh-isa101.md`**.

### ✅ Áp dụng
1. **Card "Kết quả gần nhất"** thay dải thumbnail camera: thumb camera gọn 96×62 (chấm trạng thái + tên xám — hết chữ "Sẵn sàng" xanh) + chip KQ OK/NG 22px (xám "—" khi chưa có cycle) + SN/Cycle/Recipe. VM +6 property `Latest*`/`HasLatest` từ record mới nhất trong ca. Live view vẫn ở tab Vision; ảnh cycle thật chờ vision service (ADR 0008).
2. **Bảng sản phẩm**: empty state có hướng dẫn ("Khởi tạo → Chạy…"), cột Cycle căn phải, KQ = chip màu (`DataGridTemplateColumn`, nền Ok/Ng), bộ đếm footer gộp lên góc phải header card.
3. **KPI**: số 17→26px (nhãn 12 mờ trên); Đạt/Lỗi chỉ có màu khi >0 (DataTrigger về xám khi =0); `YieldText` "—" khi chưa có dữ liệu; `AvgCycleText` tự đổi ms→s. Ngưỡng đổi màu yield: hoãn (chưa có ngưỡng cấu hình — R10).
4. **Quick actions**: bỏ dòng lý do lặp trên nút → tooltip (tự ẩn khi rỗng) + icon khoá nhỏ khi thiếu quyền (`NeedsRole`); nhóm lại hàng tiện ích R0 / hàng rủi ro R1; Gọi kỹ thuật = **Andon** viền hổ phách (`IsAndon`).
5. **`Safety.OK`**: "B.thường" → "Không kích hoạt" (en "Not triggered", zh "未触发") — hết viết tắt tối nghĩa trên màn an toàn.
6. **Nhật ký**: thêm empty state (nội dung event đã có sẵn từ S45 — phản biện xem mockup lúc chưa có sự kiện).
7. **Action bar Shell**: divider tách Reset khỏi Dừng (chống bấm nhầm); guard state đã có sẵn (`CanReset` chỉ InitAlarm/RunAlarm).

### ❌ Từ chối
- **Thu rail 560→400-420px**: 560px là quyết định spec v2 (S45) — quick action 3 cột ≥64px + KPI không vỡ khi đổi ngôn ngữ. Xét lại khi sync template v3.

### 🔧 Sửa đổi
- `AM.Modules.Dashboard/DashboardView.xaml` — card KQ gần nhất, empty state ×2, KQ chip, KPI 26px + trigger màu, quick button gọn + lock icon + Andon, counter lên header.
- `AM.Modules.Dashboard/DashboardViewModel.cs` — +`Latest*`/`YieldText`/`AvgCycleText`/`FormatCycle`; reorder quick actions; set `NeedsRole`.
- `AM.Modules.Dashboard/DashboardTileVms.cs` — `QuickActionVm` +`IsAndon`/`NeedsRole`.
- `AM.Application.Shell/MainWindow.xaml` — divider trước Reset.
- `lang/strings.{vi,en,zh}.json` — +4 key (`Dash.EmptyTitle/EmptyHint/NoCycle/LogEmpty`), sửa `Safety.OK`.
- `docs/design-notes/0010-home-content-tinh-chinh-isa101.md` — ADR mới (+index README).

### 🧪 Test & Build
- Build 0 warning · 0 error (sửa 4 lỗi analyzer S125/S1135/S3358 trong comment/ternary mới). Smoke test: app chạy 10s, log sạch ("started successfully"; JsonException UserStore trong log là của lần chạy sáng — bẫy users.json đã biết, tự fallback seed).

### ⏭️ Việc tiếp
- Nâng `HMI_Dashboard_Spec` v2.1 + ghi 3 nguyên tắc (màu-khi-có-nghĩa · empty-state-có-hướng-dẫn · xếp-theo-tần-suất-liếc) vào template — gộp cùng đợt sync template v3 (S73).
- Nối ảnh cycle thật vào card KQ khi vision service IPC (ADR 0008) sẵn sàng.

---

## [Session 73] 2026-07-02 — Shell v3: gộp header+nav 56px, alarm banner co giãn, chip kết nối + popup, kiosk config-driven

**Commit:** `991f34b`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chủ dự án nhận đề xuất Shell v3 (MainWindow.xaml từ phiên thiết kế ngoài) — gộp 7 vùng v2 còn 4 vùng để tăng diện tích content ở 1080p. Nhiệm vụ: đánh giá + tích hợp có hiệu chỉnh. Chi tiết đánh giá/phương án: **ADR `docs/design-notes/0009-shell-v3-header-nav-gop.md`**.

### ✅ Nhận từ đề xuất
- Header + nav gộp 1 hàng 56px: logo (tooltip tên máy) · chip AUTO/LOCAL/state 26px · tab điều hướng RadioButton (gạch chân 3px khi chọn, ScrollViewer + panning ngang) · recipe/clock(MinWidth chống xô)/heartbeat/ngôn ngữ/user.
- Alarm banner `Height=Auto` co giãn 36→52px qua DataTrigger — sạch tốn 36px, có alarm nút ACK vẫn ≥40px (spec §1.8); ghi chú điều hướng tự ẩn khi có alarm.
- Connection bar 40px bỏ hẳn → chip "● Thiết bị n/m · Host n/m" trên action bar + Popup 2 cột (1 `ConnItemTemplate` dùng chung, version ở footer).

### 🔧 Hiệu chỉnh so với đề xuất (4 điểm — xem ADR 0009)
1. **Kiosk KHÔNG hardcode XAML**: config `AutoMachine:KioskMode` (mặc định false — dev không bị nhốt); `Ctrl+Shift+F11` (gate Engineer+, audit log) vào/thoát kiosk lúc chạy vì màn Cài đặt chưa build.
2. **Touch sizing theo Master Index §2.9**: lệnh máy 48→**64px** (`MachineActionButton`, action bar 64→76px); HeaderButton/ConnChip 40→44px. Chrome dọc v2 284px → v3 **168px** (~+116px content).
3. **Fix bug ToggleButton+Popup `StaysOpen=False`** (bấm chip lần 2 popup mở lại ngay): guard timestamp `Popup.Closed` + `ConnChip_Checked` 250ms.
4. Popup bọc ScrollViewer `MaxHeight=460` đề phòng máy nhiều thiết bị.

### 🔧 Sửa đổi
- `AM.Application.Shell/MainWindow.xaml` — Shell v3 4 vùng (56 / Auto 36→52 / * / 76); LoginOverlay theo content row mới (Grid.Row=2).
- `AM.Application.Shell/MainWindow.xaml.cs` — nav `Button`→`RadioButton` (style `NavTabButton`, không set Foreground thủ công — kế thừa trigger; giữ logic keep-tab-khi-rebuild-theo-role); `ApplyKioskMode` + `OnPreviewKeyDown` (Ctrl+Shift+F11); guard popup.
- `AM.Application.Shell/ShellViewModel.cs` — +`DeviceOnlineText`/`HostOnlineText`/`AllConnectionsOk` (`RefreshConnectionSummary` trong ctor + tick 1s cùng chỗ cập nhật chip).
- `AM.Application.Shell/Configuration/AutoMachineOptions.cs` + `appsettings.json` — +`KioskMode` (default false).
- `docs/design-notes/0009-shell-v3-header-nav-gop.md` — ADR mới (+index README).

### 🧪 Test & Build
- `dotnet build AM.Application.Shell`: **0 warning · 0 error**. Smoke test: app chạy 10s không crash, log "AutoMachine Shell started successfully".
- Không key i18n mới (tái dùng toàn bộ `Shell.*` của v2).

### ⏭️ Việc tiếp
- Sync `HMI_UI_Architecture_Template` lên v3 + Master Index §3 (đang mô tả 7 vùng — Shell thực tế đã 4 vùng).
- Khi build màn Cài đặt: thêm nút vào/thoát kiosk (giữ phím tắt làm lối thoát dự phòng).

---

## [Session 72] 2026-06-20 — ADR 0008: tách process Vision (VisionPro FW4.8 + IPC) — net9 không reference Cognex

**Commit:** `b50e22b`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chốt dùng VisionPro (ADR 0007). Bản cài 9.x (.NET Framework 4.x) đặt ở `libs/Vision/Cognex/x64/ReferencedAssemblies`. Chạy 6 spike (app net9 throwaway) để xác định có tích hợp in-process được không.

### 🔬 Phát hiện thực nghiệm
- Managed `Cognex.VisionPro.*` **nạp được** trong net9 (KHÔNG mixed-mode như lo ban đầu; pure-IL x64) + **native interop chạy** (cấp `CogImage8Grey`) khi trỏ native path vào `x64/bin` + resolve managed từ `ReferencedAssemblies`.
- **Nạp `.vpp` (`CogSerializer`) thất bại**: BinaryFormatter (net9 đã gỡ) → shim `System.Runtime.Serialization.Formatters`+cờ được → thiếu `System.Drawing.Common` → thêm được → **SEHException native** (STA không cứu). `.vpp` là cốt lõi QuickBuild ⇒ in-process net9 **không khả thi/không an toàn** (R01).

### ✅ Thêm mới
- `docs/design-notes/0008-vision-process-separation.md` — ADR: chạy VisionPro trong **process .NET Framework 4.8 riêng** (headless host + QuickBuild authoring, nâng WinForms khi cần), trả `VisionResult` + `correlationId` qua **IPC**; main net9 + mọi module **không reference Cognex**. Gồm hợp đồng ranh giới (payload/trigger/camera/vòng đời/transport) + hệ quả (`VisionProProcessor : IVisionProcessor` là IPC client; project `AM.Vision.VisionProHost` net48).

### 🔧 Sửa đổi
- `.gitignore` — ignore `libs/Vision/Cognex/x64|x86/` (toàn bộ ~680 file SDK có license; trước chỉ ignore `*.dll` nên ~525 file non-DLL còn lọt).
- `docs/design-notes/README.md` — index +0008.

### ⏭️ Việc tiếp
- Spike host **net48** nạp `.vpp` thật (round-trip CogSerializer mà net9 fail) để chốt FW4.8 chạy; thiết kế hợp đồng IPC chi tiết + alarm 20xxx; `VisionProProcessor` IPC client. (V3 ROI editor bị thay bằng editor VisionPro; V1/V2 tái dùng làm UI hiển thị kết quả IPC.)

---

## [Session 71] 2026-06-20 — Dọn 7 warning pre-existing ở 2 test project (khôi phục chuẩn 0 warning toàn solution)

**Commit:** `e736919`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Session 70 ghi nhận "còn 7 warning pre-existing S6966/CA2007 trong AM.Services.Tests + AM.Infrastructure.Tests — không thuộc V3" (xem CHANGELOG S70 §Test & Build). Chuẩn dự án là **0 warning toàn solution**; build vẫn pass do test project đặt `TreatWarningsAsErrors=false`, nhưng vẫn phát 7 warning. Phiên này dọn nốt.

### 🔧 Sửa đổi
- `AM.Services.Tests/AM.Services.Tests.csproj` — `<NoWarn>` thêm `CA2007;S6966` (trước chỉ có `CS0067`).
- `AM.Infrastructure.Tests/AM.Infrastructure.Tests.csproj` — `<NoWarn>` thêm `S6966` (đã sẵn `CA2007` → không lặp).

### 🔧 Quyết định (Option B — NoWarn, không sửa code)
- **Theo convention test project sẵn có**: `AM.Hardware.Tests` đã NoWarn `CA2007;CA1707;xUnit1031;S108` và build sạch.
- **4/7 warning là false-positive S6966 trên Moq `Mock<T>.Raise`**: Sonar gợi ý `RaiseAsync`, nhưng `CycleCompleted`/`AlarmRaised` là event `EventHandler<T>` **đồng bộ** (convention CA1003) — `RaiseAsync` dành cho handler `Func<…,Task>`, đổi sang sẽ sai ngữ nghĩa + vỡ test. ⇒ S6966 **buộc** phải NoWarn dù theo hướng nào.
- 3/7 còn lại (`CA2007` ×2 + `S6966` trên `cts.Cancel()`) tuy sửa được dễ, nhưng `CA2007` đã được 2 test project khác NoWarn ⇒ chọn **một** chiến lược nhất quán thay vì trộn hai cách.

### 🧪 Test & Build
- `dotnet build AM.AutoFrame.sln --no-incremental`: **0 warning · 0 error** toàn solution.
- `dotnet test AM.AutoFrame.sln`: **233 pass** (Vision 10 · Architecture 6 · Hardware 38 · Infrastructure 57 · Services 122), 0 fail. Không đổi hành vi — chỉ thêm metadata suppress.

---

## [Session 70] 2026-06-19 — Vision UI V3 (VisionTeachView: ROI editor + ngưỡng + hiệu chuẩn px→mm, gate Engineer)

**Commit:** `cce281e`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Tiếp ADR `docs/design-notes/0007` (roadmap V3). Tab Công cụ từ placeholder → trình *dạy* vision thật. Phát hiện then chốt: hợp đồng phần cứng không có API ROI/threshold/calib + `VisionRecipe` mà roadmap ghi "Save →" thuộc V5 chưa có. Chốt 3 fork với chủ dự án: lưu JSON nhẹ (không kéo VisionRecipe/V5 lên) · ROI kéo-thả trên ảnh · hiệu chuẩn form+lịch sử.

### ✅ Thêm mới
- `AM.Modules.Vision/Teach/` — model JSON nhẹ: `VisionTeachConfig` (CameraId + Rois + Calibration), `VisionRoi` (X/Y/W/H px + Unit + Low/High = bản authoring của `VisionMeasurement`), `CalibrationData` + `CalibrationEntry`, `CalibrationMath` (mm/px thuần, test được), `IVisionTeachStore` + `VisionTeachStore` (JSON một file/camera; static JsonOptions CA1869, SemaphoreSlim, IDisposable).
- `AM.Modules.Vision/VisionTeachViewModel.cs` — VM dạy (R-UI, không System.Windows): ReferenceFrame, Rois, SelectedRoi, calib (KnownMm/PixelDistance/MmPerPixel/CalibHistory), CanEdit; lệnh Capture/AddRoi/DeleteRoi/ApplyCalibration/Save/Reload — lệnh ghi gate Engineer (`EnsureEngineer`).
- `AM.Modules.Vision/VisionRoiVm.cs` — ROI observable (kéo/đổi cỡ + sửa ngưỡng + IsSelected) + map ↔ `VisionRoi`.
- `AM.Modules.Vision/VisionTeachView.xaml` (+`.xaml.cs`) — UserControl phủ toàn vùng: ảnh tham chiếu + Canvas ROI (`Viewbox` Uniform → 1 đơn vị Canvas = 1 px; `Thumb` kéo/đổi cỡ) | cột phải: Chụp/Add/Delete + editor ngưỡng ROI + form hiệu chuẩn + lịch sử + Lưu/Nạp; nút ✕ phát `CloseRequested`.
- `AM.Modules.Vision.Tests/` — **project test mới**: `VisionTeachStoreTests` (round-trip ROI+calib · thiếu file→rỗng · không lẫn camera) + `CalibrationMathTests`. **10 test**.

### 🔧 Sửa đổi
- `AM.Modules.Vision/VisionViewModel.cs` — +`Teach` (VisionTeachViewModel qua DI), +`ShowTeach`/`MainAreaVisible` (tab Công cụ + Engineer → phủ vùng); notify khi đổi `ActiveTab`/`CanEditTool`.
- `AM.Modules.Vision/VisionView.xaml` — ẩn bố cục thường khi dạy (`MainAreaVisible`) + overlay `VisionTeachView` (ColumnSpan 3, hiện theo `ShowTeach`); placeholder tab Công cụ rút gọn còn thông báo cần Engineer (Engineer thấy overlay).
- `AM.Modules.Vision/VisionView.xaml.cs` — wire `TeachPanel.CloseRequested` → `ActiveTab="result"`.
- `AM.Application.Shell/ServiceCollectionExtensions.cs` — DI: `IVisionTeachStore`→`VisionTeachStore("vision-teach")` + `VisionTeachViewModel` (singleton).
- `AM.Application.Shell/lang/strings.{vi,en,zh}.json` — +24 key Vision teach; bỏ `Vision.ToolPending`. 312 key/ngôn ngữ, đồng bộ.
- `AM.AutoFrame.sln` — +`AM.Modules.Vision.Tests`.

### 🔧 Quyết định kiến trúc (chi tiết: ADR 0007 "Quyết định V3")
1. **Save → `VisionTeachConfig` JSON nhẹ** (KHÔNG kéo `VisionRecipe:RecipeBase`/V5 lên) — Save có nghĩa ngay + test round-trip; model+store đặt trong module, V5 promote lên Core sau.
2. **ROI editor = Canvas + `Thumb` trong `Viewbox`** → drag delta đã ở không gian pixel ảnh (không chia scale). **KHÔNG thêm method hợp đồng phần cứng** — authoring thuần, engine vẫn hoãn.
3. **VisionTeachView phủ toàn vùng** + ✕ đóng; gate Engineer 2 lớp (overlay theo `CanEditTool` + lệnh ghi `EnsureEngineer`).

### 🧪 Test & Build
- `AM.Modules.Vision.Tests` +10 (round-trip xác nhận STJ deserialize `IReadOnlyList<T> { get; init; }` OK). **Tổng 233 pass** (Vision 10 · Architecture 6 · Infrastructure 57 · Services 122 · Hardware 38).
- Production **0 warning · 0 error** (TreatWarningsAsErrors + AnalysisMode=All). 3 JSON i18n hợp lệ, đồng bộ key. WPF chưa nghiệm thu trực quan trong Cowork.
- *(Còn 7 warning pre-existing S6966/CA2007 trong AM.Services.Tests + AM.Infrastructure.Tests — không thuộc V3.)*

---

## [Session 69] 2026-06-19 — Vision UI V2 (số đo có cấu trúc + stats ca + trend) + hợp đồng VisionResult.Checks

**Commit:** `6cdfeb4`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Tiếp ADR `docs/design-notes/0007` (roadmap V2) + tài liệu 8 lớp vision. Nâng "hợp đồng" `VisionResult` mang số đo có cấu trúc (Lớp 4 giá trị + Lớp 6 tolerance) — driver/UI/MES đọc chung, không lộ type SDK.

### ✅ Thêm mới
- `AM.Core.Abstractions/Interfaces/Hardware/VisionMeasurement.cs` — record trung lập `(Name, Value, Unit, LowLimit?, HighLimit?, Passed)` + factory `Check()` tự tính `Passed`.
- `AM.Modules.Vision/MeasurementRow.cs` — model dòng đo đã format (ValueText/LimitText/Passed) + `From(VisionMeasurement)`.

### 🔧 Sửa đổi
- `AM.Core.Abstractions/Interfaces/Hardware/VisionResult.cs` — +`IReadOnlyList<VisionMeasurement> Checks` (giữ `Measurements` dict cũ).
- `AM.Hardware.Vision/SimulatedCameraDevice.cs` — `InspectAsync` sinh 3 phép đo Width/Height/Brightness có limit; khi FAIL đẩy Width vượt giới hạn (verdict-từng-phép-đo giải thích NG).
- `AM.Hardware.Vision/SimulatedVisionProcessor.cs` — `RunJobAsync` +1 phép đo EdgeWidth có limit.
- `AM.Modules.Vision/VisionViewModel.cs` — +`ObservableCollection<MeasurementRow> Checks` · running counters `TotalCount/PassCount/FailCount` + `YieldText` · lệnh `ResetStats` · `ApplyResult` map Checks + tăng counters.
- `AM.Modules.Vision/VisionView.xaml` — tab Kết quả: lưới phép đo (viền trái OK xanh/NG đỏ, value tô màu) + 4 thẻ stats (Total/OK/NG/Yield) + nút Đặt lại + dải trend pass/fail (ScrollViewer ngang, mới→cũ); bọc ScrollViewer dọc.
- `AM.Application.Shell/lang/strings.{vi,en,zh}.json` — +8 key Vision (Measurements/SessionStats/ResetStats/StatTotal/StatPass/StatFail/StatYield/Trend).

### 🧪 Test & Build
- `AM.Hardware.Tests` +2: SimCamera Inspect pass → mọi Check trong giới hạn; fail → ít nhất 1 Check ngoài giới hạn. **38 passed** (36→38).
- Shell + Modules.Vision compile **0 warning · 0 error** (verify OutDir tạm — app khóa bin). 3 JSON i18n hợp lệ.

### 🔧 Quyết định kiến trúc
1. **`VisionResult.Checks` = hợp đồng số đo trung lập** (Lớp 4 giá trị + Lớp 6 tolerance) — formatting để ở module (`MeasurementRow`), Core giữ trung lập. SPC/Cpk + xuất báo cáo hoãn sau V5.

---

## [Session 68] 2026-06-18 — Vision UI V1 (camera toolbar + sub-tab Kết quả/Lịch sử/Công cụ) + fix Settings + ADR 0007

**Commit:** `4c01041`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chủ dự án đưa 2 tài liệu tham khảo (mockup `hmi_vision_station_v1_1.html` + skeleton SECPC_Vision Cognex/WinForms) để phản biện + thiết kế phần Vision. Phát hiện thêm bug màn Cài đặt: bấm nút → các sub-view chồng đè nhau.

### ✅ Thêm mới
- `docs/design-notes/0007-vision-module-design.md` — ADR: phản biện 2 tài liệu (mockup va tầng trình bày, SECPC va tầng kiến trúc), chốt layering 4 tầng, adoption lấy/bỏ, mô hình VisionRecipe, mở rộng ILightController, roadmap V1–V5.
- `AM.Modules.Vision/VisionResultRow.cs` — model dòng lịch sử kết quả inspect (time/OK-NG/score).

### 🔧 Sửa đổi
- `AM.Modules.Vision/VisionView.xaml` — camera toolbar (Lớp phủ/Đóng băng/Zoom −,+,Fit) trên ảnh (+ZoomFactor ScaleTransform, crosshair theo OverlayOn); cột phải đổi thành sub-tab **Kết quả / Lịch sử / Công cụ** (light theme, nút ≥44–48px). Tab Công cụ gate Engineer (giải thích thay vì giấu — placeholder VisionTeachView V3).
- `AM.Modules.Vision/VisionViewModel.cs` — +RecentResults (ObservableCollection cap 50, NG đỏ) · +OverlayOn/IsFrozen/ZoomFactor/ZoomText · +ActiveTab/ShowResult/ShowHistory/ShowTool · +CanEditTool (gate qua IUserService.UserChanged, marshal UI) · commands SelectTab/ToggleOverlay/ToggleFreeze/Zoom In·Out·Fit; live-loop bỏ qua grab khi IsFrozen; ctor +IUserService (DI tự inject); Dispose hủy UserChanged.
- `AM.Application.Shell/lang/strings.{vi,en,zh}.json` — +11 key Vision (TabResult/TabHistory/TabTool/Overlay/Freeze/Fit/ColResult/ColScore/HistoryEmpty/ToolEngineerOnly/ToolPending).
- `docs/design-notes/README.md` — index +0007.

### 🐛 Bugs đã fix
- **Màn Cài đặt — sub-view chồng nhau**: `SettingsView.xaml` mỗi sub-view set cả `DataContext={Binding Xxx}` lẫn `Visibility={Binding ShowXxx}` trên cùng element → Visibility resolve theo VM con (không có ShowXxx) → binding fail → mặc định Visible → 3 view (Chẩn đoán/Kỹ thuật/Người dùng) hiện đè nhau. Fix: Visibility bind `DataContext.ShowXxx` qua `RelativeSource AncestorType=UserControl`.

### 🔧 Quyết định kiến trúc
1. **Vision logic ở tầng nào** → tách 4 tầng (không monolith như SECPC): Hardware.Vision (bọc SDK → FrameData/VisionResult) · Modules.Vision (UI) · WorkStation Steps (flow máy) · Services (lưu ảnh/MES). Module Vision = capability camera + teach + hiển thị, KHÔNG ôm flow/IO/PLC.
2. **Mockup HTML**: giữ bố cục ảnh+tab; bỏ chrome/tab-theo-trạm/dark/animation/kích thước desktop (Persistent Frame đã ở Shell). **SECPC**: học năng lực + recipe/light/calib/lưu ảnh; bỏ singleton/Cognex-rò/Thread-Stopwatch/INI-CSV/WinForms.

### 🧪 Build
- `AM.Modules.Vision` + `AM.Application.Shell` (toàn deps): **0 warning · 0 error** (verify qua OutDir tạm — app đang chạy khóa bin). 3 file i18n JSON hợp lệ. WPF chưa nghiệm thu trực quan trong Cowork.

---

## [Session 67] 2026-06-18 — §6.7 Vision live-view (sim trả frame → BitmapSource)

**Commit:** `ca240dc`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Vùng ảnh Vision là placeholder (sim trả `Array.Empty`). Cho sim sinh frame thật + live-view. Quyết định:
converter ở **tầng View** (giữ R-UI — VM không kéo System.Windows); pattern tổng hợp động; toggle Start/Stop. ADR `docs/design-notes/0006`.

### ✅ HAL
- `ICameraDevice` +`GrabFrameAsync` → `FrameData` (giữ `GrabImageAsync` byte[] cho call site cũ).
- `SimulatedCameraDevice`: sinh frame **Bgr24 640×480** (gradient + thanh dọc chạy theo `Environment.TickCount` + thập tâm —
  không Random); `GrabImageAsync` trả `frame.Pixels` (hết rỗng).

### ✅ UI live-view
- `VisionViewModel` (giữ R-UI, chỉ `FrameData`): +`LiveFrame`/`IsLive`/`HasNoFrame` + `ToggleLive` + `LiveLoopAsync` (poll ~10fps,
  marshal UI); `Grab` cũng set LiveFrame.
- `FrameToImageSourceConverter` (View): `FrameData`→`BitmapSource` (map PixelFormat→PixelFormats, `Create`+`Freeze`; format lạ→null).
- `VisionView`: `<Image>` bind LiveFrame qua converter + crosshair overlay + placeholder khi `HasNoFrame`; nút **Live** (toggle). i18n `Vision.Live/StopLive/NoFrame`.

### 🔍 Kết quả
- `dotnet build` → **0 error / 0 warning**. `dotnet test` → **221 passed** (+1 SimCamera GrabFrame). Architecture test xanh (VM giữ R-UI). App khởi động sạch.

---

## [Session 66] 2026-06-18 — §6.6 Settings: Quản lý người dùng (thẻ "Người dùng")

**Commit:** `66461b5`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Có login/RBAC nhưng chưa có UI quản lý user (seed cứng). Chốt phạm vi **chỉ thẻ Người dùng**; mô hình
**Approach A** (mở rộng IUserService CRUD). Xem `docs/design-notes/0005`.

### ✅ Service CRUD (IUserService + UserService)
- +`GetUsers` (UserAccount, không lộ hash) · `CreateUserAsync` · `DeleteUserAsync` · `ResetPasswordAsync` · `SetLevelAsync`;
  `Save()` rút từ SeedDefaults (DRY); hash `Task.Run(BCrypt)` ngoài UI. **Bất biến an toàn TRONG service**: không xoá Admin
  cuối cùng / không hạ quyền Admin cuối / không xoá user đang đăng nhập.

### ✅ UI quản trị (Settings → Người dùng)
- `UserAdminViewModel` + `UserRowVm` + `UserAdminView` (PasswordBox + code-behind, không bind plaintext) trong AM.Modules.Settings;
  liệt kê/thêm/đổi quyền/xoá/reset mật khẩu; gate Administrator (`CanManage`) + audit OK/DENIED mọi mutation; không đủ quyền →
  khối "cần Administrator". Thẻ "Người dùng" bật trong GridMenu (`SettingsViewModel.ShowUsers`). i18n `UserAdmin.*` + `Set.UsersDesc` (vi/en/zh).

### 🔍 Kết quả
- `dotnet build` → **0 error / 0 warning**. `dotnet test` → **220 passed** (+7 user CRUD: create/duplicate/reset/setlevel/delete/last-admin/logged-in). App khởi động sạch.
- Hoãn các thẻ Settings khác (Hiệu chuẩn/Host/Sao lưu/Phần cứng).

---

## [Session 65] 2026-06-18 — §6.5 QuickActions HAL + hold-to-confirm 1s (cửa R1)

**Commit:** `f0fb1aa`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** 5/6 nút Thao tác nhanh mờ "chưa cấu hình HAL". Wire HAL thật + giữ-1s cho cửa R1. Chốt **Approach A**
(Dashboard inject IIoModule, wire trực tiếp) + hold-to-confirm **chỉ R1**. Xem `docs/design-notes/0004`.

### ✅ Wire HAL
- `DashboardViewModel` inject `IIoModule`+`IIoTagMap` (Abstractions — KHÔNG ref AM.Hardware.IO; dùng `ResolveDo/ContainsDo`
  + `WriteDiAsync`). `HasHal` mở rộng cho cả 6 nút: BuzzerOff (light), WorkLight/Ionizer/SafetyDoor/FeedDoor (DO theo io.map),
  CallTech (thông báo). `DispatchQuickActionAsync` toggle DO (đọc `ReadAllDoAsync` → đảo) / tắt còi / op-log; `IsOn` poll DO 2s.
- io.map +`DO_FeedDoor` (Y008).

### ✅ Hold-to-confirm 1s
- `QuickActionVm.HoldMs = Risk>=R1 ? 1000 : 0`. `HoldToConfirm` attached behavior (Dashboard): R1 phải GIỮ đủ 1s mới chạy
  Command (nhả/rời sớm → huỷ); R0 bấm thường. Gắn `dash:HoldToConfirm.DurationMs="{Binding HoldMs}"` lên QuickButton.
- i18n `Dash.QA.Hold`/`CallTechDone` (vi/en/zh).

### 🔍 Kết quả
- `dotnet build` → **0 error / 0 warning**. `dotnet test` → **213 passed** (Architecture test xanh: Dashboard chỉ ref Abstractions). App khởi động sạch.

---

## [Session 64] 2026-06-17 — §6.4 Supervised Override (xác nhận 1 người: 2 bước + đếm ngược)

**Commit:** `3c5d1ca`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chốt §9(a): xác nhận = **1 người (2 bước + đếm ngược)**, không giữ-nút-2s. Mô hình = **Approach A**
(model+config riêng, dùng chung registry). Xem `docs/design-notes/0003`.

### ✅ Model + config + provider
- `OverrideActionDef` (Core: Id/LabelKey/Icon/WarningKey/OverridesGuardKey/CountdownSeconds=3) +
  `IOverrideActionProvider`/`JsonOverrideActionProvider` (Services, fail-safe rỗng) + seed `override-actions.json`.
  Handler HAL **dùng chung `IRecoveryActionRegistry`** (id duy nhất toàn cục).

### ✅ Luồng xác nhận 1 người
- `OverrideViewModel` + `OverrideActionVm` (Motion, embed vào MotionVM): nút **luôn hiện**; gating = Engineer+ & máy STOPPED
  (`guard.Evaluate(R3)` KHÔNG kèm điều kiện — cố ý vượt tầng 3). Luồng: chạm-1 mở card → **đếm ngược** (PeriodicTimer 1s,
  marshal UI) → "Xác nhận" chỉ bật khi **đếm về 0 VÀ đã nhập lý do** → **audit nặng** (`overrides=… reason=…` + LogWarning) +
  chạy HAL; Huỷ/mất quyền/máy chạy → tự đóng + audit. PANE 4 MotionView: list + card xác nhận inline (cảnh báo đỏ + ô lý do).
- Demo: `VacuumReleaseOverride` (nhả khí âm vượt guard). i18n `Override.*` (vi/en/zh).

### 🔍 Kết quả
- `dotnet build` → **0 error / 0 warning**. `dotnet test` → **213 passed** (+2 override provider). App khởi động sạch.
- Hoãn: nhả servo Z (override riêng, cần HAL servo-release); hợp nhất `GuardedActionVm` (nợ kỹ thuật, 0002/0003).

---

## [Session 63] 2026-06-17 — Design-notes infra + §6.3 Thao tác trạm (RecoveryActions, Approach C)

**Commit:** `6c6e501`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chủ dự án yêu cầu (1) hạ tầng lưu kế hoạch + các phương án để dạy tư duy thiết kế, (2) thực hiện §6.3.

### ✅ Hạ tầng "design notes"
- `docs/design-notes/` + `README.md` (quy ước ADR); `0001-am-autoframe-design-decisions.md` (giải thích 12 lựa chọn kiến trúc
  lớn: 3 tầng, interface-first, ISA-88, sim parity, attribute UI, guard 3 tầng, event-push bus, i18n, Force mode, config-driven,
  build cứng, naming — mỗi mục *quyết định/phương án khác/lý do/đánh đổi*); `0002-station-recovery-actions.md` (3 phương án §6.3).
- `CLAUDE.md` +mục "Design notes" (quy ước: nhiều cách → giới thiệu phương án + đánh đổi; lưu plan vào design-notes). Memory đã ghi.

### ✅ §6.3 Thao tác trạm (Approach C — hybrid)
- `RecoveryActionDef` (Core) + `IRecoveryActionProvider`/`IRecoveryActionRegistry` (Abstractions) +
  `JsonRecoveryActionProvider`/`RecoveryActionRegistry` (Services): metadata khai trong `recovery-actions.json`
  (id/labelKey/icon/risk/guard→signal keys/blockKey), handler HAL đăng ký theo id lúc bootstrap.
- `StationOpsViewModel` + `RecoveryActionVm` (Motion): gate qua `guard.Evaluate(risk, GuardCondition)` (tiêu thụ guard tầng 3 +
  bus §S62) + audit OK/DENIED; refresh khi SignalChanged/StateChanged/UserChanged/đổi ngôn ngữ. PANE 3 MotionView: danh sách nút
  có icon/label/chip risk/lý-do (mờ + giải thích khi bị chặn).
- Demo wiring: 3 action (ConveyorToggle, VacuumRelease có handler; **ClampRelease cố tình không** → minh hoạ UI "chưa cấu hình HAL");
  guard dùng `Safety.AllSafe` (live từ §S62). i18n `Recovery.*` + `Manual.Station.*` (vi/en/zh).

### 🔍 Kết quả
- `dotnet build` → **0 error / 0 warning**. `dotnet test` → **211 passed** (+4: registry ×2, provider ×2). App khởi động sạch.
- Nợ kỹ thuật có chủ đích (ghi 0002): QuickActions + RecoveryActions là 2 lớp song song; tương lai trích `GuardedActionVm` dùng chung.

---

## [Session 62] 2026-06-16 — §6.2 HardwareInputEventBus + Guard tầng 3 (hạ tầng)

**Commit:** `cfba162`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Guard engine mới có tầng 1 (state) + tầng 2 (role); tầng 3 (điều kiện phần cứng) là hook. Xây nền
event-push tín hiệu + nối tầng 3 — nền cho thao tác trạm (§6.3) và Supervised Override (§6.4). Chốt: mô hình
điều kiện **boolean** (không DSL), **phạm vi hạ tầng thuần + test** (chưa gắn nút UI).

### ✅ Bus tín hiệu (event-push)
- `IHardwareSignalBus` (Abstractions) + `HardwareSignalBus` (Services): `GetSignal`/`Publish`/`Snapshot` + `SignalChanged`
  (thread-safe; chỉ phát event khi giá trị đổi — không polling). `SignalChangedEventArgs` + hằng `SignalKeys` (Safety.*).
- Adapter `SafetySignalPublisher`: `ISafetyInput` → bus (snapshot ban đầu + theo dõi `SafetyStateChanged`); `Start()` lúc khởi động (App.xaml.cs).

### ✅ Guard tầng 3
- `GuardCondition` (dữ liệu thuần: OR của nhóm AND `SignalRequirement`) + factory `RequireAll`/`RequireAny` + `BlockReason`.
- `IGuardEngine.Evaluate(risk, GuardCondition? = null)` — tham số optional → **mọi call site + 13 test cũ không đổi**.
  `GuardService` +`IHardwareSignalBus?` (optional): thứ tự **state → role → condition**; chưa thoả → `ConditionNotMet` + Reason.
  Không bus / tín hiệu thiếu → coi như chưa đạt (fail-safe). `GuardResult` +`Reason`.

### 🔍 Kết quả
- `dotnet build` → **0 error / 0 warning**. `dotnet test` → **207 passed** (+14: guard tầng-3 ×7, bus ×5, publisher ×2). App khởi động sạch.
- Chưa gắn guard condition vào nút UI nào — để §6.3/§6.4 tiêu thụ.

---

## [Session 61] 2026-06-16 — Giám sát I/O increment C (an toàn): confirm set/reset có hậu quả + alarm "còn IO forced"

**Commit:** `3a776ef`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Phần an toàn còn hoãn của roadmap §6.1 — bổ sung lớp xác nhận cho set/reset có hậu quả và cảnh báo
khi còn ngõ ra bị force.

### ✅ C1 — Chạm-xác-nhận 2 bước cho set/reset có hậu quả
- `IoChannelDescriptor` +`Consequential` (mặc định false); `JsonIoTagMap` ChannelDto +`consequential`; seed `io.map.json`
  gắn cờ `consequential:true` cho Van chân không (Y000), Khóa cửa an toàn (Y002), Băng tải (Y007).
- `IoMonitorViewModel.ToggleOutput`: kênh `Consequential` → chạm-1 arm (tự huỷ 4s), chạm-2 cùng kênh mới ghi
  (dùng chung cơ chế `Arm`/`IsArmed` với Force). Kênh thường vẫn 1 chạm. Hint `Io.ConfirmSet` ("Chạm lần nữa để xác nhận").

### ✅ C2 — Alarm "còn IO forced" (banner toàn app = nhắc gỡ)
- `AlarmCodes.SafetyIoForced = 70010` (dải 70xxx → tự Critical/Safety/ResetRequired qua `AlarmPolicy`) + catalog vi/en/zh.
- `IoMonitorViewModel` inject `IAlarmService`: `ForcedCount` 0→>0 raise (message = bộ đếm), >0→0 clear. Banner persistent
  hiển thị ở mọi màn → chính là "nhắc gỡ trước khi rời màn/chạy máy" (không cần hook navigation mong manh).

### 🔍 Kết quả
- `dotnet build` → **0 error / 0 warning**. `dotnet test` → **193 passed** (test schema mảng bổ sung assert `Consequential`).
- App khởi động sạch. Hoàn tất roadmap §6.1 (Force IO A+B+C).

---

## [Session 60] 2026-06-16 — Nâng màn Giám sát I/O theo mockup (IOMap + danh sách + trạng thái phong phú)

**Commit:** `892ab96`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Màn IO mới hiện "DO0/DI3" + lưới nút, cách mockup `hmi_io_states.html` + `HMI_Naming_and_Axis_Point_Model`
khá xa. Làm increment A (nền IOMap + layout danh sách) + B (trạng thái phong phú + xi lanh).

### ✅ A — IOMap mở rộng + seed + layout danh sách + lọc
- `IoChannelDescriptor` + `IoCylinderDescriptor` (`AM.Core/Models`); `IIoTagMap` +`DiChannels`/`DoChannels`/`Cylinders`/
  `DescribeDi`/`DescribeDo` (metadata địa chỉ/tên đa ngữ/rawName/localize/kind/station/confirmDi).
- `JsonIoTagMap`: nhận **schema mảng mới** (kèm metadata) lẫn **object cũ** (`{Di:{tag:ch}}` — tương thích ngược);
  seed `AM.Application.Shell/io.map.json` (8 DI + 8 DO + 1 xi lanh, có rawName tiếng Trung minh hoạ).
- `IoMonitorViewModel` inject `IIoTagMap`; dựng kênh từ descriptor (fallback DI{n}/DO{n} nếu map rỗng); +`FilterText`
  (lọc address/tên/raw/trạm/tag); tên đổi theo ngôn ngữ (`Loc.Strings.PropertyChanged`).
- `IoMonitorView`: **danh sách 2 cột** mỗi dòng `indicator · address mono · tên (+ rawName) · hint/Gỡ` + ô lọc.

### ✅ B — Trạng thái phong phú + xi lanh
- Enum `IoIndicator {Off,On,Pending,Forced}` + `CylinderState {Clamped,Released,Mid}`.
- Chỉ báo: đèn Off/On · **Pending** (vàng nhấp nháy — DO có `confirmDi` mà giá trị ≠ confirm) · **Forced** (ô vuông đỏ chữ F,
  thay badge S59) · momentary (kind=button → tooltip). Hint động: "đang ON/OFF · bấm…", "bấm = đóng băng", "đang FORCED · bấm gỡ".
- Nhóm **Xi lanh** suy từ cặp DI: kẹp ON→KẸP · nhả ON→NHẢ · cả hai off→**▲ giữa** (nghi kẹt). *(Gom nhóm riêng — lệch nhẹ mockup, rõ hơn.)*
- i18n `Io.Filter*`/`Cylinders`/`Cyl*`/`Hint*` (vi/en/zh).

### 🔍 Kết quả
- `dotnet build` → **0 error / 0 warning** (30 projects). `dotnet test` → **193 passed** (+1 test JsonIoTagMap schema mảng).
- App khởi động sạch; `io.map.json` (array) copy ra bin/Debug. CÒN HOÃN = increment C (confirm set/reset có hậu quả + alarm "còn IO forced").

---

## [Session 59] 2026-06-15 — Tách Force IO thành chế độ riêng (phương án A)

**Commit:** `57616bc`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Set/reset và Force là HAI việc khác bản chất (set/reset = logic vẫn kiểm soát; Force = đóng băng,
cắt logic — quên gỡ là tai nạn kinh điển). S58 gộp mọi write-DO thành "Force=Admin" gây vướng: Engineer không
set/reset thử được. Phương án A: mỗi dòng = nút set/reset thường (Engineer); Force tách thành chế độ riêng (Admin).

### ✅ HAL — `IIoModule` + 2 implementer
- Interface +`ReadAllDoAsync` +`ForceDoAsync(ch,val)` +`UnforceDoAsync(ch)` +`IsDoForced(ch)` +`ForcedOutputs`.
- `SimulatedIoModule` + `AdvantechAdamIoModule` (software-layer, ADAM ghi coil thẳng): kênh đang force thì
  `WriteDiAsync` (kể cả logic máy qua `WriteDoByTagAsync`) **bị bỏ qua** → đúng nghĩa "force cắt quyền logic".
  `WriteAndWaitConfirmAsync` cũng tôn trọng force.

### ✅ UI — `IoMonitorViewModel` + `IoMonitorView`
- **Set/reset thường** (`ToggleOutput`): guard R3 (Engineer + máy dừng), **bỏ check Administrator** → sửa vướng mắc S58.
- **Chế độ Force** (toggle đầu bảng): `IsForceModeAllowed` = Administrator + máy dừng; nền + viền cảnh báo khi bật.
- **Force** (`ForceOutput`): chạm-1 arm (amber, tự huỷ 4s) → chạm-2 cùng kênh đóng băng (`ForceDoAsync`). **Gỡ** (`UnforceOutput`).
- Badge "F" trên kênh forced (mọi chế độ) + bộ đếm `ForcedCountText` "đang FORCE N IO — nhớ gỡ". Poll thêm `ReadAllDoAsync`.
- Mất quyền/máy chạy khi đang Force mode → tự thoát chế độ (force HAL vẫn giữ tới khi gỡ thủ công). Mọi thao tác audit OK/DENIED.
- i18n: thay `Io.Force*` cũ bằng `Io.WriteOk/WriteLockedBusy/WriteNeedRole/ForceMode/ForceModeHint/ForceModeNeedAdmin/ForcedCount/ConfirmFreeze/Unforce` (vi/en/zh).

### 🔍 Kết quả
- `dotnet build` → **0 error / 0 warning** (30 projects). `dotnet test` → **192 passed** (189 + 3 test force HAL).
- App khởi động sạch (log không exception). CÒN HOÃN: per-output confirm cho set/reset, alarm 70xxx "còn IO forced", seed io.map.json tên tag.

---

## [Session 58] 2026-06-15 — Force IO = Admin ở sub-tab Giám sát I/O

**Commit:** `23c4034`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Roadmap §6.1 (`SESSION_HANDOFF.md`) — gate ghi DO qua guard engine, phương án A
(chưa mở rộng HAL force/freeze; trước mắt gate write-DO theo R3 + audit + yêu cầu Administrator).

### ✅ Gate ghi DO theo guard + Admin
- `IoMonitorViewModel` +`IGuardEngine`/`IAuditService`/`IUserService`. Ghi DO = **Force IO (R3)**:
  `guard.Evaluate(R3)` (máy phải dừng + role ≥ Engineer) **+ check `CurrentLevel >= Administrator`** tại call site
  (Force IO = Admin — cao hơn R3, GuardService xử lý riêng tại call site theo thiết kế).
- `ToggleOutput`: bị chặn → `_audit.Record(...DENIED, lý do)` + return, KHÔNG gọi HAL; cho phép → ghi + `_audit.Record(...OK)`.
- Dải khóa: +`IsWriteAllowed` (disable nút DO) + `LockText` (giải thích thay vì giấu): máy chạy → "chỉ xem" ·
  thiếu quyền → "cần Administrator" · đủ → "cho phép ghi — {role}". `RefreshWriteLock()` chạy trong poll loop
  (tránh bẫy cross-thread UserChanged — không subscribe, poll 300ms tự cập nhật theo state/login).
- `IoMonitorView.xaml`: +dải khóa Row trên cùng (bind `LockText`) + nút DO `IsEnabled={IsWriteAllowed}`.
- i18n `Io.ForceOk`/`Io.ForceLockedBusy`/`Io.ForceNeedAdmin` (vi/en/zh).

### 🔍 Kết quả
- `dotnet build` → **0 Error** (30 projects). `dotnet test` → **189 passed**.
- Còn hoãn: "Force mode" thật (force/freeze) cần mở rộng `IIoModule` — roadmap §6.1/§6.2.

---

## [Session 57] 2026-06-14 — Gate Thao tác nhanh (Home) theo risk qua guard engine

**Commit:** `e74e013`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Tiếp guard engine (S56) — gắn vào QuickActions trên Home (checklist §5).

### ✅ Gate QuickActions theo risk
- `QuickActionVm` +`RiskTier Risk`. BuildQuickActions gán: đèn/còi/ion/gọi KT = **R0** (Operator); cửa an toàn/
  cấp liệu = **R1** (LineLead, máy dừng).
- `DashboardViewModel` +`IGuardEngine`/`IAuditService`/`IUserService`. `RefreshQuickActions()`: IsEnabled =
  có-HAL && guard.Allowed && điều-kiện-riêng; SubText ưu tiên: "chưa cấu hình HAL" → "cần quyền {role}" /
  "máy đang chạy" → chú thích. Cập nhật khi UserChanged (login/logout), StateChanged, poll 2s, đổi ngôn ngữ.
- `QuickAction` command: guard check + audit (OK/DENIED) trước khi gọi HAL. "Tắt còi" (ILightController) audit khi tắt.
- i18n `Dash.QA.NeedRole`/`MachineBusy` (vi/en/zh).

### 🔎 Hiệu lực hiện tại
- Chỉ "Tắt còi" có HAL thật → demo rõ: chưa đăng nhập → "cần Operator"; đăng nhập + còi kêu → bấm được + audit.
- 5 nút còn lại vẫn "chưa cấu hình HAL" (chờ wire IO) — nhưng risk đã gán, gate tự áp khi có HAL.

### 🔍 Kết quả
- `dotnet build` → **0 Error** (30 projects). `dotnet test` → **189 passed** (arch test Dashboard chỉ ref abstraction — xanh).

---

## [Session 56] 2026-06-14 — Guard engine phân quyền per-action R0–R3

**Commit:** `a1847b0`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User chốt: R2 cứng ở Engineer + phạm vi = guard engine core + gắn Motion (hoãn Override). Thực hiện
mô hình an toàn theo tầng của `HMI_Manual_Operation_and_Safety`.

### ✅ Core engine (AM.Core + Abstractions + Services)
- `RiskTier { R0, R1, R2, R3 }` + `GuardBlock { None, MachineBusy, InsufficientRole, ConditionNotMet }` (`AM.Core/Enums`).
- `GuardResult(Allowed, Block, RequiredLevel)` (`AM.Core/Models`).
- `IGuardEngine`/`GuardService`: `Evaluate(risk)` kiểm **trạng thái máy → role**; map R0=Operator·R1=LineLead·
  R2=Engineer·R3=Engineer (Force IO=Admin xử riêng). R0 chạy được cả khi máy chạy; R1+ cần máy dừng.
- `IAuditService`/`AuditService`: ghi `[AUDIT] user/action/result/detail` qua Serilog (§9.6).
- DI: đăng ký `IGuardEngine`, `IAuditService` (singleton).

### ✅ Gắn vào màn điều khiển trục (`MotionViewModel`)
- `IsAdjustAllowed` + dải khóa tính qua `_guard.Evaluate(R2)` → LineLead/Operator/chưa-đăng-nhập thấy tab Vận hành
  tay nhưng "Điều khiển trục" KHÓA kèm lý do ("🔒 Cần quyền Engineer"). Engineer/Admin mở khi máy dừng.
- Mọi lệnh trục qua `RunGuardedAsync(risk, action, body)` (defense-in-depth §9.1): jog/servo/teach=R3,
  move/home/clear/goto/homeAll/clearAll=R2 → guard chặn thì báo lý do + audit DENIED, KHÔNG gọi HAL; cho phép thì
  audit OK rồi chạy. **STOP không gate** (an toàn — luôn dừng được). i18n `Manual.NeedRole` (vi/en/zh).
- Test mới: `GuardServiceTests` (13) — ma trận role × tier × state.

### 🔍 Kết quả
- `dotnet build` → **0 Error** (30 projects). `dotnet test` → **189 passed** (+13).
- **Còn hoãn** (Master Index §11C): Supervised Override (2 bước+đếm ngược, §9.1 chưa chốt 1/2 người), thao tác trạm
  R0–R1 (RecoveryActions config), guard condition phần cứng (`HardwareInputEventBus`), gate QuickActions theo risk,
  Force IO = Admin tại IoMonitor.

---

## [Session 55] 2026-06-14 — Sửa 3 bug đăng nhập / phân quyền

**Commit:** `4212bad`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User báo: (1) hiện "Lỗi đăng nhập — xem log" dù đã login; (2) muốn đăng xuất nhanh; (3) admin
không thấy tab Vận hành tay + header hiện sai "admin · Engineer". Log (`automachine-20260614_003.log:13290`)
chỉ đúng `InvalidOperationException: calling thread cannot access this object` tại `MainWindow` handler UserChanged.

### 🐞 Nguyên nhân gốc
- **Cross-thread** (gây #1, #2, #3-nav): `UserService.LoginAsync` dùng `Task.Run(BCrypt.Verify).ConfigureAwait(false)`
  → `UserChanged?.Invoke` bắn trên thread nền. Handler `MainWindow` (đóng overlay + `BuildNavigation`) đụng UI từ
  thread nền → ném. Multicast delegate dừng tại đó → `IdentityViewModel.ApplyState` không chạy (overlay kẹt form
  login, không hiện Đăng xuất); nav không rebuild (mất Vận hành tay dù admin đủ quyền).
- **Store cũ** (gây #3-cấp): `users.json` lưu `Level` INT từ trước khi reorder enum ở S47 (admin=2). Sau S47:
  2=Engineer → admin đọc nhầm thành Engineer; không có user `linelead`.

### ✅ Fix A — marshal về UI thread (`MainWindow.xaml.cs`)
- Bọc handler `UserChanged` trong `Dispatcher.Invoke(...)`. → overlay tự đóng khi login, nav rebuild, panel
  "đã đăng nhập" + nút Đăng xuất hiện đúng. (#1, #2, #3-nav)

### ✅ Fix B — user store bền vững + tự migrate (`UserService.cs`)
- `JsonOptions` +`JsonStringEnumConverter` → Level lưu dạng tên enum (reorder sau này không phá nghĩa).
- Store envelope `{ schemaVersion, users }` (record `UserStore`); `Load()` re-seed nếu file là mảng cũ HOẶC
  `schemaVersion` < hiện tại. File cũ tự re-seed đúng cấp (admin=Administrator) + thêm `linelead`. Xác nhận runtime:
  `users.json` re-seed thành envelope v2, Level chuỗi.
- Test mới (2): `OldArrayFormatStore_ReSeeds_WithCorrectAdminLevel` + `_AddsLineLeadUser`.

### ✅ Fix C — đăng xuất nhanh (#2)
- User chốt: dùng overlay có nút Đăng xuất (đã có trong `IdentityView`). Sau Fix A, bấm User → panel đã-đăng-nhập +
  Đăng xuất hoạt động; logout → nav rebuild ẩn lại Vận hành tay.

### 🔍 Kết quả
- `dotnet build` → **0 Error**. `dotnet test` → **176 passed** (+2). Runtime: hết exception cross-thread, store re-seed đúng.

---

## [Session 52–54] 2026-06-14 — Hoàn thiện checklist: Settings GridMenu · role-gating nav · module Vision

**Commit:** S52 `ff3b2e5` · S53 `c13f817` · S54 `b81fef9`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User "làm lần lượt đi" — xử lý 3 mục checklist còn lại.

### ✅ S52 — Cài đặt kiểu GridMenu (`ff3b2e5`)
- `SettingsView`/`SettingsViewModel` đổi từ 2 sub-tab phẳng → **landing lưới thẻ** (Section=null) → mở thẻ + nút Back.
  Thẻ có chức năng: Chẩn đoán · Kỹ thuật · Giới thiệu (version app/.NET/OS). Placeholder mờ "đang phát triển":
  Phần cứng · Hiệu chuẩn · Người dùng · Host · Sao lưu. i18n `Set.*` (vi/en/zh).

### ✅ S53 — Ẩn tab theo role (`c13f817`)
- `ModuleNavigationAttribute` +`minLevel` (mặc định `UserLevel.Null` = mọi người). `MotionView` (Vận hành tay) =
  `minLevel: LineLead`. `NavigationEntry` +`MinLevel`; `NavigationBuilder` đọc; `MainWindow.BuildNavigation` lọc
  `CurrentLevel >= MinLevel`. **Rebuild nav khi `IUserService.UserChanged`** (login/logout): giữ tab đang xem nếu
  còn quyền, else về Home (`_currentViewType`). ⚠ Chưa login (Null < LineLead) ⇒ Vận hành tay ẩn (đúng spec).

### ✅ S54 — Module Vision (project thứ 30)
- `AM.Modules.Vision` (`VisionView`/`VisionViewModel`) bám `ICameraDevice` interface-only: trạng thái kết nối +
  DeviceName, nút **Grab/Inspect/Light/Calibrate** (`RunSafeAsync` bọc alarm/cancel/lỗi), **kết quả inspect**
  (PASS/NG + score + X/Y/θ + job + giờ). Vùng ảnh = **placeholder** (sim trả `Array.Empty<byte>()` — live-view
  cần vision service thật). `[ModuleNavigation("Nav.Vision", icon:"vision"(E722 Camera), order:18)]` (Operator+).
- sln add + Shell ref + đăng ký `VisionViewModel` + i18n `Vision.*`.

### 🔍 Kết quả
- `dotnet build` → **0 Error** (30 projects). `dotnet test` → **174 passed**.
- **Còn hoãn**: per-action R0–R3 (jog=EN, station ops=LL, override=EN, force=AD) cần guard engine +
  HardwareInputEventBus + chốt §9 (Master Index §11C). Cài đặt GridMenu mở rộng (Phần cứng/Calib/User/Host/Backup).

---

## [Session 51] 2026-06-14 — Login overlay dialog (thay trang riêng)

**Commit:** `13540fc`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User hỏi login nên là trang riêng hay khung hiện ra khi bấm. Khuyến nghị + user chốt: **overlay dialog**
(checklist §2: login = dialog phiên, không phải màn điều hướng; SEMI E95: dialog không che alarm bar/nav).

### ✅ Login overlay
- `MainWindow.xaml`: thêm `LoginOverlay` trong **Grid.Row=3** (chỉ phủ vùng content → header/nav/alarm/action bar/
  conn bar vẫn thấy + dùng được). Card 400px nổi giữa + nền mờ `#66000000`; thanh tiêu đề + nút ✕.
- `MainWindow.xaml.cs`: nút User mở overlay (host `IdentityView` cache, DataContext từ DI); bấm nền mờ/✕ → đóng;
  bấm card nuốt event (không đóng nhầm); **đăng nhập thành công tự đóng** (sub `IUserService.UserChanged`, User≠null).
- `IdentityView.xaml` làm gọn thành **form** (bỏ tiêu đề trang + nền màn + khung 360 — overlay tự đóng khung),
  giữ code-behind PasswordBox. Không còn dùng kiểu "trang chiếm content".

### 🔍 Kết quả
- `dotnet build` → **0 Error** (29 projects). `dotnet test` → **174 passed**.

---

## [Session 50] 2026-06-14 — Gom Chẩn đoán + Kỹ thuật vào "Cài đặt" (AM.Modules.Settings)

**Commit:** `a7233c5`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User: gom Chẩn đoán + Kỹ thuật vào Cài đặt (checklist: 2 màn này thuộc "Cài đặt", không phải nav chính).

### ✅ Module mới AM.Modules.Settings (project thứ 29)
- `SettingsView`/`SettingsViewModel`: container gom **Chẩn đoán + Kỹ thuật** làm sub-tab (host `DiagnosticsView` +
  `EngineeringView`, VM con inject từ DI — mẫu giống Vận hành tay host IoMonitor). `[ModuleNavigation("Nav.Settings",
  icon:"settings", order:95)]`.
- Bỏ `[ModuleNavigation]` ở `DiagnosticsView` + `EngineeringView` → không còn tab riêng.
- `dotnet sln add` + Shell ref + đăng ký `SettingsViewModel` (AddUiViewModels) + icon "settings" (gear E713) + i18n
  `Nav.Settings` (vi/en/zh).
- Nav 8 → **7 tab**: Home·Sản xuất·Cảnh báo·Vận hành tay·Recipe·Nhật ký·**Cài đặt**.

### 🔎 Còn thiếu so với checklist (ghi nhận, làm sau)
- "Cài đặt" theo spec là **GridMenuView** (Phần cứng, Hiệu chuẩn, User, Host GEM/MES, Backup, Giới thiệu) — hiện chỉ
  2 sub-tab; mở rộng dần. Thiếu tab **Vision** (chưa có module camera).

### 🔍 Kết quả
- `dotnet build` → **0 Error** (29 projects). `dotnet test` → **174 passed**.

---

## [Session 49] 2026-06-14 — Bỏ nút trùng chức năng (login, I/O) theo HMI_Home_Buttons_Checklist

**Commit:** `036e6c6`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User thấy nav có nút trùng chức năng (login, io) + gửi `HMI_Home_Buttons_Checklist_v1.0` + Master Index
(bản cập nhật có dòng checklist). Nguyên tắc: "một lệnh một chỗ"; login/ngôn ngữ là nút mép phải (KHÔNG tab);
IO gộp vào Vận hành tay.

### ✅ Bỏ trùng theo checklist
- **Login**: bỏ `[ModuleNavigation]` ở `IdentityView` → KHÔNG còn tab "Tài khoản". Nút **User ở header** là lối
  duy nhất (`MainWindow.ShowStandaloneView` hiện Identity trong content, bỏ chọn tab active). Khớp checklist §2.
- **I/O**: bỏ `[ModuleNavigation]` ở `IoMonitorView` → KHÔNG còn tab "Giám sát I/O" riêng. Nhúng làm **sub-tab
  "Giám sát I/O"** trong Vận hành tay (Motion ref IoMonitor; `MotionViewModel` inject `IoMonitorViewModel`,
  expose `IoMonitor`; `MotionView` host `IoMonitorView`). Sub-tab Vận hành tay: Trục · Điểm · **I/O** · Thao tác trạm · Override.
- Nav từ 10 → **8 tab**: Home · Sản xuất · Cảnh báo · Vận hành tay · Recipe · Chẩn đoán · Nhật ký · Kỹ thuật.

### 🔎 Phản biện (giữ nguyên, chưa sửa — báo user quyết)
- **Chẩn đoán (Diagnostics) + Kỹ thuật (Engineering)** cũng KHÔNG thuộc nav chuẩn (checklist: nằm trong "Cài đặt").
  Chưa có module "Cài đặt"/GridMenu → GIỮ tạm làm tab, gom vào Cài đặt ở phiên sau (tránh orphan).
- **Recipe ở header** (nút → tab Recipe) là shortcut theo Template v2 §3.1 (KHÔNG phải lỗi trùng) — giữ.
- **Action bar "Manual"** cũng mở Vận hành tay (checklist §6) = lối tắt có chủ đích — giữ (đang disabled).
- IO-write (toggle DO) trong sub-tab I/O CHƯA gate theo `IsAdjustAllowed`/role — chờ guard engine (giữ hành vi cũ).

### 🔍 Kết quả
- `dotnet build` → **0 Error** (28 projects). `dotnet test` → **174 passed**. Arch test xanh (Motion→IoMonitor hợp lệ).

---

## [Session 48] 2026-06-14 — Nav "Chuyển động" → "Vận hành tay" (dải khóa trạng thái + sub-tab)

**Commit:** `ff3adec`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User chốt lần lượt từng yêu cầu trên giao diện đang chạy. Mục đầu: đổi nav sang "Vận hành tay"
theo `HMI_Manual_Operation_and_Safety` (gộp Manual+Motion/IO).

### ✅ AM.Modules.Motion → màn Vận hành tay
- Nav: `[ModuleNavigation("Nav.ManualOp", icon:"manual", order:40)]` (icon MDL2 TouchPointer). Nhãn "Vận hành tay".
- **Dải khóa trạng thái** (§1.3): `IsAdjustAllowed = State ∉ {Running, Initializing, Resetting}` — máy chạy →
  băng xám "🔒 chỉ xem, điều chỉnh đã khóa" + khóa khu điều khiển trục/jog/bảng điểm; máy dừng → băng xanh
  "✏ Cho phép điều chỉnh — {role}". Bind `IMasterController.StateChanged` + `IUserService.UserChanged` (push).
- **Sub-tab**: Điều khiển trục · Bảng điểm (tách từ màn S46, dùng lại nguyên) · **Thao tác trạm** (empty-state,
  R0–R1, chờ guard engine) · **⚠ Override** (empty-state, chờ chốt §9 + guard engine). Bảng đèn 8 tín hiệu +
  panel phản hồi GIỮ luôn đọc được kể cả khi khóa (monitor sống — §7.2).
- `MotionViewModel` +`IMasterController` +`IUserService`; +`SubTabIndex`/`SelectSubTab`; +`IndexToVisibilityConverter`.
- i18n vi/en/zh: +`Nav.ManualOp`, `Manual.*` (dải khóa, sub-tab, empty-state).
- Phân quyền per-action R0–R3 + Thao tác trạm/Override thật: HOÃN cùng guard engine (Master Index §11C).

### 🔍 Kết quả
- `dotnet build` → **0 Error** (28 projects). `dotnet test` → **174 passed**. (App đang chạy đã xác nhận render
  bằng ảnh chụp của user — Home + màn trục đúng layout.)

---

## [Session 47] 2026-06-14 — Tích hợp bộ tài liệu HMI mới + phản biện + mô hình 4 role

**Commit:** `a07ac23`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User gửi bộ `file260614s` (10 file): Master Index, Manual Operation & Safety, Naming/Button/UI v2.0
+ 5 mockup (home_v2, manual_operation, axis_detail, io_states, adaptive_layout). Yêu cầu: phản biện nội dung
chưa phù hợp → sửa tài liệu → sửa code.

### ✅ Phản biện + adoption (tập trung `docs/HMI_Master_Index.md §11`)
- **A. Đã có**: palette v2/7 vùng/Home/màn trục/4 role.
- **B. Map không đổi core**: PackML→ISA-88 8 trạng thái; MDI→Segoe MDL2; chốt header 64px, conn bar 40px.
- **C. Hoãn có chủ đích**: `HardwareInputEventBus` (chưa có), guard engine + Supervised Override (chính sách §9
  chưa chốt), IO Force mode (HAL chưa expose freeze), adaptive layout, IO actuatorGroup/rawName.
  → **Lượt này KHÔNG build màn Vận hành tay** (an toàn-trọng yếu, phụ thuộc hạ tầng + quyết định chủ dự án).
- **D. Mâu thuẫn nội bộ tài liệu**: header 48/56/64px, conn bar 32/40px, tham chiếu treo (Calibration doc/mockup
  chưa giao), §9 "chưa chốt" vs §8 ví dụ cứng — đã liệt kê để bản sau sửa.
- **E. Phản biện thiết kế**: SuperUser (tầng 5 OEM ngoài 4 role) cần ghi rõ; màn Motion S46 nên thành sub-tab
  của Vận hành tay khi dựng; QuickAction/RecoveryAction nên chung kiểu `GuardedAction`.

### ✅ Tài liệu
- Mới trong `docs/`: `HMI_Master_Index.md` (+§11 adoption), `HMI_Manual_Operation_and_Safety_v1.0.md` + 5 mockup HTML.
- Cập nhật bản v2.0: Naming/Button/UI_Architecture_v2 (bản đầy đủ hơn) + con trỏ adoption về Master Index §11.
- `CLAUDE.md`: thứ tự đọc HMI (Master Index đọc TRƯỚC) + bảng role 4 cấp + ghi chú breaking change enum.

### ✅ Code — mô hình 4 role (nền cho guard/RBAC sau)
- `UserLevel`: thêm **`LineLead=1`** (R1 phục hồi có guard, giữa Operator–Engineer); Engineer 1→2, Admin 2→3,
  SuperUser 3→4. Mọi RBAC dùng tên enum (`>= UserLevel.X`), KHÔNG hardcode int → không nơi nào vỡ.
- `UserService`: seed user `linelead/linelead123`. `IdentityViewModel`: nhãn "Line Lead".
- Test mới (2): `Login_WithSeededLineLead_HasR1NotR2`, `RoleOrdering_*` — khoá thứ tự 4 role + SuperUser.

### 🔍 Kết quả
- `dotnet build` → **0 Error** (28 projects). `dotnet test` → **174 passed** (+2). Enum shift non-breaking (xác nhận
  bằng grep: không có so sánh int; tests RBAC dùng tên enum).

---

## [Session 46] 2026-06-13 — Màn điều khiển trục (bảng đèn 8 tín hiệu, jog/inching, điểm Set/Confirm)

**Commit:** `e8eead2`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User gửi `HMI_Naming_and_Axis_Point_Model_v1.0` + mockup `hmi_axis_detail_v1_2.html` (tổng hợp
phần mềm điều khiển máy tham khảo). Yêu cầu: phản biện điểm bất hợp lý → xây màn điều khiển trục theo mẫu.

### ✅ Phản biện + adoption (ghi `docs/HMI_Naming_and_Axis_Point_Model.md §7`)
- **Lấy**: bảng đèn 8 tín hiệu/trục, Set/Confirm, tên có nghĩa, 2-chạm, jog+inching, nhóm trục, Clear Error riêng.
- **Non-breaking**: Servo/8-tín-hiệu/phản-hồi KHÔNG nhét vào `IMotionController` (sẽ phá Gts/Advantech P/Invoke)
  → tách interface **tuỳ chọn** `IAxisDiagnostics` (tiền lệ `ISafetyInput`); UI cast runtime, driver chưa hỗ trợ thì ẩn.
- **Hoãn có chủ đích**: deadman "giữ-để-chạy" liên tục cần `IAxisJog` velocity-mode (interface chỉ có MoveAbs/Rel/Stop)
  → mỗi lần bấm = nhích một bước inching (an toàn, không dựng vòng MoveRel rủi ro). Tên IO 4 lớp/IOMap → màn Cài đặt riêng.

### ✅ AM.Core / Abstractions / Hardware (non-breaking)
- Mới: `AxisSignals` (8 tín hiệu) + `AxisFeedback` (following error/velocity/torque/load) + `IAxisDiagnostics`.
- `MotionPoint` +`SetPositions` (additive — Set/Confirm; `Positions`=confirm). PointTable test cũ vẫn xanh.
- `SimulatedMotionController` implement `IAxisDiagnostics` (+mảng servo/alarm; Clear xoá alarm; tín hiệu suy từ
  homed/moving). Move/Home KHÔNG gate servo → 27 hardware-test cũ không đổi.
- Test mới `SimulatedAxisDiagnosticsTests` (4): implements interface · servo toggle · home→origin/zero/inpos · feedback.

### ✅ AM.Modules.Motion — viết lại theo mockup (palette v2, ISA-101 phẳng)
- `AxisVm`: +8 tín hiệu + servo + SpeedPercent + IsSelected. `PointRowVm`/`PointCellVm` mới (Set/Confirm/▲delta 0.05mm).
- `MotionViewModel`: cast `IAxisDiagnostics`; poll vị trí+tín hiệu+phản hồi; servo/home/clear/move từng trục;
  HomeAll/ClearAllErrors; nhóm trục (lô 4: XYZU…); jog pad (mode Tương đối/Tuyệt đối, STOP dừng mọi trục) + inching
  3 mức/tuỳ ý/nudge; bảng điểm 2-chạm (chọn ô=1 trục, chọn tên=cả điểm → Tới/Teach), teach 1 trục gate Servo+Home,
  Lưu recipe. +`NullToVisibilityConverter`.
- `MotionView.xaml`: trái (bảng đèn 8 cột + điều khiển từng trục) · phải (jog pad + bước + phản hồi servo) · dưới
  (bảng điểm Set/Confirm + thanh chọn Tới/Teach/Lưu). i18n vi/en/zh +37 key `Axis.*`.

### 🔍 Kết quả
- `dotnet build` → **0 Error** (28 projects). `dotnet test` → **172 passed** (+4 so với S45).

---

## [Session 45] 2026-06-12 — Shell + Home theo spec HMI v2.0 (7 vùng, mockup hmi_home_v2)

**Commit:** `ae9a822`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User gửi 3 tài liệu mới từ chat "Giao diện điều khiển tự động hoá cho máy IPC 24 inch":
`hmi_home_v2.html` (mockup) + `HMI_UI_Architecture_Template_v2.0.md` + `HMI_Button_Spec_v2.0.md`.
Yêu cầu: phản biện nội dung không phù hợp → cập nhật docs/claude → sửa UI theo mockup.

### ✅ Phản biện + adoption (ghi tại `docs/HMI_UI_Architecture_Template_v2.md` §9)
- **Giữ ISA-88 8 trạng thái** (không đổi sang PackML/Stateless — việc tầng máy, 55 tests đang xanh).
- **Segoe MDL2** thay Material Design Icons (sẵn trên Windows, không thêm package).
- Hoãn có chủ đích: LOCAL/REMOTE+GEM popup, tiến độ lô (MES), Stop popup 2 lựa chọn, Start pre-check
  popup, Manual overlay, QuickActions HoldToConfirm+audit, UiScale, billboard, heartbeat >3s.
- Bắt mâu thuẫn nội bộ spec: ACK 32px (mockup) vs ≥40px (§1.8) → áp 40; legend conn bar → tooltip.

### ✅ Docs / .claude
- Mới: `docs/HMI_UI_Architecture_Template_v2.md` (spec gốc + §9 adoption), `docs/HMI_Button_Spec.md`.
- `docs/HMI_Dashboard_Spec.md` → v2.0 (work area + right rail). `CLAUDE.md` đổi thứ tự đọc UI.
- `.claude/skills/am-hmi-design/SKILL.md`: layout 7 vùng, palette v2, kích thước chạm theo mm,
  nguyên tắc "một lệnh một chỗ"/"mờ + lý do, không ẩn", banner multi-alarm, conn bar ●▲✕○.

### ✅ AM.Application.Shell — Shell v2
- `App.xaml`: toàn bộ token đổi sang **palette v2** (#DCDCDC nền, OK #1E7E46, NG #C0392B, info #1565C0,
  warn #B26A00...) — GIỮ TÊN token nên 10 module khác không phải sửa. +Ok/Ng/Info/Warn.BackgroundBrush.
- `MainWindow.xaml`: 7 vùng — header 64 (logo + badge AUTO/DRY · LOCAL · state viền màu + recipe→tab
  + clock + **heartbeat 1Hz** + ngôn ngữ + user→tab Identity), nav tab ngang 48, **alarm banner 48**
  (xám/hổ phách/đỏ theo mức, ACK 40px, chip "+N cảnh báo khác"), action bar 84 (**nút trắng phẳng 64px
  icon MDL2 trên + nhãn dưới**, chỉ Start viền xanh, Pause/Resume 1 nút, Dry run; Manual mờ + lý do),
  connection bar 40 (**Thiết bị│Host** + version, chú giải ở tooltip).
- `ShellViewModel`: banner multi-alarm (`Level` desc → `RaisedAt` desc, chỉ alarm CHƯA ACK; ACK xong
  alarm kế trồi lên), PauseResume 1 lệnh, ToggleDryRun, VersionText, tách Device/HostConnections.

### ✅ AM.Modules.Dashboard — Home v2
- Work area: sub-tab "Sản phẩm" + thumbnail camera (nền tối #1F1F1F) + **bảng truy vết SN**
  (RowHeight 40, cột SN·Vào·Cycle·Data trạm·Recipe·KQ, CHỈ dòng NG tô #F9E6E3) + footer đếm.
- Right rail 560px: KPI ca 8h (3×2) → **Thao tác nhanh** (6 nút 64px icon-trên; "Tắt còi" wired
  `ILightController.SetAsync(Buzzer=false)` chỉ enable khi còi kêu, KHÔNG ACK alarm; 5 nút còn lại
  mờ + "chưa cấu hình HAL") → Trạm & an toàn (2 cột, ISafetyInput event push) → Nhật ký 1 dòng/entry.
- `DashboardTileVms`: +`QuickActionVm` (nhận hex MDL2, convert runtime — source không chứa PUA),
  `RecordRowVm` +cột DataText (vision score/lý do NG). Bỏ time-block + banner mất kết nối (theo v2).
- i18n vi/en/zh: +52 key (badge, banner, quick actions, safety, log...).

### 🔍 Kết quả
- `dotnet build` → **0 Error** (28 projects; 7 warning có sẵn ở test projects). `dotnet test` → **168 passed**.

---

## [Session 44] 2026-06-11 — Dashboard L1 theo chuẩn HMI IPC 24" + HMI_Dashboard_Spec.md

**Commit:** `87f3607`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User yêu cầu sửa màn hình chính theo hướng "Giao diện điều khiển tự động hoá cho máy IPC 24 inch"
(ISA-101/SEMI E95 — đã chuẩn hoá trong `am-hmi-design` + `HMI_Components_Catalog.md` §1) + tạo tài liệu đi kèm.
Gap: Dashboard cũ chỉ có state + nút + bảng alarm — thiếu KPI sản xuất, station tiles, cảnh báo kết nối.

### ✅ AM.Modules.Dashboard — nâng cấp thành L1 overview đúng catalog §1
- `DashboardView.xaml` viết lại: 5 hàng — state banner (22pt + chu kỳ/alarm) → **KPI sản xuất 1h**
  (Total/OK/NG/Yield/UPH/CycleTB, 6 tile 28pt) → **banner đỏ mất kết nối** (chỉ hiện khi có thiết bị rớt)
  → lưới 2 cột (**station tiles** + alarm DataGrid | **connection panel** màu+chữ ✓/✕ an toàn mù màu)
  → hàng nút nhanh **60px** (SEMI S8; Stop cách 48px). Nội dung MaxWidth **1400px** — không giãn hết 1920.
- `DashboardViewModel`: +`IServiceScopeFactory`→`IProductionService` (KPI, scope/lần query — Scoped EF),
  +`IHardwareManagerService` (chips, poll 2s qua `PeriodicTimer` — giữ R-UI-01 không System.Windows),
  +station tiles từ `IMasterController.Stations` (sub `StateChanged` từng station), KPI refresh khi
  `CycleCompleted` + mỗi 10s; đổi ngôn ngữ cập nhật cả nhãn state trong tile + status chip.
- Mới: `DashboardTileVms.cs` (`StationTileVm`, `DeviceChipVm`); csproj +`Microsoft.Extensions.DependencyInjection.Abstractions`.
- i18n vi/en/zh: +`Dash.Production/Stations/Connections/ConnLost/Mech`, `Conn.OK/Lost`.

### ✅ Tài liệu
- **Mới `docs/HMI_Dashboard_Spec.md`**: spec màn hình chính L1 — layout, phân công Shell↔Dashboard,
  bảng thành phần↔interface, quy tắc màu/i18n/SEMI S8, **checklist nghiệm thu**, ghi rõ "đổi máy = không sửa XAML".
- `CLAUDE.md`: thêm doc vào bảng tham khảo + thứ tự đọc khi chạm Dashboard. PROJECT_STATUS cập nhật
  (kèm vá phần TODO cuối file bị cụt ký tự từ session trước).

### 🔍 Kết quả
- `dotnet build` → **0 Error** (28 projects; 7 warning có sẵn ở test projects, không thuộc thay đổi này).
- `dotnet test` → **168 passed**.
- Dashboard giờ data-driven 100% qua interface — đổi máy chỉ đổi đăng ký DI, tiles/chips tự sinh.

---

## [Session 33] 2026-06-07 — Mở maximized + chữ to chuẩn công nghiệp + icon Segoe MDL2

**Commit:** `84bce47`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User: muốn mở full màn ngay; maximize bị viền đen phải/dưới; chữ hơi bé; icon emoji chưa hợp công nghiệp.

### ✅ Khắc phục
1. **Mở maximized ngay:** `WindowState=Maximized` (vẫn có title bar, ResizeMode=CanResize).
2. **Hết viền đen khi maximize:** bỏ `MaxWidth/MaxHeight` (trước set = WorkArea làm window không lấp đầy được).
   `ClampToWorkArea` giờ chỉ giới hạn kích thước **restore** (khi rời maximize) để không tràn ở scale >100%.
3. **Chữ to hơn (ISA-101: data 16–20pt, label 12–14pt):** Window `FontSize=15` base; bump Font tokens trong App.xaml
   (Body 13→15, Small 11→13, H2 18→22, H3 16→18, LiveValue 15→18, KpiValue 28→32, H1 24→28). Nav label 15, nút nav cao 48.
4. **Icon chuẩn:** thay emoji bằng **Segoe MDL2 Assets** (icon hệ thống Windows). Lưu **mã hex** (ASCII) → convert glyph
   runtime (`(char)0xE80F`) để source không chứa ký tự PUA. Map: Home/Ringer/Connect/Settings/Document/Contact + hamburger.

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error** (24 projects). `dotnet test` → **143 passed**.

---

## [Session 43] 2026-06-09 — UI module Diagnostics + Logging (machine-agnostic, mọi máy dùng)

**Commit:** `83c948f`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Chưa chọn máy → làm 2 module UI thuần framework mọi máy đều dùng khi bring-up.

### ✅ AM.Modules.Diagnostics (project mới — thứ 27)
- `DiagnosticsViewModel`: bảng **health thiết bị** (Name/Category/Connected) từ `IHardwareManagerService.GetMonitoredDevices`,
  poll 1s; **system info** (version/uptime/RAM-process/host/OS); nút **Reconnect All** (`ConnectAllAsync`).
- `[ModuleNavigation("Nav.Diagnostics", order: 70)]`.

### ✅ AM.Modules.Logging (project mới — thứ 28)
- `LoggingViewModel`: đọc **tail file Serilog** mới nhất trong `logs/` (FileShare.ReadWrite, ~400 dòng cuối),
  **lọc theo level** (ALL/DBG/INF/WRN/ERR) + **tìm kiếm text**, auto-refresh 3s, nút **Mở thư mục log**.
- `LogLineVm` tự parse level → tô màu (WRN vàng/ERR đỏ); ListBox virtualized + monospace.
- `[ModuleNavigation("Nav.Logging", order: 75)]`.

### ✅ Wire Shell
- ProjectReference + `AddUiViewModels` + .sln + i18n (vi/en/zh) + nav glyph Segoe MDL2.

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error** (28 projects). `dotnet test` → **168 passed**.

> UI module còn lại: **Vision** (live camera). Đề xuất: dựng 1 máy reference để nghiệm thu nền thay vì thêm UI suy đoán.

---

## [Session 42] 2026-06-07 — Alarm: AlarmCategory + AlarmAction (mở rộng isStoppable nhị phân)

**Commit:** `d0154c3`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Việc nhỏ "dùng chung mọi máy" (mục (a) từ phản biện review): phân loại + hành động alarm rõ ràng hơn.

### ✅ Mở rộng alarm framework
- `AlarmCategory` (enum): General/Motion/Vision/Io/System/Communication/Production/Safety — suy từ dải mã.
- `AlarmAction` (enum): **Continue / Pause / Stop / ResetRequired** — thay cờ `isStoppable` nhị phân.
- **`AlarmPolicy`** (Core, static): `ResolveCategory/ResolveLevel/ResolveAction` từ mã + level → **dùng chung mọi máy**
  không cần annotate (Safety/Critical → ResetRequired · hardware High → Stop · còn lại → Continue).
- `AlarmModel` thêm `Category` + `Action`; `AlarmService.RaiseAsync` set qua `AlarmPolicy` (gom logic ResolveLevel về Core).
- `[AlarmInfo]` đổi `isStoppable` → `AlarmAction action` (IsStoppable suy ra); cập nhật skill/command/rule examples.
- Test: `AlarmServiceTests` +3 (Motion→Stop · Safety→ResetRequired · mã ngoài dải→Continue).

### 🔍 Kết quả
- `dotnet build` → **0 Error**. `dotnet test` → **168 passed** (Services 75→78).

> Tiếp theo (khi bắt tay máy thật): HAL thiết bị domain (`IScrewdriver`/`IForceController`/`IFeeder`) + Capability motion khi cần.

---

## [Session 41] 2026-06-07 — Phê phán + chắt lọc chuẩn HMI nâng cao (SEMI E95/EEMUA/ISA-18.2/Siemens)

**Commit:** `d663f01`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User cung cấp tài liệu HMI mở rộng; yêu cầu **phản biện phần không phù hợp** cho IPC 21–24" mouse+touch
(máy đơn lẻ) + cập nhật phần phù hợp vào docs/skill.

### ✅ Cập nhật (chỉ tài liệu/guidance — không đụng code)
- **`docs/HMI_Advanced_Standards.md`** (mới): bảng **quyết định adoption** (cái gì áp/không):
  - ❌ Bố cục 4-panel SEMI E95 (giữ ISA-101) · 🔶 không-minimize/close = option kiosk lúc deploy · 🔶 4 nền xám theo cấp ·
    ⚠️ mật độ <30% & ngưỡng alarm ISA-18.2 = nguyên tắc không phải số cứng · ⚠️ đỏ thuần #FF0000 → dùng token dịu.
  - ✅ Chắt lọc: định lượng (tương phản ≥4.5/7:1, demand vs status), EEMUA 201 (abnormal-first, overview thường trực,
    no-blank-screen, task-oriented), SEMI E95 chọn lọc (salience, dialog conventions, nhãn Title-Case, alarm-luôn-truy-cập),
    Siemens process (use-case theo vai trò, ngăn lỗi, component tái dùng).
- **`.claude/skills/am-hmi-design/SKILL.md`**: thêm mục "Bổ sung nâng cao" + caveat **"hỏi viết cho cỡ màn nào?"** +
  checklist (tương phản, demand/status, dialog không che alarm/nav, nhãn nút). Trỏ doc mới.
- **`CLAUDE.md`**: thêm `HMI_Advanced_Standards.md` vào bảng tài liệu.

> Kết luận phản biện: **giữ layout ISA-101 đã dựng** (không đập đi theo SEMI E95 4-panel trừ khi bán cho fab bán dẫn);
> không áp máy móc khuyến nghị viết cho panel nhỏ vào IPC 24".

---

## [Session 40] 2026-06-07 — Production UI: dashboard UPH/yield/cycle-time (số liệu ProductionRecorder)

**Commit:** `eeeb1b0`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Hiển thị số liệu sản xuất đã wire ở S37 (ProductionRecorder ghi mỗi cycle).

### ✅ AM.Modules.Production (project mới — thứ 26)
- `ProductionViewModel`: KPI **Total/OK/NG/Yield%/UPH/Cycle TB** qua `IProductionService.GetStatisticsAsync` theo cửa sổ
  thời gian chọn được (**1 giờ qua / Ca 8 giờ / Hôm nay**). Tự refresh khi `CycleCompleted` + định kỳ 10s + nút Làm mới.
- **Captive dependency**: VM Singleton, `IProductionService` Scoped (EF) → tạo scope mỗi query bằng `IServiceScopeFactory`.
- `ProductionView.xaml`: 6 KPI card (OK xanh, NG đỏ), combo cửa sổ + Refresh. Theme ISA-101 + i18n (Prod.*).
- `[ModuleNavigation("Nav.Production", order: 15)]` + glyph Segoe MDL2 (BarChart).
- CPM: thêm `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.0 (cho IServiceScopeFactory).
- Wire Shell: ProjectReference + `AddUiViewModels` + .sln + nav glyph + i18n (vi/en/zh).

### 🔍 Kết quả
- `dotnet build` → **0 Error** (26 projects). `dotnet test` → **165 passed**.
- Chạy máy (Start) → mỗi cycle ProductionRecorder ghi record → màn **Sản xuất** hiện UPH/yield/cycle-time tăng theo thời gian thực.

---

## [Session 39] 2026-06-07 — Engineering/Debug UI (tiêu thụ [StationUI]/[MechanismUI] + chạy SubRoutine) — KHÉP NỀN

**Commit:** `f02b39d`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục 7 (cuối) — màn kỹ sư bring-up máy: `[StationUI]`/`[MechanismUI]` đã gắn nhưng chưa ai đọc.

### ✅ AM.Modules.Engineering (project mới — thứ 25)
- `EngineeringViewModel`: **auto-discovery** Station/Mechanism từ `IMasterController.Stations`, đọc metadata
  `[StationUI]`/`[MechanismUI]` qua **reflection** (DisplayName/Group). Poll **Ready/Busy** live (500ms).
- `StationVm`/`MechanismVm`/`SubRoutineVm`. **Chạy SubRoutine** qua `ISubRoutineRunner` (gate quyền/state, bắt
  Unauthorized/InvalidOperation/Alarm → StatusMessage). **E-Stop từng cụm** (`IMechanism.EmergencyStop`).
- `EngineeringView.xaml`: cột trái Station→Mechanism (đèn Ready/Busy + E-Stop), cột phải nút SubRoutine (tên+mô tả+quyền).
- `[ModuleNavigation("Nav.Engineering", order: 80)]` + i18n (Kỹ thuật/Engineering/工程) + glyph Segoe MDL2.
- Wire Shell: ProjectReference + `AddUiViewModels` + .sln + nav glyph.

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error** (25 projects). `dotnet test` → **165 passed**.

### 🏁 KHÉP NỀN WorkStation (mục 1–7 trong gap analysis ĐÃ XONG)
1. StepSequence · 2. AxisMap/MachineConfig (+concrete IAxis) · 3. Recipe extensibility · 4. Safety interlock+andon ·
5. wire Production · 6. SubRoutines · 7. Engineering UI.
> **Đã đủ nền để dựng `AM.WorkStation.{Máy}` thật.** Còn lại là phần TÙY MÁY (driver hãng, recipe/steps/mechanisms theo máy)
> và các UI module hiển thị (Production/Vision/Logging/Diagnostics).

---

## [Session 38] 2026-06-07 — SubRoutines base (Home/Calibration/SafetyCheck chạy tay, gate quyền/state)

**Commit:** `265eb1f`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục 6 gap — thao tác setup/bảo trì chạy tay ngoài auto-cycle cần khung tái dùng + gate an toàn.

### ✅ Khung SubRoutine
- `ISubRoutine` (Abstractions): Name/Description/`RequiredLevel`/IsBusy/`ExecuteAsync`.
- **`SubRoutineBase`** (Infrastructure): busy-guard (không chạy đồng thời) + log; subclass chỉ viết `ExecuteCoreAsync`.
- `ISubRoutineRunner` + **`SubRoutineRunner`** (AM.Services): UI gọi runner, runner **gate**:
  quyền (`IUserService.HasPermission`) → `UnauthorizedAccessException`; trạng thái máy (KHÔNG chạy khi Running/Paused)
  → `InvalidOperationException`; bọc `AlarmException` → raise alarm + ném lại.
- Demo: `HomeAllSubRoutine` (Engineer, `DemoStation.HomeAsync`) + `SafetyCheckSubRoutine` (Operator, kiểm tra `ISafetyInput`).
- DI: đăng ký từng subroutine `ISubRoutine` + `ISubRoutineRunner` trong `AddDemoMachine` (máy khác thêm subroutine của mình).
- Test: `SubRoutineRunnerTests` (chạy khi đủ quyền+Idle · chặn thiếu quyền · chặn khi Running · tên lạ · raise alarm).

### 🔍 Kết quả
- `dotnet build` → **0 Error**. `dotnet test` → **165 passed** (Services 70→75).

> Còn lại trước khi dựng máy: mục 7 (Debug/Engineering UI tiêu thụ `[MechanismUI]`/`[StationUI]` + chạy SubRoutine).

---

## [Session 37] 2026-06-07 — Wire Production: CycleCompleted → tự ghi ProductionRecord (UPH/yield/SN)

**Commit:** `651c25f`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục 5 gap — `IProductionService.RecordAsync` có sẵn nhưng KHÔNG ai gọi → chưa có số liệu UPH/yield thật.

### ✅ ProductionRecorder
- `IProductionRecorder` (Abstractions) + **`ProductionRecorder`** (AM.Services): lắng nghe `IMasterController.CycleCompleted`
  → sinh **serial number** (ngày + số tăng dần) + ghi `ProductionRecord` (IsPassed=true, `CycleTimeMs` từ
  `CycleDurationMs`, recipe đang chạy) qua `IProductionService.RecordAsync`.
- **Captive dependency**: recorder là Singleton nhưng `IProductionService` là Scoped (EF DbContext) → tạo **scope mỗi lần ghi**
  bằng `IServiceScopeFactory` (đúng chuẩn DI). Lỗi ghi không làm sập sequence (try/catch + log).
- Model fault-stop: mỗi cycle hoàn thành = 1 PASS (NG ném AlarmException → không có CycleCompleted). Máy chạy
  reject-and-continue có thể ghi NG trực tiếp qua `IProductionService.RecordAsync`.
- DI: đăng ký singleton + `Start()` ở App (sau watchdog/towerlight).
- Test: `ProductionRecorderTests` (ghi PASS record đúng cycle-time/recipe/SN · SN duy nhất mỗi cycle · Dispose hủy đăng ký)
  bằng Moq (mock `IServiceScopeFactory`).

### 🔍 Kết quả
- `dotnet build` → **0 Error**. `dotnet test` → **160 passed** (Services 67→70).
- Chạy máy: mỗi cycle hoàn thành tự ghi 1 record → Dashboard/Production có UPH/yield/cycle-time thật.

> Còn lại trước khi dựng máy: mục 6 (SubRoutines base), 7 (Debug/Engineering UI). Production UI module (mục B) hiển thị stats.

---

## [Session 36] 2026-06-07 — Recipe extensibility: RecipeBase + recipe theo máy (bỏ cứng P&P)

**Commit:** `f2626c2`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục 3 gap — `Recipe` đang cứng field Pick&Place; máy khác không khớp. Tách base + recipe theo máy.

### ✅ Refactor Recipe đa hình
- **`RecipeBase`** (Core, abstract): chỉ metadata chung (Id/Name/ProductCode/Version/CreatedAt/ModifiedAt/ModifiedBy/IsActive).
- Xoá `Recipe.cs` (cứng P&P) → **`PickPlaceRecipe : RecipeBase`** chuyển về **AM.WorkStation.Demo/Recipe/** (recipe THEO MÁY,
  giữ [ParamView] P&P). Máy khác tạo lớp recipe riêng.
- `IRecipeService`/`RecipeEventArgs`/`ParameterViewModel` làm việc qua **`RecipeBase`** (đa hình).
- **`RecipeService` không còn cứng**: bỏ seed P&P hardcode → ctor nhận `IEnumerable<RecipeBase>? seedRecipes` (máy cung cấp);
  **`ValidateAsync` attribute-driven** (reflect `[ParamView]` Min/Max + Name/ProductCode bắt buộc) → đúng cho MỌI loại recipe.
- **`ParameterViewModel`**: reflect `[ParamView]` theo **runtime type** của recipe + **Clone đa hình** (JSON round-trip) →
  form tự render đúng tham số của từng máy, không sửa code.
- DI: `IRecipeService` chuyển sang `AddDemoMachine` kèm **seed `PickPlaceRecipe` mặc định** (máy khác seed recipe riêng).
- Demo: `DemoPickMechanism`/`Step02Inspect` nhận `PickPlaceRecipe`; `DemoStation` cast `ActiveRecipe as PickPlaceRecipe`.

### 🔍 Kết quả
- `dotnet build` → **0 Error**. `dotnet test` → **157 passed** (`RecipeServiceTests` viết lại dùng `TestRecipe : RecipeBase`
  + validate attribute-driven).

> Còn lại trước khi dựng máy: mục 5 (wire Production), 6 (SubRoutines), 7 (Debug/Engineering UI).

---

## [Session 35] 2026-06-07 — Safety interlock + ILightController (đèn tháp andon)

**Commit:** `fa28883`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục 4 trong gap analysis — an toàn là bắt buộc trước khi dựng máy thật.

### ✅ Interlock an toàn (gating Start)
- `BaseMasterController` nhận thêm `ISafetyInput?` (tuỳ chọn): **`StartAsync` chặn Start** khi `!IsAllSafe`
  (E-Stop/Guard/Light Curtain) → raise `SafetyInterlockBreach`, máy ở nguyên Idle. `DemoMasterController` truyền ISafetyInput.
- Test: `SafetyGateTests` (chặn khi unsafe + raise alarm · cho phép khi safe).

### ✅ Đèn tháp (andon) — ILightController + TowerLightService
- `TowerLightState` (Core): record R/Y/G + Buzzer + preset (Off/Run/Attention/Fault/FaultBuzzer).
- `ILightController` (Abstractions) + **`SimulatedLightController`** (Hardware.IO).
- `ITowerLightService` + **`TowerLightService`** (AM.Services): tự đặt đèn theo **ưu tiên an toàn → alarm → state**
  (mất an toàn = đỏ+còi; alarm = đỏ; Running/Idle = xanh; Paused/Init/Reset = vàng). Subscribe state/alarm/safety.
- DI: `ILightController` (HardwareFactory) + đăng ký `MainLight` vào HardwareManager + `ITowerLightService` start ở App.
- Test: `TowerLightServiceTests` (Idle→xanh · mất an toàn→đỏ+còi · alarm→đỏ) bằng Moq + SimulatedLight/Safety.

### 🔍 Kết quả
- `dotnet build` → **0 Error**. `dotnet test` → **157 passed** (Infra 55→57, Services 64→67).
- Connection chip "MainLight" xuất hiện thêm ở status bar.

> Còn lại mục 3 (Recipe extensibility), 5 (wire Production), 6 (SubRoutines), 7 (Debug UI).

---

## [Session 34] 2026-06-07 — Nền WorkStation: chuẩn hoá StepSequence + AxisMap/MachineConfig (concrete IAxis)

**Commit:** `d6ad153`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Làm 2 nền tảng bắt buộc trước khi dựng máy thật (mục 1 + 2 trong phân tích gap).

### ✅ Mục 1 — Chuẩn hoá Sequence (bỏ pattern trùng)
- Thêm **`AM.Infrastructure/StepSequence.cs`** — step-runner tái dùng: foreach Step (sắp theo StepNumber) +
  Validate + Execute; **để exception nổi lên** `BaseMasterController.RunLoopAsync` (đã chuẩn ISA-88: Alarm→RunAlarm→Reset,
  Cancel→dừng). Mỗi máy KHÔNG copy vòng lặp + catch-3-exception nữa.
- **Xoá `DemoMachineSequence.cs`** (mồ côi, không ai dùng, trùng vai trò với BaseMasterController).
- Cập nhật skill `am-sequence-patterns`: Station/MasterController chạy `StepSequence` thay vì viết lại vòng lặp.
- Test: `StepSequenceTests` (thứ tự step, propagate AlarmException, Validate-fail chặn Execute, cancel).

### ✅ Mục 2 — AxisMap + MachineConfig (logical → physical) + concrete IAxis ĐẦU TIÊN
- `AxisConfig` (Core): tên logic → controller + index + đơn vị + vận tốc mặc định + soft-limit.
- **`MotionAxisAdapter`** (Infrastructure) — **concrete `IAxis` đầu tiên** của framework: bọc `IMotionController`+index,
  áp vận tốc mặc định, clamp soft-limit; trạng thái cache theo lệnh.
- `IAxisMap` + **`JsonAxisMap`** (nạp `axismap.json`, `ResolveAxis(name)` → IAxis bind controller qua HardwareManager, cache).
- `MachineConfig`/`StationConfig` + `IMachineConfigProvider` + **`JsonMachineConfigProvider`** (nạp `machine.json` — layout máy).
- Sample `axismap.json` + `machine.json` (Shell, copy PreserveNewest) + đăng ký DI singleton.
- Test: `JsonAxisMapTests` (nạp, Get/TryGet, ResolveAxis cache, Home→Move cập nhật state, clamp soft-limit) —
  dùng SimulatedMotionController + HardwareManagerService thật.

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error**. `dotnet test` → **152 passed** (Infra 46→**55**: +4 StepSequence, +5 AxisMap).

> Còn lại trước khi dựng máy (mục 3–7): Recipe extensibility, Safety interlock + ILightController, wire Production,
> SubRoutines base, Debug/Engineering UI tiêu thụ [MechanismUI]/[StationUI].

---

## [Session 32] 2026-06-07 — Fix: cửa sổ tràn màn hình laptop khi scale 125% (DIP vs pixel)

**Commit:** `6f6a17e`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User báo cửa sổ tràn ra ngoài màn laptop, không kéo/resize được.

### 🐞 Nguyên nhân
- WPF `Width`/`Height` tính theo **DIP** (1/96"), không phải pixel vật lý. Màn 1920×1080 đặt **Scale 125%** → 1 DIP = 1.25px.
- Cửa sổ đặt cố định **1680×1040 DIP** = **2100×1300 px vật lý** → lớn hơn màn hình. `ResizeMode=CanMinimize` (khung cố định)
  khiến không resize được để sửa.

### ✅ Khắc phục (MainWindow)
- `ResizeMode` → **CanResize** (kéo được); kích thước mặc định giảm còn 1440×900 DIP + MinHeight/MinWidth.
- Thêm `ClampToWorkArea()` trong constructor: giới hạn Width/Height và đặt MaxWidth/MaxHeight theo
  **`SystemParameters.WorkArea`** (DIP, đã tính DPI/scale của Windows) → luôn vừa màn ở mọi mức scale (100/125/150%).

### 🔍 Kết quả
- Biên dịch OK (lỗi build trước đó chỉ do app đang chạy khóa file .exe — đóng app rồi build lại sạch).
- Chạy lại: cửa sổ vừa màn, căn giữa, kéo resize được.

---

## [Session 31] 2026-06-07 — Re-tune Shell layout theo chuẩn IPC ISA-101 (header lệnh toàn cục + alarm/status bar)

**Commit:** `9d80aff`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Áp guidance S30 vào Shell đang chạy — bố cục 4 vùng cho IPC 1920×1080.

### ✅ Shell layout mới (MainWindow + ShellViewModel)
- **Header (88px):** tên máy + **state chip** (màu theo MachineState) + Mode + Recipe đang chạy +
  **nút lệnh toàn cục Init/Start/Stop/Reset** (≥76×56, màu semantic, CanExecute theo state) + User (từ IUserService) +
  ComboBox ngôn ngữ + đồng hồ realtime.
- **Nav (240px, collapse→64px):** nút module có **glyph + label**; nút ☰ thu gọn (ẩn label). Auto-discovery giữ nguyên.
- **Content:** giới hạn bề rộng **MaxWidth 1500px** (không giãn hết màn).
- **Alarm bar (52px) — dải riêng:** alarm mới nhất + nút Acknowledge (hiện khi có alarm); nền đỏ khi active.
- **Status bar (36px) — chip kết nối:** sinh từ `IHardwareManagerService.GetMonitoredDevices()`, chấm xanh/đỏ theo
  `IsConnected`, poll 1s (DispatcherTimer).
- `ShellViewModel` (internal, DI singleton) bám IMasterController/IAlarmService/IRecipeService/IUserService/
  IHardwareManagerService + `Loc.Strings` (state/label đa ngữ, refresh khi đổi ngôn ngữ). 2 converter:
  `MachineStateToBrushConverter`, `ConnectionToBrushConverter`.
- Cửa sổ: 1680×1040, CenterScreen, ResizeMode=CanMinimize (khung cố định). i18n thêm Shell.Mode/Guest/NoAlarm.

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error** (24 projects). `dotnet test` → **143 passed**.
- Kiểm tra trực quan: `dotnet run --project AM.Application.Shell` → header có state chip + lệnh toàn cục;
  alarm bar + dãy chip kết nối ở dưới; nav ☰ thu gọn được.

> Lưu ý: Dashboard module vẫn còn cụm control + state riêng (trùng một phần với header) — có thể tinh gọn sau.

---

## [Session 30] 2026-06-07 — Cập nhật guidance UI: target IPC 21–24" 1920×1080 (ISA-101/SEMI E95)

**Commit:** `3cedd47`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** User cung cấp 2 tài liệu tham khảo; chỉ ra UI hiện thiết kế như HMI panel nhỏ (~10"), trong khi mục tiêu là
**máy tính công nghiệp 21–24" / 1920×1080, chuột + cảm ứng**. Cập nhật các file guidance để Claude thiết kế đúng hơn.

### ✅ Cập nhật (chỉ tài liệu/guidance — không đụng code, 143 tests giữ nguyên)
- **`.claude/skills/am-hmi-design/SKILL.md`** (rewrite): thêm mục **Target hardware (IPC 21–24")**; layout Shell 1920×1080
  (Header 80–96px có nút lệnh toàn cục · Nav 220–260px collapse→64px · Content lưới nhiều cột **≤~1400px** không giãn hết
  1920 · **Alarm bar 48–56px + Status bar 32–40px là 2 dải riêng**); nền theo 4 cấp ISA-101; typography (data 16–20pt);
  touch SEMI S8 (≥60×60 / ≥44×44 / gap ≥8); **Connection Status chips** (PLC/RFID/Camera/MES/HIVE/SECS-GEM/DB, SECS/GEM
  hiện cả COMM+CONTROL state); **sitemap** màn hình rộng; checklist cập nhật.
- **`docs/HMI_Components_Catalog.md`** (mới): checklist thành phần/tham số cho 17 nhóm màn (Dashboard/Auto/IO/Settings/
  Motion/Calibration/Alarm/Manual/Recipe/Connectivity/User/Maintenance/History/OEE/Vision/Traceability/System).
- **`.claude/skills/am-wpf-mvvm/SKILL.md`**: sửa sơ đồ layout từ panel nhỏ (48/200/32px) → IPC (80–96/220–260/48–56/32–40px) + touch sizing.
- **`CLAUDE.md`**: thêm dòng "UI target = IPC 21–24" 1920×1080 chuột+cảm ứng" + trỏ đọc skill/docs; thêm 2 doc HMI vào bảng tham khảo.
- **`.claude/commands/am-new-screen.md`**: ghi rõ target IPC + đọc `am-hmi-design` + components catalog.

> Lưu ý: đây là cập nhật **guidance để thiết kế đúng từ giờ**. Shell đang chạy (Header 40px, chưa tách alarm/status bar,
> chưa có connection chips) NÊN re-tune theo spec mới ở session sau — chưa làm trong session này.

---

## [Session 29] 2026-06-07 — Light theme + cửa sổ kích thước cố định + i18n toàn module

**Commit:** `58b7257`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Phản hồi sau khi chạy thử: (1) nhiều chuỗi không đổi theo ngôn ngữ, (2) đổi sang light theme, (3) khởi động kích thước cố định.

### ✅ 1. i18n toàn module (sửa "đổi ngôn ngữ không đổi theo")
- **Vấn đề:** proxy i18n `LocalizedStrings` là `internal` trong Shell → module không bind được; chuỗi trong View hardcode.
- **Giải pháp:** project mới **`AM.UI.Localization`** với proxy dùng chung `Loc.Strings` (INotifyPropertyChanged, indexer);
  Shell gọi `Loc.Strings.Attach(localization)` lúc khởi động. Mọi module bind
  `{Binding [Key], Source={x:Static loc:Loc.Strings}}` → đổi ngôn ngữ cập nhật live.
- Localize **6 module View** (Dashboard/Alarm/IoMonitor/Motion/Parameter/Identity): tiêu đề, nút, header cột DataGrid, nhãn.
- **DashboardViewModel:** nhãn machine-state lấy từ catalog key `State.{enum}`, refresh khi đổi ngôn ngữ (subscribe `Loc.Strings`).
- Bổ sung ~55 key vào `strings.{vi,en,zh}.json` (Col.*, State.*, Dash.*, Alarm.*, Io.*, Motion.*, Param.*, Id.*).

### ✅ 2. Light theme (mặc định)
- `App.xaml`: đổi palette nền/panel/text/input/border/header sang tông sáng (giữ nguyên màu semantic status ISA-101).
- Sửa foreground các nút màu (Initialize/Start/Stop/Save/...) sang `Status.ForegroundBrush` (trắng) để đọc rõ trên nền màu.

### ✅ 3. Cửa sổ kích thước cố định
- `MainWindow.xaml`: `WindowState=Normal`, `WindowStartupLocation=CenterScreen`, `ResizeMode=CanMinimize`,
  `SizeToContent=Manual`, 1180×720 → khởi động khung cố định, căn giữa, không full màn hình.

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error** (24 projects). `dotnet test` → **143 passed**.
- Kiểm tra trực quan: `dotnet run --project AM.Application.Shell` → light theme, cửa sổ cố định, đổi ngôn ngữ vi/en/zh
  cập nhật toàn bộ tiêu đề/nút/cột.

> Lưu ý: status-message runtime trong VM (Motion/Parameter/Identity) vẫn tiếng Việt — có thể i18n sau nếu cần.

---

## [Session 28] 2026-06-07 — UI module Parameter/Recipe (attribute-driven form) — mục B.3

**Commit:** `f428427`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục B.3 — Recipe editor bám ĐÚNG `IRecipeService` (`ActiveRecipe`/`GetRecipeNamesAsync`/`LoadRecipeAsync`/`SaveRecipeAsync`/`ValidateAsync`). Dùng `[ParamView]` để UI tự render (lần đầu attribute này có tác dụng).

### ✅ Recipe gắn `[ParamView]` (AM.Core)
- Gắn `[ParamView]` cho 13 tham số kỹ thuật có setter (Pick/Place X/Y/Z, Vận tốc/Gia tốc, Vision score/timeout, Timing step/clamp/vacuum) — group + unit + min/max + order. Thuần additive, không đổi hành vi (143 tests vẫn xanh).

### ✅ AM.Modules.Parameter (project mới — project thứ 23)
- `ParameterViewModel` (bám IRecipeService + IUserService): nạp danh sách recipe + recipe active, **sửa trên bản clone**
  (Reload huỷ được, không đụng cache RecipeService), Validate (`ValidateAsync`), **Save gate quyền Engineer**
  (`IUserService.HasPermission`), operatorId = CurrentUser. Bắt `ArgumentException` (validate-fail khi save) → list lỗi.
- `ParamRowVm`: sinh từ `[ParamView]` qua **reflection**, đọc/ghi giá trị về property (int làm tròn), hiện khoảng hợp lệ.
- `ParameterView.xaml`: ComboBox chọn recipe + Nạp/Khôi phục/Kiểm tra/Lưu; form group theo `[ParamView].Group`
  bằng `CollectionViewSource` + `GroupStyle`; list lỗi validate + status. Theme ISA-101.
- `[ModuleNavigation("Nav.Parameter", icon: "recipe", order: 50)]`.

### ✅ Wiring
- Shell: ProjectReference + `AddUiViewModels` đăng ký `ParameterViewModel` + thêm project .sln.
- i18n: key `Nav.Parameter` (vi/en "Recipe", zh "配方").

### 🔍 Kết quả
- `dotnet build` → **0 Warning, 0 Error** (23 projects). `dotnet test` → **143 passed**.
- Kiểm tra trực quan: `dotnet run --project AM.Application.Shell` → menu "Recipe": Nạp Default → sửa tham số →
  Kiểm tra/Lưu (cần đăng nhập engineer/admin ở màn Tài khoản, nếu không sẽ báo "Cần quyền Engineer").

> Tiếp theo (mục B): Production (UPH/yield — dùng `CycleDurationMs` đã thêm ở S25), Logging, Diagnostics, Vision.

---

## [Session 27] 2026-06-07 — UI module Motion (jog/home/move + Point Table) — mục B.2

**Commit:** `8d07549` (+ cleanup: gỡ tracking parameters.json/users.json runtime, thêm gitignore)
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Mục B.2 — Motion module bám ĐÚNG `IMotionController`/`IAxis`. Point Table tách toạ độ khỏi code (file JSON).

### ✅ Point Table — service nền (AM.Services, testable)
- `MotionPoint` (AM.Core): `Name` + `Positions` (IReadOnlyList&lt;double&gt;, tránh CA1819) + `Velocity`.
- `IPointTableService` (Abstractions) + `PointTableService` (AM.Services): nạp/lưu `points.json`,
  `AddOrUpdate` (teach theo tên, không phân biệt hoa thường), `Remove`, `Find`, `SaveAsync`/`ReloadAsync`.
  Thread-safe (Lock) + ghi file qua SemaphoreSlim, IDisposable. Đăng ký DI singleton.
- **Tests (+6 → AM.Services.Tests 64):** `PointTableServiceTests` — add/update, remove, find, save+reload, reload-discard.

### ✅ AM.Modules.Motion (project mới — project thứ 22)
- `MotionViewModel`: dựng `AxisVm` theo `IMotionController.AxisCount`, **poll vị trí** live (PeriodicTimer 300ms:
  GetPosition/IsMoving/IsHomed). Lệnh: HomeAxis/HomeAll, JogPlus/JogMinus (MoveRel theo bước), MoveAbs, StopAxis/StopAll;
  vận tốc dùng chung. Bọc `RunMotionAsync` bắt `AlarmException` (vd chưa home → MotionNotHomed) → `StatusMessage`.
- **Point Table UI:** Teach (lưu vị trí hiện tại theo tên), Go (di chuyển mọi trục tới điểm), Xoá, Lưu.
- `AxisVm` (live position + JogStep/MoveTarget người dùng nhập), `PositionsToTextConverter` hiển thị toạ độ điểm.
- `MotionView.xaml` — DynamicResource theme ISA-101, indicator Homed/Moving bằng DataTrigger.
- `[ModuleNavigation("Nav.Motion", icon: "motion", order: 40)]` → sidebar tự sinh.

### ✅ Wiring
- Shell: ProjectReference + `AddUiViewModels` đăng ký `MotionViewModel` + thêm project vào .sln.
- i18n: key `Nav.Motion` (vi "Chuyển động", en "Motion", zh "运动").

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (22 projects).
- `dotnet test` → **143 passed** (64 services + 46 infra + 27 hardware + 6 architecture).
- WPF không chạy được ở môi trường này → kiểm tra trực quan: `dotnet run --project AM.Application.Shell`
  (Home All → đèn Homed xanh → Jog/Move → Teach điểm → Go).

> Tiếp theo (mục B): Parameter/Recipe (bám `IRecipeService.ActiveRecipe/GetRecipeNamesAsync/...`), rồi Production/Logging/Diagnostics/Vision.

---

## [Session 26] 2026-06-07 — UI module Identity (login/logout/RBAC) — mục B.1

**Commit:** `a7ab3a8`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Bắt đầu mục B (UI modules) — làm lại ĐÚNG API. Module đầu: Identity dùng `IUserService` (đã có từ S24).

### ✅ AM.Modules.Identity (project mới — project thứ 21)
- `IdentityViewModel` (CommunityToolkit.Mvvm): `Username`, `IsLoggedIn`, `CurrentUser`, `CurrentLevel`, `StatusMessage`, `IsBusy`.
  `LoginCommand(password)` + `LogoutCommand` (CanExecute gate), lắng nghe `IUserService.UserChanged` →
  cập nhật trạng thái qua SynchronizationContext (R-UI: không import System.Windows trong VM). IDisposable hủy đăng ký.
- **Bảo mật:** mật khẩu KHÔNG lưu thành property — `PasswordBox.Password` đọc ở code-behind, truyền vào LoginCommand.
- `IdentityView.xaml` — form login + panel phiên (toggle bằng `BoolToVisibilityConverter` hỗ trợ `Invert`),
  Enter để đăng nhập, gợi ý user mặc định. Dùng DynamicResource theme (ISA-101).
- `[ModuleNavigation("Nav.Identity", icon: "user", order: 90)]` → sidebar tự sinh (auto-discovery), đặt cuối menu.

### ✅ Wiring
- Shell: thêm ProjectReference + `AddUiViewModels` đăng ký `IdentityViewModel` + thêm project vào .sln.
- i18n: thêm key `Nav.Identity` (vi: "Tài khoản", en: "Account", zh: "账户").

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (21 projects).
- WPF không chạy được trong môi trường này → cần `dotnet run --project AM.Application.Shell` để kiểm tra trực quan
  (login operator/operator123 → thấy cấp quyền + nút Đăng xuất; đổi ngôn ngữ → menu "Tài khoản/Account/账户").

> Chưa làm: gate các thao tác theo `HasPermission` ở từng module; wire `IAlarmCatalogService` vào Alarm UI.
> Tiếp theo (mục B.2): Motion module — AxisControlView + Point Table.

---

## [Session 25] 2026-06-07 — Nền backend: cycle-time đo thực + alarm catalog đa ngữ

**Commit:** `d9a6f8e`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Nốt 2 mục nền (mục A trong handoff) TRƯỚC khi làm UI modules — Production cần cycle-time, Alarm UI cần tên alarm đa ngữ.

### ✅ Cycle-time đo thực (Production cần)
- `CycleCompletedEventArgs` thêm `double CycleDurationMs` (constructor optional, mặc định 0 → tương thích call cũ).
  Validate `ThrowIfNegative`.
- `BaseMasterController.RunLoopAsync` đo thời gian quanh `RunOneCycleAsync` bằng
  `Stopwatch.GetTimestamp`/`GetElapsedTime` (không cấp phát Stopwatch object), log kèm `{Duration:F0}ms`,
  truyền vào event. Dashboard ViewModel không đổi (vẫn đọc `CycleCount`).

### ✅ Alarm catalog đa ngữ (i18n §7.3 — tách tên alarm khỏi UI strings)
- `IAlarmCatalogService` (Abstractions): `GetName(code)` + `GetRemedy(code)`, dịch theo `ILocalizationService.CurrentCulture`.
- `JsonAlarmCatalogService` (AM.Infrastructure/Localization): nạp `Alarms.{culture}.json`
  (mã alarm → {name, remedy}) từ thư mục `lang/`; tra theo culture hiện tại lúc gọi (đổi ngôn ngữ runtime tự phản ánh),
  fallback culture mặc định → `"Alarm {code}"`. Bất biến sau nạp → không lock khi đọc.
- Data: `lang/Alarms.{vi,en,zh}.json` — đủ 44 mã alarm (Motion/Vision/IO/System/Comm/Production/Safety).
- Đăng ký DI singleton (`AddCoreServices`), dùng chung thư mục `lang/` với strings.*.json (đã copy PreserveNewest).

### ✅ Tests (+5 → AM.Infrastructure.Tests 46, tổng 137)
- `JsonAlarmCatalogServiceTests` — dịch theo culture, fallback default, culture lạ, mã không tồn tại, remedy.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **137 passed** (58 services + 46 infra + 27 hardware + 6 architecture).

> Còn lại của mục A: (đã xong cả 2). Tiếp theo (mục B): UI Identity (IUserService) → Motion + Point Table.
> Lưu ý: tên alarm hiển thị ở Alarm module chưa wire `IAlarmCatalogService` vào `AlarmListViewModel` —
> sẽ làm khi dựng lại Alarm UI đúng (thêm cột Name + refresh khi đổi ngôn ngữ).

---

## [Session 24] 2026-06-07 — Nền backend: IUserService (login/RBAC) + lưu lựa chọn ngôn ngữ

**Commit:** `6094df5`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Hoàn thiện phần nền (service layer) TRƯỚC khi làm UI — tránh lặp lại tình trạng UI viết theo API chưa có.

### ✅ IUserService + UserService (login / RBAC)
- `IUserService` (Abstractions): CurrentUser/CurrentLevel/IsLoggedIn, `LoginAsync`, `Logout`, `HasPermission(UserLevel)`,
  event `UserChanged`. `UserChangedEventArgs` (AM.Core).
- `UserService` (AM.Services): user store JSON (`users.json`), mật khẩu băm **BCrypt** (Verify chạy ngoài thread);
  seed mặc định lần đầu (operator/engineer/admin — log cảnh báo đổi mật khẩu trước sản xuất). Thread-safe (Lock).
- Đăng ký DI singleton (AddCoreServices) + thêm ref BCrypt.Net-Next vào AM.Services.

### ✅ Lưu lựa chọn ngôn ngữ (i18n §7.4)
- `App` khôi phục culture đã lưu từ `parameters.json` (key `ui.culture`) lúc khởi động → `SetCulture`;
  mỗi lần đổi ngôn ngữ tự lưu lại (qua `IParameterService`). Lần mở sau giữ nguyên ngôn ngữ đã chọn.

### ✅ Tests (+8 → AM.Services.Tests 58)
- `UserServiceTests` — seed default, login đúng/sai/unknown, HasPermission theo cấp, logout + event, reload từ file.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **132 passed** (58 services + 41 infra + 27 hardware + 6 architecture).

> Phần nền sẵn sàng cho UI: Identity module (dùng IUserService), gate Engineer/Force ở các màn (HasPermission).

---

## [Session 23] 2026-06-07 — Revert 7-screen commit (vỡ build) + thêm HMI master template

**Commit:** `1cf7e27` (+ revert commit)
**Người thực hiện:** Claude (Cowork) + Nhan

### 🔙 Revert
- Revert commit `9c667ec` ("7 màn hình ISA-101") — code viết theo **API không tồn tại** làm vỡ build toàn solution:
  `IUserService` (chưa có), `IRecipeService.CurrentRecipe/AllRecipes/LoadAsync/SaveAsync` (thực tế là
  `ActiveRecipe/GetRecipeNamesAsync/LoadRecipeAsync/SaveRecipeAsync`), `CycleCompletedEventArgs.CycleDurationMs`
  (không có), Motion XAML set Border.Style 2 lần, + vi phạm analyzer.
- Đưa solution về trạng thái xanh (= Session 22): Dashboard + Alarm + IoMonitor + i18n + nav auto-discovery.

### ✅ Thêm
- `docs/HMI_UI_Architecture_Template.md` — master reference HMI (ISA-101/SEMI E95/PackML) để dùng khi sinh màn hình.

### 📌 Đánh giá template (tóm tắt) — sẽ làm lại các màn hình ĐÚNG API
- Đã có: i18n runtime (§7), Persistent Frame/ISA-101 (§3), nav config-driven ([ModuleNavigation], §2/§3.1), color/status (§5).
- Nên bổ sung (làm lại đúng API): `IUserService`/Identity (§4/§8), AxisControlView + Point Table (§6.2),
  ManualControlView (§6.1), ErrorDetailView (§6.5), lưu lựa chọn ngôn ngữ (§7.4).
- Không áp: Prism regions (§8 — đã dùng MS DI + auto-discovery), Group Menu 4-module cứng (§2).

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **124 passed** (50 services + 41 infra + 27 hardware + 6 architecture).

---

## [Session 22] 2026-06-05 — G0 Nav auto-discovery + AM.Modules.IoMonitor

**Commit:** `4246aaf`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Theo sơ đồ kiến trúc — lấp tầng Modules. Làm nền điều hướng trước để thêm module không phình Shell.

### ✅ G0 — Sidebar tự sinh từ [ModuleNavigation]
- `[ModuleNavigation(displayKey, icon, order)]` gắn lên DashboardView/AlarmListView/IoMonitorView.
- `NavigationBuilder` (Shell): quét assembly `AM.Modules.*` đã nạp, tìm View có attribute → danh sách entry sắp theo Order.
- `MainWindow` sinh nút nav động: Content bind i18n proxy (đổi ngôn ngữ cập nhật ngay); click → resolve View +
  ViewModel theo convention ("XxxView"→"XxxViewModel") từ DI, cache view. **Thêm module = gắn attribute, KHÔNG sửa Shell.**

### ✅ IO Monitor (AM.Modules.IoMonitor)
- `IoMonitorViewModel` — poll DI realtime (`ReadAllDiAsync`, PeriodicTimer 300ms, marshalling SynchronizationContext),
  toggle DO (`WriteDiAsync`); `IoChannelVm` (Index/Label/Value). IDisposable dừng poll.
- `IoMonitorView.xaml` — DI grid (ellipse màu ON/OFF) + DO grid (nút toggle màu theo trạng thái); `BoolToIoBrushConverter`.
- App: `ConnectAllAsync` lúc khởi động → IO Monitor/Dashboard có dữ liệu live + DO toggle hoạt động + watchdog có baseline.
- Bổ sung file dịch `strings.en.json` (đang thiếu) + key `Nav.IoMonitor` cho vi/en/zh.

### ✅ Tests
- `AM.Architecture.Tests` +1: IoMonitor module không phụ thuộc hardware concrete (6 arch tests).

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (20 projects).
- `dotnet test` → **124 passed** (50 services + 41 infra + 27 hardware + 6 architecture).

---

## [Session 21] 2026-06-05 — i18n foundation: đổi ngôn ngữ runtime (vi/en/zh) + log retention config

**Commit:** `a4fd2bd`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Theo sơ đồ kiến trúc mục tiêu — lấp tầng Infrastructure i18n. Hạ tầng cross-cutting làm TRƯỚC
khi thêm module mới (IO Monitor/Motion/Identity) để tránh retrofit.

### ✅ i18n — đổi ngôn ngữ runtime, không restart
- `ILocalizationService` (AM.Core.Abstractions): `this[key]`, `Format(key,args)`, `CurrentCulture`,
  `AvailableCultures`, `SetCulture`, event `LanguageChanged`. `LanguageChangedEventArgs` (AM.Core).
- `JsonLocalizationService` (AM.Infrastructure/Localization): nạp `strings.{culture}.json` (flat key→value)
  từ thư mục; đổi culture runtime phát event; fallback về key nếu thiếu dịch; thread-safe.
- File dịch `lang/strings.{vi,en,zh}.json` (copy ra output).
- `LocalizedStrings` (Shell): proxy WPF — binding `{Binding [Key]}` tự refresh khi đổi ngôn ngữ
  (raise PropertyChanged indexer qua Dispatcher) → **hot-reload, không restart**.
- Shell: ComboBox chọn ngôn ngữ trong side-nav; nav text bind qua proxy → đổi là cập nhật ngay.

### ✅ Log retention theo config
- `ConfigureLogging(config)`: `retainedFileCountLimit = AutoMachine:LogRetentionDays` (mặc định 30) —
  log cũ hơn N ngày tự xoá. App build config TRƯỚC khi cấu hình logging.

### ✅ Tests (+6 → AM.Infrastructure.Tests 41)
- `JsonLocalizationServiceTests` — nạp culture, đổi culture + event, không raise khi cùng culture,
  culture lạ bị bỏ qua, fallback key, Format có args.

### ⚠️ Ghi chú
- Log file giữ nguyên ngôn ngữ trong code (chuẩn cho kỹ sư) — không localize log; user reqs cho phép phương án này.
- String hiện tại của Dashboard/Alarm/App.xaml chưa migrate hết sang proxy — module mới sẽ dùng proxy từ đầu;
  migrate XAML cũ làm dần (incremental).

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **123 passed** (50 services + 41 infra + 27 hardware + 5 architecture).

---

## [Session 20] 2026-06-04 — Phase F5/F7: WordRegisterPlcBase (dedup PLC) + đổi folder docs/

**Commit:** `d0012ae`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ F5 — WordRegisterPlcBase (gom code chung PLC, có chọn lọc)
- `AM.Hardware.Comm/Plc/WordRegisterPlcBase.cs` — abstract base cho PLC **word-register, little-endian word order**:
  cung cấp sẵn `ReadWord/ReadDWord/WriteDWord/ReadFloat/WriteFloat` compose từ `ReadWordsAsync/WriteWordsAsync`.
- `InovancePlcDevice` + `MitsubishiPlcDevice` kế thừa base → bỏ ~5 typed-method trùng lặp mỗi driver.
  `WriteWordAsync` để **virtual**: Inovance override dùng FC06 (single), Mitsubishi dùng default FC16 (batch) → giữ đúng wire.
- **Có chọn lọc:** Siemens S7 (byte-oriented, big-endian) KHÔNG dùng base — protocol khác hẳn. Tránh over-abstraction.
- Dispose theo pattern `Dispose(bool)` (CA1063). InovancePlcDeviceTests vẫn pass → behavior-preserving.

### ✅ F7 — Đổi tên folder tài liệu
- `file hướng dẫn code/` (tên có dấu + khoảng trắng, gây phiền CI/cross-platform) → **`docs/`** (git mv).
- Cập nhật reference trong CLAUDE.md + PROJECT_STATUS.md (giữ nguyên CHANGELOG lịch sử).

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **117 passed** (50 services + 35 infra + 27 hardware + 5 architecture).

---

## [Session 19] 2026-06-04 — Phase F2/F3/F4/F6: tách Bootstrapper + vendor enum + arch test + log path

**Commit:** `3aaa5f6`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Thực hiện các điểm ĐÚNG còn lại của review bên thứ 3.

### ✅ F2 — Tách Bootstrapper (God-Composition-Root → extension groups)
- `ServiceCollectionExtensions.cs`: `AddAutoMachineOptions / AddDataAccess / AddCoreServices / AddUiViewModels /
  AddDemoMachine / AddHardware`. Bootstrapper còn ~90 dòng, chỉ điều phối.
- Thêm hardware/station mới → sửa đúng nhóm, không phình một file lớn.

### ✅ F3 — Vendor enum thay magic string
- `AM.Core/Enums/HardwareVendors.cs`: `MotionVendor/PlcVendor/IoVendor/ScannerVendor/VisionVendor/RobotVendor`.
- `ParseVendor<TEnum>` (Enum.TryParse ignoreCase) trong ServiceCollectionExtensions + HardwareFactory →
  hết so sánh chuỗi "KEYENCE"/"GTS", type-safe + IntelliSense.

### ✅ F4 — Architecture test (enforce Dependency Inversion)
- Project mới `AM.Architecture.Tests` (NetArchTest): **5 test** chặn `AM.Services`, `AM.Modules.Dashboard`,
  `AM.Modules.Alarm`, `AM.Core.Abstractions`, `AM.Infrastructure` phụ thuộc `AM.Hardware.*` concrete.
- Chặn "lách luật" gọi SDK hãng trực tiếp từ UI/logic — chỉ qua AM.Core.Abstractions.

### ✅ F6 — Log path cross-platform
- `ConfigureLogging`: `Path.Combine(AppContext.BaseDirectory, "logs", "automachine-.log")` thay literal `@"logs\..."`.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (19 projects).
- `dotnet test` → **117 passed** (50 services + 35 infra + 27 hardware + 5 architecture).

---

## [Session 18] 2026-06-04 — Fix: gỡ SQLite DB khỏi Git tracking (.gitignore)

**Commit:** `b53ecf7`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Review bên thứ 3 — điểm đúng & nghiêm trọng: file DB sống bị commit.

### 🔧 Sửa (Phase F1)
- `.gitignore`: thêm `*.db`, `*.db-shm`, `*.db-wal`, `*.sqlite`, `*.sqlite3`.
- `git rm --cached automachine.db{,-shm,-wal}` — gỡ khỏi tracking (file vẫn còn trên đĩa để chạy).
- DB tự tạo runtime qua `InitializeDatabaseAsync` → `EnsureCreatedAsync` nên không cần commit DB.
- Tránh: phình repo, merge conflict binary, ghi đè data sống (Recipe/Log) khi pull.

---

## [Session 17] 2026-06-04 — Phase E: README + CI/CD + AM.Modules.Alarm + Shell navigation

**Commit:** `98382af`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Phase E (roadmap) — onboarding + UI vận hành. Làm lát cắt giá trị cao: README, CI, module UI thứ 2.

### ✅ E1 — README.md
- Tổng quan, nguyên tắc HAL, kiến trúc 3 tầng, bảng 17 project, quick-start (build/test/run),
  bảng đổi hãng phần cứng qua appsettings, watchdog/alarm ranges, quy ước phát triển (CPM, commit script).

### ✅ E2 — CI/CD (GitHub Actions)
- `.github/workflows/ci.yml` — on push/PR/dispatch tới main: `windows-latest` (bắt buộc vì WPF net9.0-windows),
  setup .NET 9 → restore → build Release (TreatWarningsAsErrors) → test.

### ✅ E3 — AM.Modules.Alarm (UI vận hành thứ 2) + điều hướng Shell
- Project mới `AM.Modules.Alarm` (net9.0-windows, CPM):
  - `AlarmListViewModel` — ObservableCollection ActiveAlarms, đồng bộ realtime với AlarmService (AlarmRaised/Cleared),
    command Acknowledge/Clear/ClearAll, UI-thread marshalling qua SynchronizationContext, IDisposable.
  - `AlarmListView.xaml` (+code-behind) — DataGrid ISA-101: ellipse màu theo Level, Code/Station/Message/Time/Ack,
    nút Ack/Clear mỗi dòng + Clear All; `AlarmLevelToColorConverter` (Low/Medium/High/Critical → Status brush).
- Shell: thêm **side-nav** (Dashboard / Alarms) chuyển `MainContent`; đăng ký `AlarmListViewModel` DI.

### ⚠️ Ghi chú
- ViewModel của module WPF (net9.0-windows) chưa unit-test riêng (giống Dashboard) — logic AlarmService đã được
  test đầy đủ ở AM.Services.Tests; VM chủ yếu là binding/marshalling.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (18 projects).
- `dotnet test` → **112 passed** (50 services + 35 infrastructure + 27 hardware).

---

## [Session 16] 2026-06-04 — Fix IDE1006 cho private static readonly fields

**Commit:** `3e8db3a`
**Người thực hiện:** Claude (Cowork) + Nhan

### 🔧 Sửa
- `.editorconfig`: rule `private_fields_should_be_camel_case` áp cho MỌI private field (kể cả `static readonly`)
  → IDE1006 "Missing prefix '_'" báo sai trên `Transitions`, `JsonOptions`, `DefaultSimEndpoint`...
  (mâu thuẫn với CS07: static readonly dùng PascalCase).
- Thêm rule `static_readonly_fields_should_be_pascal_case` **đặt trước** rule underscore — Roslyn lấy rule khớp
  đầu tiên theo thứ tự file → `private static readonly` dùng PascalCase, instance field vẫn bắt buộc `_camelCase`.
- Xác minh bằng `EnforceCodeStyleInBuild=true`: 0 IDE1006 trên toàn solution (4 field: BaseMasterController,
  ParameterService, JsonIoTagMap, SimulatedOpcUaClient).

---

## [Session 15] 2026-06-04 — Phase D: CPM + IOptions validation + ProductionService + DeviceNames

**Commit:** `506d45f`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Khép các điểm "đúng nhưng nhẹ" còn lại của đánh giá: #12, #16, #14, #5.

### ✅ D4 — DeviceNames constants (claim #5)
- `AM.Core/Constants/DeviceNames.cs` — 12 hằng số tên device (MainMotion/MainCamera/...).
- Bootstrapper.RegisterHardwareDevices dùng `DeviceNames.X` thay magic string → IntelliSense + an toàn refactor.

### ✅ D3 — ProductionService (claim #14)
- `IProductionService` + `ProductionService` (AM.Services) trên `IProductionRepository` có sẵn:
  - `RecordAsync` ghi record mỗi cycle; `GetStatisticsAsync` tính **UPH / yield / avg cycle time**.
- `ProductionStatistics` record (AM.Core). Đăng ký DI Scoped (khớp lifetime EF repo).
- `ProductionServiceTests` (+3): record persist, yield/UPH/avgCycle đúng, empty khi không có data.

### ✅ D2 — IOptions + validate fail-fast (claim #16)
- `AutoMachineOptions` (Shell/Configuration) cho section "AutoMachine".
- Bootstrapper: `AddOptions<AutoMachineOptions>().Bind(...).Validate(...)` (DatabasePath không rỗng,
  LogRetentionDays 1..3650, DataRetentionDays 1..36500).
- App.xaml.cs: **ép resolve `.Value` lúc startup** → config sai ném OptionsValidationException ngay (fail-fast),
  hiển thị dialog lỗi thay vì chạy với giá trị sai.
- Thêm package `Microsoft.Extensions.Options.ConfigurationExtensions`.

### ✅ D1 — Central Package Management (claim #12)
- `Directory.Packages.props` (root) `ManagePackageVersionsCentrally=true` — gom version NuGet một chỗ.
- Strip `Version=` khỏi **14 .csproj** + `Directory.Build.props` (analyzers).
- Pin các version thả nổi (CPM cấm floating): NetAnalyzers 9.0.0, SonarAnalyzer 9.32.0.97167.
- Vendor SDK comm (NModbus4/FluentModbus/libplctag/OPCFoundation/Basler.Pylon) đang comment trong csproj →
  ghi chú thêm PackageVersion cụ thể khi kích hoạt.
- → chống version drift giữa 17 projects; một nguồn version duy nhất.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **112 passed** (50 services + 35 infrastructure + 27 hardware).

---

## [Session 14] 2026-06-04 — Phase C: Hardware Watchdog + IsConnected base + auto-reconnect

**Commit:** `f48f874`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Claim #2 (watchdog 6.2) — mất kết nối giữa chừng không làm sập máy. Gắn trực tiếp với IHardwareDevice (Phase A).

### ✅ IsConnected vào IHardwareDevice base
- Thêm `bool IsConnected { get; }` vào `IHardwareDevice`; **gỡ khỏi 11 interface con** (additive, 0 driver bị sửa —
  implementation đã có sẵn IsConnected nay thoả base). `ISerialDevice` bridge `IsConnected => IsOpen` (default member).
- → cho phép health-monitoring **generic** thay vì switch theo kiểu.

### ✅ Registry hỗ trợ monitor
- `IHardwareManagerService.GetMonitoredDevices()` → `IReadOnlyList<MonitoredDevice>` (Name/Category/IHardwareDevice).
- `MonitoredDevice` record mới (AM.Core.Abstractions).

### ✅ HardwareWatchdogService (AM.Services)
- `IHardwareWatchdogService` + impl: poll `IsConnected` mỗi chu kỳ; khi connected→disconnected:
  - raise alarm `CommConnectionFail`,
  - phát `DeviceDisconnected` event (MasterController subscribe → EmergencyStop),
  - auto-reconnect bằng **RetryHelper** (back-off luỹ tiến) — best-effort, không làm sập watchdog.
- `HardwareDisconnectedEventArgs` (AM.Core). `PollOnceAsync` public → unit test deterministic.
- Thêm ProjectReference AM.Services → AM.CommonTools (dùng RetryHelper sẵn có).

### ✅ Wiring
- Bootstrapper: đăng ký `IHardwareWatchdogService` singleton.
- App.xaml.cs: subscribe `DeviceDisconnected → masterController.EmergencyStop` + `watchdog.Start()`;
  cleanup qua provider disposal (IDisposable).

### ✅ Tests (+5 → AM.Services.Tests 47)
- `HardwareWatchdogServiceTests` — không alarm khi vẫn connected; drop → alarm + reconnect; fire event;
  reconnect thất bại không throw + giữ disconnected; Start/Stop toggles IsRunning.

### ⚠️ Quyết định scope (minh bạch)
- **KHÔNG** thêm `ErrorOccurred`/`GetLastError` (claim 0.1) vào base — event trên interface buộc ~20 driver
  phải implement (events không có default impl thực dụng). Watchdog dùng **IsConnected polling** là đủ phát hiện lỗi.
  Retrofit error-surface để dành làm khi thực sự cần (mỗi driver muốn báo lỗi chi tiết).

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **109 passed** (47 services + 35 infrastructure + 27 hardware).

---

## [Session 13] 2026-06-04 — Phase B: AM.Infrastructure.Tests (13 transitions) + end-to-end sequence

**Commit:** `6e37960`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Vá khoảng trống test lõi đúng như claim #11 (BaseMasterController/BaseMechanism/StationBase chưa có test).

### ✅ B1 — AM.Infrastructure.Tests (project mới, 35 tests)
- `TestDoubles.cs` — RecordingAlarmService + TestMechanism/TestStation/TestMasterController (expose FireTrigger).
- `BaseMasterControllerTests` — **đủ 13 transition ISA-88 hợp lệ** (Theory/MemberData) + transition không hợp lệ
  bị từ chối (Fire=false, state giữ nguyên) + StateChanged (Previous/New/Trigger) + SetOperationMode chỉ trong Idle.
- `BaseMechanismTests` — IsReady sau Initialize; **IsBusy guard** (Interlocked) từ chối thao tác đồng thời + nhả guard
  sau exception; EmergencyStop KHÔNG throw dù OnEmergencyStop ném.
- `StationBaseTests` — RunCycle Running→Idle; AlarmException → RunAlarm + rethrow; EmergencyStop → RunAlarm.

### ✅ B2 — End-to-end Simulated sequence (EndToEndSequenceTests)
- Run loop chạy nhiều cycle → CycleCount tăng + CycleCompleted fire → Stop về Idle.
- **Pause halt / Resume continue**: paused thì loop dừng ở checkpoint (cycle không tăng), resume thì tiếp tục.
- **Safety-trip**: `SimulatedSafetyInput.ForceState(guard mở)` giữa chu trình → cycleBody ném AlarmException →
  run loop FireTrigger(Error) → **RunAlarm** + alarm 70010 được raise.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (17 projects).
- `dotnet test` → **104 passed** (42 services + 35 infrastructure + 27 hardware).

---

## [Session 12] 2026-06-04 — Phase A: Wire Dashboard + IHardwareDevice base + doc drift

**Commit:** `2795775`
**Người thực hiện:** Claude (Cowork) + Nhan
**Bối cảnh:** Phản biện đánh giá bên thứ 3 (dựa trên snapshot Session 8 cũ) → thực thi 8 điểm đúng, bắt đầu Phase A.

### ✅ A1 — Wire Dashboard vào Shell (claim #1 đúng)
- `MainWindow.xaml`: thay placeholder bằng `ContentControl x:Name="MainContent"`.
- `MainWindow.xaml.cs`: `OnWindowLoaded` resolve `DashboardViewModel` từ DI (UI thread → capture SynchronizationContext), set `MainContent.Content = new DashboardView{...}`.
- Shell csproj + ProjectReference `AM.Modules.Dashboard`; Bootstrapper đăng ký `DashboardViewModel` singleton.
- **DoD:** app khởi động hiển thị Dashboard (state indicator + alarm list + Start/Stop/Reset).

### ✅ A2 — IHardwareDevice base + bỏ switch trong ConnectAllAsync (claim #4 → fix #2)
- `IHardwareDevice { ConnectAsync; DisconnectAsync; }` mới — hợp đồng lifecycle chung.
- **11 interface** kế thừa `IHardwareDevice`, gỡ khai báo Connect/Disconnect trùng lặp (additive — KHÔNG sửa implementation nào): Motion/Camera/IO/Modbus/Tcp/OpcUa/EthernetIp/Plc/Robot/BarcodeScanner/SafetyInput.
- `ISerialDevice`: bắc cầu bằng default interface method (`ConnectAsync→OpenAsync`, `DisconnectAsync→CloseAsync`) — không đụng SerialPortWrapper/Simulated.
- `HardwareManagerService.ConnectAllAsync/DisconnectAllAsync` → **vòng lặp generic `is IHardwareDevice`**, xoá switch 8-nhánh. Thêm hardware mới không phải sửa manager.
- Đăng ký thêm 4 device vào registry (MainPLC/MainRobot/MainScanner/MainSafety) + `HardwareCategory.SafetyTerminal=16`.

### ✅ A3 — Dọn doc drift (claim #8, #10)
- `CLAUDE.md`: sửa danh sách interface Hardware + project AM.Hardware.* cho khớp code thật; đánh dấu rõ TODO chưa có (IAxisGroup, ILightController, EStopMonitor→thay bằng ISafetyInput).

### ✅ Tests (+4 → AM.Services.Tests 42)
- `HardwareManagerServiceTests` — ConnectAll/DisconnectAll generic qua IHardwareDevice, bỏ qua object không phải device, Resolve case-insensitive.

### 📌 Phản biện đánh giá bên thứ 3 (tóm tắt)
- **Sai dữ kiện:** #6 (Parameter lưu JSON chứ không phải EF/SQLite → không có "bất nhất"), #13 (Serilog ĐÃ có file rolling 30 ngày), #15 (CheckPauseAsync + ct ĐÃ propagate trong run loop).
- **Lỗi thời:** "32 tests" (thực tế 69), #8 (ISafetyInput đã thêm S11).
- **Opinion/YAGNI:** #3 (Stateless lib), #7 (dual-generic Station), #9 (tách SECS process).
- **Đúng, đã/đang làm:** #1 (A1), #2+#4 (A2), #10 (A3). Còn lại #5/#12/#14/#16 + UI modules → Phase B–E.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error**.
- `dotnet test` → **69 passed** (42 services + 27 hardware).

---

## [Session 11] 2026-06-04 — HAL abstraction (EPIC 0) + Scanner/Vision/Safety/IO-tagmap

**Commit:** `6888d66`
**Người thực hiện:** Claude (Cowork) + Nhan
**Tham khảo:** Kế hoạch Task — Tách phần cứng (HAL). Làm phần additive/buildable ngay, không cần SDK hãng.

### ✅ EPIC 0 — Abstraction trung lập (nền tảng)
- DTO trung lập (AM.Core):
  - `Enums/PixelFormat.cs` (Mono8/Mono16/Rgb24/Bgr24/BayerRg8 — không phụ thuộc System.Drawing).
  - `Models/FrameData.cs` (Pixels/Width/Height/Format/Timestamp) — ảnh trung lập hãng.
  - `Models/MotionStatus.cs` (Positions/Homed/Moving/FaultCode, đơn vị mm).
  - `Models/EventArgs/BarcodeReceivedEventArgs.cs`, `SafetyStateChangedEventArgs.cs` (CA1003).
- Interface mới (AM.Core.Abstractions/Interfaces/Hardware):
  - `IVisionProcessor` — LoadJob/RunJob(FrameData)→VisionResult, tách hẳn camera khỏi vision tool.
  - `IBarcodeScanner` — TriggerAsync + CodeReceived event, trung lập Keyence/Cognex.
  - `ISafetyInput` — CHỈ ĐỌC E-Stop/Guard/LightCurtain + SafetyStateChanged (an toàn vật lý do mạch lo).
  - `IAxis` — trục đơn đơn vị mm (MoveAbs/MoveRel/Home/Position/IsMoving/IsHomed).
  - `IIoTagMap` — phân giải tag IO → kênh.
- `VisionResult` tách khỏi ICameraDevice → file riêng, **bổ sung X/Y/AngleDeg/Pass** (additive, không phá vỡ smart-camera path hiện có).

### ✅ EPIC 4.3 — SimulatedVisionProcessor (AM.Hardware.Vision)
- Trả VisionResult giả lập theo passRate; chạy end-to-end không cần VisionPro/dongle.

### ✅ EPIC 2.3 — SimulatedSafetyInput (AM.Hardware.IO)
- Mặc định all-safe; `ForceState` mô phỏng E-Stop/mở cửa → phát SafetyStateChanged để khoá logic.

### ✅ EPIC 2.2 — IO tag-map (AM.Hardware.IO)
- `JsonIoTagMap` nạp `io.map.json` (tag→kênh, case-insensitive) + `IoTagExtensions`
  (ReadDiByTag/WriteDoByTag/WriteAndWaitConfirmByTag). Logic máy gọi IO bằng tag, đổi đấu dây chỉ sửa JSON.
- `AM.Application.Shell/io.map.json` mẫu (PartPresent_A, Vac_A, TowerGreen...).

### ✅ EPIC 5 — Scanner (project mới AM.Hardware.Scanner, protocol-only theo plan 5.1)
- `TcpBarcodeScannerBase` (TCP line-based, timeout, AlarmException, no-read detection) +
  `KeyenceScanner` (LON) + `CognexScanner` (TRIGGER ON, "NO READ") + `SimulatedBarcodeScanner` (queue/serial).

### ✅ EPIC 0.4/6.1 (một phần) — HardwareFactory
- `AM.Application.Shell/HardwareFactory.cs` — điểm chọn vendor cho peripherals
  (IVisionProcessor/IBarcodeScanner/ISafetyInput/IIoTagMap) theo `appsettings`, ép Simulated khi UseSimulation.
- Bootstrapper gọi `HardwareFactory.RegisterPeripherals`. `appsettings.json`: thêm block Vision/Scanner + Io:TagMapFile.

### ✅ Tests (AM.Hardware.Tests +10 → 27 total)
- `HalAbstractionTests` — SimVision, SimSafety (event), JsonIoTagMap (resolve/load/case-insensitive), IoTagExtensions, SimScanner.
- `ScannerLoopbackTests` — Keyence/Cognex qua scanner server giả loopback (line protocol + no-read alarm).

### ⚠️ Quyết định scope (minh bạch)
- **Nhóm B (SDK-nặng) CHƯA làm**: EtherCAT motion (PCIE-1203+Leadshine), Beckhoff ADS, Basler/Hik camera, VisionPro.
  → Cần SDK hãng; sẽ tạo project riêng + Simulated khi có DLL (libs/ đã chuẩn bị).
- **Triệt để tách project** cho driver Session 9 (Inovance/Mitsubishi/Siemens/GTS...) là refactor cơ học lớn,
  **để pha riêng** tránh trộn với feature mới. HardwareFactory hiện quản peripherals; motion/io/plc vẫn ở RegisterRealHardware.
- Interface cũ (IMotionController/IIoModule/ICameraDevice) **chưa** thêm ErrorOccurred/GetLastError (0.1) — sẽ retrofit kèm watchdog 6.2 để tránh phá vỡ 8 driver hiện có.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **0 Warning, 0 Error** (16 projects).
- `dotnet test` → **65 passed** (38 services + 27 hardware).

---

## [Session 10] 2026-06-03 — libs/ Vendor DLL Structure

**Commit:** `0fb161c`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới

- `libs/README.md` — Hướng dẫn chi tiết từng vendor: nơi tải SDK, DLL cần copy, đường dẫn đích, link website
- `libs/Motion/Gts/{x64,x86}/.placeholder` — GTS 固高 Googoltech GTS-400/800 (`gts.dll`, P/Invoke)
- `libs/Motion/Advantech/{x64,x86}/.placeholder` — Advantech PCI-1245/1265 (`ADVMOT.dll`, P/Invoke)
- `libs/Vision/Cognex/x64/.placeholder` — Cognex VisionPro (`Cognex.VisionPro.dll + CogSocketServer.dll`)
- `libs/Vision/HIK/x64/.placeholder` — HIK Robot MVS (`MvCameraControl.Net.dll + MVSDKmd.dll`)
- `libs/Vision/Basler/x64/.placeholder` — Basler Pylon (`PylonC.NET.dll` hoặc NuGet `Basler.Pylon`)
- `libs/IO/Advantech-ADAM`, `Mitsubishi-QSeries`, `Omron-NX` — không cần DLL (Modbus/TCP protocol)

### 🔧 Sửa đổi
- `.gitignore` — Thêm rule loại trừ `*.dll` trong `libs/` nhưng giữ `.placeholder` và `README.md`
- `AM.Hardware.Motion.csproj` — Thêm comment hướng dẫn uncomment khi có `gts.dll` / `ADVMOT.dll`
- `AM.Hardware.Vision.csproj` — Thêm comment hướng dẫn cho Cognex/HIK/Basler DLL reference

### 🔧 Quy tắc sử dụng
- **DLL không commit lên Git** (bản quyền vendor)
- Developer clone repo → tự copy DLL từ SDK theo hướng dẫn trong `libs/README.md`
- Khi có DLL → uncomment phần `<Content>` hoặc `<Reference>` trong `.csproj` tương ứng
- Build vẫn pass khi **không có DLL** (chỉ Simulated drivers hoạt động)

---

## [Session 9] 2026-06-03 — Real hardware drivers (Modbus, Inovance, 固高 GTS, Advantech, Mitsubishi, Siemens, Robot)

**Commit:** `5183b0d`
**Người thực hiện:** Claude (Cowork) + Nhan
**Mục tiêu:** Bổ sung driver phần cứng thật, chạy được cho sản phẩm thật, giữ build clean (0 warning).

### ✅ Abstractions mới (AM.Core.Abstractions / AM.Core)
- `IPlcDevice` — đọc/ghi bit/word/dword/float theo địa chỉ vendor (D100, M10, X0...).
- `IRobotDevice` — move/pose/IO/raw-command cho robot qua socket.
- `RobotPose` record (Cartesian X/Y/Z/Rx/Ry/Rz) trong AM.Core/Models.
- `HardwareCategory`: thêm `Plc=14`, `Servo=15`.

### ✅ Modbus TCP thật (AM.Hardware.Comm/Modbus/ModbusTcpClient.cs)
- Thay skeleton bằng implementation thật: tự dựng khung **MBAP trên raw TcpClient**, zero NuGet.
- Đủ FC01–FC06/FC15/FC16, big-endian, thread-safe, timeout + AlarmException mapping.

### ✅ Inovance (AM.Hardware.Comm/Inovance)
- `InovancePlcDevice : IPlcDevice` — PLC H3U/H5U/AM qua Modbus; parse D/M/X/Y + base-offset cấu hình.
- `InovanceServoDrive : IMotionController` — servo IS620/SV660 qua Modbus, **CiA402 Profile Position**
  (enable 06→07→0F, new-setpoint bit4, poll target-reached), register map cấu hình được.
- `SimulatedPlcDevice : IPlcDevice` (AM.Hardware.Comm/Plc) — in-memory.

### ✅ 固高 GTS motion (AM.Hardware.Motion/Gts) — chạy thật trên PC có card
- `GtsNative` — P/Invoke `gts.dll` (GT_Open/Reset/AxisOn/PrfTrap/SetPos/Update/GetEncPos/GetSts/Stop...).
- `GtsMotionController : IMotionController` — trapezoid point-to-point, đổi mm↔pulse, poll status bit.
- Biên dịch không cần DLL (DllImport resolve runtime).

### ✅ Advantech (AM.Hardware.Motion/Advantech + AM.Hardware.IO/Advantech)
- `AdvantechNative` + `AdvantechMotionController : IMotionController` — P/Invoke Common Motion API (ADVMOT.dll).
- `AdvantechAdamIoModule : IIoModule` — ADAM-6000 series qua Modbus TCP (DI/DO/AI + WriteAndWaitConfirm).

### ✅ Mitsubishi + Siemens PLC (tự implement protocol, zero dependency)
- `MitsubishiPlcDevice : IPlcDevice` — **MC Protocol 3E binary** qua socket (D/M/X/Y/W/R/B/L, hex/dec radix).
- `SiemensS7PlcDevice : IPlcDevice` — **S7comm / ISO-on-TCP (RFC1006)**: COTP CR + Setup Comm + ReadVar/WriteVar,
  vùng DB/M/I/Q, big-endian, địa chỉ DB10.DBW20 / DB10.DBX0.1 / MW100...

### ✅ Robot (AM.Hardware.Comm/Robot)
- `SocketRobotDevice : IRobotDevice` — TCP ASCII command/response theo dòng, template lệnh cấu hình.
- `SimulatedRobotDevice : IRobotDevice` — in-memory.

### ✅ DI wiring + config
- `Bootstrapper.RegisterRealHardware()` — chọn driver theo `appsettings` (UseSimulation=false):
  Motion: Simulated|Gts|Advantech|InovanceServo · Plc: Inovance|Mitsubishi|Siemens · Io: Simulated|AdvantechAdam · Robot: Simulated|Socket.
- Simulation branch: thêm `IPlcDevice`→SimulatedPlcDevice, `IRobotDevice`→SimulatedRobotDevice.
- `appsettings.json`: thêm block `Motion`/`Plc`/`Robot`/`Io` (host/port/slave/vendor).

### ✅ Tests (AM.Hardware.Tests — project mới, 17 tests)
- `ModbusTcpClientTests` — round-trip FC03/FC06/FC16 qua **Modbus slave giả loopback** (verify MBAP thật trên wire).
- `InovancePlcDeviceTests` — ánh xạ địa chỉ + word/dword/float/bit qua SimulatedModbusClient.
- `SocketRobotDeviceTests` — giao thức line-based qua **robot server giả loopback**.
- `SimulatedDeviceTests` — SimulatedPlcDevice, SimulatedRobotDevice, AdvantechAdamIoModule.

### ⚠️ Ghi chú phần cứng SDK-native
- SDK 固高 GTS (`gts.dll`) và Advantech (`ADVMOT.dll`) là DLL độc quyền theo card, **không tải qua NuGet được**.
  Driver dùng P/Invoke nên build không cần DLL và chạy thật khi PC sản xuất đã cài driver/SDK của card.
- Các hằng số bit-status (GTS/Advantech) và register map (Inovance servo) cần đối chiếu manual của thiết bị.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **Build succeeded, 0 Warning, 0 Error**.
- `dotnet test` → **55 passed** (38 services + 17 hardware).

---

## [Session 8] 2026-06-02 — DI wiring + Demo 3-tier + Unit Tests + Dashboard

**Commit:** `9f6898f`
**Người thực hiện:** Claude (Cowork) + Nhan
**Mục tiêu:** Vá 3 lỗ hổng làm framework "viết xong nhưng chưa dùng được" + thêm UI đầu tiên.

### ✅ Task 1 — Fix Bootstrapper DI (Gap 1)
- `AM.Application.Shell/Bootstrapper.cs`:
  - Đăng ký `IHardwareManagerService → HardwareManagerService` và `IStationSyncService → StationSyncService`
    (trước đây code đã viết nhưng KHÔNG có trong DI → mọi `Resolve<T>()` fail at runtime).
  - Thêm `RegisterDemoMachine()` — đăng ký DemoPick/DemoInspect Mechanism, DemoStation,
    DemoMasterController, và map `IMasterController → DemoMasterController` (cho Dashboard resolve).
  - Thêm `RegisterHardwareDevices()` — đăng ký 8 hardware device vào named registry của
    HardwareManagerService (MainMotion/MainCamera/MainIO/MainModbus/MainSerial/MainTcp/MainOpcUA/MainEthernetIP).
- `AM.Application.Shell/App.xaml.cs`: gọi `RegisterHardwareDevices(_serviceProvider)` sau khi build container.

### ✅ Task 2 — Demo machine 3-tier (Gap 2)
Minh hoạ kiến trúc 3 tầng cho developer làm máy mới:
- `AM.WorkStation.Demo/Mechanisms/DemoPickMechanism.cs` — `[MechanismUI]`, extends BaseMechanism, gọi IMotionController.
- `AM.WorkStation.Demo/Mechanisms/DemoInspectMechanism.cs` — extends BaseMechanism, gọi ICameraDevice.
- `AM.WorkStation.Demo/Stations/DemoStation.cs` — `[StationUI]`, extends StationBase<DemoStation>, điều phối 2 mechanism.
- `AM.WorkStation.Demo/Controllers/DemoMasterController.cs` — extends BaseMasterController, template methods
  InitializeCoreAsync/RunOneCycleAsync/ResetCoreAsync/ShouldReinitialize.
- `AM.WorkStation.Demo.csproj`: thêm ProjectReference `AM.Infrastructure`.

### ✅ Task 3 — Unit Tests (Gap 3)
- `AM.Services.Tests/` (xUnit + Moq + FluentAssertions, net9.0):
  - `AlarmServiceTests.cs` — raise/ack/clear, level resolution theo code range (safety critical).
  - `RecipeServiceTests.cs` — load/save/getall.
  - `StationSyncServiceTests.cs` — RegisterSlot, Signal/WaitAsync, timeout, cancellation, ResetAll, multi-slot.
  - `GlobalUsings.cs` — global using Xunit.
- Thêm project vào solution. **38/38 tests pass, 0 warning** (đã fix xUnit1031, CA2007, S6608).

### ✅ Task 4 — WPF Dashboard module
- `AM.Modules.Dashboard/` (net9.0-windows, UseWPF, CommunityToolkit.Mvvm):
  - `DashboardViewModel.cs` — ObservableObject; bind MachineState + CycleCount + ActiveAlarms;
    RelayCommand Initialize/Start/Stop/Pause/Resume/Reset với CanExecute theo state; subscribe
    StateChanged/CycleCompleted/AlarmRaised/AlarmCleared; UI-thread marshalling qua SynchronizationContext; IDisposable.
  - `DashboardView.xaml` + `.xaml.cs` — ISA-101 layout: state indicator (Ellipse màu theo state),
    control buttons (48px, nút Dừng tách 48px màu đỏ), DataGrid alarm list. Dùng DynamicResource color tokens.
  - `Converters/MachineStateToColorConverter.cs` — MachineState → Status.*Brush.
- Thêm project vào solution.

### 🔍 Kết quả
- `dotnet build AM.AutoFrame.sln` → **Build succeeded, 0 Warning, 0 Error**.
- `dotnet test` → **38 passed**.

---

## [Session 7] 2026-05-31 — Solution Structure Docs + HMI Design Rules

**Commit:** `8fa4568`
**Người thực hiện:** Claude (Cowork) + Nhan
**Nguồn tham khảo:** AutoMachine_Solution_Structure.md + HMI_UI_Design_Rules_1.md

### ✅ Thêm mới / Cập nhật

- `CLAUDE.md` — Cập nhật **Cấu trúc solution** đầy đủ:
  - Thêm `AM.Hardware.Communication` (Serial/TCP/Modbus/SECS-GEM)
  - Thêm interfaces còn thiếu: `IAxisGroup`, `ILightController`, `ICommunicationDevice`, `IUserService`, `IProductionService`, `ILocalizationService`
  - Thêm `AM.Modules.*` (10 UI modules: Alarm, Parameter, Production, IO, Motion, Vision, Identity, Logging, Diagnostics, SecsGem)
  - Thêm `AM.UI.Controls/` và `AM.UI.Resources/` (Themes: Colors.Dark/Light, Typography, Controls, StatusStyles)
  - Thêm cấu trúc `AM.WorkStation.{MachineName}/` đầy đủ (Steps, SubRoutines, Recipe, Config/AxisMap+IOMap)
- `.claude/skills/am-wpf-mvvm/SKILL.md` — Bổ sung **ISA-101 HMI Design Rules**:
  - Semantic color tokens (Status.Normal/Warning/Alarm/Critical/Disabled — bất biến giữa themes)
  - Background tokens Dark/Light đầy đủ với hex values
  - Quy tắc màu KHÔNG ĐƯỢC VI PHẠM (đỏ/vàng chỉ cho alarm, equipment normal = xám)
  - DataTrigger pattern cho device state
  - Animation rules (chỉ Critical alarm 1 Hz, không vượt 3 Hz SEMI S8)
  - ListView virtualization template
  - ThemeService runtime switch Dark/Light
  - Format số liệu và đơn vị (decimal places, timestamp 24h)
  - ISA-101 checklist trước khi release màn hình
- `.claude/skills/am-hmi-design/SKILL.md` — **Skill mới**: HMI design reference đầy đủ:
  - 4-level screen hierarchy (Overview → Process Area → Faceplate → Engineering)
  - Layout shell cố định (Top Bar/Side Menu/Content/Status Bar)
  - Alarm display rules ISA-18.2/EEMUA 191 (mức độ, màu, nhấp nháy, rate)
  - Equipment state symbols (≥2 cách biểu thị: màu + icon/text)
  - I/O display rules (DI ○/● khác DO □/■)
  - Navigation rules (max 3 click, breadcrumb)
  - Performance targets (response time, update rate per data type)
  - Release checklist đầy đủ (màu, alarm, navigation, data, ergonomics, i18n)

### 🔧 Quyết định chọn lọc
- **CLAUDE.md**: chỉ cập nhật cấu trúc solution (bản đồ file), không copy toàn bộ implementation detail
- **am-wpf-mvvm**: thêm ISA-101 rules vào skill sẵn có (không tạo file trùng lặp)
- **am-hmi-design**: tạo skill riêng vì HMI design là domain riêng biệt, lazy-load khi cần
- **Không thêm**: toàn bộ XAML ResourceDictionary files (200+ dòng) → quá dài, developer tự viết theo template

---

## [Session 6] 2026-05-31 — 3-Tier Base Classes + Services + Coding Rules Alignment

**Commit:** `832266f`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới

- `AM.Infrastructure/BaseMechanism.cs` — Abstract base cho Mechanism: `IsBusy` guard dùng `Interlocked`, `EmergencyStop` wrapper không throw, template methods (`InitializeCoreAsync`, `HomeCoreAsync`, `OnEmergencyStop`), `ExecuteWithBusyGuardAsync<T>` helper, `IAsyncDisposable`
- `AM.Infrastructure/StationBase.cs` — Abstract base cho Station: `RegisterMechanism`, `SetState` + `StateChanged` event, `RunCycleAsync` template, parallel `HomeAsync` cho tất cả mechanisms, typed logger `ILogger<TStation>`
- `AM.Infrastructure/BaseMasterController.cs` — Abstract base cho MasterController: ISA-88 transition table (13 transitions), `FireTrigger` thread-safe, `CheckPauseAsync` gate (SemaphoreSlim), run loop với 3-catch hierarchy, `CancelAsync` cho stop/dispose
- `AM.Services/HardwareManagerService.cs` — Implement `IHardwareManagerService`: device registry, `Resolve<T>` type-safe, `ConnectAllAsync/DisconnectAllAsync` dùng pattern matching (IMotionController/ICameraDevice/IIoModule)
- `AM.Services/StationSyncService.cs` — Implement `IStationSyncService`: `SemaphoreSlim`-based pipeline sync, `RegisterSlot/Signal/WaitAsync(timeout)`, `ResetAll`, `IDisposable`
- `AM.Core/Models/EventArgs/MachineStateChangedEventArgs.cs` — EventArgs cho StateChanged (CA1003): `PreviousState`, `NewState`, `Trigger`, `ChangedAt`
- `AM.Core/Models/EventArgs/CycleCompletedEventArgs.cs` — EventArgs cho CycleCompleted (CA1003): `CycleCount`, `CompletedAt`

### 🔧 Sửa đổi

- `AM.Core.Abstractions/Interfaces/Machine/IStation.cs` — `StateChanged` event: `EventHandler<MachineState>` → `EventHandler<MachineStateChangedEventArgs>` (CA1003)
- `AM.Core.Abstractions/Interfaces/Machine/IMasterController.cs` — `StateChanged` + `CycleCompleted` event: dùng EventArgs wrappers (CA1003)
- `AM.Infrastructure/AM.Infrastructure.csproj` — Thêm `AM.Core.Abstractions` project reference
- `AM.Application.Shell/AM.Application.Shell.csproj` — Thêm explicit `Microsoft.Extensions.Configuration.*` packages (fix wpftmp build)
- `AM.Application.Shell/App.xaml` — **Bug fix**: Xóa `StartupUri` (crash runtime), thêm color token + string ResourceDictionary
- `AM.Application.Shell/MainWindow.xaml` — `{DynamicResource ...}` thay hardcoded colors, `{StaticResource ...}` thay hardcoded strings
- `AM.Application.Shell/MainWindow.xaml.cs` — `System.Windows.Application.Current.Resources[...]` thay `Brushes.Red/Green`
- `AM.Application.Shell/Bootstrapper.cs` — CA1515→`internal`, S125 remove commented code, CA1305 + CultureInfo.InvariantCulture, CA2007 fix
- `AM.Services/AlarmService.cs` — Fix `ResolveLevel`: dùng range constants (10000-69999→High, 40000-49999+70000-79999→Critical) thay vì specific alarm codes (MotionTimeout/SystemCritical đã bị miss)
- `AM.Data/Repositories/AlarmRepository.cs` — Fix `MapToModel`: restore `IsAcknowledged`, `AcknowledgedAt`, `AcknowledgedBy` từ entity
- `AM.Core/Models/AlarmModel.cs` — `Acknowledge(operatorId, DateTime?)` — optional timestamp để preserve gốc khi load từ DB
- `AM.WorkStation.Demo/Steps/Step01Initialize.cs` — Rename file (bỏ underscore, B1 fix)
- `AM.WorkStation.Demo/Steps/Step02Inspect.cs` — Rename file (bỏ underscore, B2 fix)
- `Directory.Build.props` — `TargetFramework`: `net8.0-windows` → `net9.0` (khớp với .csproj thực tế)

### 🐛 Bugs đã fix

- **Runtime crash**: `StartupUri="MainWindow.xaml"` + `async void OnStartup` tạo 2 MainWindow (thiếu parameterless ctor → crash)
- **CA1003**: Events `StateChanged<MachineState>`, `CycleCompleted<int>` → phải dùng `EventArgs` subclass
- **Alarm severity bug**: `MotionTimeout` (10001) và `MotionNotHomed` (10002) bị resolve thành `Medium` vì range check dùng `AlarmCodes.MotionEstop` (10003) làm lower bound
- **B1/B2 CA1707**: File names `Step01_Initialize.cs`, `Step02_Inspect.cs` đổi thành không có underscore
- **DB data loss**: `AlarmRepository.MapToModel` bỏ sót acknowledged state khi load history

### 🔧 Quyết định kiến trúc

1. **`BaseMasterController` dùng static `Dictionary<>` transition table** (không dùng `Stateless` NuGet): Tránh thêm dependency, state machine đủ đơn giản, performance tốt hơn với Dictionary direct access (CA1859).
2. **`StationBase<TStation>` generic** (CRTP pattern): `ILogger<TStation>` cho phép log entries hiển thị tên concrete class thay vì `StationBase` — suppressed S6672 với justification rõ ràng.
3. **`HardwareManagerService.ConnectAllAsync` dùng pattern matching** (không reflection): Type-safe, no runtime overhead, explicit về hardware interfaces được support — future hardware interfaces cần thêm vào switch.
4. **`EmergencyStop` trong BaseMechanism/StationBase/BaseMasterController**: Luôn wrap trong try-catch với CA1031 pragma — safety critical path KHÔNG được throw bất cứ điều gì.

---

## [Session 5] 2026-05-30 — Karpathy Rules + Alarm Dictionary + Context Management

**Commit:** `be47f2a`
**Người thực hiện:** Claude (Cowork) + Nhan
**Nguồn tham khảo:** `Claude_Effective_Usage_AutoMachine.md` (Karpathy/ECC/Anthropic best practices)

### ✅ Thêm mới / Cập nhật

- `CLAUDE.md` — Thêm **Karpathy 4 Rules** (Think First / Surgical / Simple / Success First) dưới dạng bảng ngắn gọn. Claude tự áp dụng trong mọi task, kể cả khi user không nhắc.
- `file hướng dẫn code/PROMPT_TEMPLATES.md` — Thêm **PT-00 Plan Template**: format plan chuẩn cho task > 30 phút (Files thay đổi, Approach options, Steps, Success Criteria, Confirm).
- `file hướng dẫn code/QUICK_REFERENCE.md` — Thêm 4 section mới:
  - **Magic Phrases** — 8 câu thêm vào prompt để điều chỉnh hành vi Claude
  - **Anti-patterns** — 6 lỗi phổ biến + cách đúng
  - **Hallucination signs** — 6 dấu hiệu Claude đang bịa + cách xử lý
  - **Context Health** — 5 dấu hiệu session đang có vấn đề
- `.claude/skills/am-alarm-dictionary/SKILL.md` — **Skill mới**: alarm code ranges, format message chuẩn, template AlarmCodes.cs, cách throw AlarmException, checklist thêm alarm, giải thích isStoppable.

### 🔧 Quyết định chọn lọc nội dung
Từ 730 dòng tài liệu gốc, chỉ lấy 4 thứ **chưa có** trong dự án:
1. Karpathy rules → CLAUDE.md (hành vi Claude, không phải coding rules)
2. Magic phrases + anti-patterns → QUICK_REFERENCE.md (tham khảo nhanh)
3. Plan template → PROMPT_TEMPLATES.md (dùng trước task lớn)
4. Alarm dictionary skill → skills/ (lazy-load khi cần)

**Không thêm:** model selection table (đã có trong AGENTS.md), daily workflow (đã có), phase workflow (quá meta), context % advice (không kiểm soát được từ user).

---

## [Session 4] 2026-05-29 — Claude Code Hooks

**Commit:** `TBD`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới

- `.claude/hooks/pre-write-arch.sh` — **PreToolUse Write**: Kiểm tra file C# sắp viết đúng layer chưa (interface → Abstractions, hardware driver → Hardware.*, Step naming CA1707, Service không new concrete class)
- `.claude/hooks/post-write-cs.sh` — **PostToolUse Write+Edit**: Sau khi lưu file .cs, tự động kiểm tra CA1707 (underscore), CA2000 (CancellationTokenSource), RSPEC-6602/6605 (LINQ), CA1031 (bare catch), thiếu file header, thiếu XML doc
- `.claude/hooks/post-build.sh` — **PostToolUse Bash**: Sau `dotnet build/test`, parse output thành bảng tóm tắt — violations theo rule code, files có lỗi, top errors cần fix
- `.claude/hooks/check-session-end.sh` — **Stop**: Khi Claude kết thúc, kiểm tra uncommitted changes + hiển thị TODO list từ PROJECT_STATUS.md, nhắc chạy `/am-done`

### 🔧 Sửa đổi
- `.claude/settings.local.json` — Wire 4 hooks: PreToolUse(Write), PostToolUse(Write), PostToolUse(Edit), PostToolUse(Bash), Stop

### 🔧 Quyết định thiết kế
1. **Exit 0 cho tất cả hooks (không block)**: Hooks chỉ cảnh báo, không chặn Claude — tránh false positive làm gián đoạn workflow. Nếu muốn chặn hoàn toàn đổi `exit 0` → `exit 2` trong `pre-write-arch.sh`.
2. **Shell script thay vì Node.js**: Không cần runtime cài thêm, portable trên mọi môi trường Linux/WSL/sandbox.
3. **post-write-cs chạy trên file path** (không phải stdin content): Đọc trực tiếp file vừa lưu để phân tích chính xác hơn là phân tích content từ JSON input.

---

## [Session 3] 2026-05-29 — Hệ thống tracking + Auto-commit workflow

**Commit:** `d068d2e`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới
- `PROJECT_STATUS.md` — **Mới**: Snapshot toàn bộ trạng thái dự án, TODO list, known bugs, key files map. Claude đọc file này đầu mỗi session thay vì scan toàn bộ source.
- `CHANGELOG.md` — **Mới**: Lịch sử thay đổi theo session, ghi cả quyết định kiến trúc.
- `scripts/am-commit.sh` — **Mới**: Shell script git add+commit+push tự động xử lý Windows filesystem index.lock issue (dùng `mv` thay vì `rm`).
- `.claude/commands/am-done.md` — **Mới**: Slash command `/am-done` — workflow cuối session: cập nhật status → thêm changelog entry → commit → push.
- `.claude/hooks/check-session-end.sh` — **Mới**: Hook chạy khi Claude Stop, kiểm tra nếu có uncommitted changes mà status chưa cập nhật thì nhắc chạy `/am-done`.

### 🔧 Sửa đổi
- `CLAUDE.md` — Thêm checklist bắt buộc cuối session (cập nhật status + changelog + commit).
- `.claude/settings.local.json` — Thêm `permissions.allow` cho git commands + `hooks.Stop` trigger hook khi Claude kết thúc.

### 🔧 Quyết định thiết kế
1. **PROJECT_STATUS.md thay vì chỉ CLAUDE.md**: CLAUDE.md mô tả kiến trúc (ít thay đổi), PROJECT_STATUS.md mô tả trạng thái hiện tại (thay đổi mỗi session) — tách biệt để tiết kiệm token khi Claude đọc.
2. **`mv` thay vì `rm` cho index.lock**: Windows filesystem mount không cho phép unlink file trong `.git/`, nhưng cho phép rename/move.
3. **Hook Stop + /am-done command**: Hook nhắc tự động, command cho phép Claude chủ động thực hiện — hai lớp đảm bảo không bỏ sót update.

---

## [Session 2] 2026-05-29 — Kiến trúc 3 tầng + Claude Code Integration

**Commit:** `cb525a5`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới

#### AM.Core — Enums
- `Enums/MachineState.cs` — Cập nhật từ 10 state → **8 state ISA-88**: Uninitialized, Initializing, Idle, Running, Paused, InitAlarm, RunAlarm, Resetting
- `Enums/MachineTrigger.cs` — **Mới**: 10 triggers (Initialize, InitializeDone, Start, Pause, Resume, Stop, Error, Reset, ResetDone, ResetDoneUninitialized)
- `Enums/OperationMode.cs` — **Mới**: Normal, DryRun
- `Enums/HardwareCategory.cs` — **Mới**: 9 categories (General, Axis, IOController, Camera, Robot, Scanner, Instrument, MotionCard, LightController)
- `Enums/UserLevel.cs` — **Mới**: Null=-1, Operator=0, Engineer=1, Administrator=2, SuperUser=3

#### AM.Core — Attributes
- `Attributes/AlarmInfoAttribute.cs` — **Mới**: `[AlarmInfo(displayName, remedy, isStoppable)]` cho alarm code constants
- `Attributes/MechanismUIAttribute.cs` — **Mới**: `[MechanismUI(displayName, group, order)]` cho Mechanism classes
- `Attributes/StationUIAttribute.cs` — **Mới**: `[StationUI(displayName, icon, order)]` cho Station classes
- `Attributes/ModuleNavigationAttribute.cs` — **Mới**: `[ModuleNavigation(displayName, icon, region, order)]` cho Prism Views
- `Attributes/ParamViewAttribute.cs` — **Mới**: `[ParamView(label, unit, min, max, group, order)]` cho Recipe properties

#### AM.Core.Abstractions — Machine interfaces
- `Interfaces/Machine/IMechanism.cs` — **Mới**: Name, Category, IsReady, IsBusy, InitializeAsync, HomeAsync, EmergencyStop
- `Interfaces/Machine/IStation.cs` — **Mới**: Name, State, Mechanisms, StateChanged event, RunCycleAsync, EmergencyStop
- `Interfaces/Machine/IMasterController.cs` — **Mới**: ISA-88 full state machine, InitializeAsync/StartAsync/StopAsync/ResetAsync, SetOperationMode

#### AM.Core.Abstractions — Service interfaces
- `Interfaces/Services/IHardwareManagerService.cs` — **Mới**: Register, Resolve<T>, ResolveAll<T>, IsRegistered, ConnectAllAsync, DisconnectAllAsync
- `Interfaces/Services/IStationSyncService.cs` — **Mới**: RegisterSlot, Signal, WaitAsync (2 overloads), ResetAll

#### Claude Code Integration (.claude/)
- `.claude/rules/common/coding-standards.md` — **Mới**: 17 rules R01–R17 (safety, arch, async, hardware timeout, exceptions, logging, null safety, simulation, IDisposable, file header, XML doc, Sonar table, attributes, UserLevel)
- `.claude/rules/csharp/csharp-patterns.md` — **Mới**: 15 patterns CS01–CS15 (naming, constructor, timeout, exception filter, sequence loop, List.Find/Exists, JsonOptions, ConfigureAwait, state machine switch, WPF dispatch, IDisposable, EF Core, records, sealed)
- `.claude/commands/am-new-driver.md` — **Mới**: Slash command tạo hardware driver
- `.claude/commands/am-new-step.md` — **Mới**: Slash command tạo sequence step (enforces no underscore)
- `.claude/commands/am-new-mechanism.md` — **Mới**: Slash command tạo Mechanism với [MechanismUI]
- `.claude/commands/am-new-station.md` — **Mới**: Slash command tạo Station với [StationUI]
- `.claude/commands/am-new-screen.md` — **Mới**: Slash command tạo WPF screen ISA-101
- `.claude/commands/am-alarm.md` — **Mới**: Slash command thêm alarm code
- `.claude/commands/am-review.md` — **Mới**: Slash command review code (10 categories)
- `.claude/commands/am-test.md` — **Mới**: Slash command tạo unit tests
- `.claude/skills/am-hardware-patterns/SKILL.md` — **Mới**: Interface + real driver + simulator templates
- `.claude/skills/am-sequence-patterns/SKILL.md` — **Mới**: Step + MachineSequence templates
- `.claude/skills/am-mechanism-patterns/SKILL.md` — **Mới**: [MechanismUI] + hwManager.Resolve patterns
- `.claude/skills/am-station-patterns/SKILL.md` — **Mới**: [StationUI] + IStationSyncService patterns
- `.claude/skills/am-testing/SKILL.md` — **Mới**: xUnit + Moq + FluentAssertions templates
- `.claude/skills/am-wpf-mvvm/SKILL.md` — **Mới**: ViewModel + XAML + Prism Module + ISA-101 checklist

#### AI Documentation
- `CLAUDE.md` — **Mới** (project root): Project instructions cho Claude — kiến trúc, build rules, workflow
- `file hướng dẫn code/AGENTS.md` — **Cập nhật**: Thêm ECC routing table + 3 agents mới (MechanismDeveloper, StationDeveloper, MasterControllerDeveloper), cập nhật agents cũ lên .NET 9
- `file hướng dẫn code/PROMPT_TEMPLATES.md` — **Cập nhật**: Thêm PT-12 (Mechanism), PT-13 (Station), PT-14 (MasterController)
- `file hướng dẫn code/QUICK_REFERENCE.md` — **Cập nhật**: Thêm 3-tier arch rules, slash commands, attributes quick ref, state machine diagram, ISA-88 naming conventions
- `.cursorrules` — **Cập nhật**: Thêm R-ARCH-05/06/07, R-MECH-01/02/03/04, R-STATION-01/02/03/04, R-PERM-01/02/03, R-ATTR-01/02

### 🔧 Quyết định kiến trúc

1. **State machine 8 trạng thái thay vì 10**: Giảm phức tạp, bỏ `Stopping` và `Homing` riêng lẻ. `Resetting` bao gồm cả home sequence.
2. **IHardwareManagerService thay vì inject hardware trực tiếp**: Mechanism không nhận `IMotionController` qua DI constructor, thay vào đó gọi `hwManager.Resolve<T>("name")` — linh động hơn khi cấu hình nhiều hardware cùng loại.
3. **StationSyncService dùng SemaphoreSlim**: Không busy-wait, không dùng event/flag thủ công — tránh race condition và deadlock.
4. **Attributes cho auto-registration**: `[MechanismUI]`, `[StationUI]`, `[ModuleNavigation]` cho phép UI tự scan + đăng ký panel/tab/menu mà không cần code thủ công trong Bootstrapper.

---

## [Session 1] 2026-05-28 — Initial Framework Setup

**Commit:** `fe716a9`
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Tạo mới từ đầu

#### Solution & Build
- `AM.AutoFrame.sln` 