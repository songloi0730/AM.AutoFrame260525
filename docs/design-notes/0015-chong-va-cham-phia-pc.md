# 0015 — Chống va chạm phía PC (MoveGuard) + giới hạn mềm thống nhất

**Trạng thái:** Đã chốt và thực hiện (S99) — chủ dự án chọn Đ1 trong khảo sát `docs/Reference_Automation_Software_Survey.md`
và cho đi tiếp với các lựa chọn khuyến nghị.
**Liên quan:** `HMI_Manual_Operation_and_Safety_v1.0.md` (guard R0–R3, tín hiệu "Z an toàn"), 0012 (break-glass
phải ồn ào), 0013 (phanh Z), 0014 (toàn vẹn cấu hình SHA-256).

## Bối cảnh

Khảo sát 18 phần mềm máy công khai (S98) cho thấy 2/18 nguồn có lớp chống va chạm phía PC cấu hình được (RefAuto-C:
nhóm điều kiện kiểm trước mọi lệnh; RefAuto-B: mô hình không gian + vùng cấm), và nguồn tốt nhất về an toàn
(RefAuto-A) giữ một interlock Z cứng "trục ngang chỉ chạy khi Z ở cao độ an toàn, đọc encoder thật".

Rà mã AM.AutoFrame tìm ra 3 lỗ hổng:

1. **Màn Vận hành tay gọi thẳng `IMotionController`** — không kiểm giới hạn mềm.
2. **Đường sequence (`MotionAxisAdapter`) KẸP đích im lặng** về biên giới hạn mềm: lệnh tới 600 mm thành tới
   500 mm, không ai biết. Máy đi tới một chỗ không ai ra lệnh.
3. Interlock "Z an toàn" chỉ nằm trong **guard của màn tay** (`GeometricGuardFor`); sequence, chạy lặp, đi tới điểm
   từ nơi khác không qua nó. Không có cách khai thêm luật cho máy cụ thể.

## Các phương án đã cân nhắc

### Mô hình luật

| Phương án | Đặc điểm | Đánh giá |
|---|---|---|
| **A. Luật interlock theo trục (CHỌN)** | "Trục M di chuyển [trong vùng V] thì CẦN: trục K trong khoảng, DI bật/tắt". JSON, kiểm ở một lớp bọc `IMotionController` | Đúng cách người chỉnh máy nói ("X chỉ chạy khi Z lên"); kiểm chứng dễ bằng test; không hiểu hình học — nhiều cặp trục thì danh sách dài |
| B. Mô hình không gian (RefAuto-B) | Cây hệ toạ độ, vùng cấm, cao độ an toàn, kiểm đường thẳng | Tổng quát nhưng phải mô hình hoá từng máy, khó kiểm chứng, nặng gấp nhiều lần cho máy demo 4 trục |
| C. Interlock cứng trong Mechanism | Mỗi Mechanism tự chặn | Ít hạ tầng; luật rải rác, màn tay (gọi thẳng card) vẫn hở |

**Vì sao không chép nguyên RefAuto-C** ("nhóm = AND mọi điều kiện → va chạm"): ngữ nghĩa AND đối xứng chặn luôn
cả lệnh **thoát khỏi** vùng nguy hiểm (Z đang ở dưới, X ở đâu cũng được ⇒ nhóm thoả ⇒ cấm cả nhấc Z lên). Luật
dạng "trục di chuyển → điều kiện cần" có chiều rõ ràng, nhấc Z lên không bị chặn bởi luật của X.

### Chỗ đặt lớp kiểm

| Phương án | Đánh giá |
|---|---|
| **Decorator `GuardedMotionController` bọc `IMotionController` trong DI (CHỌN)** | Một điểm chặn cho MỌI đường: màn tay, Mechanism, `IAxis` adapter, HomeAll; `HardwareManager` cũng nhận bản bọc |
| Kiểm trong từng caller | Dễ sót đường mới (đúng nợ mà RefAuto-B tự ghi nhận) |

### Giới hạn mềm khi vượt

| Phương án | Đánh giá |
|---|---|
| **Từ chối + `AlarmException` 10005 ở mọi đường (CHỌN)** | Máy không đi tới chỗ không ai ra lệnh; đổi hành vi sequence (hết kẹp im lặng) |
| Tay từ chối, sequence giữ kẹp | Giữ test cũ nhưng giữ luôn lỗi "đi tới chỗ khác lệnh" |

### Tắt luật (bypass)

| Phương án | Đánh giá |
|---|---|
| **Administrator tắt tạm TỪNG luật kèm lý do, ồn ào (CHỌN)** | Cứu máy kẹt ở tư thế mà luật chặn mọi hướng thoát. Ồn ào theo 0012: audit + alarm 70012 thường trực (banner cả app) tới khi bật lại; tự bật lại khi đổi user hoặc khởi động lại (trạng thái tắt KHÔNG lưu đĩa) |
| Không cho tắt | An toàn nhất; máy kẹt thì phải sửa file cấu hình (bị chữ ký SHA-256 phát hiện) |

## Quyết định

- `move-protection.json` cạnh exe (thuộc nhóm file ký SHA-256 của 0014). Thiếu file = máy không khai luật (hợp lệ,
  chỉ còn giới hạn mềm); file hỏng = log lỗi + KHÔNG nạp luật nào + alarm 40014 (không chặn máy chạy, nhưng ồn).
- Luật:
  ```json
  { "id": "X_NEEDS_Z_UP", "description": "PickX chỉ chạy khi PickZ đã nhấc",
    "moving": "PickX",
    "zone": { "min": null, "max": null },
    "require": [ { "axis": "PickZ", "min": -5 } , { "di": "DI.Cyl.Retracted", "on": true } ] }
  ```
  - `zone` tuỳ chọn: luật chỉ áp khi **đoạn đường** của trục di chuyển (vị trí hiện tại → đích) cắt vùng.
  - `require[].axis`: **cả đoạn** từ vị trí hiện tại tới đích đã ra lệnh gần nhất của trục kia phải nằm trong
    khoảng (trục kia đang chạy cũng được xét thận trọng). Trục kia **chưa về gốc ⇒ không đạt** (vị trí không tin được
    — buộc về gốc Z trước X, như RefAuto-A).
  - `require[].di`: tag DI qua `IIoTagMap`.
- `GuardedMotionController` (AM.Infrastructure/Motion): kiểm giới hạn mềm + luật trước `MoveAbs`/`MoveRel`/`HomeAxis`
  (đích về gốc coi là 0); vi phạm → **không phát lệnh**, ném `AlarmException` 10005 hoặc **70011** kèm lý do đọc được
  ("PickX bị chặn [X_NEEDS_Z_UP]: PickZ = -50.000, cần ≥ -5.000"). `HomeAllAxesAsync` đi thẳng xuống controller (thứ tự
  về gốc là việc của controller/driver — giới hạn ghi nhận).
- **Jog liên tục**: kiểm lúc bắt đầu với đích nhìn trước `vị trí + hướng × lookahead`; trong lúc jog, vòng 20 ms kiểm
  lại, vi phạm → dừng jog + raise alarm 70011 (không có caller để ném exception). `lookahead = max(cấu hình, |v| × 0,1 s)`.
- Capability: bọc giữ nguyên ngữ nghĩa "có thì cast được" — `GuardedJogMotionController` (thêm `IAxisJog`) chỉ dùng
  khi controller thật có jog; diag/phanh không phải lệnh chuyển động → VM lấy qua `IMotionControllerDecorator.Inner`.
- `MotionAxisAdapter` bỏ kẹp, để lớp bọc từ chối.
- Interlock "Z an toàn" cứng trong guard màn tay GIỮ NGUYÊN (lớp phòng thủ thứ hai, R3).

## Hệ quả

- Mọi lệnh điểm tới đích ngoài giới hạn mềm giờ bị từ chối (trước đây bị kẹp). Sequence gặp lỗi này sẽ alarm thay vì
  đi sai chỗ — đúng ý đồ.
- **Đây là bảo vệ quy hoạch phía PC**: không thay limit cứng, relay an toàn, STO hay PLC an toàn. Windows treo thì
  không có gì chặn. Ghi rõ trong checklist nghiệm thu P5.
- Chưa có: điều kiện tổng/hiệu hai trục (giàn kép), kiểm thứ tự trong HomeAll, trình soạn luật trên UI (sửa JSON +
  ký lại manifest). Làm khi có máy cần.
- **Bypass mới có API** (`IMoveGuard.SetBypassAsync` + alarm 70012 + tự bật lại khi đổi user); nút tắt/bật trên UI
  (thẻ trong Cài đặt kèm audit) là việc tiếp theo.
- Máy demo: `axismap.json` đổi tên trục `PickX/PickZ/PlaceX/PlaceZ` → `X/Y/Z/U` cho khớp chỉ số mà Mechanism và màn tay
  thực dùng (0=X, 1=Y, 2=Z); giới hạn mềm theo chỉ số giữ nguyên. Luật demo: X và Y chỉ chạy khi Z ≥ −0,5 mm.
- Thực hiện S99: `MoveGuard`, `GuardedMotionController`/`GuardedJogMotionController` (AM.Infrastructure/Motion),
  `IMoveGuard`, `IMotionControllerDecorator`, `MoveProtectionRule`; 21 test mới (`MoveGuardTests`).
