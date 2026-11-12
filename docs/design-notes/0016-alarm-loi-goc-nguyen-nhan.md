# 0016 — Alarm: đánh dấu lỗi gốc + hiện Nguyên nhân/Cách xử lý cho operator

**Trạng thái:** Đã chốt và thực hiện (S101) — chủ dự án chọn Đ3 trong `docs/Reference_Automation_Software_Survey.md`.
**Liên quan:** `HMI_Advanced_Standards.md` (EEMUA 201 / ISA-18.2 — không flood, phân mức), `HMI_UI_Architecture_Template_v2.md`
(multi-alarm banner), 0002 (hành động phục hồi trạm).

## Bối cảnh

Khảo sát S98 (RefAuto-A) cho thấy 4 thực hành alarm AM.AutoFrame chưa có: đánh dấu **lỗi gốc** trong một chùm alarm,
trường **Nguyên nhân**, hoà mã mới vào catalog người dùng khi nâng cấp, và **RESET bị từ chối phải nói lý do**.

Rà mã S101 tìm thêm:
1. Catalog `lang/Alarms.*.json` đã có **"Cách xử lý" (remedy) cho cả 57 mã — nhưng KHÔNG màn nào hiển thị** (chỉ test đọc).
   Operator thấy mã + thông điệp động, không thấy hướng xử lý.
2. Banner chọn alarm theo Level giảm dần rồi **mới nhất trước** ⇒ trong một chùm cùng mức (mất kết nối → timeout → rơi hàng),
   banner hiện **hệ quả cuối cùng** — đúng kịch bản "operator đi sửa cái cuối" mà RefAuto-A cảnh báo.
3. Reset máy (state machine) **không xoá alarm**; alarm mất khi Xoá ở màn Cảnh báo hoặc khi "chủ" của nó tự xoá
   (phanh 10009, Force IO 70010, bypass 70012).

## Các phương án

### Xác định lỗi gốc

| Phương án | Đặc điểm | Đánh giá |
|---|---|---|
| **A. Cửa sổ thời gian (CHỌN)** | Alarm đầu tiên khi không có chùm nào đang mở = gốc; mọi alarm raise trong 200 ms kể từ gốc (gốc còn active) = hệ quả, ghi `RootCauseCode` | Không cần biết quan hệ nhân quả; đúng với chùm tức thời (mất nguồn driver, EMG). Sai với hệ quả chậm (timeout 10 s sau mất kết nối) — chấp nhận, nhãn "gốc" chỉ là gợi ý |
| B. Bảng quan hệ khai báo | Khai "50001 ⇒ 10001, 10008…" | Chính xác hơn nhưng phải bảo trì bảng cho mọi máy; quan hệ phụ thuộc phần cứng |
| C. Tham chiếu tường minh khi raise | Caller truyền `causedBy` | Chính xác nhất cho mã tự viết; phần lớn alarm sinh ở driver/engine không biết nguyên nhân |

### Hiển thị

| Phương án | Đánh giá |
|---|---|
| **Banner: Level giảm dần → GỐC trước hệ quả → mới nhất; ghi "lỗi gốc · kéo theo n" (CHỌN)** | An toàn vẫn ưu tiên (mức cao hơn luôn lên trước — EEMUA 201); trong cùng mức thì gốc lên trước |
| Banner luôn hiện gốc bất kể mức | Có thể che alarm an toàn mức cao hơn là hệ quả — không chấp nhận |

Màn Cảnh báo: cột "Gốc" (hệ quả hiện "↳ <mã gốc>", gốc in đậm) + **khung chi tiết của dòng đang chọn**: tên alarm, Nguyên nhân
thường gặp, Cách xử lý (từ catalog theo ngôn ngữ đang chọn).

### Nguyên nhân (cause)

Thêm trường `cause` vào catalog (tuỳ chọn — rỗng thì ẩn dòng). Điền cho các mã hay gặp nhất (chuyển động, IO, an toàn, vision,
giao tiếp); mã còn lại điền dần.

## Không làm (và vì sao)

- **Hoà mã mới vào catalog người dùng**: AM.AutoFrame chỉ có catalog đi kèm bản cài (`lang/` cạnh exe, ghi đè khi cập nhật),
  không có bản người dùng sửa riêng ⇒ không có chỗ bị "kẹt" mã cũ như RefAuto-A. Mức alarm đã thuộc phần mềm
  (`AlarmPolicy`/`[AlarmInfo]`), file chỉ có lời văn.
- **"RESET bị từ chối phải nói lý do"**: Reset ở đây là lệnh state machine, không xoá alarm, nên không có tình huống "Reset
  không xoá được alarm". Câu hỏi lớn hơn — **Reset có nên xoá các alarm đã hết nguyên nhân không** (RefAuto-A làm vậy) —
  là quyết định vận hành của chủ dự án, ghi lại để bàn riêng. → **Đã chốt "Có" ở S102, xem 0017.**
- **Lưu lỗi gốc vào lịch sử DB**: cần migration EF; lịch sử + Pareto hiện đủ dùng. Làm khi có nhu cầu phân tích chùm.

## Hệ quả

- `AlarmModel.RootCauseCode` (null = gốc), `IsRootCause`; `AlarmService` gán lúc raise (cửa sổ cấu hình được, mặc định 200 ms).
- `IAlarmCatalogService.GetCause`; catalog thêm `cause` cho một phần mã ×3 ngôn ngữ.
- Banner đổi thứ tự trong cùng mức (gốc trước). Nhãn gốc là **gợi ý theo thời gian**, không phải chẩn đoán.
