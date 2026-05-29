#!/usr/bin/env bash
# -------------------------------------------------------
# File:    am-commit.sh
# Purpose: Git add + commit + push với workaround cho
#          Windows filesystem index.lock issue
# Usage:   bash scripts/am-commit.sh "commit message"
# -------------------------------------------------------

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || {
  echo "❌ Không tìm thấy git repo. Chạy từ trong thư mục dự án."
  exit 1
}

cd "$REPO_ROOT"

COMMIT_MSG="${1:-"chore: auto-update PROJECT_STATUS + CHANGELOG"}"

# ── Fix index.lock nếu bị kẹt ─────────────────────────────
LOCK_FILE="$REPO_ROOT/.git/index.lock"
if [ -f "$LOCK_FILE" ]; then
  echo "⚠️  index.lock tồn tại — đang xử lý..."
  # Thử move thay vì xoá (Windows filesystem không cho phép unlink)
  mv "$LOCK_FILE" "$LOCK_FILE.bak" 2>/dev/null && echo "  → Đã move index.lock → index.lock.bak" || {
    echo "  → Không thể xử lý index.lock. Đóng Visual Studio / VS Code rồi thử lại."
    exit 1
  }
fi

# ── Kiểm tra có gì thay đổi không ────────────────────────
if git diff --quiet && git diff --staged --quiet && [ -z "$(git ls-files --others --exclude-standard)" ]; then
  echo "✅ Không có thay đổi nào để commit."
  exit 0
fi

# ── Git add tất cả ────────────────────────────────────────
echo "📦 Staging tất cả thay đổi..."
git add -A 2>/dev/null || true

# Fix lock lần 2 nếu git add tạo ra lock mới
if [ -f "$LOCK_FILE" ]; then
  mv "$LOCK_FILE" "$LOCK_FILE.bak2" 2>/dev/null || true
fi

# ── Commit ────────────────────────────────────────────────
echo "💾 Committing: $COMMIT_MSG"
git commit -m "$COMMIT_MSG" 2>/dev/null || {
  # Thử một lần nữa sau khi fix lock
  if [ -f "$LOCK_FILE" ]; then mv "$LOCK_FILE" "$LOCK_FILE.bak3" 2>/dev/null || true; fi
  git commit -m "$COMMIT_MSG"
}

# ── Push ──────────────────────────────────────────────────
echo "🚀 Pushing lên remote..."
if git push origin "$(git branch --show-current)" 2>/dev/null; then
  echo "✅ Push thành công!"
else
  echo "⚠️  Push thất bại (có thể do proxy/network trong sandbox)."
  echo "   Chạy thủ công: git push origin $(git branch --show-current)"
fi

echo ""
echo "📊 Commit mới nhất:"
git log --oneline -1
