#!/usr/bin/env bash
# -------------------------------------------------------
# Hook: post-build.sh
# Trigger: PostToolUse — Bash (dotnet build / test / run)
# Mục đích: Parse build output, tóm tắt lỗi CA/Sonar
# -------------------------------------------------------

INPUT=$(cat)

# Lấy command và output
TOOL_CMD=$(echo "$INPUT" | python3 -c "
import sys, json
try:
    d = json.load(sys.stdin)
    print(d.get('tool_input', {}).get('command', ''))
except: pass
" 2>/dev/null)

# Chỉ xử lý dotnet build/test/run
echo "$TOOL_CMD" | grep -qE 'dotnet (build|test|run)' || exit 0

TOOL_OUTPUT=$(echo "$INPUT" | python3 -c "
import sys, json
try:
    d = json.load(sys.stdin)
    r = d.get('tool_response', {})
    if isinstance(r, str): print(r)
    elif isinstance(r, dict):
        print(r.get('output', r.get('content', r.get('stdout', ''))))
except: pass
" 2>/dev/null)

# Đếm
ERROR_LINES=$(echo "$TOOL_OUTPUT" | grep -E ': error (CA|CS|S[0-9]|RSPEC)' || true)
WARN_LINES=$(echo "$TOOL_OUTPUT"  | grep -E ': warning (CA|CS|S[0-9]|RSPEC)' || true)
ERROR_COUNT=$(echo "$ERROR_LINES" | grep -c "[^[:space:]]" 2>/dev/null; true); ERROR_COUNT=${ERROR_COUNT:-0}
WARN_COUNT=$(echo "$WARN_LINES" | grep -c "[^[:space:]]" 2>/dev/null); [ -z "$WARN_COUNT" ] && WARN_COUNT=0
BUILD_FAILED=$(echo "$TOOL_OUTPUT" | grep -c "Build FAILED" 2>/dev/null); [ -z "$BUILD_FAILED" ] && BUILD_FAILED=0
BUILD_OK=$(echo "$TOOL_OUTPUT" | grep -c "Build succeeded" 2>/dev/null); [ -z "$BUILD_OK" ] && BUILD_OK=0

# Build sạch — im lặng
if [ "$BUILD_OK" -gt 0 ] && [ "$ERROR_COUNT" -eq 0 ] && [ "$WARN_COUNT" -eq 0 ]; then
  echo "✅ Build succeeded — không có CA/Sonar violations"
  exit 0
fi

echo ""
echo "🔨 BUILD SUMMARY"
echo "══════════════════════════════════════════════════"

if [ "$BUILD_FAILED" -gt 0 ]; then
  echo "  ❌ BUILD FAILED  ($ERROR_COUNT errors, $WARN_COUNT warnings)"
else
  echo "  ✅ Build succeeded  ($WARN_COUNT warnings)"
fi
echo ""

# Nhóm theo rule code
ALL_VIOLATIONS=$(echo -e "$ERROR_LINES\n$WARN_LINES" | grep -oE '(error|warning) (CA[0-9]+|CS[0-9]+|S[0-9]+|RSPEC-[0-9]+)' | sort | uniq -c | sort -rn)
if [ -n "$ALL_VIOLATIONS" ]; then
  echo "  📋 Violations theo rule:"
  echo "$ALL_VIOLATIONS" | while read count sev rule; do
    ICON="⚠️ "; [ "$sev" = "error" ] && ICON="❌"
    printf "    %s %-14s × %s\n" "$ICON" "$rule" "$count"
  done
  echo ""
fi

# Files có lỗi
FILES=$(echo -e "$ERROR_LINES\n$WARN_LINES" | grep -oP '[A-Za-z0-9._]+\.cs' | sort -u)
if [ -n "$FILES" ]; then
  echo "  📁 Files:"
  echo "$FILES" | while read f; do printf "    → %s\n" "$f"; done
  echo ""
fi

# Top 5 lỗi cụ thể để fix ngay
if [ -n "$ERROR_LINES" ]; then
  echo "  🔍 Errors cần fix:"
  echo "$ERROR_LINES" | head -5 | while read line; do
    printf "    %s\n" "$line"
  done
fi

echo "══════════════════════════════════════════════════"
echo ""
exit 0
