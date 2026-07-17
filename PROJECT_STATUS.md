# PROJECT_STATUS.md — AM.AutoFrame
> **⚡ Claude: Đọc file này TRƯỚC khi bắt đầu bất kỳ thay đổi nào.**
> File này là snapshot trạng thái dự án. Cập nhật cuối cùng sau mỗi session làm việc.
>
> 📌 **Tiếp tục mạch HMI v2 + Guard Engine (S44–S57)?** Đọc **`docs/SESSION_HANDOFF.md`** — bàn giao chi tiết:
> trạng thái, các BẪY đã gặp (cross-thread UserChanged, users.json migration, analyzer), workflow, và roadmap
> phần còn hoãn (Force IO, HardwareInputEventBus, thao tác trạm, Override...).

---

## 🗓️ Cập nhật lần cuối
**Ngày:** 2026-07-17
**Session:** #95 — **Học màn manual máy tham khảo RefSeq-A (form template trạm): backup bảng điểm khi lưu + chạy lặp 2 điểm**. Chủ dự án yêu cầu đối chiếu form template màn manual của RefSeq-A với màn Trục & Điểm — kết quả rà: các chức năng chính (bảng điểm move/teach 1 trục hoặc cả điểm, bảng đèn tín hiệu, jog abs/rel, gate quyền từng khối) ta ĐÃ CÓ tương đương (mẫu 2 chạm + guard thay confirm dialog); 2 thứ đáng học đã làm: **(1) Backup bảng điểm mỗi lần lưu** — `PointTableService.SaveAsync` snapshot file cũ vào `points-backup/points_{timestamp}.json` TRƯỚC khi ghi đè (giữ 20 bản mới nhất; backup lỗi không chặn lưu chính) → teach nhầm có đường lùi; **(2) Chạy lặp 2 điểm** (kiểm độ lặp lại khi cân máy bằng đồng hồ so): khối mới trong card Bảng điểm — combo A ⇄ combo B + số vòng (1–100) + nút ▶ Chạy lặp/■ Dừng + tiến độ "vòng i/n"; guard R2 (Engineer + máy dừng) + audit `RepeatRun A <-> B xN`; **STOP đỏ và rời màn đều hủy vòng lặp**; combo/ô vòng khóa khi đang chạy. Chưa làm (ghi nhận): ảnh trạm minh hoạ per-station (cần asset thật — P5). i18n +8 key ×3. **+1 test → 340 pass**. **Smoke UIA end-to-end**: Home tất cả → chọn Home⇄PickUp ×2 → tiến độ "vòng 1/2" hiện → "Chạy lặp xong" + audit đúng; phát hiện đúng thiết kế: trục CHƯA home thì chạy lặp bị chặn alarm 10002; bấm Lưu → `points-backup/` sinh snapshot. **Còn hàng đợi**: P5 máy thật.
**Commit:** `(điền sau)`  ·  (S94: `bbdce33` · S93: `4073c4c` · S92: `e27ba92`)

---

## 🗓️ Session #94
**Session:** #94 — **Gộp Bảng điểm vào pane Điều khiển trục → sub-tab "Trục & Điểm" (yêu cầu chủ dự án)**. Vấn đề: muốn "trục di chuyển đến điểm đã cài" phải nhảy giữa 2 sub-tab (Điều khiển trục ↔ Bảng điểm); luồng teach thực tế còn nặng hơn (jog → teach → jog tinh chỉnh → teach lại → thử Tới — ping-pong liên tục). Rà 6 sub-tab màn Vận hành tay, đưa 3 phương án qua AskUserQuestion (A gộp hẳn 1 tab / B giữ 2 tab + khối điểm nhanh / C hai cột) — **chốt A**: bảng điểm (bảng X/Y/Z/U + chọn 2 chạm + thanh Tới/Teach/Lưu + hint) chuyển nguyên khối xuống DƯỚI khu điều khiển trục trong cùng pane, full-width; bỏ sub-tab "Bảng điểm" → còn 5 sub-tab; tab đầu đổi nhãn **"Trục & Điểm"** (Axes & Points / 轴与点位). Index sub-tab GIỮ NGUYÊN số cũ (1 bỏ trống — không đánh lại 2..5, tránh đụng mọi ConverterParameter). **Fix kèm theo**: `StatusMessage` (lý do guard chặn jog/move) trước giờ CHỈ hiển thị ở pane Bảng điểm — jog bị từ chối ở pane trục không thấy lý do; gộp xong hiển thị ngay trên pane thao tác. Key `Manual.Tab.Points` bỏ (3 ngữ). **Smoke UIA** (tạo `bin/points.json` 3 điểm demo Home/PickUp/Place — bài học test-với-dữ-liệu): tab đầu "Trục & Điểm" hiện đúng, sub-tab Bảng điểm biến mất, bảng điểm + 3 điểm hiện ngay trên pane trục, chạm "Home" → thanh chọn "Đang chọn: Điểm · Home" hiện. Build 0 warning; không đổi logic VM (GoToSelection/Teach/Save giữ nguyên) nên không cần test mới — 339 pass giữ nguyên. **Còn hàng đợi**: P5 máy thật.
**Commit:** `bbdce33`  ·  (S93: `4073c4c` · S92: `e27ba92` · S91: `a42c64e`)

---

## 🗓️ Session #93
**Session:** #93 — **Trang "Thông số máy" + toàn vẹn cấu hình SHA-256 + sửa layout Người dùng (3 yêu cầu chủ dự án)**. **(1) Thẻ MỚI "Thông số máy"** trong Cài đặt (`MachineConfigView(Model)`, Administrator + audit): *Nhận diện máy* (tên máy · line · vị trí → `machine.json`, GIỮ NGUYÊN stations khi ghi; mã máy day-code chỉ hiển thị) + *Kết nối thiết bị* (UseSimulation + IP/cổng Modbus·PLC·Robot·Scanner·ADAM·OPC-UA·EtherNet/IP → ghi thẳng `appsettings.json` chỉ đụng key màn này quản; validate cổng 1–65535) → lưu = audit `Machine.SaveConfig` + **tự ký lại manifest** + banner vàng "KHỞI ĐỘNG LẠI để áp dụng" (DI đọc config lúc boot). **(2) Câu hỏi gộp config → KHÔNG gộp** (`design-notes/0014`): file khác nhau về *vòng đời/người ghi* (app-tự-ghi points/parameters/users/recipes ↔ chỉnh-khi-deploy machine/axismap/io.map/analog.map/appsettings) — gộp thì app save đè tay sửa + hỏng 1 file mất tất; thay bằng **`IConfigIntegrityService`**: SHA-256 nhóm 7 file cấu-hình-máy vào `config.manifest.json` (kèm ai ký/lúc nào), **boot đối chiếu → lệch = alarm 40013** (phát hiện, KHÔNG chặn máy — 0012), bảng trạng thái Khớp/ĐÃ SỬA/MẤT FILE/Chưa ký + nút **"Ký lại"** (Administrator, audit); giới hạn trung thực ghi rõ: tamper-evident với thao tác thường, cần chống giả mạo thật thì HMAC DayCodeSecret (P5). **(3) Layout Người dùng & phân quyền**: bug chính — ListBoxItem thiếu `HorizontalContentAlignment=Stretch` nên cột `*` không giãn → combo/nút mỗi dòng dạt trái lệch nhau; sửa + **header cột** (Tài khoản/Quyền) thẳng hàng template, tiêu đề khối "Thêm tài khoản"/"Đặt lại mật khẩu", khối reset hiện hint "Chọn một tài khoản trước" khi chưa chọn (ẩn form), spacing 12 thống nhất, nút Thêm màu primary. **(4) BUG NGHIÊM TRỌNG phát hiện nhân tiện: `appsettings.json` KHÔNG được copy vào bin** từ trước tới nay (csproj thiếu CopyToOutputDirectory; config nạp `optional:true` từ BaseDirectory) → **app chạy toàn giá trị default, appsettings repo không có tác dụng** — đã thêm copy; các giá trị default trùng config hiện tại nên hành vi không đổi, nhưng từ giờ sửa config MỚI THẬT SỰ ăn. i18n +26 key ×3 + alarm 40013 ×3; backup targets +`config.manifest.json`. **+7 test → 339 pass toàn repo** (đếm lại chuẩn sau khi phát hiện dll test stale làm số S91/S92 báo thấp). **Smoke UIA end-to-end**: mở thẻ → 7 file "Chưa ký" → nạp đúng tên máy/IP → sửa tên+line → Lưu → 7 file "Khớp" + manifest + banner restart + audit 2 dòng + machine.json GIỮ stations → layout Users 3/3 đạt → **tamper io.map.json ngoài app → restart → alarm 40013 hiện đúng file trên banner**. **Còn hàng đợi**: P5 máy thật.
**Commit:** `4073c4c`  ·  (S92: `e27ba92` · S91: `a42c64e` · S90: `31a8d47`)

---

## 🗓️ Session #92
**Session:** #92 — **GÓI D phanh Z + tab Sản xuất chi tiết SP + fix quyền không cập nhật + gọn nav (4 yêu cầu chủ dự án)**. **(1) BUG quyền**: đăng nhập thấp → đăng xuất → đăng nhập admin mà màn gate vẫn đòi quyền — 2 nguyên nhân lớp nhau: `AuditViewModel`/`BackupViewModel` xử lý `UserChanged` đụng ObservableCollection trên THREAD NỀN (LoginAsync Task.Run) → handler ném cross-thread và **chặn mọi subscriber phía sau** (nav/gate đứng im). Sửa 2 lớp: marshal RunOnUIThread ở 2 VM (như UserAdminViewModel đã làm đúng) + `UserService.RaiseUserChanged` gọi **từng subscriber CÔ LẬP** try/catch-log (một VM hỏng không giết cập nhật của cả app — về sau ai quên marshal cũng không tái phát triệu chứng). **(2) Nav gọn 7 tab** (Bảng điều khiển·Sản xuất·Vision·Cảnh báo·Analog·Recipe·Cài đặt): chip Recipe header thành **hiển thị thuần** (hết 2 nút Recipe — tab nav là đường vào duy nhất); tab **Vận hành tay RA KHỎI nav** — cửa vào duy nhất = nút **Manual** action bar (cùng mạch nút chế độ Dry run·Từng bước, enable LineLead+; MainWindow thêm `ShowStandaloneView`); tab **Nhật ký → thẻ trong Cài đặt** (chất bảo trì, cạnh Chẩn đoán/Audit). **(3) Tab Sản xuất** thêm sub-tab **"Chi tiết sản phẩm"** (trả lời "chưa có thông tin chi tiết từng sản phẩm"): bảng từng SP trong cửa sổ (thời gian·SN·recipe·KQ màu·cycle·điểm vision·lý do NG·người vận hành, virtualized 500 dòng) + lọc **SN contains + KQ OK/NG** (lọc client-side trên record đã nạp) + panel **Pareto NG theo lý do** top 10 (đếm+%+bar, tính trên toàn kết quả lọc); pane Tổng quan giữ nguyên KPI+trend. **(4) GÓI D — phanh trục Z** (hybrid chốt S90, `design-notes/0013`): interface **`IAxisBrake`** (capability tuỳ chọn như IAxisJog — controller không có thì UI ẩn hẳn); sim implement; UI trong Điều khiển trục: nhả = **2 bước** (guard R2 Engineer+máy dừng → cảnh báo "trục rơi tự do" → xác nhận) + **alarm 10009 banner đỏ thường trực** cả app khi đang nhả + đóng = 1 chạm không cần quyền; **bất biến an toàn: rời màn (Unloaded) / đổi user / rớt quyền → phanh TỰ ĐÓNG**; audit `Brake.Release/Engage Z` kèm lý do. i18n +31 key ×3 + alarm 10009 ×3. **+3 test → 320 pass**. **Smoke UIA end-to-end**: nav đúng 7 tab · Manual mở Vận hành tay · nhả phanh 2 bước → banner đỏ + alarm toàn app → về Home → mở lại: ĐÃ TỰ ĐÓNG · audit ghi cả 2 chiều kèm "tự đóng: rời màn Vận hành tay" · Sản xuất sub-tab chi tiết + lọc + Pareto hiện · thẻ Nhật ký trong Cài đặt mở được. **Còn hàng đợi**: P5 máy thật (IAxisBrake driver thật map DO phanh; ZAxisIndex vào machine.json).
**Commit:** `e27ba92`  ·  (S91: `a42c64e` · S90: `31a8d47` · S89: `231ee0e`+`bcef5b3`)

---

## 🗓️ Session #91
**Session:** #91 — **GÓI C: Giám sát analog (áp suất/chân không/nhiệt độ/lưu lượng) — ngưỡng 4 mức + time van LƯU TRONG RECIPE** (chốt với chủ dự án S90: "Theo RECIPE" — đổi sản phẩm là đổi bộ ngưỡng). **Model**: `AnalogChannelConfig` (kênh máy khai trong `analog.map.json` cạnh exe — Id/Name/Unit/AiChannel + scale tuyến tính RawMin..RawMax→EngMin..EngMax, thang âm chân không OK + `SafeMin/SafeMax` tuỳ chọn) + `AnalogLimits` (4 mức **Lv Pick Up / On Check / Blow Off / Off Check** + **OnTimeMs/OffTimeMs/BlowTimeMs** — trễ van/xilanh trước khi kiểm, trả lời câu hỏi "thời gian trễ tín hiệu" của chủ dự án) lưu trong `RecipeBase.AnalogLimits` (dict theo Id kênh). **Service**: `IAnalogMonitorService`/`AnalogMonitorService` — nạp map tolerant (thiếu file = máy không có analog, hợp lệ; file hỏng = log + bỏ qua), poll 200ms PeriodicTimer, một kênh đọc lỗi → giá trị null nhưng KHÔNG giết vòng poll; **khoảng an toàn CHỈ xét khi máy Running** (máy đứng vacuum về 0 là bình thường) + debounce 5 mẫu (1s) + alarm **30006** MỘT lần/đợt vượt (re-arm khi về trong khoảng). **UI**: module MỚI `AM.Modules.Analog` (project #30) — tab "Analog" (order 30): lưới card kênh (giá trị live + bar theo thang + tóm tắt ngưỡng ↑/on/blow/↓) + panel kênh chọn 7 ô nhập (label luôn hiện) → **Ghi vào recipe** (Engineer+ — dưới quyền: ô disable + hint; chưa có recipe active → báo nạp trước; parse Invariant→CurrentCulture) + **audit** `Analog.WriteLimits.{id}` kèm giá trị. **Sim**: `SimulatedIoModule` AI có giá trị nền 2–8V/kênh + random-walk ±0.03V. Demo map 4 kênh: VAC_PP1/VAC_PP2 (0..-100 kPa) · PRESS_MAIN (0..1000 kPa, safe 150–900) · TEMP_HEAD (0..100°C, safe ≤95). Backup targets +`analog.map.json`; i18n +18 key ×3 + alarm 30006 ×3. **+12 test case → 317 pass toàn repo** (scale, map tolerant, poll, debounce+Running-only, kênh lỗi cô lập). **Smoke UIA end-to-end**: 4 card live đúng scale → login engineer → nạp recipe Default → chọn PP1 → ghi 7 giá trị → status "Đã ghi ngưỡng Vacuum PP1 vào recipe Default." + card cập nhật "↑-60 · on -55 · blow -5 · ↓-10" + audit JSONL đúng actor. Station dùng ngưỡng khi chạy sequence: đọc `recipe.AnalogLimits["VAC_PP1"]` — service giám sát không đụng Lv*. **Còn hàng đợi**: Gói D phanh Z (toggle+confirm Engineer + banner đỏ khi nhả + tự đóng khi rời màn/logout, ~1 phiên) · P5 máy thật.
**Commit:** `a42c64e`  ·  (S90: `31a8d47` · S89: `231ee0e`+`bcef5b3` · S88: `abdcedc`)

---

## 🗓️ Session #90
**Session:** #90 — **Lịch sử cảnh báo + Pareto · fix danh sách user trống + audit quản trị user** (5 đề xuất của chủ dự án, chốt làm Gói A+B trước). **Gói A**: BUG danh sách user luôn TRỐNG — `UserAdminView` ListBox **thiếu `ItemsSource`** từ ngày viết màn (chủ dự án phát hiện qua ảnh) → đã bind + label LUÔN HIỆN cho mọi ô nhập (Tên đăng nhập/Mật khẩu/Quyền/Mật khẩu mới) + **audit 4 thao tác quản trị user** (Create/Delete/SetLevel/ResetPassword kèm NGƯỜI thực hiện — hiện trong màn Nhật ký audit, truy được "ai mượn phiên admin thêm user"). **Gói B**: màn Cảnh báo thêm sub-tab **"Lịch sử"** (AlarmHistory DB có sẵn từ P0 — xoá alarm active không mất lịch sử): lọc từ/đến ngày + text, bảng ≤500 dòng, export CSV, **Pareto tần suất theo mã** (top 15, đếm+%+bar, tính trên toàn kết quả lọc) — trả lời "lỗi nào hay xảy ra". Kiểm chứng UIA với DỮ LIỆU THẬT: 10 dòng lịch sử + Pareto hiện, 5 user hiện đủ, app sống (phát hiện thêm: UIA Select() không kích Command RadioButton — phải click chuột thật; sln build không refresh dll bin → luôn build Shell tường minh trước smoke). 159/159 Services tests pass. **Còn hàng đợi đã chốt**: Gói C giám sát analog (ngưỡng theo RECIPE + time settings van/xilanh, ~2 phiên) · Gói D phanh Z (toggle+confirm Engineer + banner đỏ khi nhả + tự đóng khi rời màn, ~1 phiên).
**Commit:** `31a8d47`  ·  (S89: hotfix crash `231ee0e`+`bcef5b3` · S88: P4 `abdcedc` · S87: RefUX-A `25ec2bf`)

---

## 🗓️ Session #89
**Session:** #89 — **HOTFIX crash mở Vận hành tay / Cài đặt** (chủ dự án báo). Nguyên nhân: `Run.Text` là DP **mặc định TwoWay** (bẫy WPF) — S84 bind nó vào indexer chỉ-đọc `Loc.Strings[...]` trong `CalibrationPanelView.xaml` → XamlParseException ngay lúc load view; view nhúng ở CẢ Vận hành tay lẫn Cài đặt nên mở màn nào cũng thoát app. Log app sạch (crash trước khi kịp ghi) — truy bằng **Windows Event Log**. Sửa: `Mode=OneWay` (rà toàn repo đúng 1 chỗ thiếu) + App đăng ký `DispatcherUnhandledException`/`AppDomain.UnhandledException` → **Log.Fatal trước khi chết** (chỉ log, không nuốt lỗi — crash sau này có dấu vết ngay trong log app). Kiểm chứng bằng **UI Automation thật**: duyệt 7 tab trước đăng nhập → login `engineer` → duyệt 8 tab (gồm Vận hành tay). **Crash thứ 2 cùng lớp** (chủ dự án báo tiếp): màn Cảnh báo thoát KHI CÓ alarm — `DataGridCheckBoxColumn` (cột DataGrid cũng mặc định TwoWay) bind vào `IsAcknowledged` setter private, chỉ nổ khi list có dòng (lần duyệt trước list rỗng nên lọt); handler Log.Fatal vừa thêm cho stack ngay trong log app. Sửa Mode=OneWay + kiểm chứng UIA: đăng nhập sai 5 lần TỰ TẠO alarm 40010 thật → mở màn Cảnh báo có dòng — app sống, 0 FTL. Bài học: XAML phải test bằng điều hướng tới màn + màn danh sách phải test VỚI DỮ LIỆU.
**Commit:** `231ee0e`  ·  (S88: P4 `abdcedc` · S87: RefUX-A `25ec2bf` · S86: P3.3 `31da608`)

---

## 🗓️ Session #88
**Session:** #88 — **ROADMAP P4 HOÀN TẤT (P4.1–P4.4) → P0–P4 SẠCH BẢNG, chỉ còn P5 máy thật**. **P4.1 Single-step**: engine thêm `SingleStep`/`IsWaitingStep`/`StepOnce` — bật là gate TỰ CÀI sau mỗi nhóm order (bất biến 5: station không biết gì); `StepOnce` chỉ mở gate của single-step, KHÔNG vượt mặt gate của `RequestPause` thật (pause thật vẫn đi Resume có resume-check); tắt toggle khi đang đứng → bấm Bước tiếp một lần nữa là chạy liên tục; Shell: toggle "Từng bước" trên action bar (Engineer+, chấm ● khi bật) + nút "Bước tiếp ▶" trên banner khi engine đứng gate (poll 1s); 3 test engine. **P4.2 Sequence per-recipe**: `RecipeBase.SequenceFile` → convention `recipes/{Name}.sequence.json` → file mặc định máy; `SequenceSource` nhận IRecipeService+IAlarmService, nghe RecipeChanged → invalidate cache + **VALIDATE SỚM** (sequence recipe mới hỏng → alarm 60005 ngay lúc đổi, không đợi bấm Chạy); 4 test. **P4.3 Settings hoàn thiện — HẾT PLACEHOLDER**: thẻ "Phần cứng" (`HardwareView`: tên·category·driver Sim/thật·trạng thái poll 1s + **reconnect TỪNG thiết bị** Engineer+ audit — khác Chẩn đoán chỉ Reconnect All) + thẻ "Host" (`HostView`: endpoint OPC-UA/Modbus/PLC/EIP/DB từ config read-only + trạng thái sống; Shell bơm endpoint qua DI — module không đụng IConfiguration) + **nút vào/thoát kiosk** trong Cài đặt (Engineer+; `IKioskService` mới, MainWindow attach getter/setter; Ctrl+Shift+F11 thành dự phòng). **P4.4 Production**: `ProductionOptions` (`AutoMachine:Production`) — **ca làm việc config** (ShiftStartHour 8 + LengthHours 8, `GetShiftStartLocal` lặp đều) thay cửa sổ trượt 8h cứng, Dashboard + Production cùng MỘT định nghĩa ca; **KPI yield màu-khi-có-nghĩa** (vàng<95 đỏ<90 config — hoãn từ ADR 0010 nay xong, cả 2 màn); **export CSV** record theo cửa sổ (escape chuẩn); **trend theo giờ** (yield bar màu theo mức + X̄ cycle + n=… — SPC đơn giản, tự ẩn khi rỗng); 10 test (6 shift + 4 yield level). i18n +27 key ×3 (**399 chuỗi**). **+17 test → 317 pass** (2 bug thật trong test mới bị bắt khi chạy: kỳ vọng SingleCycle sai + deadlock xUnit vì station stub không yield — xem CHANGELOG). Build 0 warning, smoke boot sạch. Việc còn lại roadmap: **P5 tích hợp máy thật** (khi có phần cứng).
**Commit:** `abdcedc`  ·  (S87: RefUX-A `25ec2bf` · S86: P3.3 `31da608` · S85: P3.2 `6f05f0d`)

---

## 🗓️ Session #87
**Session:** #87 — **Rà bộ UX guidelines web/mobile (bí danh RefUX-A) theo yêu cầu chủ dự án → chắt lọc có phê phán, KHÔNG cài skill**. Đánh giá: repo là AI-skill sinh design-system cho web/mobile (67 UI styles Glassmorphism/Claymorphism..., 161 palettes, 57 font pairings, 99 UX guidelines) — nhóm styles/palette/typography **ngược triết lý ISA-101** (High-Performance HMI yên tĩnh, palette v2 là bảng màu duy nhất) nên KHÔNG áp và KHÔNG cài làm skill (trigger tự động sẽ đề xuất style marketing web vào HMI — gây hại); nhóm đáng giá duy nhất là **UX guidelines interaction/feedback platform-agnostic có định lượng**. Kết quả: **`HMI_Advanced_Standards.md` +§7** (bảng KHÔNG áp ghi rõ lý do để phiên sau không bê nhầm + 11 quy tắc ÁP: feedback ≤300ms/không im lặng, disable chống double-fire, đủ bộ trạng thái nút kèm lý do, animation 150–300ms không trang trí, thông báo tạm 3–5s vs phải-ACK, không layout shift, truncation+tooltip, form label/validate cạnh ô, số ngăn nghìn + ngày một định dạng, empty state có lối đi, 2 bước cho hành động không đảo ngược); **skill am-hmi-design +10 mục checklist** "Interaction & feedback"; CLAUDE.md trỏ §7; bí danh RefUX-A vào `alias.local.md` (không commit). Docs-only — không đổi code, 300 tests giữ nguyên. Việc tiếp: **P4** hoặc **P5** theo roadmap §4.
**Commit:** `25ec2bf`  ·  (S86: P3.3 `31da608` · S85: P3.2 `6f05f0d` · S84: P2 `a6ff044`)

---

## 🗓️ Session #86
**Session:** #86 — **P3.3 Backup & restore hoàn tất → P3 XONG TOÀN BỘ (P0–P3 sạch bảng)**. `IBackupService` + `BackupService`: zip dữ liệu vận hành (db · users.json · points.json · parameters.json · io.map/machine/axismap.json · calibration-history.json · recovery/override-actions.json · appsettings.json · recipes/) — chỉ gom mục đang tồn tại; **3 loại bản lưu**: `am-backup-*` (tay, chọn thư mục qua OpenFolderDialog), `am-auto-*` (tự động mỗi ngày 1 bản lúc app chạy, giữ `Backup:KeepCount`=7 bản mới nhất, lỗi chỉ log không phá app), `am-prerestore-*` (**TỰ sao lưu trạng thái hiện tại trước MỌI lần phục hồi** — không mất đường lùi); restore giải nén đè có **chặn path-traversal**, chống trùng tên file cùng giây, xong yêu cầu KHỞI ĐỘNG LẠI app (service đã nạp dữ liệu cũ vào RAM). **Settings thẻ "Sao lưu & phục hồi" hết placeholder** (Admin gate): nội dung sẽ backup + nút Sao lưu ngay + danh sách bản lưu + **phục hồi confirm 2 bước** (chọn bản → cảnh báo đỏ ghi-đè → xác nhận lần 2). i18n +13 key ×3 (**380 chuỗi**). **+3 test → 300 pass**, build 0 warning, smoke: log "[Backup] Auto-backup hàng ngày BẬT" + tạo thật `am-auto-*.zip` ngay lần boot đầu. **Settings chỉ còn 2 placeholder: Phần cứng + Host (P4.3)**. Việc tiếp theo roadmap §4: **P4** (single-step, sequence per-recipe, Settings hoàn thiện, Production/SPC) hoặc **P5** tích hợp máy thật.
**Commit:** `31da608`  ·  (S85: P3.2 `6f05f0d` · S84: P2 `a6ff044` · S83: P3.1 `f813b91`)

---

## 🗓️ Session #85
**Session:** #85 — **P3.2 Auto-logout + màn Audit hoàn tất**. **Auto-logout**: `InactivityMonitor` (Shell) hook `InputManager.PreProcessInput` (chuột/phím/cảm ứng toàn app) + DispatcherTimer 30s — idle ≥ `Security:AutoLogoutMinutes` (mặc định **15 phút** — Q6 chốt config được, 0=tắt) và đang đăng nhập → `Logout()` **hạ quyền về "Chưa đăng nhập", máy VẪN chạy** (0012: an toàn phiên không gây downtime) + audit "AutoLogout" + đăng nhập xong tính idle lại. **Audit lưu bền**: `AuditService` ngoài structured log giờ append JSONL `logs/audit-yyyyMMdd.jsonl` (một file/ngày, dọn file quá `LogRetentionDays` lúc boot, ghi lỗi không phá thao tác gốc); `IAuditService.Query(from,to,userFilter,max)` đọc từ ngày mới về cũ dừng sớm khi đủ. **Màn Audit**: Settings thẻ MỚI "Nhật ký audit" (gate Administrator — dưới quyền hiện thông điệp): bảng 5 cột (thời gian/user/thao tác/kết quả OK-DENIED đỏ/chi tiết, virtualized 500 dòng), lọc từ/đến ngày + user, **export CSV** (escape chuẩn, SaveFileDialog). i18n +14 key ×3 (**367 chuỗi**). **+3 test → 297 pass**, build 0 warning, smoke: log "[AutoLogout] Bật — idle 15 phút". Việc tiếp: **P3.3 Backup & restore** (đang làm cùng đợt).
**Commit:** `6f05f0d`  ·  (S84: P2 `a6ff044` · S83: P3.1 `f813b91` · S82: P1 6/6 `4a9d35f`)

---

## 🗓️ Session #84
**Session:** #84 — **ROADMAP P2 HOÀN TẤT (P2.1+P2.2+P2.3 — Calibration trọn gói)**. **P2.1** `docs/HMI_Calibration_Model_v1.0.md` (chuẩn hiện hành): calib ≠ setting, phân loại `frequency` routine/rare quyết định chỗ đứng UI, **wizard 2 nhánh theo `autoThreshold`** (đo → trong ngưỡng áp 1 chạm : vượt ngưỡng hướng dẫn chỉnh tay → đo lại), lịch sử + audit, `requiresCalibAfterChange`+usage counter (khái niệm, code P5), 4 quyết định ADR (framework ở Abstractions+Services không project riêng · routine đăng ký code không config JSON · 1 module UI 2 chỗ nhúng · kết quả bù ghi recipe) — Master Index hết tham chiếu treo. **P2.2** contracts `ICalibrationRoutine`/`ICalibrationService`/`ICalibrationWizard` (+2 enum, 2 model record); `CalibrationService` (registry chống trùng Id + lịch sử JSON giữ 200 + audit) + `CalibrationWizard` state machine 7 trạng thái — **bất biến: không áp khi chưa đo/vượt ngưỡng/khác kết quả đo gần nhất**; 5 test (2 nhánh, cấm áp sai trạng thái, đo lỗi→Failed→Reset, lịch sử sống qua reload). **P2.3** module MỚI `AM.Modules.Calibration` (project #29) — `CalibrationPanelView(Model)` dùng chung, 2 subclass mỏng `Routine/RareCalibrationPanelViewModel` chốt frequency cho DI; **sub-tab "Hiệu chỉnh"** trong Vận hành tay (pane 5, TỰ ẨN khi máy không có routine) + **thẻ "Hiệu chuẩn" Settings hết placeholder** (rare); demo `PickOffsetCalibrationRoutine` (LineLead+, ngưỡng 0.05mm khớp §9): đo lệch sim (±0.12mm, đo lại co 0.35 như đã chỉnh tay) → áp vào `PickPositionX/Y` recipe active qua `SaveRecipeAsync`, sau áp còn nhiễu dư ±0.01 (đo lại thấy trong ngưỡng); route đăng ký DI `ICalibrationRoutine` + `RegisterCalibrationRoutines` lúc boot. **+5 test → 294 pass**, build 0 warning, smoke: log "[Calib] Đăng ký routine demo.pick-offset", i18n **353 chuỗi ×3**. Việc tiếp: **P3.2 Auto-logout + audit UI** rồi **P3.3 Backup**.
**Commit:** `a6ff044`  ·  (S83: P3.1 `f813b91` · S82: P1 6/6 `4a9d35f` · S81: P1 4/6 `00c5367`)

---

## 🗓️ Session #83
**Session:** #83 — **P3.1 Chính sách đăng nhập nhà máy + break-glass (chốt lại theo `design-notes/0012`)**. Chủ dự án phản biện DoD gốc (lockout gây downtime khi hỏng máy; tài khoản dùng chung theo vai không hợp ép-đổi-mật-khẩu; mất quyền admin vĩnh viễn khi người giữ mật khẩu rời đi) → chốt qua AskUserQuestion: **KHÔNG lockout** (Q1: chỉ audit — sai ≥5 lần liên tiếp → alarm 40010, đăng nhập đúng vẫn vào ngay), **break-glass kép** (Q2: day-code + file), **banner thay ép đổi** (Q3). Thực hiện: `SecurityOptions` (config `AutoMachine:Security`); UserService — đếm chuỗi sai + audit mọi lần đăng nhập; user `service` + **mã 8 số theo ngày** HMAC-SHA256(secret, machineId+yyyyMMdd) ±1 ngày → SuperUser tạm + alarm 40011 (secret rỗng = TẮT — mặc định repo; tool `scripts/am-daycode.ps1` **đã kiểm chứng thực nghiệm khớp C#** bằng spike); file **`am-recovery.key`** cạnh exe → lúc boot XOÁ NGAY (một lần dùng) + cửa sổ 30' đăng nhập `recovery/recovery` = Administrator tạm + alarm 40012 — KHÔNG đụng users.json (giữ danh sách user, khác đường xoá-file re-seed cũ); MinLength 8 khi tạo/đổi; 2 tên break-glass cấm tạo tài khoản; `HasDefaultPasswordsAsync` (cache, invalidate khi Save) → **banner vàng thường trực** trên Shell khi còn mật khẩu mặc định (alarm/prompt đè lên; tắt ~1s sau khi đổi hết); alarm catalog + strings 3 ngữ. **+8 test → 289 pass** (2 test cũ nâng mật khẩu lên 8 ký tự theo policy), build 0 warning, smoke boot sạch (i18n 327×3). Việc tiếp: **P2 Calibration** (3 phiên) hoặc **P3.2 Auto-logout + audit UI**.
**Commit:** `f813b91`  ·  (S82: P1 6/6 `4a9d35f` · S81: P1 4/6 `00c5367` · S80: P0 `b72cf8b`)

---

## 🗓️ Session #82
**Session:** #82 — **ROADMAP P1 HOÀN TẤT (P1.4 + P1.5 — 2 mục cuối)**. **P1.4 Guard hình học**: `MotionSignalPublisher` (AM.Services) poll vị trí Z mỗi 100ms → publish tín hiệu `Motion.ZAtSafe` lên `HardwareSignalBus` (bus dedup — consumer vẫn event-push; **fail-safe**: chưa kết nối/lỗi đọc → false); `SignalKeys.MotionZAtSafe` mới; MotionViewModel khai `GeometricGuardFor(axis)` — jog/nudge/move/hold trên **X/Y/U bị chặn khi Z chưa ở độ cao an toàn** (0±0.5mm, trục Z được miễn để còn nâng lên), blockReason `Manual.ZNotSafe` 3 ngữ + audit DENIED. **P1.5 Jog deadman**: interface `IAxisJog` (StartJog velocity-mode / KeepAlive / StopJog, `WatchdogTimeoutMs=200`); `SimulatedMotionController` implement — vòng tích phân 25ms, **mất KeepAlive >200ms → TỰ DỪNG** (UI treo/crash không thể để trục chạy tiếp); jog pad MotionView thành **giữ-để-chạy** qua attached behavior `JogHoldBehavior` (PreviewMouseDown/Up + MouseLeave + LostMouseCapture — nhả/rời nút là Stop), VM nuôi KeepAlive 80ms nền; HAL không có IAxisJog → **fallback inching** (hành vi cũ, an toàn); STOP đỏ hủy hold + StopAllAxes. **+6 test (4 deadman + 2 publisher) → 281 pass**, build 0 warning, smoke boot sạch (log `[MotionSignals] Started`, i18n 326 chuỗi ×3). **P1 xong toàn bộ 6/6** — việc tiếp theo roadmap §4: **P2 Calibration** (3 phiên) hoặc **P3.1 Password policy + lockout** (1 phiên).
**Commit:** `4a9d35f`  ·  (S81: P1 4/6 `00c5367` · S80: P0 `b72cf8b` · S79: roadmap `35c75cc`)

---

## 🗓️ Session #81
**Session:** #81 — **ROADMAP P1: xong 4/6 mục (P1.1/P1.2/P1.3/P1.6)**. **P1.1** chốt chính sách §9 với chủ dự án: Override = **1 người, 2 bước + đếm ngược 3s** (giữ S64) · **R2 cứng Engineer** · ngưỡng Set–Confirm **0.05mm config** — docs HMI_Manual_Operation + Master Index §9 đánh dấu ĐÃ CHỐT. **P1.2** ĐÍNH CHÍNH gap C2: màn Vận hành tay ĐÃ TỒN TẠI từ S48 (MotionView, 5 sub-tab, gate LineLead) — việc thật chỉ là nối **nút Manual action bar** → tab Vận hành tay (enable theo quyền + tooltip 3 ngữ). **P1.3** `PhysicalButtonMonitor`: poll 50ms edge-detect `DI.Btn.Start/Stop/Reset` → lệnh master (master tự kiểm interlock/state — không logic riêng; giữ nút không lặp lệnh). **P1.6** `BannerOperatorPromptService` (IOperatorPrompt → nút ĐỘNG trên banner; headless → tự chọn lựa chọn an toàn nhất đứng đầu); PickStation init **HỎI operator** khi liệu sót (Máy tự thoát / Đã lấy tay — lặp tới khi cảm biến sạch) + implement `IResumeVerifiable` kiểm **bất biến hình học Z-an-toàn** (không snapshot per-station vì gantry dùng chung — Z bị đẩy khi pause → từ chối resume + prompt). **+6 test → 275 pass**, build 0 warning, app boot sạch. **Còn lại P1.4 (guard hình học) + P1.5 (jog deadman)** — code chuyển động an toàn-trọng-yếu, mỗi mục 1 phiên riêng.
**Commit:** `00c5367`  ·  (S80: P0 `b72cf8b` · S79: roadmap `35c75cc` · S78: Prompt D `6c71301`)

---

## 🗓️ Session #80
**Session:** #80 — **ROADMAP P0 hoàn tất (cả 4 mục)**: **P0.1** E-Stop vào state machine — `EmergencyStop()` fire trigger Error (Running/Paused→RunAlarm, Initializing→InitAlarm — thêm transition Paused+Error, bảng 14 cạnh) + raise alarm 70001 fire-and-forget + wire `ISafetyInput.SafetyStateChanged` (E-Stop vật lý → EmergencyStop; cửa mở KHÔNG estop từ software — spec §8); **P0.2** `RetentionCleanupService` (IRetentionCleanupService — dọn alarm+production cũ hơn `DataRetentionDays` ngay lúc boot + mỗi 24h, xác nhận log runtime); **P0.3** users.json schema cũ → backup `.bak-{timestamp}` TRƯỚC khi re-seed ghi đè; **P0.4** docs sync: `HMI_UI_Architecture_Template_v3.md` MỚI (chuẩn hiện hành — shell 4 vùng + prompt banner + kiosk + 3 nguyên tắc), Master Index (§1/§2 +3 nguyên tắc/§3 bố cục 4 vùng), Dashboard spec v2.1, CLAUDE.md trỏ v3 + sửa README stale. **+11 test mới → 269 pass** (Infra 62, Services 128), build 0 warning, app boot sạch + log Retention chạy. Việc tiếp: **P1** — chốt §5 Q1–Q7 của roadmap rồi dựng màn Vận hành tay.
**Commit:** `b72cf8b`  ·  (S79: roadmap `35c75cc` · S78: Prompt D `6c71301` · S77: engine `4789c51`)

---

## 🗓️ Session #79
**Session:** #79 — **Đánh giá toàn diện + ROADMAP hoàn thiện** (`docs/ROADMAP_HOAN_THIEN.md`): rà 6 trục (an toàn/bảo mật/chức năng/hiệu chỉnh/UI/tích hợp), kiểm chứng gap trực tiếp trong code — nổi bật: **E-Stop không đổi state machine** (EmergencyStop không fire trigger — máy vẫn hiện "Đang chạy"), **DataRetentionDays không được thực thi** (DeleteOlderThanAsync 0 caller — DB phình vô hạn), **users.json re-seed ghi đè không backup**, **nút vật lý DI.Btn.* chưa wire**, **không lockout/password-policy/auto-logout**, calibration = trục trắng (tài liệu tham chiếu treo). Kế hoạch P0–P5 (~17 phiên P0–P4) kèm DoD từng mục + 7 câu hỏi cần chủ dự án chốt (§5) + hợp đồng vision app riêng (§6). Việc tiếp: **P0.1 E-Stop state machine** (ưu tiên 🔴 số 1).
**Commit:** `35c75cc`  ·  (S78: Prompt D `6c71301` · S77: engine `4789c51` · S76: ẩn danh+ADR `798e6c9`)

---

## 🗓️ Session #78
**Session:** #78 — **Prompt D: máy mẫu DemoPickPlace end-to-end trên mô phỏng**. `SimIoService` (IIoService+IMotionService, delay+xác suất lỗi cấu hình `DemoSimOptions`) + 6 station (Scanner/Feed/Pick/Vision/Place/Report — homing Z→X→Y, Abort GIỮ vacuum khi đang giữ hàng, kiểm liệu sót đầu cycle+init) + `recipes/DemoPickPlace.sequence.json` (spec §2) + `DemoMasterController` nối engine (mỗi cycle=1 sản phẩm; Pause/Resume override→RequestPause/Resume dừng giữa cycle ở ranh giới bước; Abort→alarm 60006, sequence hỏng→60005). Dashboard mini-log ăn TRỰC TIẾP sự kiện engine (StepCompleted lỗi/NG + ProductCompleted); KPI/bảng SP/card KQ đi đường IProductionService (ReportStation ghi record thật: SN scanner, OK/NG, vision score — không đường dữ liệu riêng cho UI). **Nút mới**: banner Shell 3 nút trả lời operator prompt (Thử lại / Bỏ qua-Engineer+ / Dừng máy) thay popup chặn thread. **4 kịch bản nghiệm thu (test tự động, engine+station+SimIoService thật trên file sequence thật)**: (a) 20 sản phẩm liên tục — 20 record PASS, SN không trùng, KPI khớp log; (b) vacuum fail 100% → retry đúng 2 lần (1 đầu + retry=1) → prompt → operator Abort → 0 record; (c) Pause giữa cycle dừng ở ranh giới bước (vision CHƯA chạy) → Resume chạy nốt; (d) Stop khi đang giữ hàng → vacuum GIỮ + sản phẩm Aborted → Reset+Init tự thoát liệu sót → chạy lại 1 sản phẩm sạch. **258 test pass** (20 engine + 5 demo + 233 cũ), build 0 warning, app boot sạch với DI graph mới (keyed stations + engine + resolver). Việc tiếp (tuỳ chọn): vòng review phản biện ADR+engine; đấu ảnh cycle thật vào card KQ khi vision IPC (ADR 0008) xong.
**Commit:** `6c71301`  ·  (S77: engine+test `4789c51` · S76: ẩn danh+ADR `798e6c9` · S74: Home v2.1 `970f078`)

---

## 🗓️ Session #77
**Session:** #77 — **AM.Core.Sequencing (Prompt C, theo ADR 0011 đã duyệt + 2 hiệu chỉnh)**: project mới standalone — contracts spec §1 nguyên văn (`IStation`/`StepContext`/`StationResult`) + `IStationResolver` (engine không thấy DryIoc) + `IResumeVerifiable`/`IOperatorPrompt`; `SequenceLoader` 2 pha gom TOÀN BỘ lỗi (tên station chết LÚC NẠP + gợi ý tên đã đăng ký); `SequenceEngine`: nhóm `order` song song, timeout linked-CTS, onError/retry/onRetryExhausted, prompt operator không-chặn-thread (Respond trong args), NG bypass trừ `runOnNg`, pause ranh giới bước + resume-check, Stop sạch + sản phẩm dở Aborted. **20/20 test** (đủ 6 case spec §4 + validator + prompt/resume/blackboard) — coverage engine core **92.7% line** (package 85.5%). Commit `4789c51`.

---

## 🗓️ Session trước
**Session:** #75 — **Sequence Requirements (khảo sát máy tham khảo RefSeq-A)**: đọc dự án tham khảo RefSeq-A (C# WinForms, 8 trạm thread-per-station + bit bắt tay), điền `docs/private/Sequence_Requirements_RefSeqA.md` *(local, không commit)* theo template — 10 mục: vai trò 8 trạm, vòng đời init phụ thuộc chéo, ngữ nghĩa Pause (giữa bước + resume-check vị trí)/Stop (hủy ngay + Thread.Abort)/EMG (mọi Error-warning → EMG toàn máy)/Reset (xóa bit + re-init), chính sách lỗi popup-operator (không auto-retry, timeout mặc định 600s), song song giả (bit handshake), traceability MES + data-host + CSV, 4 mode chạy, anti-pattern KHÔNG bắt chước + 7 hành vi đáng học. Nhập bộ spec sequence vào docs/: `SequenceEngine_Spec.md` (chuẩn thiết kế), `DemoMachine_IO_Map.md`, `Sequence_Requirements_Template.md`. Việc tiếp: thiết kế `AM.Core.Sequencing` CHỈ từ 3 file này.
**Commit:** `8be4ef0` *(S75 được gộp + ẩn danh hoá ở S76 — hash gốc đã bị viết lại)*  ·  (S74: Home v2.1 `970f078` · S73: Shell v3 `991f34b` · S72: ADR 0008 Vision IPC `b50e22b`)

---

## 📊 Trạng thái tổng quan

| Hạng mục | Trạng thái | Ghi chú |
|----------|-----------|---------|
| Solution structure | ✅ Hoàn thành | **30 projects** (CPM, +AM.Modules.Analog S91), production 0 warning, **340 tests pass** · light theme + i18n toàn module (AM.UI.Localization) + cửa sổ cố định |
| AM.Core | ✅ Hoàn thành | Enums (+PixelFormat) + 5 Attributes + Models (+RobotPose +FrameData +MotionStatus) + EventArgs |
| AM.Core.Abstractions | ✅ Hoàn thành | Hardware (16 ifaces, **+IHardwareDevice base**: mọi device kế thừa → ConnectAll generic) + Machine + Services |
| AM.Core.Sequencing | ✅ Hoàn thành | **Mới (S77, ADR 0011)** — sequence engine khai báo: contracts (`IStation`/`StepContext`/`StationResult`/`IStationResolver`/`IResumeVerifiable`/`IOperatorPrompt`), `SequenceLoader` 2 pha gom lỗi, `SequenceEngine` (order song song, timeout linked-CTS, onError/retry/prompt, pause ranh giới bước + resume-check). Standalone — không reference DryIoc/hardware/UI |
| AM.Core.Sequencing.Tests | ✅ Hoàn thành | **Mới (S77)** — 20 tests: 6 case spec §4 + validator + prompt/resume-check/blackboard; station = fake thuần; coverage engine core 92.7% |
| AM.Hardware.Scanner | ✅ Hoàn thành | **Keyence + Cognex (TCP line) + Simulated** — IBarcodeScanner |
| AM.Hardware.Motion | ✅ Hoàn thành | Sim (+**IAxisDiagnostics**: 8 tín hiệu/servo/phản hồi; +**IAxisJog** jog deadman 200ms, S82) + **GtsMotionController (固高, P/Invoke)** + **AdvantechMotionController (P/Invoke)** |
| AM.Hardware.Vision | ✅ Hoàn thành | SimulatedCameraDevice (+**GrabFrameAsync sinh frame Bgr24 live, S67**) + SimulatedVisionProcessor (IVisionProcessor) |
| AM.Hardware.IO | ✅ Hoàn thành | Sim + AdvantechAdamIoModule (+**force/unforce/ReadAllDo** — kênh forced bỏ qua write của logic, S59) + SimulatedSafetyInput + JsonIoTagMap + IoTagExtensions |
| AM.Hardware.Comm | ✅ Hoàn thành | **Modbus TCP thật (raw MBAP)**, Inovance PLC+servo, Mitsubishi MC 3E, Siemens S7, Robot socket+sim, PLC sim |
| AM.Services | ✅ Hoàn thành | Alarm, Recipe, Parameter, HardwareManager, StationSync, Watchdog, Production, UserService, **GuardService (3 tầng: state→role→condition), HardwareSignalBus + SafetySignalPublisher (event-push)** |
| AM.Services.Tests | ✅ Hoàn thành | **122 tests** (Alarm, Recipe, StationSync, HardwareManager, Watchdog, Production, UserService +**CRUD/last-admin**, PointTable, Guard 3 tầng, SignalBus, SafetyPublisher, RecoveryActions, Override provider) |
| AM.Infrastructure (i18n) | ✅ Hoàn thành | **JsonAlarmCatalogService** — Alarms.{vi,en,zh}.json (44 mã), dịch tên/remedy theo culture |
| AM.Hardware.Tests | ✅ Hoàn thành | **36 tests**: Modbus MBAP, Inovance/ADAM, Robot+Scanner loopback, SimVision/SimSafety, SimAxisDiagnostics, IO force semantics, IoTagMap schema mảng, **SimCamera GrabFrame live-view (S67)** |
| AM.Data | ✅ Hoàn thành | EF Core SQLite, AlarmRepository, ProductionRepository |
| AM.Infrastructure | ✅ Hoàn thành | BaseMechanism, StationBase\<T\>, BaseMasterController, **JsonLocalizationService (i18n runtime)** |
| AM.CommonTools | ✅ Hoàn thành | Guard, RetryHelper |
| AM.WorkStation.Demo | ✅ Hoàn thành | Full 3-tier: DemoPick/InspectMechanism → DemoStation → DemoMasterController; **+Sequencing (S78)**: SimIoService + 6 station (Scanner/Feed/Pick/Vision/Place/Report) + adapters, master nối SequenceEngine (mỗi cycle=1 sản phẩm, Pause/Resume→ranh giới bước, Abort→60006) |
| AM.WorkStation.Demo.Tests | ✅ Hoàn thành | **Mới (S78)** — 5 tests: 4 kịch bản nghiệm thu Prompt D (20 sản phẩm/KPI, vacuum-fail retry+prompt+Abort, Pause-giữa-cycle+Resume, Stop-giữ-hàng+Reset+chạy-lại) + vòng đời ISA-88 master nối engine; chạy engine+station+SimIoService thật trên file sequence thật |
| AM.Modules.Dashboard | ✅ Hoàn thành | **Home v2.1** (S74, ADR 0010): work area (card "Kết quả gần nhất" + bảng truy vết SN empty-state, KQ chip màu) + right rail 560px (KPI ca 8h số 26px màu-khi-có-nghĩa, **quick actions đủ HAL — S65** + tooltip lý do + Andon, trạm & an toàn ISafetyInput event, nhật ký) — spec: `docs/HMI_Dashboard_Spec.md` v2 (cần nâng v2.1) |
| AM.Modules.Alarm | ✅ Hoàn thành | active alarms + acknowledge/clear, đồng bộ realtime |
| AM.Modules.IoMonitor | ✅ Hoàn thành | Danh sách "địa chỉ·tên" (IOMap) + ô lọc + chỉ báo Off/On/Pending/Forced + nhóm Xi lanh ▲giữa (S60); set/reset thường (Engineer; **có hậu quả → chạm-2-bước**) + Chế độ Force (Admin) + **alarm 70010 "còn IO forced"** (S61); nav tự sinh từ [ModuleNavigation] |
| AM.Modules.Identity | ✅ Hoàn thành | **Mới** — login/logout/RBAC (IUserService); password ở code-behind; nav order 90 |
| AM.Modules.Motion | ✅ Hoàn thành | **Màn điều khiển trục v2** (S46): bảng đèn 8 tín hiệu + servo/home/clear/move từng trục + jog pad/inching + phản hồi servo + bảng điểm Set/Confirm 2-chạm + **Thao tác trạm (RecoveryActions, S63) + Supervised Override (xác nhận 1 người, S64)**. Bám `IMotionController` + `IAxisDiagnostics` (tuỳ chọn); nav order 40 |
| AM.Modules.Parameter | ✅ Hoàn thành | **Mới** — recipe editor attribute-driven ([ParamView] reflection); Save gate Engineer; nav order 50 |
| AM.Application.Shell | ✅ Hoàn thành | Bootstrapper + HardwareFactory + **Shell v3 — 4 vùng Persistent Frame** (S73, ADR 0009): header+nav gộp 56px (chip AUTO/LOCAL/state + tab RadioButton), alarm banner co giãn 36→52 + ACK 40px + chip "+N", action bar 76px (lệnh máy 64px + Dry run + chip kết nối n/m + popup Thiết bị│Host), kiosk config-driven (Ctrl+Shift+F11 Engineer+) |
| AM.UI.Localization | ✅ Hoàn thành | Proxy i18n dùng chung `Loc.Strings` (module bind `{x:Static loc:Loc.Strings}`) |
| .claude/ (AI config) | ✅ Hoàn thành | rules(2) + commands(9) + skills(8) + hooks(4) |
| PROJECT_STATUS.md + CHANGELOG.md | ✅ Hoàn thành | Tracking system, auto-commit workflow |
| scripts/am-commit.sh | ✅ Hoàn thành | Git wrapper xử lý Windows index.lock |
| `libs/` vendor DLLs | ✅ Structure tạo xong | Placeholder + README; DLL do developer tự copy từ SDK |
| AM.Infrastructure.Tests | ✅ Hoàn thành | **55 tests**: ISA-88 + busy-guard + StationBase + e2e + i18n + alarm catalog + **StepSequence (4) + AxisMap (5)** |
| AM.Modules.Engineering | ✅ Hoàn thành | **Mới** — auto-discovery [StationUI]/[MechanismUI] + chạy SubRoutine + E-Stop từng cụm; nav order 80 |
| AM.Modules.Production | ✅ Hoàn thành | **Mới** — KPI UPH/yield/cycle-time (IProductionService), tự refresh khi CycleCompleted; nav order 15 |
| AM.Modules.Diagnostics | ✅ Hoàn thành | **Mới** — device health + system info + Reconnect All; nav order 70 |
| AM.Modules.Logging | ✅ Hoàn thành | **Mới** — tail file Serilog + lọc level/search + mở thư mục; nav order 75 |
| AM.Modules.Vision | ✅ Hoàn thành | **V1–V2 (S68–69)**: camera toolbar + sub-tab **Kết quả·Lịch sử·Công cụ**; tab Kết quả có **lưới phép đo** (`VisionResult.Checks`) + **stats ca** + trend; live-view + Grab/Inspect/Light/Calibrate (S67). **V3 (S70)**: tab Công cụ = **VisionTeachView** (gate Engineer, phủ toàn vùng) — chụp ảnh tham chiếu + ROI editor (Canvas/`Thumb`) + ngưỡng + calib px→mm (form+lịch sử) + Lưu/Nạp JSON (`VisionTeachConfig`/`IVisionTeachStore`). Roadmap V4–V5 (ILightController per-channel · VisionRecipe) ở ADR `docs/design-notes/0007` |
| AM.Modules.Vision.Tests | ✅ Hoàn thành | **Mới (S70)** — 10 test: `VisionTeachStore` round-trip JSON (ROI+calib) + thiếu file→rỗng + per-camera; `CalibrationMath` mm/px |
| CI/CD + README | ✅ Hoàn thành | `.github/workflows/ci.yml` (windows, build+test) + README.md |

---

## 🏗️ Kiến trúc thực tế — 14 projects

```
AM.Core                  — Enums, Models, 5 Attributes, AlarmCodes, AlarmException, EventArgs
AM.Core.Abstractions     — Interfaces: Hardware(8) + Machine(3) + Services(5) + Repos(2) + IStep
AM.CommonTools           — Guard, RetryHelper
AM.Hardware.Motion       — SimulatedMotionController
AM.Hardware.Vision       — SimulatedCameraDevice
AM.Hardware.IO           — SimulatedIoModule
AM.Hardware.Comm         — Modbus/Serial/TCP (real+sim), OpcUa/EthernetIP (sim only)
AM.Services              — AlarmService, RecipeService, ParameterService,
                           HardwareManagerService, StationSyncService
AM.Services.Tests        — 32 unit tests (xUnit + Moq + FluentAssertions)
AM.Data                  — AutoMachineDbContext, AlarmRepository, ProductionRepository
AM.Infrastructure        — BaseMechanism, StationBase<T>, BaseMasterController, DispatcherHelper
AM.WorkStation.Demo      — DemoPickMechanism, DemoInspectMechanism, DemoStation,
                           DemoMasterController, Step01Initialize, Step02Inspect
AM.Modules.Dashboard     — DashboardViewModel, DashboardView [⚠️ chưa wire vào Shell]
AM.Application.Shell     — WPF entry, Prism+DryIoc Bootstrapper, 8 hw devices registered
```

### 3-Tier Machine Hierarchy — ✅ Đầy đủ cả interface + base + demo

```
[✅ Interface]  IMasterController       AM.Core.Abstractions/Interfaces/Machine/
[✅ Interface]  IStation                AM.Core.Abstractions/Interfaces/Machine/
[✅ Interface]  IMechanism              AM.Core.Abstractions/Interfaces/Machine/
[✅ Base]       BaseMasterController    AM.Infrastructure/ (ISA-88 13 transitions, FireTrigger, CheckPauseAsync)
[✅ Base]       StationBase<T>          AM.Infrastructure/ (RegisterMechanism, SetState, RunCycle template)
[✅ Base]       BaseMechanism           AM.Infrastructure/ (IsBusy guard, EmergencyStop wrapper)
[✅ Demo]       DemoMasterController    AM.WorkStation.Demo/Controllers/
[✅ Demo]       DemoStation             AM.WorkStation.Demo/Stations/
[✅ Demo]       DemoPickMechanism       AM.WorkStation.Demo/Mechanisms/
[✅ Demo]       DemoInspectMechanism    AM.WorkStation.Demo/Mechanisms/
```

### ISA-88 State Machine (8 states, 10 triggers)
```
States:   Uninitialized → Initializing → Idle → Running ⇄ Paused
                              ↓                    ↓
                          InitAlarm            RunAlarm → Resetting → Idle/Uninitialized
Triggers: Initialize, InitializeDone, Start, Pause, Resume, Stop,
          Error, Reset, ResetDone, ResetDoneUninitialized
```

---

## 📁 Key files — vị trí và nội dung

### Build & Config
| File | Nội dung |
|------|---------|
| `Directory.Build.props` | TreatWarningsAsErrors=true, AnalysisMode=All, .NET 9, CA suppressions |
| `.editorconfig` | Code style |
| `.cursorrules` | AI coding rules (Cursor/Copilot) |
| `AM.AutoFrame.sln` | 15 projects |

### AI Instructions (đọc theo thứ tự)
| File | Nội dung | Đọc khi nào |
|------|---------|------------|
| `PROJECT_STATUS.md` | **File này** — snapshot thực tế | ✅ Luôn đọc TRƯỚC |
| `CLAUDE.md` | Kiến trúc, build rules, behavior | ✅ Luôn đọc |
| `CHANGELOG.md` | Lịch sử session, quyết định kiến trúc | Khi cần hiểu lý do |
| `.claude/rules/common/coding-standards.md` | R01–R17 | Auto-load Claude Code |
| `.claude/rules/csharp/csharp-patterns.md` | CS01–CS15 | Auto-load Claude Code |
| `docs/AGENTS.md` | 9 agents + ECC routing table | Khi cần routing |
| `docs/QUICK_REFERENCE.md` | Quick ref (in ra dán màn hình) | Tra cứu nhanh |
| `docs/PROMPT_TEMPLATES.md` | PT-00 đến PT-14 | Khi tạo component mới |

### Hardware Interfaces thực tế (AM.Core.Abstractions/Interfaces/Hardware/)
| Interface | Mô tả |
|-----------|-------|
| `IMotionController` | Connect, MoveAbs, MoveRel, Home, GetPosition |
| `ICameraDevice` | Connect, Grab, RunTool, GetResult |
| `IIoModule` | Connect, ReadDI, WriteDO, ReadAI, WriteAO |
| `IModbusClient` | Connect, ReadCoils, ReadHolding, WriteCoil, WriteRegister |
| `ISerialDevice` | Connect, SendAsync, DataReceived event |
| `ITcpDevice` | Connect, SendAsync, ReceiveAsync |
| `IOpcUaClient` | Connect, ReadNode, WriteNode, Subscribe |
| `IEthernetIpClient` | Connect, ReadTag, WriteTag |

### Machine Interfaces (AM.Core.Abstractions/Interfaces/Machine/)
| Interface | Mô tả |
|-----------|-------|
| `IMechanism` | Name, IsReady, IsBusy, InitializeAsync, HomeAsync, EmergencyStop |
| `IStation` | Name, State, Mechanisms, RunCycleAsync, StateChanged event |
| `IMasterController` | ISA-88 full state machine, Initialize/Start/Stop/Reset/EmergencyStop |

### Service Interfaces (AM.Core.Abstractions/Interfaces/Services/)
| Interface | Implemented by |
|-----------|---------------|
| `IAlarmService` | `AM.Services/AlarmService.cs` ✅ |
| `IRecipeService` | `AM.Services/RecipeService.cs` ✅ |
| `IParameterService` | `AM.Services/ParameterService.cs` ✅ |
| `IHardwareManagerService` | `AM.Services/HardwareManagerService.cs` ✅ |
| `IStationSyncService` | `AM.Services/StationSyncService.cs` ✅ |
| `IPointTableService` | `AM.Services/PointTableService.cs` ✅ (Point Table — toạ độ đặt tên JSON) |
| `IAxisMap` | `AM.Infrastructure/Motion/JsonAxisMap.cs` ✅ (trục logic→IAxis qua `MotionAxisAdapter` — concrete IAxis đầu tiên) |
| `IMachineConfigProvider` | `AM.Infrastructure/Configuration/JsonMachineConfigProvider.cs` ✅ (layout máy machine.json) |

### Enums (AM.Core/Enums/)
| Enum | Values |
|------|--------|
| `MachineState` | Uninitialized, Initializing, Idle, Running, Paused, InitAlarm, RunAlarm, Resetting |
| `MachineTrigger` | Initialize, InitializeDone, Start, Pause, Resume, Stop, Error, Reset, ResetDone, ResetDoneUninitialized |
| `HardwareCategory` | General=0, Axis=1, IOController=2, Camera=3, Robot=4, Scanner=5, Instrument=6, MotionCard=7, LightController=8, ModbusTcp=9, SerialPort=10, OpcUaClient=11, EthernetIp=12, TcpDevice=13 |
| `UserLevel` | Null=-1, Operator=0, **LineLead=1**, Engineer=2, Administrator=3, SuperUser=4 (4 role vận hành + SuperUser OEM) |
| `OperationMode` | Normal, DryRun |
| `AlarmLevel` | Info, Warning, Error, Critical |

### EventArgs (AM.Core/Models/EventArgs/)
| Class | Dùng cho |
|-------|---------|
| `AlarmEventArgs` | IAlarmService.AlarmRaised/AlarmCleared |
| `RecipeEventArgs` | IRecipeService.RecipeChanged |
| `MachineStateChangedEventArgs` | IMasterController/IStation.StateChanged |
| `CycleCompletedEventArgs` | IMasterController.CycleCompleted (`CycleCount`, `CompletedAt`, **`CycleDurationMs`**) |
| `SerialDataReceivedEventArgs` | ISerialDevice.DataReceived |
| `OpcUaValueChangedEventArgs` | IOpcUaClient.ValueChanged |

### Attributes (AM.Core/Attributes/)
| Attribute | Target | Params |
|-----------|--------|--------|
| `[AlarmInfo]` | AlarmCodes fields | displayName, remedy, isStoppable |
| `[MechanismUI]` | Mechanism classes | displayName, group, order |
| `[StationUI]` | Station classes | displayName, icon, order |
| `[ModuleNavigation]` | Prism View classes | displayName, icon, region, order |
| `[ParamView]` | Recipe properties | label, unit, min, max, group, order |

### Alarm Code Ranges
```
10000–10999  Motion / Axis
20000–20999  Vision / Camera
30000–30999  I/O / Sensor
40000–40999  System / Application
50000–50999  Communication / Network
60000–60999  Production / Recipe
70000–70999  Safety / Interlock
```

### .claude/ Skills (8 skills)
| Skill | Lazy-load khi |
|-------|--------------|
| `am-hardware-patterns` | Tạo driver mới |
| `am-sequence-patterns` | Tạo Step / Sequence |
| `am-mechanism-patterns` | Tạo Mechanism |
| `am-station-patterns` | Tạo Station |
| `am-testing` | Viết unit tests |
| `am-wpf-mvvm` | Tạo WPF screen + ISA-101 rules |
| `am-alarm-dictionary` | Thêm alarm code mới |
| `am-hmi-design` | Thiết kế HMI/UI |

---

## ⚠️ Known Issues & TODO

### BUGS hiện tại
*(Không có bug nào đang mở)*

### TODO tiếp theo
> ⚡ **Nguồn TODO chính từ S79: `docs/ROADMAP_HOAN_THIEN.md`** — bảng ưu tiên §4: P0.1 E-Stop state machine (🔴 số 1) → P0.2 Retention job → P0.3 users.json backup → P0.4 sync docs → P1 Vận hành tay (cần chốt §5 Q1–Q7). Các dòng dưới đã gộp vào roadmap, giữ để truy vết:
- [x] **Prompt D — máy mẫu DemoPickPlace end-to-end** ✅ HOÀN THÀNH (S78): SimIoService + 6 station + sequence JSON + master nối engine + dashboard bridge + banner prompt 3 nút; 4 kịch bản nghiệm thu đạt (test tự động)
- [ ] (Tuỳ chọn) Vòng review phản biện ADR 0011 + engine (ChatGPT/Gemini → lọc bằng chứng theo SequenceEngine_Spec + requirements local) như quy trình chương sách
- [ ] Đấu ảnh cycle thật vào card "Kết quả gần nhất" khi vision service IPC (ADR 0008) sẵn sàng — hiện dùng placeholder tối
- [ ] (Giai đoạn 2 sequence) single-step mode · pipeline maxProductsInFlight>1 · resources chống tranh chấp · resume-from-crash (đã ghi lý do hoãn ở ADR 0011 §6)
- [ ] Sync `HMI_UI_Architecture_Template` + Master Index §3 lên **v3** — Shell đã đổi 7 vùng → 4 vùng (ADR 0009), tài liệu đang mô tả bố cục cũ; cùng đợt nâng `HMI_Dashboard_Spec` lên v2.1 (card KQ gần nhất — ADR 0010) + ghi 3 nguyên tắc: màu-khi-có-nghĩa, empty-state-có-hướng-dẫn, xếp-theo-tần-suất-liếc
- [ ] Màn Cài đặt: thêm nút vào/thoát kiosk (hiện chỉ có Ctrl+Shift+F11 Engineer+)
- [ ] Vision V4 — `ILightController` per-channel + `SimulatedLightController` + test (ADR 0007 Quyết định 5)
- [ ] Vision V5 — `VisionRecipe` model (promote `VisionTeachConfig` lên Core) + validate attribute-driven + test
- [ ] (Nợ test, ngoài phạm vi V3) S6966/CA2007 trong AM.Services.Tests + AM.Infrastructure.Tests (pre-existing)
- [ ] Dựng 1 máy reference để nghiệm thu nền framework (đề xuất từ S43)