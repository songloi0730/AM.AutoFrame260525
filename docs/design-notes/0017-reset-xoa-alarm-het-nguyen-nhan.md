# 0017 — Reset xoá alarm đã hết nguyên nhân, alarm còn nguyên nhân thì giữ và NÓI LÝ DO

**Trạng thái:** Đã chốt và thực hiện (S102) — chủ dự án chọn "Có, Reset xoá" (câu hỏi mở của 0016).
**Liên quan:** 0016 (lỗi gốc), 0012 (hành vi nguy hiểm phải ồn ào), 0013 (phanh Z), 0015 (bypass luật va chạm),
`HMI_Button_Spec.md` (nút Reset).

## Bối cảnh

Trước S102, Reset là lệnh state machine (RunAlarm/InitAlarm → Resetting → Idle) và **không xoá alarm nào**: operator phải
vào màn Cảnh báo xoá tay từng dòng. RefAuto-A (nguồn tốt nhất về an toàn trong khảo sát S98) làm khác: RESET xoá mọi alarm đã
hết nguyên nhân; alarm còn nguyên nhân thì ở lại, và **RESET không xoá được thì phải nói vì sao** ("EMG vẫn đang nhấn") —
đây là tình huống operator hay bấm loạn nhất.

Alarm trong AM.AutoFrame có hai loại:
- **Sự kiện** (timeout, chụp ảnh lỗi, sequence lỗi…): nguyên nhân là một lần xảy ra — Reset xoá được.
- **Điều kiện kéo dài**: E-Stop/cửa/rèm (7000x), Force IO (70010), luật va chạm đang tắt (70012), phanh Z đang nhả (10009),
  file cấu hình lệch manifest (40013). Xoá khi điều kiện còn là nói dối operator — và chủ của alarm không raise lại.

## Các phương án

| Phương án | Đặc điểm | Đánh giá |
|---|---|---|
| A. Danh sách mã "không bao giờ Reset xoá" | Tĩnh trong `AlarmPolicy` | Đơn giản; nhưng E-Stop đã nhả vẫn không xoá được bằng Reset (ai xoá?), và không nói được lý do cụ thể |
| **B. Nguồn điều kiện đăng ký (CHỌN)** | `IAlarmConditionSource` trả "lý do còn giữ" cho mã nó sở hữu (hoặc null = đã hết). Reset hỏi mọi nguồn; mã không ai nhận = sự kiện ⇒ xoá | Lý do chính xác theo trạng thái thật (đọc ngõ vào an toàn, trạng thái phanh, danh sách Force); thêm nguồn mới không sửa Reset |
| C. Mỗi chủ tự xoá alarm khi điều kiện hết | Không cần Reset | Alarm tự biến mất khi lỗi chập chờn — RefAuto-A cấm đúng điều này (máy có thể tự chạy lại) |

## Quyết định

- `IAlarmService.ClearResolvedAsync(raisedBefore)` → `AlarmClearResult` (đã xoá / giữ kèm lý do).
  - Chỉ xét alarm raise **trước** lúc bắt đầu Reset — alarm phát sinh TRONG lúc Reset (vd về gốc lỗi) là mới, giữ lại,
    lý do "phát sinh trong lúc Reset".
  - Mã có nguồn điều kiện báo còn ⇒ giữ + lý do; ngược lại ⇒ xoá (kể cả chưa ACK — Reset là xác nhận của operator).
- Nguồn đăng ký lúc khởi động (`App.xaml.cs`), tránh vòng phụ thuộc DI:
  `SafetyAlarmCondition` (70001/70002/70003/70004 theo `ISafetyInput`), `IoForcedAlarmCondition` (70010),
  `BrakeAlarmCondition` (10009), `MoveGuard` (70012), `ConfigIntegrityService` (40013).
- `BaseMasterController.ResetAsync`: Reset thành công ⇒ gọi xoá ⇒ phát `IMasterController.ResetCompleted` kèm kết quả.
  Cả nút mềm lẫn nút vật lý đi chung đường này.
- Shell: còn alarm bị giữ ⇒ hỏi trên banner (khung prompt có sẵn) liệt kê "[mã] lý do", nút "Đã hiểu".

## Hệ quả

- Hành vi mới: sau Reset, alarm sự kiện biến mất khỏi danh sách đang hoạt động (vẫn còn trong Lịch sử DB).
- Reset KHÔNG được bỏ qua điều kiện an toàn: E-Stop còn nhấn ⇒ 70001 ở lại và được nói rõ.
- Nguồn điều kiện mới (máy cụ thể) phải tự đăng ký; quên đăng ký ⇒ alarm đó bị coi là sự kiện và bị xoá — ghi trong
  checklist thêm alarm điều kiện.

## Bổ sung S103 — Reset bị TỪ CHỐI khi mạch an toàn chưa đạt

Smoke trên app (nút giả lập E-Stop, S103) lộ ra: Reset khi E-Stop còn nhấn vẫn đưa máy về Idle (giữ 70001), sau đó nút Reset
khoá vì chỉ bật ở trạng thái lỗi ⇒ **70001 kẹt, không còn cách xoá bằng Reset**. Sửa: `BaseMasterController.ResetAsync` kiểm
`ISafetyInput.IsAllSafe` trước; chưa đạt ⇒ không đổi trạng thái, không gọi `ResetCoreAsync` (về gốc khi mất động lực vốn cũng
hỏng), không xoá alarm nào, phát `ResetCompleted` với `IsRefused` + lý do ⇒ banner "Reset bị từ chối — E-Stop vẫn đang nhấn…".
Đúng thực hành RefAuto-A (RESET không xoá được phải nói vì sao). Nguồn điều kiện an toàn (`SafetyAlarmCondition`) vẫn giữ để
phủ trường hợp alarm an toàn còn sót khi Reset từ trạng thái khác.

**Nút giả lập E-Stop** (action bar, chỉ khi `UseSimulation=true` VÀ `ISafetyInput` là `SimulatedSafetyInput`): cần CẢ HAI
điều kiện vì bản phần cứng thật hiện vẫn đăng ký `SimulatedSafetyInput` (chưa có driver an toàn thật) — nếu chỉ kiểm kiểu, máy
thật sẽ có nút này. Đây KHÔNG phải nút EMG mềm (RefAuto-A §3.1 quy tắc 6): nó giả lập nút vật lý để thử, máy thật không có.

