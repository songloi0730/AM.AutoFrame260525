# 0012 — Chính sách bảo mật đăng nhập cho môi trường nhà máy (P3.1)

**Ngày:** 2026-07-07 (Session 82) · **Trạng thái:** ĐÃ CHỐT với chủ dự án · **Thay thế:** DoD gốc của P3.1 trong `ROADMAP_HOAN_THIEN.md`

## Bối cảnh

Roadmap P3.1 gốc đề xuất theo chuẩn web/IT: lockout sau 5 lần sai (khoá 5 phút) + `MustChangePassword` ép đổi
mật khẩu lần đầu. Chủ dự án phản biện dựa trên thực tế nhà máy:

1. **Tài khoản dùng chung theo vai** — nhiều người biết chung mật khẩu; kỹ sư vận hành nhiều máy;
   một người đổi mật khẩu thì các ca khác không nắm được thông tin đổi.
2. **Lockout gây downtime** — bị khoá đúng lúc hỏng máy thì không vào chỉnh được; nhà máy không có
   cơ chế khôi phục online (email/OTP) như web.
3. **Mất quyền admin vĩnh viễn** — người cấp cao đổi mật khẩu admin rồi rời đi (về nước) → không ai
   vào được quyền đó nữa. Cần break-glass: mật khẩu theo thời gian hoặc file đổi tên để vượt qua.

Mô hình đe doạ thật của IPC trong xưởng: kẻ tấn công phải **đứng ngay tại máy** (trust boundary = cửa xưởng),
không có brute-force từ xa. Rủi ro lớn nhất của cơ chế khoá không phải bảo mật mà là **downtime lúc 2h sáng**.

## Các phương án đã cân nhắc

### Q1 — Chống dò mật khẩu (thay lockout)
| Phương án | Ưu | Nhược |
|---|---|---|
| A. Delay tăng dần (1s→2s→5s, trần 10s) + audit | Chống vét cạn, không bao giờ khoá | Người thật vẫn vướng vài giây lúc sự cố |
| B. Lockout ngắn 1 phút | Chặn vét cạn mạnh | Vẫn có thể vướng 1 phút đúng lúc sự cố |
| **C. Chỉ audit + alarm, không chặn** ✅ | **Zero rủi ro downtime**; ca trưởng vẫn được báo khi có người dò | Vét cạn không bị cản (chấp nhận: phải đứng tại máy, gõ tay; alarm nổi lên ngay) |

### Q2 — Break-glass khôi phục quyền Admin
Phát hiện khi khảo sát: cơ chế "đổi tên file" **đã tồn tại de-facto** — xoá/đổi tên `users.json` → app re-seed
mặc định (`admin/admin123`, P0.3 có backup). Nhược: mất toàn bộ user đã tạo, phải khôi phục tay.

| Phương án | Ưu | Nhược |
|---|---|---|
| A. Chỉ day-code | Hết hạn theo ngày; 1 tool dùng mọi máy (khớp mô hình kỹ sư đa máy) | Mất tool = chỉ còn đường xoá users.json |
| B. Chỉ file recovery | Không cần tool; giữ nguyên danh sách user | Ai đọc docs đều biết cách (vẫn cần đứng tại máy) |
| **C. Cả hai** ✅ | Day-code chính (kỹ sư hãng), file dự phòng (mất cả tool) | Nhiều code hơn (~0.5 phiên) |
| D. Giữ nguyên (xoá users.json) | Không code thêm | Mất danh sách user; mật khẩu mặc định ai cũng biết |

### Q3 — Mật khẩu mặc định của tài khoản seed
| Phương án | Ưu | Nhược |
|---|---|---|
| **A. Banner cảnh báo thường trực, không ép đổi** ✅ | Hợp tài khoản dùng chung; quản lý xưởng tự quyết | Máy có thể chạy lâu dài với mật khẩu mặc định (đã có banner nhắc) |
| B. Ép đổi riêng admin | Bảo vệ quyền cao nhất | Vẫn dính vấn đề "người đổi không báo ca khác" ở chính admin |
| C. Ép đổi tất cả (roadmap gốc) | Chuẩn IT | Đúng rủi ro chủ dự án nêu — loại |

## Phương án chọn (đã chốt qua AskUserQuestion, S82)

1. **Q1 = C**: KHÔNG lockout, KHÔNG delay. Mỗi lần đăng nhập sai → audit log; sai ≥5 lần liên tiếp
   (đếm theo username, reset khi đúng) → alarm nhẹ 40010 "Nhiều lần đăng nhập sai" để ca trưởng biết.
2. **Q2 = C (cả hai)**, nguyên tắc bù cho backdoor có chủ đích là **phải ồn ào — vào được nhưng không vào lén được**:
   - **Day-code**: đăng nhập user `service` + mã 8 số = `HMAC-SHA256(secret, machineId + yyyyMMdd) mod 10^8`.
     Chấp nhận ±1 ngày (lệch đồng hồ/ca đêm). Phiên = SuperUser. Secret + machineId nằm trong config
     triển khai (repo chỉ giữ placeholder); tool sinh mã: `scripts/am-daycode.ps1` (nhận secret làm tham số —
     secret KHÔNG commit). Đăng nhập thành công → alarm 40011 + audit.
   - **File recovery**: đặt file `am-recovery.key` cạnh exe → lúc boot app **xoá file ngay** (một lần dùng),
     mở cửa sổ 30 phút cho phép đăng nhập `recovery` (mật khẩu cố định `recovery`) = Administrator tạm.
     Kích hoạt → alarm 40012 + audit. KHÔNG đụng users.json — danh sách user giữ nguyên.
   - Rủi ro chấp nhận: secret nằm trong binary/config có thể bị dịch ngược; file recovery ai đọc docs cũng
     biết — cả hai đều yêu cầu tiếp cận vật lý IPC, cùng mức tin cậy với việc xoá users.json vốn đã tồn tại.
3. **Q3 = A**: không ép đổi; `IUserService` expose "còn tài khoản dùng mật khẩu mặc định" → banner vàng
   thường trực trên Shell (tắt khi đổi hết). MinLength giữ lại (config `Security:MinPasswordLength`, mặc định 8)
   áp khi tạo user/đổi mật khẩu — không thể gây downtime nên không cần nới.

## Hệ quả

- DoD P3.1 trong roadmap được viết lại theo note này (bỏ lockout + MustChangePassword).
- 2 đường break-glass đều là backdoor **được thiết kế và audit** — thay cho backdoor vô tình (xoá users.json).
  Vẫn giữ nguyên hành vi re-seed (là lớp cuối cùng khi mất cả tool lẫn docs).
- Alarm codes mới: 40010 (nhiều lần sai), 40011 (đăng nhập day-code), 40012 (kích hoạt file recovery).
- P3.2 (auto-logout + audit UI) không bị ảnh hưởng bởi note này.
