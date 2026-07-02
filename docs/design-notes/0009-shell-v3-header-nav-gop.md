# 0009 — Shell v3: gộp header+nav, banner co giãn, chip kết nối, kiosk config-driven

**Ngày:** 2026-07-02 (Session 73)
**Trạng thái:** Đã chốt, đã triển khai
**Liên quan:** `AM.Application.Shell/MainWindow.xaml(.cs)`, `ShellViewModel.cs`, ADR 0001,
`docs/HMI_UI_Architecture_Template_v2.md` (v2 — Shell không còn bám 100%, cần sync lên v3)

## Bối cảnh

Shell v2 (S45) dùng 7 vùng Persistent Frame với chrome dọc **284px**
(header 64 + nav 48 + banner 48 + action bar 84 + connection bar 40), nội dung chỉ còn ~796px (~74%)
ở 1080p. Chủ dự án nhận đề xuất Shell v3 (file MainWindow.xaml từ phiên thiết kế ngoài) gộp còn
4 vùng: header+nav 56 · banner co giãn 36→52 · content · action bar 64 gộp connection chip.
Nhiệm vụ: đánh giá và tích hợp có hiệu chỉnh.

## Đánh giá đề xuất v3

**Nhận (giữ nguyên ý tưởng):**
- Gộp header + nav thành 1 hàng 56px: tab RadioButton (hành vi loại trừ tự nhiên hơn Button tự quản màu).
- Alarm banner `Height=Auto` co giãn 36→52 qua DataTrigger — khi sạch chỉ tốn 36px, khi có alarm nút ACK vẫn ≥40px (spec §1.8); ghi chú điều hướng tự ẩn khi có alarm.
- Connection bar 40px → chip "● Thiết bị n/m · Host n/m" ở action bar + Popup 2 cột dùng chung 1 `ConnItemTemplate` (v2 lặp 2 DataTemplate); version xuống footer popup.
- Clock `MinWidth` chống xô layout; tab trong ScrollViewer ngang đề phòng thêm module / màn 1366×768.

**Sửa (4 vấn đề):**

| # | Vấn đề trong đề xuất | Hiệu chỉnh |
|---|---------------------|------------|
| 1 | Kiosk hardcode XAML (`WindowStyle=None` + `NoResize`) — dev/bảo trì bị nhốt ngay lần chạy đầu, màn Cài đặt (nơi dự kiến đặt nút thoát) chưa build | Config `AutoMachine:KioskMode` (mặc định false); bật trên IPC sản xuất. Ctrl+Shift+F11 (gate Engineer+, audit log) vào/thoát lúc chạy |
| 2 | Touch sizing vi phạm Master Index §2.9: nút lệnh máy 48px (chuẩn lệnh chính ≥64px), nút header/chip 40px (v2 đã chốt 44px) | Action bar 64→76px, lệnh máy (Init/Start/Pause/Stop/Reset) style `MachineActionButton` 64px; Dry run/Manual giữ 48px; HeaderButton/ConnChip 44px |
| 3 | Bug WPF kinh điển ToggleButton + Popup `StaysOpen=False`: bấm chip lần 2 → popup đóng trên mouse-down rồi click toggle mở lại ngay — không đóng được bằng chip | Guard timestamp ở code-behind: `Popup.Closed` ghi thời điểm; `ConnChip_Checked` trong 250ms sau đó → hủy mở lại |
| 4 | Popup dài vô hạn khi máy nhiều thiết bị | ScrollViewer `MaxHeight=460` quanh 2 cột |

## Phương án đã cân nhắc (kiosk)

- **A. Hardcode như đề xuất** — đơn giản, đúng IPC sản xuất; nhưng khoá luôn môi trường dev, vi phạm nguyên tắc config-driven (Master Index §2.10). ✗
- **B. Config + phím tắt gate role (CHỌN)** — `KioskMode` trong appsettings, Ctrl+Shift+F11 Engineer+; không cần UI mới, audit qua log. ✓
- **C. Nút thoát trong màn Cài đặt** — đúng vị trí lâu dài nhưng màn Cài đặt chưa tồn tại; sẽ bổ sung khi build Settings (giữ phím tắt làm lối thoát dự phòng). Hoãn.

## Hệ quả

- Chrome dọc 284 → **168px** (56+36+76): nội dung ~912px (~84%) ở 1080p, thêm ~116px cho work area.
- `ShellViewModel` thêm `DeviceOnlineText` / `HostOnlineText` / `AllConnectionsOk` — tính lại trong tick 1s cùng chỗ cập nhật chip.
- Code-behind nav chuyển `Button`→`RadioButton` (style `NavTabButton`, indicator gạch chân 3px); logic giữ-tab-khi-rebuild-theo-role giữ nguyên; KHÔNG set Foreground thủ công (kế thừa trigger).
- Tên máy không còn chữ trên header (chỉ logo + tooltip) — đánh đổi lấy chỗ cho 8 tab; nếu nhà máy nhiều máy giống nhau cạnh nhau cần nhận diện, cân nhắc thêm `MachineId` từ machine.json vào chip.
- **Nợ tài liệu:** `HMI_UI_Architecture_Template_v2.md` + Master Index §3 vẫn mô tả 7 vùng — cần bản v3 đồng bộ (TODO).
