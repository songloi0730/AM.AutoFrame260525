# /am-done — Kết thúc session: cập nhật status + commit + push

Chạy workflow kết thúc session:
1. Cập nhật PROJECT_STATUS.md
2. Thêm entry mới vào CHANGELOG.md
3. Commit tất cả thay đổi
4. Push lên GitHub

---

## Bước 1 — Thu thập thông tin session

Trước khi cập nhật file, hãy xem lại:

```bash
# Xem những file nào đã thay đổi trong session này
git diff --name-only HEAD
git status --short
```

Dựa vào danh sách file thay đổi, Claude sẽ tự tổng hợp nội dung cho status + changelog.

---

## Bước 2 — Cập nhật PROJECT_STATUS.md

Mở `PROJECT_STATUS.md` và cập nhật:

1. **`Cập nhật lần cuối`** — đổi ngày + mô tả session
2. **`Commit`** — sẽ điền sau khi commit xong (dùng placeholder `TBD`)
3. **Bảng `Trạng thái tổng quan`** — đổi trạng thái các hạng mục vừa hoàn thành
4. **Bảng `Known Issues & TODO`** — xoá items đã làm xong, thêm items mới phát hiện
5. **Bảng `Key files`** — thêm file mới nếu có

**Nguyên tắc cập nhật bảng trạng thái:**
- `✅ Hoàn thành` — code hoạt động, không có known issues
- `⚠️ Cần sửa` / `⚠️ Skeleton` — có nhưng chưa đầy đủ, ghi chú cụ thể
- `❌ Chưa có` — chưa tạo

---

## Bước 3 — Thêm entry vào CHANGELOG.md

Thêm block mới VÀO ĐẦU FILE (sau header), theo template:

```markdown
## [Session N] YYYY-MM-DD — Tiêu đề ngắn mô tả session

**Commit:** `TBD` ← điền sau khi commit
**Người thực hiện:** Claude (Cowork) + Nhan

### ✅ Thêm mới
- `Path/File.cs` — Mô tả ngắn gọn chức năng

### 🔧 Sửa đổi
- `Path/File.cs` — Thay đổi gì, lý do gì

### 🐛 Bugs đã fix
- CAXXX: Mô tả fix

### 🔧 Quyết định kiến trúc (nếu có)
1. **Vấn đề** → Giải pháp và lý do chọn
```

**Quy tắc viết entry:**
- Tiêu đề ngắn, súc tích — đọc là biết làm gì
- Mỗi file thay đổi quan trọng = 1 dòng, ghi rõ nội dung
- Bugs fix: ghi cả rule code (CA1707, RSPEC-6602...) để tra cứu sau
- Quyết định kiến trúc: giải thích TẠI SAO chọn giải pháp đó

---

## Bước 4 — Commit + Push

```bash
# Cách 1: Dùng script tự động (xử lý lock file)
bash scripts/am-commit.sh "feat/fix/chore: mô tả commit"

# Cách 2: Thủ công nếu script fail
cd /path/to/AM.AutoFrame
mv .git/index.lock .git/index.lock.bak 2>/dev/null || true
git add -A
mv .git/index.lock .git/index.lock.bak2 2>/dev/null || true
git commit -m "commit message"
git push origin main
```

**Commit message format:**
```
feat:    thêm tính năng mới
fix:     sửa bug
refactor: cải thiện code không đổi behavior
docs:    cập nhật tài liệu (CLAUDE.md, CHANGELOG, STATUS)
chore:   cấu hình, CI, không ảnh hưởng code logic
test:    thêm/sửa tests
```

Sau khi commit xong, quay lại PROJECT_STATUS.md và CHANGELOG.md để điền hash commit thật vào chỗ `TBD`.

---

## Bước 5 — Xác nhận hoàn thành

Báo cáo cho user:
```
✅ Session hoàn thành!

📝 Đã cập nhật:
  - PROJECT_STATUS.md (trạng thái, TODO)
  - CHANGELOG.md (Session N — [tóm tắt ngắn])

💾 Commit: [hash] — [message]
🚀 Push: [thành công / cần push thủ công: git push origin main]

📋 Việc cần làm tiếp (từ TODO list):
  - T1: [todo cao nhất chưa làm]
  - T2: ...
```
