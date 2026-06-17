# 0002 — Thao tác trạm (RecoveryActions) — §6.3

**Bối cảnh.** Màn Vận hành tay có sub-tab "Thao tác trạm" (PANE 3) đang empty-state. Cần: danh sách thao tác *phục hồi*
mức R1 (băng tải xả, nhả/đóng xi lanh, tắt/bật khí âm…) — mỗi thao tác mang **risk + guard (điều kiện phần cứng) + blockReason
+ audit**, gọi HAL thật. Nền đã có: guard engine 3 tầng + `GuardCondition` + `IHardwareSignalBus` (§S62), và pattern
`QuickActionVm` (Dashboard) đã làm "guarded action" nhưng hardcode trong code.

Câu hỏi thiết kế cốt lõi: **mô hình hoá một "thao tác" thế nào** — đặc biệt phần *halCommand* (lệnh phần cứng) nối ra sao.

## Các phương án

### A — Config thuần + dispatcher chuỗi `halCommand`
JSON khai trọn vẹn gồm `"halCommand": "Vacuum.Off"`; một `IHalCommandDispatcher` map chuỗi → lệnh HAL thật (đăng ký ở Shell).
- **+** Thêm/sửa thao tác **không cần biên dịch lại**; bám sát ví dụ trong tài liệu an toàn §4 nhất; thuần dữ liệu.
- **−** Lỗi tên `halCommand` chỉ lộ **lúc chạy**; thêm một tầng gián tiếp chuỗi→hàm phải tự bảo trì; dispatcher dễ phình thành
  "God switch".

### B — Khai bằng code trong WorkStation (typed)
Mỗi máy khai action bằng C# — `RecoveryAction(id, label, risk, guard, Func<ct,Task> execute)` — `execute` gọi thẳng method
nghiệp vụ của Mechanism.
- **+** **Type-safe tuyệt đối**, refactor an toàn, gọi được thao tác giàu (không chỉ bật/tắt DO); IDE bắt lỗi ngay.
- **−** Thêm/sửa thao tác phải **biên dịch lại**; nhãn/i18n + risk + guard nằm trong code → ít "data-driven" như tài liệu mong;
  mỗi máy lặp code khung.

### C — Hybrid: config metadata + handler code theo id  ✅ **CHỌN**
JSON khai *metadata* (`id`, `labelKey`, `icon`, `risk`, `guard`→signal keys, `blockKey`, `requiresAdmin`); WorkStation **đăng ký
handler theo id** (`registry.Register(id, Func<ct,Task>)`) gọi HAL thật.
- **+** Phần khai báo **data-driven** (i18n, risk, guard sửa không cần biên dịch) + phần thực thi **type-safe**;
  **đúng pattern `QuickActions` đang dùng** (id trong code + HAL theo id) → nhất quán, nợ kỹ thuật thấp;
  thiếu handler cho một id → UI tự báo "chưa cấu hình HAL" (mờ + lý do) thay vì nổ.
- **−** Hai nơi (config + handler) phải **khớp id**; một id trong config mà quên Register → thao tác mờ (đã biến điểm yếu này
  thành phản hồi UI rõ ràng).

## Quyết định & lý do
Chọn **C**. Cân bằng đúng nhu cầu dự án: tài liệu muốn guard/nhãn/risk *khai bằng dữ liệu* (C đáp ứng), nhưng thực thi phần cứng
là chỗ **không được sai kiểu** nên giữ trong code có kiểm tra biên dịch (C đáp ứng). Quan trọng: C **đồng dạng** với
`QuickActionVm` sẵn có → không đẻ ra mô hình thứ hai lệch nhau. A bị loại vì rủi ro "chuỗi halCommand sai chỉ biết lúc chạy"
trên phần mềm điều khiển máy là không đáng. B bị loại vì mất tính data-driven cho guard/i18n mà tài liệu coi trọng.

## Hệ quả / đánh đổi còn lại
- **Nợ kỹ thuật có chủ đích:** `QuickActionVm` (Dashboard) và `RecoveryActionVm` (Motion) hiện là **hai lớp song song** cùng hình
  dạng. Chưa trích `GuardedActionVm` dùng chung phiên này để không làm mất ổn định Dashboard. *Việc tương lai:* hợp nhất thành một
  `GuardedActionVm` + helper `EvaluateEnablement(guard, risk, condition, hasHal)` ở nơi dùng chung.
- **Tín hiệu guard:** demo dùng `Safety.*` (đã publish từ §S62). Tín hiệu máy-riêng (`Vac.Ok`, `Z.AtWorkHeight`) cần WorkStation
  publish thêm — làm khi máy thật cần, không thuộc phiên này.
- **Override (§6.4)** là cơ chế *vượt* guard (đảo guard), KHÁC RecoveryActions (chạy *trong* guard) — vẫn chờ chốt chính sách §9(a).

## Liên kết
- Triển khai: Session 63 (xem `CHANGELOG.md`). Nền: [0001 §6, §7, §10](0001-am-autoframe-design-decisions.md).
