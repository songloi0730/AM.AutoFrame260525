# HMI_Manual_Operation_and_Safety — v1.0

Chính sách cho màn **Vận hành tay** (gộp Manual + Motion/IO cũ) và mô hình phân quyền theo mức rủi ro.
Đây là tài liệu CHÍNH SÁCH AN TOÀN, không chỉ UI — mọi thao tác tác động cơ cấu đều tuân theo đây.
Đi kèm: `HMI_UI_Architecture_Template_v2.0.md`, mockup `hmi_manual_operation_v1.html`.

---

## 1. Nguyên tắc gốc

1. **Một màn duy nhất, hành vi đổi theo trạng thái máy.** Không tách Manual và Motion/IO. Ranh giới quyết định mà người dùng quan tâm không phải "trạm hay trục" mà là **"máy đang chạy hay đã dừng"** — gộp theo đúng trục đó.
2. **Phân quyền theo MỨC RỦI RO của hành động, không theo màn hình hay theo thiết bị.** Thao tác là vô hạn và đặc thù từng máy; mức rủi ro thì hữu hạn và chung. Mỗi máy khai thao tác của nó vào đúng mức (giống triết lý HAL).
3. **An toàn theo tầng, tầng trên không bị tầng dưới vượt qua.** Thứ tự kiểm tra: trạng thái máy → role → guard điều kiện. Fail ở tầng nào, báo lý do ở tầng đó.
4. **Không bao giờ giải quyết thiếu quyền bằng chia sẻ tài khoản.** Mỗi người một tài khoản; cần làm nhiều hơn thì nâng role cho tài khoản đó, không đưa mật khẩu cấp cao cho người cấp thấp. Audit log phải luôn ghi đúng người thật.
5. **Nới guard để chiều một ngoại lệ là sai.** Ngoại lệ chấp nhận rủi ro phải đi qua cơ chế Override riêng, minh thị, không làm yếu guard của trường hợp thường ngày.

---

## 2. Bốn vai trò (role)

| Role | Làm được | Ví dụ |
|------|----------|-------|
| **Operator** | Chạy máy, ACK alarm, quick actions tiện ích (R0), cấp liệu | Start/Stop, bật đèn, tắt còi, gọi kỹ thuật |
| **Line Lead** (operator cấp cao — vai trò bổ sung) | + Thao tác phục hồi đóng gói có guard (R1), recovery gắn theo lỗi | Chạy băng tải xả liệu, nhả/kẹp xi lanh, bật/tắt khí âm (trong guard) |
| **Engineer** | + Chuyển động có kiểm soát (R2), jog tự do/teach (R3), Supervised Override, sửa recipe, reconnect | Jog trục, teach điểm, đưa trục về điểm gỡ kẹt, override nhả liệu |
| **Admin** | + Force IO, hardware config, quản lý user | Force output, đổi vendor phần cứng, tạo/sửa user |

Mỗi operator giỏi được nâng lên Line Lead bằng **tài khoản riêng của họ** — đây là lời giải cho "lỗi nhỏ hàng ngày cần quyền cao": hạ ngưỡng thao tác R1 xuống Line Lead thay vì gọi kỹ sư hay chia mật khẩu admin.

---

## 3. Bốn mức rủi ro (risk tier) — cố định, chung mọi máy

| Tier | Bản chất | Ví dụ thao tác | Role tối thiểu |
|------|----------|----------------|----------------|
| **R0 — Tiện ích** | Không tác động liệu/an toàn | Đèn, còi, ionizer | Operator |
| **R1 — Phục hồi đóng gói** | Tác động cơ cấu nhưng CÓ guard ràng buộc, không tự do | Băng tải xả liệu, đóng/nhả xi lanh, bật/tắt khí âm | Line Lead |
| **R2 — Chuyển động có kiểm soát** | Dịch trục theo điểm dạy sẵn / giới hạn chặt, tốc độ giới hạn | Đưa trục về điểm gỡ kẹt (move-to-point) | Engineer (xem §6 — có thể hạ có điều kiện) |
| **R3 — Tự do / đè an toàn** | Trục tọa độ tự do, nhả servo, force IO, teach | Jog tự do, Force Output, nhả servo trục Z | Engineer / Admin |

Thao tác đặc thù máy được khai vào đúng tier trong config — không phát sinh tier mới.

---

## 4. Guard (điều kiện) — gắn vào từng thao tác

Mỗi thao tác KHÔNG phải lệnh trần bật/tắt, mà mang theo tiền điều kiện và hệ quả an toàn, khai dưới dạng dữ liệu (không if-else rải rác trong code). Guard đọc trạng thái thật từ `HardwareInputEventBus` (vị trí trục, cảm biến chân không, cửa…).

Ví dụ cốt lõi — "tắt khí âm" mang guard chống rơi liệu:
```json
{ "id": "VacuumOff", "label": "Tắt khí âm đầu hút",
  "risk": "R1", "halCommand": "Vacuum.Off",
  "guard": "Z1.AtOrBelow(workHeight) || Blow.AssistReady",
  "blockReason": "Z chưa hạ — tắt khí âm có thể làm rơi liệu",
  "roles": ["LineLead+"], "audit": true }
```

Nút bị guard chặn → **mờ + hiện `blockReason`** (giải thích thay vì giấu). Thao tác nguy hiểm-có-điều-kiện không bao giờ chạy khi điều kiện chưa đạt, bất kể ai bấm.

---

## 5. Supervised Override — ngoại lệ chấp nhận rủi ro có kiểm soát

Dành cho tình huống mục tiêu ĐẢO NGƯỢC so với guard thường ngày. Ví dụ: khí âm yếu do dị vật làm kênh → người dùng *chủ động muốn* nhả liệu (có người đỡ) để lấy dị vật, tức cố ý bỏ qua guard "giữ liệu".

KHÔNG xử lý bằng cách nới guard (sẽ làm yếu bảo vệ cho 99% trường hợp còn lại). Thay vào đó là luồng riêng:

- Nút **luôn hiện** (không mờ-rồi-tự-sáng), bấm vào mở luồng xác nhận chủ động.
- Hộp thoại nêu rõ hệ quả vật lý + yêu cầu xác nhận an toàn.
- **Cơ chế xác nhận** (mặc định an toàn — chưa giả định người bấm rảnh tay):
  **xác nhận hai bước + nhả có đếm ngược vài giây**, KHÔNG dùng giữ-nút-2-giây.
  *(⚠ ĐIỂM CẦN XÁC NHẬN: nếu máy luôn có ≥2 người — một bấm, một đỡ — có thể đổi sang giữ-nút-2-giây. Nếu một người vừa bấm vừa đỡ thì giữ-nút là sai vì không rảnh tay. Quyết định theo thực tế từng máy.)*
- **Quyền: Engineer trở lên** — bỏ qua bảo vệ an toàn là việc của người hiểu hệ thống, dù động tác trông đơn giản. Operator/Line Lead xử lý *trong* guard; *vượt* guard là mức cao hơn.
- **Trạng thái: chỉ STOPPED**, chu trình dừng hẳn để "có người đỡ" khả thi.
- **Audit nặng + bắt buộc nhập lý do.**

```json
{ "id": "ReleaseVacuumOverride", "label": "Nhả liệu (override) để lấy dị vật",
  "type": "SupervisedOverride", "halCommand": "Vacuum.Off",
  "precondition": "StateIn(STOPPED)", "overrides": "VacuumOff.guard",
  "confirm": "TwoStep+Countdown(3s)",
  "warning": "Liệu sẽ rơi tự do. Xác nhận có người đỡ, tay ra khỏi vùng nguy hiểm.",
  "roles": ["Engineer+"], "audit": "high", "requireReason": true }
```

`"overrides"` minh thị guard nào bị cố ý bỏ qua — guard thường ngày vẫn nguyên cho mọi người khác.

**Nhả servo / thả trục tự do** (để có không gian lấy dị vật) là Override RIÊNG, không gộp nút với nhả khí âm — hệ quả vật lý khác: trục đứng Z có thể tụt do trọng lực khi nhả servo. Cảnh báo riêng: "Trục Z có thể tụt khi nhả servo — đỡ cơ cấu trước."

---

## 6. Điểm R2 — chính sách mặc định + van xả

R2 (di chuyển trục có kiểm soát để gỡ kẹt) là vùng xám. Chính sách:
- **Mặc định: R2 ở Engineer.**
- **Cho phép từng máy hạ xuống "Line Lead có điều kiện ngặt"** nếu máy đó hay kẹt liệu và gọi kỹ sư mỗi lần là bất khả thi — nhưng CHỈ move-to-point dạy sẵn, tốc độ giới hạn, KHÔNG jog tự do. Khai trong config máy đó (`r2DowngradeToLineLead: true`).
- Mặc định an toàn, có van xả khai báo được cho máy thực sự cần. *(Điểm này cũng cần bạn xác nhận theo từng dòng máy.)*

---

## 7. Màn "Vận hành tay" — thiết kế giao diện

Kế thừa Persistent Frame (header, nav, banner, action bar, thanh kết nối). Tab hiện theo role: **Line Lead trở lên** (Operator không thấy). Vùng làm việc chia:

**Bố cục: phần CỐ ĐỊNH (an toàn, không cuộn) + sub-tab (nội dung, cuộn nội bộ).**
- Cố định trên cùng: dải khóa trạng thái (§7.1) + dải giám sát rút gọn một dòng (vị trí các trục + tín hiệu an toàn). Luôn thấy bất kể đang ở sub-tab nào.
- Sub-tab: **Thao tác trạm | Điều khiển trục | Bảng điểm | ⚠ Override**. Mỗi pane cuộn nội bộ độc lập.
- Lý do dùng sub-tab thay vì nén tất cả vào một màn cuộn dọc: nếu cuộn cả màn, nút an toàn (thao tác trạm, dải khóa) có thể trôi khỏi tầm nhìn khi kéo xuống xem bảng 50 điểm. Sub-tab giữ an toàn luôn cố định, chỉ nội dung dày (bảng điểm) mới cuộn — cuộn nằm đúng chỗ.
- KHÔNG giảm kích thước nút thao tác/jog để nhét vừa màn (≥48–64 px, hậu quả vật lý). Chỉ LED/đèn tín hiệu chỉ-đọc được giảm (16→12 px).

### 7.1 Dải trạng thái khoá (đầu màn, luôn hiện)
Băng ngang nêu rõ màn đang ở chế độ nào, để không ai nhầm:
- Máy **EXECUTE/STARTING** → băng xám: "🔒 Máy đang chạy — chỉ xem, mọi điều chỉnh đã khóa." Toàn bộ khu tác động mờ.
- Máy **STOPPED/PAUSED/IDLE** → băng xanh: "✏ Cho phép điều chỉnh — {role hiện tại}."
- Đây là tầng an toàn §1.3, bind MỘT cờ container `IsAdjustAllowed = StateIn(IDLE,STOPPED,PAUSED) && role>=LineLead`, KHÔNG để mỗi nút tự kiểm tra (tránh nút "lọt lưới").

### 7.2 Khu giám sát (luôn sống, mọi trạng thái)
Phần ĐỌC hiển thị kể cả khi máy chạy — đây là lý do màn này mở được lúc EXECUTE mà Manual cũ không: DRO vị trí các trục, following error, servo, trạng thái IO sống, soft-limit. Không có nút tác động ở khu này.

### 7.3 Khu "Thao tác trạm" (R0–R1, Line Lead+)
Thao tác đóng gói có interlock theo từng trạm: nhả/kẹp xi lanh, bật/tắt khí âm (guard chống rơi), chạy băng tải xả liệu, nâng/hạ Z về điểm dạy. Nút bị guard chặn → mờ + blockReason. Mở khi IsAdjustAllowed.

### 7.4 Khu "Trục & IO thô" (R2–R3, Engineer+ / Force: Admin)
Jog deadman (giữ-để-chạy, watchdog HAL), home, move-to-point, teach (xác nhận cũ→mới + audit), force IO. "Dừng chuyển động" là nút riêng (dừng motion, khác Stop chu trình). Force Output riêng Admin, ngoài EXECUTE, audit.

### 7.5 Khu "Override có giám sát" (Engineer+)
Tách bạch khỏi các khu trên, nhãn cảnh báo rõ. Mỗi nút theo §5: luôn hiện, bấm → luồng xác nhận hai bước + đếm ngược + lý do + audit nặng.

---

## 8. Recovery gắn theo lỗi (khuyến nghị)

Thay vì bắt người dùng tự mở khu thao tác rồi tìm nút, **đính thao tác phục hồi vào đúng alarm**: khi máy báo "liệu lệch tại Loader", ErrorDetailView hiện sẵn nút recovery phù hợp ("Nhả xi lanh kẹp → chỉnh → kẹp lại") cho Line Lead. An toàn hơn (quyền hẹp đúng ngữ cảnh, không mở rộng khu thao tác) và nhanh hơn. Mỗi alarm khai `recoveryActions: [id…]` trỏ tới các action đã định nghĩa.

---

## 9. Bất biến an toàn (kiểm tra khi review code)

1. Mọi thao tác R1+ chạy qua engine kiểm tra **trạng thái máy → role → guard**, không nút nào gọi HAL trực tiếp bỏ qua engine.
2. Khu điều chỉnh bind MỘT cờ container theo trạng thái máy; EXECUTE thì cả container khóa.
3. Tín hiệu guard đọc từ `HardwareInputEventBus` (event push), không polling.
4. Jog dùng watchdog HAL: UI gửi tick định kỳ khi giữ, HAL tự dừng nếu mất tick > 200 ms (UI treo/mất kết nối → trục dừng).
5. Move-to-point và "→ Tới" chạy tốc độ giới hạn an toàn, bất kể override tốc độ.
6. Mọi R1+ và mọi Override ghi audit: user thật, thời gian, lệnh, guard bị override (nếu có), lý do, kết quả.
7. Override không bao giờ mở khi máy EXECUTE.
8. Nhả servo trục đứng cảnh báo nguy cơ tụt do trọng lực trước khi thực hiện.

---

*Cần xác nhận từ người dùng: (a) cơ chế confirm của Override — một người hay hai người đỡ liệu (§5); (b) R2 cứng ở Engineer hay cho hạ có điều kiện theo máy (§6).*


---

> **Adoption AM.AutoFrame**: phản biện + quyết định "build gì / map gì / hoãn gì" cho codebase thật
> nằm tập trung ở `docs/HMI_Master_Index.md §11` (nguồn DUY NHẤT). Đọc đó trước khi hiện thực màn này.
