#!/usr/bin/env bash
# -------------------------------------------------------
# Hook: pre-write-arch.sh
# Trigger: PreToolUse — Write
# Mục đích: Kiểm tra file sắp được tạo đúng layer không
#   - Interface → phải ở AM.Core.Abstractions
#   - Hardware driver → phải ở AM.Hardware.*
#   - Service impl   → phải ở AM.Services
#   - Step/Mechanism/Station → phải ở AM.WorkStation.*
# -------------------------------------------------------

INPUT=$(cat)

FILE_PATH=$(echo "$INPUT" | python3 -c "
import sys, json
d = json.load(sys.stdin)
print(d.get('tool_input', {}).get('file_path', ''))
" 2>/dev/null)

CONTENT=$(echo "$INPUT" | python3 -c "
import sys, json
d = json.load(sys.stdin)
print(d.get('tool_input', {}).get('content', ''))
" 2>/dev/null)

# Chỉ xử lý file .cs
[[ "$FILE_PATH" != *.cs ]] && exit 0

FILENAME=$(basename "$FILE_PATH" .cs)
VIOLATIONS=()

# ── Rule 1: Interface phải ở AM.Core.Abstractions ─────────
if echo "$CONTENT" | grep -qP '^\s*public interface I\w+'; then
  if ! echo "$FILE_PATH" | grep -q 'AM.Core.Abstractions'; then
    VIOLATIONS+=("Interface '$FILENAME' phải đặt trong AM.Core.Abstractions/Interfaces/ (không phải $(dirname "$FILE_PATH" | xargs basename))")
  fi
fi

# ── Rule 2: Hardware concrete class phải ở AM.Hardware.* ──
if echo "$CONTENT" | grep -qP 'IMotionController|ICameraDevice|IIoModule'; then
  if echo "$FILE_PATH" | grep -qE 'AM\.Services|AM\.WorkStation|AM\.Core(?!\.Abstractions)'; then
    # Chỉ cảnh báo nếu là implementation (không phải interface hoặc reference qua DI)
    if echo "$CONTENT" | grep -qP 'class\s+\w+\s*:\s*(IMotionController|ICameraDevice|IIoModule)'; then
      VIOLATIONS+=("Hardware driver '$FILENAME' phải đặt trong AM.Hardware.{Motion|Vision|IO}/ không phải $(dirname "$FILE_PATH" | xargs basename)")
    fi
  fi
fi

# ── Rule 3: Concrete hardware trong constructor Service ───
if echo "$FILE_PATH" | grep -q 'AM.Services'; then
  if echo "$CONTENT" | grep -qP 'new\s+(Simulated\w+|Ltdmc\w+|Cognex\w+)\s*\('; then
    VIOLATIONS+=("AM.Services không được 'new' concrete hardware — inject qua IHardwareManagerService")
  fi
fi

# ── Rule 4: BaseMechanism/StationBase dùng sai project ───
if echo "$CONTENT" | grep -qP 'class\s+\w+\s*:\s*BaseMechanism'; then
  if ! echo "$FILE_PATH" | grep -q 'AM.WorkStation'; then
    VIOLATIONS+=("Mechanism '$FILENAME' phải đặt trong AM.WorkStation.{MachineName}/Mechanisms/")
  fi
fi

if echo "$CONTENT" | grep -qP 'class\s+\w+(Station|StationBase)'; then
  if echo "$CONTENT" | grep -qP ':\s*StationBase<'; then
    if ! echo "$FILE_PATH" | grep -q 'AM.WorkStation'; then
      VIOLATIONS+=("Station '$FILENAME' phải đặt trong AM.WorkStation.{MachineName}/Stations/")
    fi
  fi
fi

# ── Rule 5: Step naming convention ───────────────────────
if echo "$CONTENT" | grep -qP 'class\s+Step\d{2}_'; then
  VIOLATIONS+=("CA1707 Tên Step có underscore — đổi thành Step01Initialize không phải Step01_Initialize")
fi

# ── Output ────────────────────────────────────────────────
if [ ${#VIOLATIONS[@]} -eq 0 ]; then
  exit 0
fi

echo ""
echo "🏗️  ARCHITECTURE GUARD — $(basename "$FILE_PATH")"
echo "══════════════════════════════════════════════════"
for v in "${VIOLATIONS[@]}"; do
  echo "  ⚠️  $v"
done
echo ""
echo "  Tham khảo: CLAUDE.md → Kiến trúc 3 tầng"
echo "══════════════════════════════════════════════════"
echo ""

# Exit 0: cảnh báo nhưng không block (Claude quyết định)
# Đổi thành exit 2 nếu muốn block hoàn toàn
exit 0
