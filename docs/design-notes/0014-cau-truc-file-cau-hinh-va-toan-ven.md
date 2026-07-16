# 0014 — Cấu trúc file cấu hình: KHÔNG gộp + manifest SHA-256 + trang Thông số máy

**Trạng thái:** Chốt và thực hiện S93
**Liên quan:** design-notes/0012 (an toàn ưu tiên khả dụng, hành vi bất thường phải ỒN ÀO)

## Bối cảnh

Chủ dự án hỏi: app chưa có trang cấu hình thông số máy (tên máy, line, IP…); hiện có nhiều file
config rời rạc — có nên **gộp thành 1–2 file** và dùng **SHA-256 kiểm tra file có bị chỉnh sửa**?

Hiện trạng ~12 file: `appsettings.json`, `machine.json`, `axismap.json`, `io.map.json`,
`analog.map.json`, `recovery-actions.json`, `override-actions.json` (nhóm *cấu hình máy* — chỉnh khi
triển khai) và `points.json`, `parameters.json`, `users.json`, `calibration-history.json`, `recipes/`
(nhóm *dữ liệu vận hành* — app tự ghi liên tục).

## Các phương án đã cân nhắc

| Phương án | Đặc điểm | Đánh giá |
|---|---|---|
| A. Gộp tất cả vào 1–2 file | Ít file, dễ copy | **BÁC**: trộn file app-tự-ghi với file chỉnh-khi-deploy → app save đè tay sửa (mất thay đổi), hỏng 1 file mất TẤT CẢ, diff/backup/rollback từng phần bất khả, mỗi lần ghi points là "file cấu hình" đổi hash → kiểm toàn vẹn thành vô nghĩa |
| B. **Giữ nguyên phân file theo vòng đời + manifest SHA-256 + trang cấu hình (CHỌN)** | File nào giữ vai trò đó; nhóm cấu-hình-máy được ký `config.manifest.json`; sửa qua UI có audit + tự ký lại | Toàn vẹn có ý nghĩa (nhóm ký là nhóm ít đổi), sửa hợp lệ không báo giả, thao tác thường ngày không đụng |
| C. Gộp riêng nhóm machine-definition (machine+axismap+io.map+analog.map) thành một `machine.json` lớn | 4 file → 1 | Hoãn P5: đổi schema đụng 4 loader + template máy mới; lợi ích chưa đủ so với rủi ro lúc này |

## Quyết định (B)

1. **Trang "Thông số máy"** (Cài đặt → thẻ mới, Administrator):
   - Nhận diện máy: tên máy / line / vị trí → `machine.json` (thêm 2 trường `line`, `location`;
     GIỮ NGUYÊN phần `stations` khi ghi). Mã máy (day-code) chỉ hiển thị.
   - Kết nối thiết bị: UseSimulation + IP/cổng Modbus, PLC, Robot, Scanner, ADAM, OPC-UA,
     EtherNet/IP → ghi thẳng `appsettings.json` (chỉ set key màn này quản, phần còn lại giữ nguyên);
     banner "cần khởi động lại" vì DI đọc config lúc boot.
   - Mỗi lần lưu: audit `Machine.SaveConfig` + **tự ký lại manifest**.
2. **`IConfigIntegrityService`**: SHA-256 từng file nhóm cấu-hình-máy vào `config.manifest.json`
   (kèm ai ký/lúc nào). Boot đối chiếu → lệch = **alarm 40013** (phát hiện, KHÔNG chặn máy chạy —
   0012). Bảng trạng thái (Khớp/ĐÃ SỬA/MẤT FILE/Chưa ký) + nút **"Ký lại"** (Administrator, audit).
3. Giới hạn trung thực: manifest thường (không HMAC) là **tamper-evident với thao tác thường**,
   không chống được kẻ sửa cả manifest — đủ cho mục tiêu "biết file có bị chỉnh tay không";
   nếu cần chống giả mạo thật sự (P5) thì HMAC bằng DayCodeSecret.

## Hệ quả

- Backup targets thêm `config.manifest.json`.
- Máy chưa từng ký → mọi file "Chưa ký" (vàng), không alarm — admin ký lần đầu sau khi setup.
- Restore backup từ bản cũ sẽ báo lệch (đúng kỳ vọng — restore là thay đổi cấu hình thật).
