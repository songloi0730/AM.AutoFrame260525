#!/usr/bin/env bash
# -------------------------------------------------------
# Hook: check-session-end.sh
# Trigger: Stop — khi Claude kết thúc session
# Mục đích: Nhắc cập nhật PROJECT_STATUS + CHANGELOG + commit
# -------------------------------------------------------

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0
cd "$REPO_ROOT"

# Đếm uncommitted changes
UNCOMMITTED=$(git status --porcelain 2>/dev/null | grep -v '^??' | wc -l | tr -d ' ')
UNTRACKED=$(git status --porcelain 2>/dev/null | grep '^??' | wc -l | tr -d ' ')
TOTAL=$((UNCOMMITTED + UNTRACKED))

# Kiểm tra PROJECT_STATUS.md và CHANGELOG.md có được cập nhật chưa
STATUS_STAGED=$(git status --porcelain PROJECT_STATUS.md 2>/dev/null | wc -l | tr -d ' ')
CHANGELOG_STAGED=$(git status --porcelain CHANGELOG.md 2>/dev/null | wc -l | tr -d ' ')

# ── Không có gì thay đổi — im lặng ───────────────────────
if [ "$TOTAL" -eq 0 ]; then
  exit 0
fi

echo ""
echo "╔══════════════════════════════════════════════════╗"
echo "║        📋 AM.AutoFrame — SESSION SUMMARY          ║"
echo "╠══════════════════════════════════════════════════╣"

# Thống kê file thay đổi
echo "║                                                  ║"
printf "║  📝 Thay đổi chưa commit: %-5s file(s)          ║\n" "$TOTAL"

# Danh sách file thay đổi (tối đa 8)
git status --porcelain 2>/dev/null | head -8 | while read status file; do
  printf "║     %-2s %-44s ║\n" "$status" "$(basename "$file")"
done
EXTRA=$((TOTAL - 8))
[ "$EXTRA" -gt 0 ] && printf "║     ... và %d file khác%-28s ║\n" "$EXTRA" ""

echo "║                                                  ║"
echo "╠══════════════════════════════════════════════════╣"

# Trạng thái docs
if [ "$STATUS_STAGED" -gt 0 ] && [ "$CHANGELOG_STAGED" -gt 0 ]; then
  echo "║  ✅ PROJECT_STATUS.md  — đã cập nhật             ║"
  echo "║  ✅ CHANGELOG.md       — đã cập nhật             ║"
  DOCS_OK=1
else
  [ "$STATUS_STAGED" -eq 0 ] && \
    echo "║  ❌ PROJECT_STATUS.md  — CHƯA cập nhật           ║"
  [ "$CHANGELOG_STAGED" -eq 0 ] && \
    echo "║  ❌ CHANGELOG.md       — CHƯA cập nhật           ║"
  DOCS_OK=0
fi

echo "║                                                  ║"
echo "╠══════════════════════════════════════════════════╣"

# TODO nhắc nhở
if [ "$DOCS_OK" -eq 0 ]; then
  echo "║  👉 Chạy /am-done để hoàn tất session:           ║"
  echo "║     1. Cập nhật PROJECT_STATUS.md                ║"
  echo "║     2. Thêm entry vào CHANGELOG.md               ║"
  echo "║     3. bash scripts/am-commit.sh \"mô tả\"         ║"
else
  echo "║  👉 Commit & push:                               ║"
  echo "║     bash scripts/am-commit.sh \"loại: mô tả\"      ║"
fi

# Hiển thị top TODO từ PROJECT_STATUS.md nếu có
if [ -f "PROJECT_STATUS.md" ]; then
  echo "║                                                  ║"
  echo "║  📌 TODO cao nhất (từ PROJECT_STATUS.md):        ║"
  grep -E '^\| T[0-9].*🔴' PROJECT_STATUS.md 2>/dev/null | head -3 | while IFS='|' read _ num item prio note _; do
    ITEM=$(echo "$item" | sed 's/^[[:space:]]*//' | cut -c1-40)
    printf "║     %-44s ║\n" "$(echo "$num" | tr -d ' '): $ITEM"
  done
fi

echo "║                                                  ║"
echo "╚══════════════════════════════════════════════════╝"
echo ""
exit 0
