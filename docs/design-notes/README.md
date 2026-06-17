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
