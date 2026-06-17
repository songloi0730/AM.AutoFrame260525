# 0004 — QuickActions HAL + hold-to-confirm — §6.5

**Bối cảnh.** "Thao tác nhanh" trên Home có 6 nút nhưng chỉ `BuzzerOff` nối HAL (qua `ILightController`); 5 nút còn lại mờ
"chưa cấu hình HAL". Cần wire HAL thật + thêm giữ-1s xác nhận cho thao tác cửa (R1).

## Hai quyết định

### 1) Cách wire HAL cho QuickActions
- **A — Dashboard inject `IIoModule`+`IIoTagMap`, wire trực tiếp  ✅ CHỌN:** toggle DO theo tag io.map; BuzzerOff giữ
  `ILightController`; CallTech = thông báo. *+* tự chứa, tường minh; Dashboard **cần `IIoModule` để poll IsOn dù sao** nên không
  phát sinh phụ thuộc thừa. *−* logic dispatch nằm trong VM (chấp nhận — 6 nút Home khá cố định).
- **B — Dùng chung `IRecoveryActionRegistry` (§6.3):** đăng ký handler quick-action ở App.xaml.cs, Dashboard gọi
  `ExecuteAsync(id)`. *+* thống nhất dispatch HAL. *−* vẫn phải inject `IIoModule` để poll IsOn → thêm gián tiếp mà không bớt
  phụ thuộc; registry mang nghĩa "recovery" hơi lệch. Loại (chưa đáng).

### 2) Phạm vi hold-to-confirm
- **Chỉ R1 (cửa) ✅ CHỌN:** giữ-1s cho SafetyDoor/FeedDoor (có hậu quả vật lý); R0 (đèn/ion/còi/gọi KT) bấm thường.
- **Mọi nút:** an toàn hơn nhưng phiền cho R0 rủi-ro-thấp. Loại.

## Hiện thực
- `QuickActionVm.HoldMs = Risk >= R1 ? 1000 : 0`.
- `HoldToConfirmBehavior` (attached prop `DurationMs` trên Button): nếu >0 → chặn click thường, `DispatcherTimer(DurationMs)`;
  giữ đủ → `Command.Execute(CommandParameter)`; nhả/rời sớm → huỷ. Đặt trong `AM.Modules.Dashboard` (self-contained); có thể
  chuyển `AM.UI.Controls` khi tái dùng (vd áp cho RecoveryActions/Override sau).
- `HasHal` mở rộng cho cả 6 id; `QuickAction(id)` giữ guard + audit, dispatch toggle DO / light / notify; IsOn poll từ `ReadAllDoAsync`.

## Hệ quả / đánh đổi
- Nợ kỹ thuật (xem 0002/0003): QuickActions/RecoveryActions/Override vẫn là 3 lớp guarded-action song song; `HoldToConfirmBehavior`
  là mảnh dùng chung đầu tiên có thể trích ra `AM.UI.Controls` khi hợp nhất `GuardedActionVm`.
- CallTech demo = op-log; máy thật nối andon/thông báo sau.

## Liên kết
- Triển khai: Session 65 (`CHANGELOG.md`). Nền: [0001 §6](0001-am-autoframe-design-decisions.md), [0002](0002-station-recovery-actions.md).
