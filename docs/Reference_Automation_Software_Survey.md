# Khảo sát phần mềm máy tự động hoá công khai — bài học cho giao diện và chức năng AM.AutoFrame

> **Phiên bản:** 1.0 · **Session:** 98 · **Ngày:** 2026-11-12
> **Nguồn:** 18 dự án mã nguồn mở viết bằng C# và C++ trên GitHub, bí danh **RefAuto-A…R** (bảng đối chiếu
> nằm trong `docs/private/`, không commit). Tài liệu này đã ẩn danh: không nêu tên repo, chủ repo, công ty,
> khách hàng hay mã sản phẩm. Mọi mô tả máy đã tổng quát hoá.
> **Cách đọc:** §2 là danh mục nguồn, §3 là phân tích theo chủ đề (mỗi chủ đề kết thúc bằng phần "AM.AutoFrame
> đứng ở đâu"), §4 là ma trận đối chiếu, §5 là các đề xuất kèm phương án để chủ dự án chọn, §6 là những thứ
> KHÔNG nên bắt chước.

---

## 1. Mục đích, phạm vi, phương pháp

**Mục đích.** AM.AutoFrame là khung tự viết, chưa chạy trên máy thật. Khảo sát này tìm bằng chứng từ phần
mềm máy mà người khác đã viết (một phần đã chạy sản xuất) để đối chiếu giao diện và chức năng, tìm chỗ thiếu,
và tìm chỗ AM.AutoFrame đang làm khác số đông mà cần lý do rõ ràng.

**Cách tìm (lặp lại được).**

| Bước | Làm gì | Kết quả |
|---|---|---|
| 1 | Tìm repo theo từ khoá tiếng Anh và tiếng Trung (máy tự động, điều khiển chuyển động, phần mềm máy tính chủ, tra keo, EtherCAT, SECS/GEM…) | Rất nhiễu: mod game VR, Arduino, robot ROS |
| 2 | **Tìm theo mã nguồn**: tên hàm SDK của 6 hãng card chuyển động/PLC phổ biến ở Trung Quốc, Hàn Quốc, Mỹ, Đức + vài từ khoá kiến trúc máy | 208 repo; ai gọi SDK card thật thì gần như chắc là phần mềm máy thật |
| 3 | Lọc ≥ 300 KB, bỏ fork, đọc README và cây thư mục qua API (chưa tải) | 37 ứng viên |
| 4 | Chọn 18 cho đa dạng: khung máy, máy thật nhiều loại, thiết bị bán dẫn, C++ Qt | Loại những repo đã có sẵn trong thư mục làm việc |
| 5 | Tải rút gọn (một commit, chỉ mã nguồn/giao diện/cấu hình/tài liệu/ảnh docs), đọc tĩnh bằng script + đọc tay file lõi | Không build, không chạy mã lạ |

**Giới hạn.** Chỉ đọc tĩnh, không chạy được giao diện (trừ 3 repo có ảnh chụp trong tài liệu). Mẫu tự tìm, thiên
về hệ sinh thái Trung Quốc và Hàn Quốc. Nhiều repo là khung đang viết dở; số sao không nói lên chất lượng (§6).

---

## 2. Danh mục 18 nguồn

| Bí danh | Loại phần mềm (tổng quát) | Công nghệ | Quy mô | Đáng học nhất |
|---|---|---|---|---|
| **RefAuto-A** | Máy gắp linh kiện từ khay vào khay lắp ráp, có vision định vị, đã chạy sản xuất | C# WPF (.NET Framework) + bản viết lại C++ WinUI 3 | ~118k dòng; tài liệu thiết kế 1.700 dòng | **An toàn, máy trạng thái, alarm, khôi phục sau dừng** |
| **RefAuto-B** | Máy vi lắp ráp quang học chính xác | C# WPF Prism, .NET 9, 36 project | ~144k dòng; tài liệu kiến trúc dày | **Quy tắc trang, quyền theo năng lực, mô hình không gian + snapshot hiệu chuẩn** |
| **RefAuto-C** | Khung phần mềm máy kiểu handler/căn chỉnh | C++ Qt/QML | ~92k dòng | **Máy trạng thái 13 trạng thái, chống va chạm cấu hình được, lỗi kèm lựa chọn xử lý** |
| **RefAuto-D** | Khung đa ngành + mẫu thiết bị bán dẫn + mẫu máy đo | C# WPF, .NET 8, 124 project | ~157k dòng | Tách abstraction/implementation, "hướng dẫn sửa ở hiện trường", MES ngoại tuyến |
| **RefAuto-E** | Khung máy có tài liệu đánh số | C# WinForms (thư viện control phong cách web) + bản WPF | ~329k dòng (gồm thư viện UI) | Shell 3 cột, danh mục điều hướng có mức rủi ro |
| **RefAuto-F** | Phần mềm máy tra keo **thương mại**, bản tuỳ biến cho một khách hàng | C# WinForms | ~276k dòng | Quyền đặt cho từng nút, chế độ chạy, chương trình dạng danh sách lệnh |
| **RefAuto-G** | Máy đo/tinh chỉnh linh kiện quang, nền tảng test + plugin | C# WinForms, 30 project | ~276k dòng | Bước đo khai báo tài nguyên bằng attribute, phân loại bin |
| **RefAuto-H** | Runtime máy điều khiển bằng cấu hình JSON, plugin driver | C# WPF + HTML dashboard | ~102k dòng | Bộ 3 màn cho mỗi tài nguyên: Giám sát / Quản lý / Thử |
| **RefAuto-I** | Khung máy có trình soạn luồng kéo-thả | C# WinForms | ~24k dòng | Trạng thái "chờ dọn liệu", chế độ chạy rodage/hiệu chuẩn |
| **RefAuto-J** | Mẫu phần mềm thiết bị bán dẫn/PCB: PLC + SECS/GEM | C# WPF, .NET 8, EF Core | ~39k dòng | Bắt tay trigger/reply với PLC, nhật ký thao tác bằng attribute |
| **RefAuto-K** | Phần mềm máy kiểu "PLC" có ảnh chụp | C# WPF Prism, .NET 8 | ~55k dòng | Lưới khay tô màu theo trạng thái bắt tay ô |
| **RefAuto-L** | Demo máy phân loại sản phẩm theo kết quả đo | C# WPF Prism, .NET 8, 139 test | ~10k dòng | Màn giám sát bin + bảng mô phỏng tay |
| **RefAuto-M** | Máy bán dẫn dạng khung dẫn | C# WPF | ~42k dòng | Màn teach riêng cho từng loại cơ cấu |
| **RefAuto-N** | AOI 3D | C++ Qt | ~341k dòng (gồm thư viện) | Cấu hình đèn tháp dạng ma trận, chạy từng bước |
| **RefAuto-O** | Khung máy 6 lớp | C# WPF | ~13k dòng | Chủ yếu là ví dụ **không nên** (§6) |
| **RefAuto-P** | Trình soạn luồng vision + chuyển động dạng node | C# WPF | ~25k dòng | 11 tín hiệu trạng thái trục |
| **RefAuto-Q** | Khung WPF | C# WPF Prism | ~31k dòng | Giá trị thấp |
| **RefAuto-R** | Phần mềm máy keo | C++ Qt | ~42k dòng | Bài học về mã sinh hàng loạt (§6) |

---

## 3. Phân tích theo chủ đề

### 3.1 Shell và bố cục màn hình

| Nguồn | Bố cục chính |
|---|---|
| RefAuto-A | 5 trang: **Main** (trạng thái + mode, Start/Stop/Reset/Origin, đèn EMG/khí, 2 lưới khay vẽ động theo số hàng/cột, live 2 camera, đếm OK/NG, cycle time, %NG) · **Manual** (trục theo nhóm, jog/inching, bảng DI/DO, **chạy từng bước của chu trình Auto**) · **Teaching** (điểm + quản lý model) · **Setting** · **Vision Edit**. Banner alarm nằm ở shell, hiện trên mọi trang |
| RefAuto-B | **6 mục điều hướng cấp 1 cố định**: Tổng quan sản xuất · Thao tác sản xuất · Trung tâm recipe · Trung tâm module · Chẩn đoán vận hành · Truy xuất chất lượng. Có quy định cấm thêm mục cấp 1 |
| RefAuto-E | Cột 1 điều hướng cấp 1 + avatar · cột 2 điều hướng cấp 2 · cột 3 vùng làm việc · thanh trạng thái dưới (motion, PLC, thông điệp mới nhất). 11 module, ~38 trang |
| RefAuto-K (ảnh) | Nav icon trái thu gọn · banner thông điệp toàn chiều ngang trên cùng · 2 khung camera bên trái · 2 lưới khay ở giữa · rail phải (recipe, mode, trạng thái, 5 nút điều khiển) · thanh trạng thái (người dùng, cấp quyền, CPU, RAM) |
| RefAuto-L (ảnh) | Tab ngang trên cùng · header recipe + chip trạng thái · nút Start/Stop/Reset đếm · thẻ bin có thanh dung lượng · thống kê theo hạng · bảng kết quả cuộn · panel "mô phỏng một lần" |
| RefAuto-N | Cột trái (mạng, người dùng, Alarm/Log, Home, Quản lý user, Cài đặt/Debug, Chẩn đoán) · khối trạng thái (máy chưa về gốc / không chạy, sản lượng, OK/NG, UPH, Yield, PPM, nút xoá đếm) |
| RefAuto-H | Mỗi tài nguyên (trục, IO, camera, alarm, task…) có **3 màn**: *Monitor* (chỉ xem) · *Man* (quản lý cấu hình) · *Debug* (thao tác thử) |

**Nhận xét chung.**
- Số đông dùng **điều hướng trái + vùng làm việc + thanh trạng thái dưới**; header hiển thị recipe đang chạy và
  trạng thái máy. Phần tử xuất hiện ở gần như mọi nguồn: **recipe hiện tại, trạng thái máy, người dùng/cấp quyền,
  trạng thái kết nối phần cứng, đếm OK/NG/UPH/Yield**.
- Màn chính của máy có khay luôn có **lưới khay vẽ động, mỗi ô tô màu theo trạng thái** (trống / có hàng / đang
  xử lý / OK / NG / bỏ qua). RefAuto-A cho phép **bấm ô để chọn phạm vi chạy**.
- RefAuto-B và RefAuto-E đều có **quy tắc thành văn khi nào được tạo trang mới** (chỉ khi có quyền riêng, giao
  dịch lưu riêng hoặc vòng đời riêng) — chống phình điều hướng.
- RefAuto-E: trang được cache; **timer làm mới chỉ chạy khi trang đang hiện**, và bỏ qua vòng làm mới khi người
  dùng đang gõ ô lọc (tránh mất focus, nhấp nháy).

**AM.AutoFrame đứng ở đâu.** Shell v3 (header + nav gộp, banner co giãn, action bar, chip kết nối) đã có đủ các
phần tử "số đông"; nav đã gọn còn 7 tab (S92). Chưa có: **lưới khay/ô có trạng thái** như một thành phần chuẩn,
**quy tắc thành văn khi nào tạo trang mới**, quy ước **dừng làm mới khi trang ẩn**.

### 3.2 Máy trạng thái và chế độ vận hành

| Nguồn | Trạng thái | Điểm khác AM.AutoFrame |
|---|---|---|
| RefAuto-A (11) | INIT · NOT_HOMED · HOMING · IDLE · **ARMED** · RUNNING · **PAUSING** · PAUSED · **WAIT_OPERATOR** · ALARM · **EMG** | Bảng "trạng thái nào cho phép chuyển động"; tách ARMED (sẵn sàng, chờ nút cứng) khỏi RUNNING; WAIT_OPERATOR khi máy cần người nạp/đổi khay |
| RefAuto-C (13) | Idle · Initializing · Initialized · **Purging** · Ready · Running · Pausing · Paused · Stopping · Error · **Aborting** · **Aborted** · Reseting | Sự kiện **Purge** (xả hết hàng trong máy); tách **Error** (xử lý được) khỏi **Abort** (phải khởi tạo lại cả máy) |
| RefAuto-I (15) | Chờ khởi tạo → Chờ reset → Chờ về gốc → Đang về gốc → Ready → Running … Alarm → **Chờ dọn liệu** → (lấy hàng ra) → Ready | Sau Stop hoặc Alarm, máy **không về Ready ngay** mà phải xác nhận đã dọn hàng. Jog, hiệu chuẩn, **chạy rodage (burn-in)** là trạng thái con của Running |
| RefAuto-B | Module: Offline · Initializing · Idle · Preparing · Ready · Executing · Paused · Completed · Stopping · **Recovering** · Faulted. Kết nối: Disconnected · Connecting · Ready · **Degraded** · Reconnecting · Faulted | Trạng thái kết nối có **Degraded** |
| RefAuto-D | 8 trạng thái + **chế độ Automatic / Manual / Maintenance / Setup** | Chế độ tách khỏi trạng thái |
| RefAuto-F | Chế độ chạy **Normal / Dry (không keo) / Camera (chỉ chạy vision)** · hành động khi lỗi Normal / PromptReject / AutoReject · tự xả keo khi rảnh lâu | — |

**Dừng có hai mức (RefAuto-A).** STOP lần 1 → PAUSING: chạy nốt tới **checkpoint** gần nhất rồi dừng, hàng không
bị bỏ giữa chừng. Checkpoint chỉ đặt ở ranh giới bước, **không bao giờ giữa lúc trục Z đang hạ hay đang chuyển
chân không**. STOP lần 2 (hoặc giữ > 2 s) → dừng giảm tốc ngay tại chỗ, giữ servo, giữ chân không, đánh dấu
"dừng không an toàn" → khi chạy tiếp phải nâng Z về cao độ an toàn trước.

**AM.AutoFrame đứng ở đâu.** 8 trạng thái ISA-88 + Pause tới ranh giới bước của sequence engine + `IResumeVerifiable`
(xác minh cơ cấu trước khi chạy tiếp) + chế độ Normal/DryRun/từng bước. **Chưa có**: trạng thái/sự kiện **dọn
liệu (Purge)**, trạng thái **chờ người thao tác** tách khỏi Paused, khái niệm **dừng hai mức** và cờ
"dừng không an toàn", chế độ **chỉ chạy vision**/rodage.

### 3.3 An toàn và interlock

Đây là phần RefAuto-A làm tốt nhất trong cả 18 nguồn — tài liệu thiết kế viết rõ lý do từng quy tắc:

1. **"Phần mềm không phải lớp an toàn."** IO qua Ethernet không có chứng nhận an toàn; EMG và rèm sáng phải
   đi vào relay an toàn cắt nguồn động lực / STO. Khi chủ đầu tư quyết định không làm vậy, tài liệu **ghi lại quyết
   định đó và hệ quả**, và phần mềm coi **mất kết nối IO là dừng khẩn** (huỷ mọi chuyển động, mức Fatal).
2. **Không có nút EMG trên màn hình** — nút mềm tạo cảm giác an toàn sai.
3. **Không nút mềm nào làm trục chạy chu trình.** Nút START trên màn hình chỉ đưa máy vào ARMED; chạy thật phải
   bấm nút Start cứng ở máy (người đứng đúng chỗ, nhìn thấy vùng làm việc). Bài học kèm theo: đường chạy trong
   self-test khác đường chạy thật nên test đạt mà máy vẫn sai → gom về một hàm khởi động duy nhất.
4. **Nút bấm đọc theo sườn + chống dội 20 ms** — đọc theo mức thì nút kẹt làm máy tự khởi động lại liên tục.
5. **Tín hiệu an toàn dùng tiếp điểm thường đóng** — đứt dây = trạng thái an toàn.
6. **Van chân không không bị cắt khi EMG**, phần mềm cũng không tự tắt van ở bất kỳ đường dừng nào (tránh rơi
   hàng thành vật cản cho lần về gốc sau) — có bài tự kiểm canh điều này.
7. **Rèm sáng bị cắt chặn chuyển động ở mọi chế độ**, kể cả Manual; rèm thông trở lại **không tự chạy tiếp**, phải
   bấm RESET.
8. **Giám sát an toàn chạy độc lập** (≤ 20 ms), có quyền ép trạng thái qua một `CancellationToken` chung; các
   routine không tự kiểm tra an toàn rải rác. **Watchdog**: vòng điều khiển không tick trong 500 ms → alarm và dừng.
9. **Interlock Z an toàn bất khả xâm phạm**: trục ngang chỉ chạy khi mọi trục Z ở cao độ an toàn, **đọc encoder
   thật** chứ không tin lệnh nâng trước đó đã trả về thành công; không được nới để rút cycle time.
10. **Ràng buộc phần cứng do code giữ, không do file cấu hình**: cờ "bắt về gốc sau EMG" bị bỏ qua khi máy dùng
    encoder tương đối, vì "tắt được bằng file cấu hình thì sớm muộn sẽ có người tắt".
11. **Cờ "đã về gốc" thuộc phần mềm**, không suy từ bit hoàn tất của driver (driver không biết phần mềm đã khởi
    động lại). Về gốc có **bước O0 kiểm tra không chuyển động**, liệt kê đúng dòng không đạt.
12. **Phanh trục đứng**: đo độ tụt khi Servo OFF 10 lần, yêu cầu < 0,5 mm; chỉnh thời điểm đóng phanh so với cắt
    mô-men.

Các nguồn khác bổ sung:
- **RefAuto-C — chống va chạm cấu hình được.** Nhóm va chạm gồm các điều kiện (một trục, hai trục kết hợp, xi
  lanh, DI) và điều kiện bỏ qua; mọi lệnh chuyển động gọi `checkCollision(trục, đích)` trước khi phát, vi phạm thì
  ném lỗi có mô tả. Có trình soạn riêng, bật/tắt được.
- **RefAuto-B — kiểm tra không gian.** Cây hệ toạ độ (máy → dầm → trạm/đầu hút/camera), các **vùng** (làm việc,
  đi qua, bàn giao, hiệu chuẩn, **cấm vào**), cao độ an toàn; bộ kiểm tra xét điểm cuối, đường thẳng, khe hở và vùng
  cấm trước khi phát lệnh. Tài liệu ghi rõ đây là **bảo vệ quy hoạch phía PC, không thay hard limit, EMG hay PLC
  an toàn**. Mọi lệnh điểm đi qua một lớp kiểm soft limit, hướng hard limit, servo; mọi tốc độ bị cắt bởi MaxVel.
  Về gốc **chỉ thành công khi thấy tín hiệu cảm biến thật**, không tin "motion done" của card.
- **RefAuto-B, RefAuto-L — thứ tự về gốc**: Z trước, rồi X/Y; Reset ở màn chính và Reset sau khởi tạo dùng
  **chung một chương trình**, kết thúc ở "vị trí an toàn chung".
- **RefAuto-P, RefAuto-G** — tập tín hiệu trạng thái trục đầy đủ hơn: ALM, ±EL, EMG, ORG, ±SL (soft limit), INP,
  EZ, RDY, DSTOP; trạng thái lệnh: hoàn tất, EMG, chạm limit, alarm, treo, timeout, **bị cấm về gốc / bị cấm chạy**,
  lỗi về gốc, lỗi vòng kín encoder.

**AM.AutoFrame đứng ở đâu.** Đã có nhiều điều tương đương (chính sách R0–R3, guard, phanh Z tự đóng khi rời màn,
nút vật lý Start/Stop/Reset, xác minh trước Resume, LED 7 tín hiệu trục). **Chưa có**: chống va chạm/vùng cấm cấu
hình được; quy tắc "nút mềm không chạy chu trình"; cam kết thành văn về EMG không có nút mềm, van chân không
không cắt khi dừng, rèm sáng phải RESET; watchdog vòng điều khiển; bài kiểm độ tụt phanh trong checklist nghiệm thu.

### 3.4 Alarm

| Khía cạnh | Cách làm hay nhất tìm thấy |
|---|---|
| Mức | RefAuto-A: **Fatal** (cắt servo, bắt về gốc) · **Error** (dừng chu trình, giữ servo) · **Warning** (không dừng, đèn vàng nháy) · **Info** (chỉ log) |
| Mã | RefAuto-A: khối theo nguồn (hệ thống, an toàn, **mỗi trục một khối**, IO, vision, chu trình, dữ liệu) + **offset chung cho mọi trục** (+00 mất kết nối, +01 alarm driver, +02 timeout, +03 soft limit, +04 hard limit, +05 timeout về gốc, +06 chưa về gốc, +07 servo off bất ngờ, +08 sai lệch bám) → đọc mã biết ngay trục nào lỗi gì |
| Nội dung | Hai ngôn ngữ, **Nguyên nhân**, **Cách xử lý**, **Chi tiết động** (mã lỗi driver, vị trí, điểm vision) |
| Ai sở hữu gì | Lời văn nằm trong file JSON — thuộc người dùng, không bị ghi đè. **Mức alarm thuộc phần mềm**, được lấy lại từ bản mẫu mỗi lần nạp (vì hạ Fatal xuống Warning bằng tay là máy chạy qua sự cố). Mã mới trong phiên bản mới được **hoà vào** file của người dùng; mã chưa định nghĩa hiện "chưa định nghĩa: <mã>" chứ không nuốt |
| Lỗi gốc | Alarm **đầu tiên trong cụm 200 ms** được đánh dấu lỗi gốc, hiện đầu và in đậm; alarm kéo theo thu gọn bên dưới — tránh operator sửa cái cuối cùng |
| Xoá | Không tự xoá Error/Fatal; Warning cũng phải RESET; alarm hết nguyên nhân nhưng chưa RESET thì chặn Start. **RESET không xoá được thì phải nói vì sao** ("EMG vẫn đang nhấn") |
| Xử lý có lựa chọn | RefAuto-C: lỗi mang theo **danh sách lựa chọn** cho operator — Thử lại · Bỏ qua · Huỷ · Bỏ qua sản phẩm · **Lấy hàng ra bằng tay** · **Đặt hàng vào bằng tay** · **Đặt không qua vision**; chế độ tự động có thể đặt sẵn lựa chọn mặc định; lỗi không xử lý được ném loại riêng → Abort, phải khởi tạo lại |
| Trạng thái | RefAuto-D: Active / Cleared / **Acknowledged** — alarm có bước xác nhận |
| Hiển thị | RefAuto-E: ngăn kéo alarm đang hoạt động + thông báo nổi góc dưới (Error 6 s, Warn 5 s, Info 3 s) + dòng trạng thái "[giờ] [nguồn] nội dung [mã]" |

**AM.AutoFrame đứng ở đâu.** Đã có: dải mã theo nhóm, `[AlarmInfo]` giữ mức trong code, catalog JSON tên + cách xử
lý 3 ngôn ngữ, lịch sử + Pareto, banner đa alarm, hành động phục hồi trạm (design-note 0002). **Chưa có**: offset
chung cho mỗi trục, **trường Nguyên nhân + Chi tiết động** riêng, **đánh dấu lỗi gốc**, hoà mã mới vào catalog
người dùng khi nâng cấp, **thông điệp khi RESET bị từ chối**, lựa chọn "lấy hàng bằng tay / đặt không vision".

### 3.5 Phân quyền

| Nguồn | Mô hình | Ghi chú |
|---|---|---|
| RefAuto-A | Operator (chỉ Main) · Technician (+ Manual, Teaching) · Engineer (tất cả) | Đơn giản, theo trang |
| RefAuto-B | **Vai trò sở hữu "năng lực chức năng"**, không phân quyền từng nút. Nhân viên · Kỹ sư · **Hãng (vendor: comm, IO, firmware)** · Admin (cố định đủ quyền, không gỡ được) | Không có năng lực thì **ẩn điều hướng**, nhưng lệnh vẫn kiểm lại ở ViewModel/Service. Quyền lưu trong một file JSON |
| RefAuto-E | Mỗi trang khai **vai trò mặc định + mức rủi ro**, đồng bộ vào DB lúc khởi động; module cấp 1 chỉ hiện nếu có ≥ 1 trang con được phép | Gần với R0–R3 của AM.AutoFrame |
| RefAuto-F | None · Operator · Technician · Engineer · Admin · Hãng. **Admin chuột phải vào bất kỳ nút nào để chọn cấp nào bị khoá**, lưu theo form; có **chế độ xem trước giao diện như một cấp thấp hơn** | Linh hoạt tối đa nhưng khó kiểm toán |
| RefAuto-C | Từng **trường cấu hình** có thể đánh dấu cần quyền Kỹ sư | Quyền ở mức dữ liệu |
| RefAuto-J | RBAC kiểu web admin (phòng ban, chức vụ, vai trò, menu) + nhật ký thao tác bằng attribute | Quá nặng cho máy đơn |

Cấp **"Hãng/Vendor"** xuất hiện ở 3/18 nguồn (cho người của nhà sản xuất máy chỉnh comm, IO, firmware).

**AM.AutoFrame đứng ở đâu.** 5 cấp + 4 mức rủi ro R0–R3 + guard + audit — chặt hơn hầu hết nguồn. Hai ý đáng cân
nhắc: **xem trước giao diện theo cấp** (giúp Admin kiểm phân quyền) và **chú thích quyền theo năng lực** thay vì
theo nút để danh sách quyền không phình.

### 3.6 Recipe, cấu hình, hiệu chuẩn

- **RefAuto-B — recipe có phiên bản và kế thừa**: recipe cha, số revision, phần ghi đè; bộ giải phát hiện vòng
  kế thừa/thiếu cha và **đóng băng một snapshot hiệu lực** trước khi chạy; runner chỉ nhận recipe đã giải.
- **RefAuto-B — snapshot hiệu chuẩn 5 trạng thái**: Nháp · Đã kiểm · **Đang dùng** · Đã thay thế · Không hợp lệ.
  Mỗi chu kỳ sản xuất **đóng băng snapshot đang dùng**; kỹ sư sửa nháp hay hiệu chuẩn lại không đổi toạ độ của
  chu kỳ đang chạy, snapshot mới chỉ áp cho chu kỳ sau.
- **RefAuto-B — trung tâm "cấu hình đã đổi"**: mỗi lần lưu tăng một số revision và ghi phạm vi (topology, trục,
  trạm, hiệu chuẩn, module, vision); mỗi trang chỉ đăng ký phạm vi mình cần. Trạng thái thời gian thực **không**
  đi đường này.
- **RefAuto-D — cấu hình thiết bị theo máy**: danh mục thiết bị của từng loại máy + cấu hình JSON mỗi thiết bị;
  JSON có sẵn ưu tiên hơn mặc định trong code. **MES ngoại tuyến**: trường dữ liệu của khách hàng chỉ sống trong lớp
  chuyển đổi, bên trong dùng ID tham số ổn định; mất kết nối thiết bị không được làm mất tham số kỹ sư vừa nhập.
- **RefAuto-A** — sổ khay ghi xuống đĩa sau mỗi ô xong và khi vào EMG/ALARM để điều tra, **nhưng không dùng để
  chạy tiếp qua một lần tắt/mở app**: mở lại app luôn bắt về gốc. Khi Start sau Pause, **so cảm biến chân không với
  sổ**: phần mềm ghi có hàng mà cảm biến báo không → chặn, alarm; cảm biến báo có mà phần mềm ghi không → nhận
  hàng, soi lại rồi đặt.
- **RefAuto-C** — đèn tháp và còi là **bảng trạng thái → (đỏ/vàng/xanh: tắt/sáng/nháy, còi)** trong file cấu hình;
  RefAuto-N có màn chỉnh ma trận này, nhập/xuất được.

**AM.AutoFrame đứng ở đâu.** Đã có: recipe + ngưỡng analog theo recipe, backup bảng điểm mỗi lần lưu, toàn vẹn cấu
hình SHA-256, mô hình hiệu chuẩn có lịch sử và nhắc hạn, tower light service. **Chưa có**: recipe kế thừa/phiên
bản, **đóng băng snapshot recipe + hiệu chuẩn cho mỗi chu kỳ**, đối chiếu cảm biến ↔ sổ hàng khi Resume ở mức
khung (hiện tuỳ từng station qua `IResumeVerifiable`), màn chỉnh ma trận đèn tháp.

### 3.7 Vận hành tay, teach, chạy thử

- **Chạy từng bước của chu trình Auto** từ màn Manual: RefAuto-A, RefAuto-N.
- **Màn teach riêng cho từng loại cơ cấu** (trục, xi lanh, IO, chân không): RefAuto-M; RefAuto-E có màn quản lý
  "actuator" chung (xi lanh, gripper, chân không, đèn tháp).
- **Chương trình bước do kỹ sư soạn** (MoveAxis, MoveAxes, HomeAxis, MoveToWorkstation, SetOutput, WaitInput, Delay,
  **Confirm**, StopAxes) và gắn nhãn cho mục đích (Reset hệ thống, vị trí an toàn): RefAuto-B.
- **Ma trận vị trí có tên theo trạm** (Approach, Pick, Place, CalibrationTarget): RefAuto-B.
- **Panel mô phỏng một lần** — nhập dữ liệu giả rồi chạy một chu kỳ để kiểm luồng không cần phần cứng: RefAuto-L.
- **Self-test bằng tham số dòng lệnh** với IO giả và driver giả: RefAuto-A; selftest phải đi đúng đường vận hành
  thật (§3.3 mục 3).

**AM.AutoFrame đứng ở đâu.** Màn Vận hành tay đã mạnh (trục & điểm, jog liên tục/bước, interlock nêu lý do, chạy
lặp 2 điểm, phanh Z). **Chưa có**: chạy từng bước một station từ màn tay, ma trận vị trí có tên theo trạm, nút
"mô phỏng một chu kỳ" trên UI.

### 3.8 Trình soạn luồng / sequence

5/18 nguồn có trình soạn luồng: kéo-thả node (RefAuto-P, RefAuto-D), cây lệnh có if/while/script C# biên dịch lúc
chạy và gọi DLL bằng reflection (RefAuto-I), bước JSON (RefAuto-O), trình thông dịch kiểu IEC 61131 (RefAuto-H).
RefAuto-G tách **bước đo** (khai báo thiết bị cần bằng attribute) ⟂ **recipe tham số** ⟂ **bộ tính kết quả** ⟂
**tổ hợp bước** ghép bằng UI, có chế độ đơn giản cho người không chuyên. RefAuto-F (thương mại) dùng **danh sách
lệnh tra keo**, mỗi lệnh một form tham số.

**AM.AutoFrame đứng ở đâu.** Sequence JSON theo recipe + engine (ADR 0011) — cùng hướng với số đông, an toàn hơn kiểu
script tự do. Kiểu "khai báo tài nguyên bằng attribute" của RefAuto-G khớp với triết lý `[ParamView]`/`[MechanismUI]`.

### 3.9 Dữ liệu sản xuất, truy xuất, MES, SECS/GEM

- RefAuto-B dành **một mục điều hướng cấp 1 cho "Truy xuất chất lượng"** (SN, kết quả, ảnh, đường cong, bản ghi MES).
- RefAuto-J: bắt tay với PLC theo vai trò **Value / Trigger / Reply**; bảng trigger (alarm/event × khi bật/khi đổi)
  + handler đăng ký bằng attribute tự hiện trong dropdown cấu hình; biến SECS lấy từ DB hoặc từ điểm PLC.
- RefAuto-D, RefAuto-F, RefAuto-J: SECS/GEM với trạng thái điều khiển E30 (offline/local/remote).
- RefAuto-G, RefAuto-L: **phân loại kết quả vào bin**, màn bin có dung lượng.

**AM.AutoFrame đứng ở đâu.** Có tab Sản xuất + chi tiết sản phẩm + Pareto NG; SECS/GEM còn là module tuỳ chọn.

### 3.10 Tài liệu và kiểm thử

- **RefAuto-A**: tài liệu thiết kế "sống" ghi cả quyết định của chủ đầu tư, ngày chốt và lý do đổi ý; có thư mục
  bàn giao phiên làm việc cho trợ lý AI (giống mô hình PROJECT_STATUS + memory của AM.AutoFrame).
- **RefAuto-D**: "Hướng dẫn sửa ở hiện trường" — với mỗi interface ghi mục đích, ai implement, ai gọi, và **khi máy
  chạy sai ở hiện trường thì sửa lớp nào**.
- **RefAuto-E**: bộ tài liệu đánh số 00–10 (quản trị tài liệu, kiến trúc, phát triển, tính năng, kiểm thử, vận hành,
  hướng dẫn sử dụng, ghi chú phát hành…), mỗi tài liệu có mã, phiên bản, trạng thái.
- **RefAuto-B**: project **kiểm luật kiến trúc** (test phụ thuộc giữa các lớp) + checklist khi thêm trang mới.
- **RefAuto-L**: 139 test + badge độ phủ.

---

## 4. Ma trận đối chiếu

✅ có · 🟡 một phần · ❌ chưa có. Cột "Nguồn" là nơi thấy cách làm tốt nhất.

| # | Năng lực | AM.AutoFrame | Nguồn |
|---|---|---|---|
| 1 | Shell nav + header recipe/trạng thái + chip kết nối | ✅ | đa số |
| 2 | Lưới khay/ô tô màu theo trạng thái, bấm ô chọn phạm vi | ❌ | A, K |
| 3 | Quy tắc thành văn khi nào tạo trang mới | ❌ | B, E |
| 4 | Dừng làm mới khi trang ẩn | 🟡 (chưa thành quy ước) | E |
| 5 | Trạng thái dọn liệu / Purge | ❌ | C, I |
| 6 | Trạng thái chờ người thao tác tách khỏi Paused | ❌ | A |
| 7 | Dừng hai mức + cờ "dừng không an toàn" | 🟡 (Pause tới ranh giới bước) | A |
| 8 | Chế độ Dry run / từng bước | ✅ | A, F |
| 9 | Chế độ chỉ chạy vision / rodage | ❌ | F, I |
| 10 | Nút mềm không chạy chu trình (chỉ ARM) | ❌ (Start trên action bar) | A |
| 11 | Chống va chạm / vùng cấm cấu hình được | ✅ S99 (`MoveGuard`, design-notes/0015) | B, C |
| 12 | Watchdog vòng điều khiển | ❌ (chưa thấy) | A |
| 13 | Rèm sáng phải RESET mới chạy lại | 🟡 (chưa thành văn) | A |
| 14 | Về gốc chỉ thành công khi thấy cảm biến thật | 🟡 (tuỳ driver) | A, B |
| 15 | Alarm: Nguyên nhân + Cách xử lý + Chi tiết động | 🟡 (có Cách xử lý) | A |
| 16 | Alarm: mức thuộc phần mềm, lời văn thuộc người dùng | ✅ | A |
| 17 | Alarm: đánh dấu lỗi gốc trong cụm | ❌ | A |
| 18 | Alarm: hoà mã mới vào catalog người dùng | ❌ (chưa kiểm) | A |
| 19 | RESET bị từ chối phải nói lý do | 🟡 (pre-check Start có, RESET chưa rõ) | A |
| 20 | Lỗi kèm lựa chọn xử lý (thử lại / lấy tay / đặt không vision) | 🟡 (recovery actions ADR 0002) | C |
| 21 | Quyền theo cấp + mức rủi ro | ✅ (chặt hơn đa số) | E |
| 22 | Xem trước giao diện theo cấp | ❌ | F |
| 23 | Recipe phiên bản + kế thừa | ❌ | B |
| 24 | Đóng băng snapshot recipe + hiệu chuẩn mỗi chu kỳ | ❌ | B |
| 25 | Đối chiếu cảm biến ↔ sổ hàng khi Resume | 🟡 (theo station) | A |
| 26 | Ma trận đèn tháp cấu hình được + màn chỉnh | 🟡 (service có, màn chưa) | C, N |
| 27 | Chạy từng bước một station từ màn tay | ❌ | A, N |
| 28 | Mô phỏng một chu kỳ từ UI | ❌ | L |
| 29 | Hướng dẫn "sai ở hiện trường thì sửa lớp nào" | ❌ | D |
| 30 | Test luật kiến trúc | ✅ (`AM.Architecture.Tests` — S98 ghi nhầm ❌, đính chính S99) | B |

---

## 5. Đề xuất (kèm phương án — chủ dự án chọn)

Xếp theo giá trị cho máy thật (P5). Mỗi đề xuất là một việc riêng; chưa làm việc nào trong session này.

### Đ1. Chống va chạm / vùng cấm phía PC (ma trận #11) — giá trị cao nhất · ✅ ĐÃ LÀM S99 (phương án A, design-notes/0015)

| Phương án | Mô tả | Đánh đổi |
|---|---|---|
| **A. Điều kiện va chạm theo trục** (kiểu RefAuto-C) | `IMoveGuard` gọi trước mọi lệnh MoveAbs/Jog: danh sách nhóm điều kiện (trục X trong khoảng ⇒ Z phải ≤ an toàn; xi lanh duỗi ⇒ cấm trục Y…) trong `move-protection.json`, màn soạn cho Engineer | Đơn giản, đúng tư duy người chỉnh máy; không hiểu hình học, nhiều cặp trục thì danh sách dài |
| **B. Mô hình không gian** (kiểu RefAuto-B) | Hệ toạ độ + vùng + cao độ an toàn, kiểm điểm cuối và đường đi | Tổng quát, đẹp; tốn công mô hình hoá mỗi máy, khó kiểm chứng |
| **C. Interlock cứng trong Mechanism** | Mỗi Mechanism tự chặn (như interlock Z của RefAuto-A) | Ít hạ tầng nhất; luật rải rác, không cấu hình được |

Gợi ý: **A + giữ interlock Z cứng trong code** cho máy demo. Dù chọn gì cũng ghi rõ: đây là bảo vệ phía PC, không
thay limit cứng / relay an toàn.

### Đ2. Nút mềm không chạy chu trình (ma trận #10)

| Phương án | Mô tả | Đánh đổi |
|---|---|---|
| **A. Theo RefAuto-A** | Start trên action bar chỉ đưa máy vào trạng thái "Sẵn sàng (chờ nút cứng)"; chạy thật bằng nút Start vật lý | An toàn nhất; máy không có nút cứng thì không chạy được |
| **B. Cấu hình theo máy** | `machine.json` khai `startSource: hardware | software | both`; mặc định `hardware` khi máy có DI.Btn.Start | Linh hoạt; cờ cấu hình có thể bị đổi ẩu (RefAuto-A §3.3 mục 10) → cần audit + chữ ký cấu hình (đã có SHA-256) |
| **C. Giữ như hiện tại** | Start mềm + pre-check | Tiện khi demo; ngược thực hành ở RefAuto-A |

Đây là quyết định an toàn — nên ghi design-note nếu đổi.

### Đ3. Alarm: lỗi gốc + Nguyên nhân + Chi tiết động + hoà mã mới (ma trận #15, #17, #18, #19)
Ít rủi ro, giá trị cao cho operator. Một phương án hợp lý: thêm `cause` vào catalog JSON, `Detail` động vào
`AlarmModel`, cờ `IsRootCause` (alarm đầu tiên trong cửa sổ 200 ms) cho banner; khi nạp catalog thì hoà mã có trong
bản mẫu mà file người dùng thiếu; RESET không xoá được alarm nào thì hiện lý do trên banner.

### Đ4. Dọn liệu và chờ người thao tác (ma trận #5, #6, #7)

| Phương án | Mô tả | Đánh đổi |
|---|---|---|
| **A. Thêm trạng thái vào máy trạng thái 8 trạng thái** | `Purging`, `WaitOperator` | Rõ ràng trên đèn tháp và UI; phá vỡ "8 trạng thái" đã chốt, nhiều test phải sửa |
| **B. Giữ 8 trạng thái, thêm "lý do Paused"** | `PauseReason = Operator | WaitMaterial | Checkpoint | UnsafeStop` + lệnh Purge chạy như một sequence riêng từ Idle | Ít đổi kiến trúc; tower light/UI phải đọc thêm lý do |

### Đ5. Đóng băng snapshot recipe + hiệu chuẩn mỗi chu kỳ (ma trận #24)
Engine sequence nhận một bản sao bất biến recipe + hiệu chuẩn lúc Start; sửa trong lúc chạy chỉ áp cho chu kỳ sau.
Khớp với ADR 0011 (sequence JSON theo recipe) và mô hình hiệu chuẩn hiện có.

### Đ6. Thành phần lưới khay/ô (ma trận #2)
Control dùng chung cho Home: số hàng/cột từ recipe, ô tô màu theo trạng thái (theo palette ISA-101 — chỉ OK/NG/lỗi
mới có màu mạnh), chọn ô để giới hạn phạm vi chạy (Engineer+, audit).

### Đ7. Việc nhỏ, làm khi tiện
- Quy ước làm mới UI: dừng poll khi trang ẩn (#4).
- Màn chỉnh ma trận đèn tháp (#26).
- Chạy từng bước một station từ màn tay (#27); nút "mô phỏng một chu kỳ" ở chế độ simulation (#28).
- "Xem trước như cấp X" cho Admin (#22).
- Tài liệu "sai ở hiện trường thì sửa lớp nào" cho các interface chính (#29) — ghép vào `QUICK_REFERENCE.md`.
- ~~Test luật kiến trúc (#30)~~ — đã có sẵn `AM.Architecture.Tests` (đính chính S99).
- Checklist nghiệm thu P5: đo độ tụt phanh Z (Servo OFF 10 lần, < 0,5 mm), rèm sáng phải RESET, mất kết nối IO = dừng khẩn.

---

## 6. Những thứ KHÔNG nên bắt chước

| Quan sát | Nguồn | Vì sao |
|---|---|---|
| Máy trạng thái chỉ có `SetState`, không bảng chuyển, không guard | RefAuto-O | Trạng thái nào cũng nhảy sang trạng thái nào cũng được |
| Tạm dừng bằng vòng `Task.Delay(100)` thăm dò; `Thread.Sleep` rải khắp mã máy | RefAuto-O, RefAuto-M, RefAuto-G | Trễ, chặn luồng, khó huỷ |
| Banner màu đỏ dùng cả cho thông báo "đã lưu" | RefAuto-K | Màu alarm mất nghĩa (ngược ISA-101) |
| Mỗi nút điều khiển một màu nền khác nhau | RefAuto-K, RefAuto-L | Màn hình "ồn" khi máy bình thường |
| Quyền đặt cho từng nút bằng chuột phải | RefAuto-F | Linh hoạt nhưng không kiểm toán được, khó tái tạo giữa các máy (RefAuto-B chọn ngược lại có chủ ý) |
| RBAC kiểu web admin (phòng ban, chức vụ, menu) | RefAuto-J | Quá nặng cho một máy đơn |
| Đường gọi thẳng SDK card cũ song song với lớp an toàn mới | RefAuto-B (tự ghi nhận nợ) | Lệnh đi vòng qua kiểm soft limit |
| README hoành tráng, module "dự đoán hiệu năng bằng ML", "cân bằng tải", nhiều hàm rỗng | RefAuto-R, RefAuto-Q | Dấu hiệu mã sinh hàng loạt chưa chạy máy; số sao không nói lên chất lượng |
| Sổ tiến độ dùng để chạy tiếp qua lần tắt/mở app | (RefAuto-A đã bỏ có chủ ý) | Vị trí và hàng trong máy không còn tin được sau khi tắt app |

---

## 7. Liên quan

- `docs/HMI_Manual_Operation_and_Safety_v1.0.md` — chính sách an toàn màn tay (so với §3.3).
- `docs/HMI_Button_Spec.md` — nút Start trên action bar (so với Đ2).
- `docs/design-notes/0002-station-recovery-actions.md` — hành động phục hồi (so với §3.4 lựa chọn xử lý).
- `docs/design-notes/0011-sequencing-engine.md` — engine sequence (so với §3.8, Đ5).
- `docs/HMI_Calibration_Model_v1.0.md` — mô hình hiệu chuẩn (so với §3.6).
- `docs/HMI_Advanced_Standards.md` — quyết định áp dụng ISA-101/ISA-18.2.
