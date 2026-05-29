#!/usr/bin/env bash
# -------------------------------------------------------
# Hook: post-write-cs.sh
# Trigger: PostToolUse — Write / Edit
# Mục đích: Kiểm tra file .cs vừa tạo/sửa theo rules AM.AutoFrame
#   1. CA1707  — underscore trong tên Step class
#   2. File header — phải có comment block chuẩn
#   3. CA2000  — CancellationTokenSource không có 'using var'
#   4. RSPEC-6602/6605 — LINQ FirstOrDefault/Any thay vì List.Find/Exists
#   5. Architecture — không inject concrete class trong Service/WorkStation
# -------------------------------------------------------

# Đọc JSON từ stdin (Claude Code truyền vào)
INPUT=$(cat)

# Lấy file path từ JSON input
FILE_PATH=$(echo "$INPUT" | python3 -c "
import sys, json
d = json.load(sys.stdin)
# PostToolUse: tool_input chứa file_path
ti = d.get('tool_input', {})
print(ti.get('file_path', ti.get('path', '')))
" 2>/dev/null)

# Chỉ xử lý file .cs
[[ "$FILE_PATH" != *.cs ]] && exit 0
[ ! -f "$FILE_PATH" ] && exit 0

ISSUES=()
WARNINGS=()

# ── 1. CA1707: Step class tên có underscore ───────────────
# Pattern: class Step\d\d_\w (ví dụ Step01_Initialize)
if grep -qP 'class\s+Step\d{2}_\w' "$FILE_PATH" 2>/dev/null; then
  ISSUES+=("CA1707  Tên Step có underscore → đổi thành Step01Initialize (không có _)")
fi

# ── 2. File header ────────────────────────────────────────
# Phải có comment block // File: hoặc // -------
if ! head -5 "$FILE_PATH" | grep -qE '//[-]{3,}|// File:|// Purpose:'; then
  WARNINGS+=("HEADER  Thiếu file header comment (// --- / File: / Purpose:)")
fi

# ── 3. CA2000: CancellationTokenSource không có 'using' ──
# Tìm 'new CancellationTokenSource' không có 'using' ở trước
if grep -qP '(?<!using var\s{0,20})(?<!using\s)CancellationTokenSource\.CreateLinkedTokenSource' "$FILE_PATH" 2>/dev/null; then
  # Kiểm tra chắc chắn hơn: có CreateLinkedTokenSource mà không có 'using var' trên cùng dòng
  while IFS= read -r line; do
    if echo "$line" | grep -q 'CreateLinkedTokenSource' && ! echo "$line" | grep -q 'using var'; then
      ISSUES+=("CA2000  CreateLinkedTokenSource thiếu 'using var' (memory leak CancellationToken)")
      break
    fi
  done < <(grep -n 'CreateLinkedTokenSource' "$FILE_PATH" 2>/dev/null)
fi

# ── 4. RSPEC-6602: LINQ FirstOrDefault trên List ─────────
if grep -qP '\.\s*FirstOrDefault\s*\(' "$FILE_PATH" 2>/dev/null; then
  ISSUES+=("RSPEC-6602  .FirstOrDefault() → dùng List<T>.Find() thay thế")
fi

# ── 5. RSPEC-6605: LINQ Any trên List ────────────────────
if grep -qP '\.\s*Any\s*\(' "$FILE_PATH" 2>/dev/null; then
  # Loại bỏ false positive khi dùng IEnumerable (không phải List)
  if grep -qP '_\w+\s*\.\s*Any\s*\(|List<.*>\s+\w+.*\.\s*Any\(' "$FILE_PATH" 2>/dev/null; then
    WARNINGS+=("RSPEC-6605  .Any() trên List → cân nhắc dùng List<T>.Exists() thay thế")
  fi
fi

# ── 6. CA1031: catch Exception không có pragma ────────────
if grep -q 'catch (Exception' "$FILE_PATH" 2>/dev/null; then
  if ! grep -q 'pragma warning disable CA1031' "$FILE_PATH" 2>/dev/null; then
    ISSUES+=("CA1031  catch (Exception) thiếu #pragma warning disable CA1031 với justification")
  fi
fi

# ── 7. Architecture: concrete hardware trong Services ────
FILENAME=$(basename "$FILE_PATH")
DIR_PATH=$(dirname "$FILE_PATH")
if echo "$DIR_PATH" | grep -qE 'AM\.Services|AM\.WorkStation'; then
  if grep -qE 'new\s+(Simulated|Ltdmc|Cognex|Keyence)\w+\s*\(' "$FILE_PATH" 2>/dev/null; then
    ISSUES+=("ARCH  Service/WorkStation không được new concrete hardware class — dùng interface qua DI")
  fi
fi

# ── 8. XML doc thiếu trên public member ──────────────────
PUBLIC_COUNT=$(grep -cP '^\s+public\s+((?!override|class|sealed|abstract|static|readonly)\w+\s+)+\w+\s*[\(\{<]' "$FILE_PATH" 2>/dev/null || echo 0)
XMLDOC_COUNT=$(grep -c '///' "$FILE_PATH" 2>/dev/null || echo 0)
if [ "$PUBLIC_COUNT" -gt 2 ] && [ "$XMLDOC_COUNT" -eq 0 ]; then
  WARNINGS+=("XMLDOC  File có $PUBLIC_COUNT public members nhưng không có /// XML doc comment")
fi

# ── Output ────────────────────────────────────────────────
if [ ${#ISSUES[@]} -eq 0 ] && [ ${#WARNINGS[@]} -eq 0 ]; then
  echo "✅ $(basename "$FILE_PATH") — OK (AM.AutoFrame checks passed)"
  exit 0
fi

echo ""
echo "🔍 AM.AutoFrame check: $(basename "$FILE_PATH")"
echo "─────────────────────────────────────────────"

for issue in "${ISSUES[@]}"; do
  echo "  ❌ $issue"
done

for warn in "${WARNINGS[@]}"; do
  echo "  ⚠️  $warn"
done

ISSUE_COUNT=${#ISSUES[@]}
WARN_COUNT=${#WARNINGS[@]}
echo "─────────────────────────────────────────────"
echo "  $ISSUE_COUNT lỗi build | $WARN_COUNT cảnh báo"
echo ""

# Exit 0 để không block — chỉ thông báo cho Claude
exit 0
