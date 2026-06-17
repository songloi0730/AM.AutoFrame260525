# 0003 — Supervised Override — §6.4

**Bối cảnh.** Có tình huống mục tiêu ĐẢO NGƯỢC guard thường ngày: vd khí âm yếu do dị vật → người vận hành *chủ động muốn*
nhả liệu (có người đỡ) để lấy dị vật — tức cố ý bỏ qua guard "giữ liệu" (HMI_Manual_Operation_and_Safety §5). KHÔNG xử lý
bằng cách nới guard chung (sẽ làm yếu bảo vệ cho 99% trường hợp còn lại). Cần một luồng *vượt guard có kiểm soát*.

Nền sẵn: guard engine 3 tầng (§S62), pattern provider/registry + embed VM + audit của RecoveryActions (§S63).

## Hai quyết định

### 1) Cơ chế xác nhận (chính sách §9a) — **1 người: 2 bước + đếm ngược**
- **Đã chọn:** một người, **xác nhận 2 bước + nhả có đếm ngược vài giây** (mặc định 3s).
- **Phương án khác:** *giữ-nút-2-giây* — chỉ đúng khi máy LUÔN có ≥2 người (một bấm, một đỡ). Nếu một người vừa bấm vừa đỡ thì
  giữ-nút sai (không rảnh tay). Chủ dự án xác nhận kịch bản 1 người → chọn 2-bước+đếm-ngược.
- Kèm: **bắt buộc nhập lý do**, **audit nặng**, **Engineer+**, **chỉ STOPPED** (đều bất biến trong code).

### 2) Mô hình hoá so với RecoveryActions (§6.3)
- **A — Model + config riêng, dùng chung registry handler  ✅ CHỌN:** `OverrideActionDef` + `override-actions.json`
  (thêm `warningKey`/`overridesGuard`/`countdownSec`); handler HAL dùng chung `IRecoveryActionRegistry` (id duy nhất).
  *+* tách bạch hai mô hình an toàn KHÁC bản chất (thường = chạy *trong* guard; override = *vượt* guard, luôn hiện, xác nhận nặng)
  → không trộn vào một danh sách/VM dễ nhầm; vẫn tái dùng sổ handler. *−* thêm một model+provider (nhưng gần như sao chép mẫu §6.3).
- **B — Gộp vào RecoveryActions, thêm `type:"override"`:** ít loại hơn nhưng trộn hai UX/an-toàn khác hẳn vào một chỗ →
  dễ nhầm "bấm là chạy" với "bấm là vượt guard"; logic VM phân nhánh rối. Loại.

## Hệ quả / đánh đổi
- Override **bỏ qua tầng-3** (mục đích) nhưng VẪN giữ **Engineer+ & STOPPED** (gate bằng `guard.Evaluate(R3)` không kèm condition).
  Không cấu hình hạ hai điều kiện này được — cố ý, để an toàn.
- **Nhả servo Z** là override RIÊNG (hệ quả vật lý khác: Z tụt do trọng lực khi nhả servo) — KHÔNG gộp nút với nhả khí âm.
  Hoãn tới khi có HAL servo-release; khi làm: thêm 1 mục override + cảnh báo riêng "đỡ cơ cấu trước".
- **Nợ kỹ thuật (xem 0002):** QuickActions / RecoveryActions / Override nay là 3 lớp "guarded action" song song —
  tương lai hợp nhất `GuardedActionVm` + helper đánh giá enablement dùng chung.

## Liên kết
- Triển khai: Session 64 (`CHANGELOG.md`). Nền: [0001 §6,§7](0001-am-autoframe-design-decisions.md), [0002](0002-station-recovery-actions.md).
