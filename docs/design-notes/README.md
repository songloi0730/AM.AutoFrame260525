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
| [0011](0011-sequencing-engine.md) | AM.Core.Sequencing — sequence engine khai báo | **Chờ duyệt.** Loader/validator 2 pha (lỗi lúc nạp vs lúc chạy), `IStationResolver` trên DryIoc keyed, pseudocode vòng lặp (retry/prompt/pause-gate), hành vi học từ RefSeq-A (resume-check, init liệu sót, `IOperatorPrompt`), 1 nguồn sự kiện → dashboard+log, bảng 14 anti-pattern → cách tránh |
