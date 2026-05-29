#!/usr/bin/env bash
# Hook chạy khi Claude (Stop) — kiểm tra xem status files đã được cập nhật chưa

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0
cd "$REPO_ROOT"

# Kiểm tra xem có uncommitted changes không
UNCOMMITTED=$(git status --porcelain 2>/dev/null | wc -l | tr -d ' ')

# Kiểm tra PROJECT_STATUS.md có được modified trong session này không
STATUS_MODIFIED=$(git status --porcelain PROJECT_STATUS.md CHANGELOG.md 2>/dev/null | wc -l | tr -d ' ')

if [ "$UNCOMMITTED" -gt 0 ] && [ "$STATUS_MODIFIED" -eq 0 ]; then
  echo ""
  echo "┌─────────────────────────────────────────────────────────┐"
  echo "│  ⚠️  NHẮC NHỞ: Session chưa được lưu lại!               │"
  echo "│                                                          │"
  echo "│  Có $UNCOMMITTED file thay đổi chưa commit.               │"
  echo "│  PROJECT_STATUS.md và CHANGELOG.md chưa được cập nhật.  │"
  echo "│                                                          │"
  echo "│  👉 Chạy: /am-done                                       │"
  echo "│     để cập nhật status + changelog + commit + push       │"
  echo "└─────────────────────────────────────────────────────────┘"
  echo ""
elif [ "$UNCOMMITTED" -gt 0 ]; then
  echo ""
  echo "⚠️  Còn $UNCOMMITTED file chưa commit. Chạy /am-done để commit."
  echo ""
fi

exit 0
