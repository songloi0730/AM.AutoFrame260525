# 0005 — Quản lý người dùng (Settings → Người dùng) — §6.6

**Bối cảnh.** Có login + RBAC (`IUserService`, users.json, BCrypt) nhưng chưa có UI quản trị: user seed cứng, không
thêm/xoá/đổi quyền/reset mật khẩu được. §6.6 (chốt chỉ thẻ "Người dùng") cho Administrator quản lý tài khoản.

## Phương án
- **A — Mở rộng `IUserService` thêm CRUD  ✅ CHỌN:** UserService đã sở hữu store + BCrypt + lock + migrate → thêm
  `GetUsers/CreateUser/DeleteUser/ResetPassword/SetLevel` + `Save()`. *+* một nguồn sự thật, tái dùng hạ tầng sẵn có.
  *−* interface to hơn (chấp nhận; vẫn là "quản lý phiên + tài khoản").
- **B — `IUserAdminService` riêng:** hai service đụng cùng file/lock → đồng bộ phức tạp, dễ lệch. Loại.
- **C — VM sửa users.json trực tiếp:** lặp BCrypt/lock/migrate trong UI, dễ sai + phá đóng gói. Loại.

## Quyết định an toàn (bất biến TRONG service, không phụ thuộc UI)
- KHÔNG xoá **Administrator cuối cùng**; KHÔNG hạ quyền Admin cuối cùng → tránh khoá mình ra khỏi hệ thống.
- KHÔNG xoá **user đang đăng nhập**.
- `GetUsers()` chỉ trả `username + level` (KHÔNG lộ hash).
- Gate UI = Administrator (CLAUDE.md); mọi mutation **audit** (OK/DENIED). Mật khẩu nhập qua `PasswordBox` + code-behind
  (không bind plaintext) — mẫu IdentityView login.

## Hệ quả
- `Save()` rút từ logic ghi của `SeedDefaults` (DRY). Mật khẩu hash `await Task.Run(BCrypt)` ngoài UI như `LoginAsync`.
- Hoãn các thẻ Settings khác (Hiệu chuẩn/Host phụ thuộc phần cứng/tích hợp; Sao lưu; Phần cứng trùng Chẩn đoán).

## Liên kết
- Triển khai: Session 66 (`CHANGELOG.md`). Nền: [0001 §2 phân quyền UserLevel](0001-am-autoframe-design-decisions.md).
