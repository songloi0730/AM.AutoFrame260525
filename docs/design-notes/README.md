# Design Notes — AM.AutoFrame

Thư mục này lưu **quyết định thiết kế** của dự án theo phong cách **ADR** (Architecture Decision Record):
không chỉ ghi *làm gì*, mà ghi **các phương án đã cân nhắc + đánh đổi + vì sao chọn**. Mục đích:

- **Onboarding**: người mới đọc để hiểu *tư duy* phía sau code, không phải đoán.
- **Trí nhớ dự án**: 6 tháng sau vẫn biết "vì sao hồi đó chọn cách này, đã loại cách nào".
- **Dạy thiết kế**: mỗi note cho thấy một bài toán được mổ xẻ ra sao.

## Quy ước

- Mỗi note một file: `NNNN-tieu-de-ngan.md` (số tăng dần, 4 chữ số).
- Cấu trúc mỗi quyết định: **Bối cảnh → Các phương án (đặc điểm/đánh đổi) → Phương án chọn → Lý do → Hệ quả/đánh đổi còn lại**.
- Note bất biến sau khi viết; nếu đổi hướng → viết note MỚI "Superseded by NNNN" thay vì sửa lịch sử.
- Liên kết tới commit/session (`PROJECT_STATUS.md`, `CHANGELOG.md`) để truy vết.

> **Quy ước cho AI/automation** (xem `CLAUDE.md` mục "Design notes"): khi lập implementation plan cho task lớn
> (>30 phút / an toàn / kiến trúc), lưu plan + **các phương án đã cân nhắc** vào đây trước khi code.

## Index

| # | Tiêu đề | Nội dung |
|---|---------|----------|
| [0001](0001-am-autoframe-design-decisions.md) | Các lựa chọn thiết kế tổng thể | Giải thích mọi quyết định kiến trúc lớn của AM.AutoFrame (living doc) |
| [0002](0002-station-recovery-actions.md) | Thao tác trạm (RecoveryActions) | §6.3 — 3 phương án mô hình hoá action + vì sao chọn Hybrid |
| [0003](0003-supervised-override.md) | Supervised Override | §6.4 — xác nhận 1 người (2 bước+đếm ngược) + model riêng dùng chung registry |
| [0004](0004-quickactions-hal-hold-confirm.md) | QuickActions HAL + hold-to-confirm | §6.5 — wire DO trực tiếp + giữ-1s cho cửa R1 |
| [0005](0005-user-management.md) | Quản lý người dùng | §6.6 — mở rộng IUserService CRUD + bất biến last-admin |
| [0006](0006-vision-live-view.md) | Vision live-view | §6.7 — sim sinh frame + converter FrameData→BitmapSource (giữ R-UI) |
| [0007](0007-vision-module-design.md) | Thiết kế module Vision | Phản biện mockup HTML + SECPC(Cognex) → layering 4 tầng, adoption lấy/bỏ, Vision recipe model, mở rộng ILightController + roadmap V1–V5 |
| [0008](0008-vision-process-separation.md) | Tách process Vision (VisionPro FW4.8 + IPC) | VisionPro 9.x = .NET Framework; `.vpp`/CogSerializer crash native trên net9 (6 spike) → chạy VisionPro trong process **FW4.8 riêng**, trả `VisionResult` qua IPC; main net9 sạch Cognex |
| [0009](0009-shell-v3-header-nav-gop.md) | Shell v3 — gộp header+nav, chip kết nối, kiosk | Đánh giá đề xuất ngoài: nhận 4 vùng (chrome 284→168px), sửa 4 điểm — kiosk config-driven + Ctrl+Shift+F11, lệnh máy 64px, fix double-toggle Popup, ScrollViewer popup |
| [0010](0010-home-content-tinh-chinh-isa101.md) | Home v2.1 — tinh chỉnh nội dung ISA-101 | Phản biện 7 điểm: áp card "KQ gần nhất" + empty state + KPI màu-khi-có-nghĩa + Andon + divider Reset; từ chối thu rail 560→420 (giữ spec v2); 3 nguyên tắc cho template v3 |
| [0011](0011-sequencing-engine.md) | AM.Core.Sequencing — sequence engine khai báo | Loader 2 pha gom lỗi (station chết lúc nạp) · `IStationResolver` trên DryIoc keyed · pseudocode vòng lặp (order song song, linked-CTS timeout, onError/retry/prompt, pause ranh giới bước) · hành vi học từ RefSeq-A (resume-check, liệu sót, prompt thay popup) · hoãn giai đoạn 2 · bảng 14 anti-pattern |
| [0011](0011-sequencing-engine.md) | AM.Core.Sequencing — sequence engine khai báo | **Chờ duyệt.** Loader/validator 2 pha (lỗi lúc nạp vs lúc chạy), `IStationResolver` trên DryIoc keyed, pseudocode vòng lặp (retry/prompt/pause-gate), hành vi học từ RefSeq-A (resume-check, init liệu sót, `IOperatorPrompt`), 1 nguồn sự kiện → dashboard+log, bảng 14 anti-pattern → cách tránh |
| [0015](0015-chong-va-cham-phia-pc.md) | Chống va chạm phía PC (MoveGuard) | Luật "trục di chuyển → điều kiện cần" (trục/DI/zone) trong move-protection.json, kiểm ở lớp bọc `IMotionController` cho MỌI đường; giới hạn mềm TỪ CHỐI thay kẹp im lặng; jog giám sát nhìn trước 20 ms; bypass Admin ồn ào |
| [0016](0016-alarm-loi-goc-nguyen-nhan.md) | Alarm: lỗi gốc + Nguyên nhân/Cách xử lý | Cửa sổ 200 ms đánh dấu hệ quả (`RootCauseCode`); banner cùng mức GỐC trước; khung chi tiết màn Cảnh báo hiện tên/nguyên nhân/cách xử lý từ catalog; không làm: hoà catalog, "Reset nói lý do" (Reset không xoá alarm — để chủ dự án quyết) |
| [0017](0017-reset-xoa-alarm-het-nguyen-nhan.md) | Reset xoá alarm hết nguyên nhân | `IAlarmConditionSource` (an toàn, Force IO, phanh, luật va chạm, cấu hình) báo alarm nào còn nguyên nhân; Reset xoá phần còn lại, giữ phần còn kèm lý do + hỏi "Đã hiểu" trên banner; nguồn lỗi ⇒ giữ (fail-safe) |
