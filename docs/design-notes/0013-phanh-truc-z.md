# 0013 — Phanh trục Z: nhả để chỉnh tay (Gói D)

**Trạng thái:** Đã chốt với chủ dự án (S90), thực hiện S92
**Liên quan:** `HMI_Manual_Operation_and_Safety_v1.0.md` (mức rủi ro R2–R3), design-notes/0012 (an toàn ưu tiên khả dụng, hành vi nguy hiểm phải ỒN ÀO)

## Bối cảnh

Máy có trục Z mang tải trọng lực (đầu hút/tool). Khi setup, kỹ thuật viên cần **nhả phanh cơ khí
để đẩy trục Z bằng tay** về vị trí mong muốn (nhanh và trực quan hơn jog). Nhả phanh khi servo off
= trục có thể **rơi tự do** → đè tay, vỡ tool, hỏng sản phẩm. Màn máy tham khảo có nút phanh Z
ngay trên màn vận hành tay.

## Các phương án đã cân nhắc

| Phương án | Đặc điểm | Đánh giá |
|---|---|---|
| A. Giữ-để-nhả (deadman như jog) | Nhả chỉ khi đang GIỮ nút; buông là đóng | An toàn nhất trên giấy, nhưng **bất khả thi thao tác**: chỉnh Z bằng tay cần CẢ HAI tay đỡ/đẩy trục — "luôn phải có một người giữ nút phanh" (chủ dự án bác) |
| B. Toggle, quyền SuperUser | Bật/tắt tự do nhưng khóa ở quyền cao nhất | Quyền **quá cao so với thực tế**: người chỉnh máy hằng ngày là Engineer (chủ dự án bác) |
| C. **Toggle + confirm 2 bước ở Engineer + banner đỏ + tự đóng (CHỌN)** | Nhả = 2 bước có cảnh báo rơi tự do (Engineer, máy dừng — guard R2); đang nhả = **banner đỏ thường trực** (alarm 10009 — cả app thấy); **tự đóng** khi rời màn Vận hành tay / đổi user / rớt quyền; đóng = 1 chạm không cần quyền | Khớp thực tế thao tác 2 tay; trạng thái nguy hiểm không thể im lặng và không thể bị bỏ quên |

## Quyết định

Phương án C, cụ thể:
- `IAxisBrake` — capability tuỳ chọn của motion controller (như `IAxisJog`): controller không có phanh → UI ẩn hẳn khối phanh.
- Nhả: bước 1 kiểm guard R2 (Engineer + máy không chạy) → bước 2 xác nhận cảnh báo "trục có thể rơi tự do" → nhả + **alarm 10009** + audit `Brake.Release Z`.
- Đang nhả: dải đỏ trong màn Vận hành tay + banner alarm toàn app (mẫu forced-IO).
- Đóng: 1 chạm, **không cần quyền** (về trạng thái an toàn luôn được phép); audit kèm lý do (nút / tự đóng rời màn / tự đóng đổi user).
- Bất biến: rời màn Vận hành tay (`Unloaded`) hoặc user tụt dưới Engineer → phanh tự đóng.

## Hệ quả

- Trục Z demo cứng `ZAxisIndex = 2` (convention XYZU) — máy thật đưa vào `machine.json` (P5).
- Driver thật (GTS/Advantech) chưa implement `IAxisBrake` — khi làm P5 map vào DO phanh hoặc lệnh servo.
- Nếu đóng phanh THẤT BẠI (lỗi hardware), alarm 10009 giữ nguyên — còn nhả còn banner.
