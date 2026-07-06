# AM.AutoFrame HMI — Master Index (cho Claude Code)

Đầu mối toàn bộ thiết kế HMI sinh ra trong phiên thiết kế giao diện vận hành.
Khi sinh bất kỳ màn HMI / ViewModel / config nào, ĐỌC file này trước, rồi mở tài liệu chi tiết tương ứng.

> **Cách dùng trong repo**: đặt cả thư mục này cạnh `CLAUDE.md`. Thêm vào `CLAUDE.md`:
> *"Mọi màn hình HMI phải tuân theo `docs/hmi/HMI_Master_Index.md` và các tài liệu nó trỏ tới.
> Trước khi sinh view/viewmodel mới, chạy Checklist §7 của Master Index."*

---

## 0. Bối cảnh

- **Nền tảng**: WPF/.NET 9, Prism + DryIoc, MVVM, Stateless (PackML), HAL vendor-isolation, EF Core/SQLite.
- **Phần cứng đích**: IPC 24" (và 21.5") 1920×1080, cảm ứng + chuột, găng ESD.
- **Lĩnh vực**: điện tử/bán dẫn, ưu tiên dây chuyền liên tục (không phải lô).
- **Tích hợp**: SECS/GEM, MES, OPC-UA, EtherCAT.

---

## 1. Bộ tài liệu (đọc theo nhu cầu)

| File | Nội dung | Khi nào đọc |
|------|----------|-------------|
| **HMI_UI_Architecture_Template_v3.md** | **CHUẨN HIỆN HÀNH (S79)**: bố cục shell 4 vùng (header+nav gộp 56 · banner co giãn 36→52 + operator prompt · content · action bar 76 + chip kết nối), kiosk config-driven, 3 nguyên tắc nội dung | Mọi màn HMI — đọc TRƯỚC |
| HMI_UI_Architecture_Template_v2.0.md | Tài liệu GỐC: 10 nguyên tắc nền, màu/chữ/icon, config schemas — **phần bố cục shell 7 vùng đã bị v3 thay thế**; còn hiệu lực: §3.4/§3.5 work area + right rail, §5 palette, §7 schemas | Khi làm nội dung Home / cần palette+schema |
| **HMI_Button_Spec_v2.0.md** | Bảng chuẩn Nút → Điều kiện → Hành động → Mở ra → Role cho mọi phần tử Home | Khi làm nút/lệnh |
| **HMI_Manual_Operation_and_Safety_v1.0.md** | Màn Vận hành tay (gộp Manual+Motion), 4 role, 4 mức rủi ro R0–R3, guard, Supervised Override, sub-tab | Màn vận hành tay, thao tác có rủi ro |
| **HMI_Naming_and_Axis_Point_Model_v1.0.md** | Đặt tên IO/biến, mô hình Trục–Điểm, Set/Confirm, layout thích ứng 3 tầng nhất quán, trạng thái IO, set/reset vs Force | Trục, điểm, IO, đặt tên |
| **HMI_Calibration_Model_v1.0.md** *(chưa có — sẽ viết ở ROADMAP_HOAN_THIEN P2.1)* | Calib: phân loại frequency (routine/rare), gắn sự kiện thay thiết bị, wizard 2 nhánh tự động/thủ công theo sai số | Calib, hiệu chỉnh, bảo trì |

Mockup HTML (tham chiếu trực quan, tỷ lệ thật 1920×1080):

| File | Màn |
|------|-----|
| hmi_home_v2.html | Home — work area + right rail + sub-tab |
| hmi_manual_operation_v1.html | Vận hành tay — 5 khu, sub-tab, dải khóa trạng thái |
| hmi_axis_detail_v1.html | Điều khiển trục chi tiết — bảng đèn 8 tín hiệu, bảng điểm Set/Confirm |
| hmi_adaptive_layout.html | Layout thích ứng nhỏ/vừa/lớn (4→20 trục) |
| hmi_io_states.html | Trạng thái IO + set/reset vs Force mode |
| hmi_calib_wizard.html *(chưa có — sẽ làm cùng ROADMAP P2)* | Wizard hiệu chỉnh — 2 nhánh tự động/thủ công theo sai số |
| hmi_motion_v2.html *(đã thay bằng hmi_manual_operation_v1.html)* | (bản gộp cũ — không dùng nữa) |

---

## 2. Nguyên tắc bất biến (KHÔNG vi phạm)

1. **ISA-101**: nền xám trung tính, màu CHỈ cho trạng thái/cảnh báo. Phẳng, không gradient/bóng 3D/nút tròn.
2. **EEMUA 201**: banner alarm 1 dòng ưu tiên cao nhất + ACK + chip "+N"; ACK ≠ tắt còi.
3. **SEMI E95**: phân vùng cố định. (E95 KHÔNG quy định vị trí ngôn ngữ.)
4. **Persistent Frame nhất quán tuyệt đối**: header/nav/banner/action bar/thanh kết nối giống hệt mọi máy — cho hành động phản xạ của Operator.
5. **Giải thích thay vì giấu**: nút không khả dụng → mờ + toast lý do. Không ẩn nút (trừ tab nguyên-module theo role).
6. **Một lệnh một chỗ**: không trùng nút giữa các vùng.
7. **Lệnh có hậu quả vật lý cần xác nhận chủ động**: nhấn-giữ 1 s (cửa), 2 bước + đếm ngược (override), 2 chạm (bảng điểm).
8. **An toàn = event push** qua `HardwareInputEventBus`, không polling. Khu điều chỉnh bind MỘT cờ container theo trạng thái máy.
9. **Cảm ứng theo mm**: nút thường ≥48 px, lệnh chính/jog ≥64 px, dòng bảng ≥44 px, khoảng cách ≥8 px. `UiScale` theo PPI.
10. **Config-driven**: khác biệt giữa máy đi qua config, KHÔNG hardcode trong Shell/Home.
11. **Màu chỉ khi có ý nghĩa trạng thái** (ADR 0010): Lỗi=0 là xám trung tính, KQ OK/NG mới có màu, thiết bị bình thường = chấm nhỏ chứ không chữ màu.
12. **Vùng trống phải nói bước tiếp theo**: mọi empty state có hướng dẫn hành động, không để vùng trắng câm.
13. **Xếp theo tần suất liếc nhìn**: KQ gần nhất > KPI ca > thao tác > log.

---

## 3. Bố cục shell (4 vùng — v3 từ S73, chi tiết ở HMI_UI_Architecture_Template_v3.md)

```
[1] Header+Nav 56   logo(tooltip tên máy) · chip AUTO/DRY·LOCAL·state │ tab điều hướng │ recipe·clock+heartbeat·🌐·👤
[2] Banner 36→52    alarm ưu tiên cao nhất + ACK + "+N khác" HOẶC operator prompt (Thử lại·Bỏ qua·Dừng máy)
[3] Content *       Home: work area (card KQ gần nhất + bảng SP) + right rail 560 (KPI → Thao tác nhanh → Trạm&an toàn → Nhật ký)
[4] Action bar 76   Init·Start·Pause/Resume·Stop │ Reset (divider) · Dry run·Manual · chip "● Thiết bị n/m · Host n/m"+popup
```

Vùng 1,2,4 = Persistent Frame (mọi màn kế thừa). Action bar bind `stateMachine.CanFire(trigger)`.
Chrome dọc 168px (v2 cũ 7 vùng = 284px). *(Bản 7 vùng cũ: xem template v2 — chỉ để tra cứu lịch sử.)*

---

## 4. Màn Vận hành tay (gộp Manual + Motion/IO)

- **Tab hiện theo role**: Line Lead trở lên (Operator không thấy).
- **Đổi hành vi theo trạng thái máy**: EXECUTE → chỉ xem; IDLE/STOPPED/PAUSED → cho điều chỉnh. Bind cờ container `IsAdjustAllowed`.
- **Bố cục**: dải khóa trạng thái + giám sát rút gọn (CỐ ĐỊNH, không cuộn) → sub-tab (cuộn nội bộ):
  `Thao tác trạm | Điều khiển trục | Bảng điểm | ⚠ Override`.
- **4 role**: Operator < Line Lead < Engineer < Admin. Nâng quyền = tài khoản riêng, KHÔNG chia mật khẩu.
- **4 mức rủi ro**: R0 tiện ích (OP) · R1 phục hồi đóng gói có guard (LineLead) · R2 chuyển động kiểm soát (EN, hạ có điều kiện) · R3 tự do/force (EN/AD).
- **Guard**: thao tác mang tiền điều kiện + blockReason (vd tắt khí âm chỉ khi Z hạ). Đọc từ HardwareInputEventBus.
- **Supervised Override**: cố ý vượt 1 guard (vd nhả liệu lấy dị vật). Engineer+, chỉ STOPPED, xác nhận 2 bước + đếm ngược + lý do + audit nặng. Không nới guard thường.

---

## 5. Trục, Điểm, IO

**Trục/Điểm tách rời.** Điểm tham chiếu `axisId`, không lưu trong trục. Một điểm có toạ độ trên nhiều trục, mỗi trục có `setPos` + `confirmPos` (lệch quá ngưỡng → cảnh báo).

**Bảng điểm gọn** (20–50 điểm): chạm ô = chọn 1 trục, chạm tên = cả điểm; rồi 1 cặp Tới/Teach ở thanh dưới (2 chạm). Teach chỉ trục đã Servo ON + Home.

**Nút trục cần có**: Servo ON/OFF · Home · **Clear Error riêng/trục** · Move-to-point (tốc độ giới hạn) · Jog deadman (watchdog HAL, mất tick >200ms→dừng) · inching 3 mức + ô nhập · nhóm trục XYZU/Tap · Dừng chuyển động (khác Stop chu trình). Bảng đèn 8 tín hiệu/trục: Alarm·+Limit·−Limit·Origin·EStop·Zero·InPos·Servo.

**Đặt tên**:
- IO vật lý: `{Node}.{Slot}.{Channel}_{LogicAddr}_{Function}`.
- Biến code: `DI_/DO_/AI_/AO_/AX_/PT_` + cụm chức năng + hậu tố trạng thái (`_Reached`, `_Extended`…).
- HMI hiện **địa chỉ trước tên**: `X017 · Chân không đầu hút 1`. Địa chỉ mono, KHÔNG dịch.
- `localize:false` → giữ tên gốc + `rawName` sau dấu /. Định danh kỹ thuật không bắt buộc dịch.

**Trạng thái IO** (hình+màu): OFF xám · ON xanh (do logic) · chờ vàng nhấp nháy · **FORCED ô vuông đỏ** · ▲ giữa hành trình (xi lanh 2 cảm biến off=nghi kẹt). IO xi lanh khai theo `actuatorGroup` để suy ra KẸP/NHẢ/GIỮA.

**Output set/reset vs Force (phương án A)**: mặc định mỗi dòng là nút bấm-được (set/reset, logic vẫn kiểm soát). Force = chế độ riêng (toggle Admin, ngoài EXECUTE), nền đỏ, bấm=đóng băng, đếm "force N IO" + nhắc gỡ. Ranh giới hiển nhiên theo chế độ.

---

## 6. Layout thích ứng (4 → 20 trục) — nhất quán 3 tầng

| Quy mô | Điều kiện | Render |
|--------|-----------|--------|
| Nhỏ | ≤1 trạm, ≤6 trục | FLAT |
| Vừa | ≤4 trạm và ≤12 trục | HORIZONTAL TABS |
| Lớn | ≥5 trạm hoặc >12 trục | SIDEBAR trái |

`layoutHint: auto|flat|tabs|sidebar` — ép tay khi nhà máy ưu tiên nhất quán tuyệt đối.

**Bất biến qua mọi render**: thứ tự sub-tab · quy luật "chọn phạm vi → nội dung" · đơn vị hàng trục `[tên][trạng thái][vị trí][ON|Home|Clear]`.
Trục thuộc trạm hay chung: khai `"station": "X"` hoặc `null`. Trạm KHÔNG lên nav chính.

---

## 6b. Calibration / Hiệu chỉnh

- **Calib ≠ Setting**: Setting = cấu hình tĩnh; calib = quy trình động cần máy chuyển động, có bước + kết quả đo. Không trộn.
- **Phân theo `frequency` khai trong config** (không cố định theo chức năng): `routine` → sub-tab "Hiệu chỉnh" trong Vận hành tay (tự ẩn nếu máy không có mục routine); `rare` → Cài đặt → Bảo trì & Hiệu chuẩn (Admin).
- **Gắn sự kiện thay thiết bị**: `requiresCalibAfterChange` → bấm "đã thay đầu vít"/usage vượt ngưỡng → nhắc + nhảy thẳng wizard.
- **Mỗi calib là wizard 2 nhánh** theo sai số: đo tự động → nếu trong `autoThreshold` thì áp tự động (1 chạm); nếu vượt thì đẩy sang nhánh chỉnh tay có hướng dẫn từng bước → đo lại → lặp đến khi đạt → lưu recipe + audit.
- KHÔNG menu phẳng, KHÔNG nhét vào nút Setting chung. Chi tiết: `HMI_Calibration_Model_v1.0.md`.

---

## 7. Checklist sinh màn hình mới

1. Màn Level mấy (1 overview/2 nhóm/3 chi tiết/4 chẩn đoán)? Kế thừa Persistent Frame (vùng 1,2,3,6,7).
2. Dùng template view nào sẵn có? View mới → định nghĩa empty state + role + interlock TRƯỚC khi vẽ.
3. Mọi nút: precondition theo `CanFire`/cờ container, role, target view, audit hay không, mức rủi ro R0–R3.
4. Không thêm màu ngoài bảng. Không icon-only. Vùng chạm ≥48 px (lệnh chính ≥64).
5. Chuỗi UI qua `ILocalizationService`; định danh kỹ thuật (địa chỉ/biến/IO localize:false) giữ gốc; địa chỉ luôn hiện.
6. An toàn/trạng thái: event push HardwareInputEventBus, không polling. Khu điều chỉnh = 1 cờ container.
7. Lệnh hậu quả vật lý: chọn cơ chế xác nhận (giữ 1s / 2 bước+đếm ngược / 2 chạm) theo mức rủi ro.
8. Khác biệt theo máy → config (HomeSubViews, QuickActions, RecoveryActions, stations, IOMap, layoutHint), KHÔNG hardcode.

---

## 8. Config schemas (gom một chỗ)

```jsonc
// machine.config.json
{
  "MachineId": "AM-SCR-02",
  "UiScale": 1.0,                        // 24"→1.0, 21.5"→1.1
  "VisionLayout": "Dual",               // None|Single|Dual|Quad|SingleLive
  "layoutHint": "auto",                 // auto|flat|tabs|sidebar
  "HomeSubViews": ["ProductTracking","ScrewForceChart","WorkPositionMap"],
  "ProductDataColumns": [ { "key":"ScrewSummary","header":"Data trạm","format":"{okCount}/{total} vít · max {maxTorque} N·m" } ],

  "stations": [
    { "id":"Loader",  "axes":["AX_Loader_X","AX_Loader_Z"], "cylinders":["CYL_Loader_Clamp"], "io":["DI_Loader_Present"] },
    { "id":"Adjust",  "axes":["AX_X_Adjust","AX_Y_Adjust","AX_Z_Adjust"] }
  ],
  "sharedAxes": ["AX_Gantry_X"],

  "QuickActions": [
    { "id":"WorkLight","icon":"LightbulbOutline","type":"Toggle","halCommand":"IO.WorkLight","roles":["Operator+"] },
    { "id":"SafetyDoor","icon":"LockOpenOutline","type":"HoldToConfirm","halCommand":"Safety.RequestDoorUnlock","interlock":"StateIn(IDLE,PAUSED,STOPPED)","audit":true }
  ],
  "RecoveryActions": [
    { "id":"VacuumOff","label":"Tắt khí âm","risk":"R1","halCommand":"Vacuum.Off","guard":"Z1.AtOrBelow(workHeight)||Blow.AssistReady","blockReason":"Z chưa hạ — có thể rơi liệu","roles":["LineLead+"],"audit":true },
    { "id":"ReleaseVacuumOverride","type":"SupervisedOverride","halCommand":"Vacuum.Off","precondition":"StateIn(STOPPED)","overrides":"VacuumOff.guard","confirm":"TwoStep+Countdown(3s)","warning":"Liệu sẽ rơi tự do. Có người đỡ.","roles":["Engineer+"],"audit":"high","requireReason":true }
  ],
  "ConnectionBar": { "devices":["PLC","EtherCAT","ScrewDriver1","Cam1"], "hosts":["MES","OPC-UA","DB"] }
}
```

```jsonc
// axis định nghĩa
{ "id":"AX_X_Adjust","displayName":"Trục X điều chỉnh","station":"Adjust","cardType":"PCIeM60","axisNo":1,
  "pulsePerMm":1000,"homingMode":1021,"maxSpeed":500000,"accelTime":0.1,"decelTime":0.1,
  "inPosError":200,"softLimitPosEnable":false,"softLimitPos":10000000,"slaveAxis":false }

// point
{ "id":"PT_Screw_AssemblyPoint","displayName":"Điểm lắp ráp","station":"Screw","index":3,
  "axes":{ "AX_X_Adjust":{"setPos":125.52,"confirmPos":125.52,"speedPct":100},
           "AX_Z_Adjust":{"setPos":112.56,"confirmPos":112.30,"speedPct":100} } }

// IOMap
{ "physical":"1.2.17_X017_AdjPlatformVacuumReached","var":"DI_AdjPlatform_VacuumReached",
  "address":"X017","type":"DI","station":"AdjPlatform","actuatorGroup":"Clamp_Adjust",
  "displayName":{"vi":"Chân không đầu hút 1","zh":"吸嘴真空1"},"localize":true,"rawName":"吸嘴真空1" }
```

---

## 9. Điểm còn chờ xác nhận từ chủ dự án (thay khi có số thật)

1. Cơ chế confirm Supervised Override: 1 người (2 bước+đếm ngược, đang dùng) hay 2 người đỡ (giữ-nút)?
2. R2 (move-to-point gỡ kẹt): cứng ở Engineer hay cho hạ Line Lead có điều kiện theo từng máy?
3. Số trục/trạm thật của máy chủ lực + trục nào thuộc trạm / dùng chung.
4. Ngưỡng cảnh báo lệch Set–Confirm theo loại trục.

---

## 10. Lịch sử quyết định chính trong phiên (vì sao thế này)

- Home: bỏ sidebar dọc & live-view 2×2 (giá trị thấp); work area + right rail; vùng giữa thành bảng truy vết sản phẩm + thumbnail kết quả tĩnh (không stream).
- Action bar dưới cùng persistent (công thái học + nhất quán), không đưa Start/Stop lên header.
- Gộp Manual + Motion/IO → "Vận hành tay" (ranh giới người dùng quan tâm là chạy/dừng, không phải trạm/trục).
- Phân quyền theo MỨC RỦI RO, thêm role Line Lead → giải bài "lỗi nhỏ cần quyền cao" mà không chia mật khẩu.
- Override tách khỏi guard thường (không nới guard để chiều ngoại lệ).
- Layout thích ứng + nhất quán 3 tầng (giải mâu thuẫn thích ứng vs vị trí cố định).
- IO: địa chỉ vật lý luôn hiện + không dịch; set/reset thường tách khỏi Force mode (phương án A).
- Tham khảo phần mềm điều khiển máy công nghiệp: lấy cấu trúc (tên IO 4 lớp, Set/Confirm, bảng đèn 8 tín hiệu, Clear Error/trục), bỏ thẩm mỹ (nút tròn/gradient) và mật độ cao khỏi màn vận hành.

---

## 11. Quyết định adoption AM.AutoFrame + phản biện (Session 47, 14/06/2026)

> Mục này do team AM.AutoFrame thêm sau khi phản biện TOÀN BỘ bộ tài liệu. Spec gốc giữ nguyên ở trên.
> Đây là "bộ lọc" giữa spec thiết kế (lý tưởng) và codebase thật (.NET 9, ISA-88, HAL hiện có).
> Các doc khác (Template v2, Naming, Manual_Operation) trỏ về đây làm nguồn adoption DUY NHẤT.

### A. Đã có trong code (khớp spec)
- **Palette v2 · 7 vùng Persistent Frame · Home work-area + right rail · alarm banner multi-alarm · action bar trắng icon-trên · connection bar Thiết bị│Host** — Shell + Home (S45).
- **Màn điều khiển trục**: bảng đèn 8 tín hiệu, servo/home/clear/move, jog+inching, bảng điểm Set/Confirm 2-chạm — `AM.Modules.Motion` (S46), qua interface tuỳ chọn `IAxisDiagnostics` (sim implement).
- **Mô hình 4 role** (Operator < Line Lead < Engineer < Admin): `UserLevel` thêm `LineLead=1` (S47), seed user `linelead/linelead123`. SuperUser giữ làm tầng OEM trên Admin (xem phản biện E1).

### B. Map khái niệm spec → codebase (KHÔNG đổi core)
| Spec | AM.AutoFrame |
|------|--------------|
| PackML IDLE/EXECUTE/STARTING/STOPPED/ABORTED/COMPLETE + Stateless `CanFire` | ISA-88 8 trạng thái (`MachineState`) + `BaseMasterController` (55 tests). `IsAdjustAllowed = State∈{Idle,Paused,InitAlarm,RunAlarm}` (máy không chạy). KHÔNG thay state machine để chiều tên PackML. |
| Material Design Icons | Segoe MDL2 (sẵn Windows, 1 màu, không thêm package) |
| Header 48px (§3) vs 64px (Template §3.1) vs 56px (mockup manual) | **Chốt 64px** (đang chạy) — xem D1 |
| Connection bar 32px (§3) vs 40px (Template §2) | **Chốt 40px** — xem D2 |

### C. Hoãn có chủ đích (cần hạ tầng / xác nhận — KHÔNG làm nửa vời)
Màn **Vận hành tay** và hệ con là khối lớn, AN TOÀN-TRỌNG YẾU, phụ thuộc nhiều thứ chưa có:
1. ~~**`HardwareInputEventBus`**~~ ✅ **XONG (S62)**: `IHardwareSignalBus` (event-push, thread-safe) + `SafetySignalPublisher` nối `ISafetyInput`→bus; `GuardCondition` (mô hình boolean) + `IGuardEngine.Evaluate(risk, condition?)` nối guard tầng 3. Mới wire nguồn Safety.* — IO/trục (vị trí Z, chân không) publish thêm khi §6.3 cần. Numeric → tín hiệu bool dẫn xuất do HAL publish (chưa làm DSL).
2. **Guard engine + Supervised Override** — cơ chế confirm Override CHƯA chốt (§9.1: 1 người 2-bước+đếm-ngược hay 2 người giữ-nút). KHÔNG build flow an toàn khi chính sách chưa xác nhận (rule "safety-first" + "think-first").
3. **R2 downgrade về Line Lead** — per-machine, default chưa chốt (§9.2).
4. ~~**IO Force mode** (phương án A)~~ ✅ **XONG (S59)**: `IIoModule` +`ForceDoAsync`/`UnforceDoAsync`/`IsDoForced`/`ForcedOutputs`/`ReadAllDoAsync` (Sim + ADAM); kênh forced bỏ qua `WriteDiAsync` của logic. IoMonitor: set/reset (Engineer) + Chế độ Force (Admin, chạm-2-bước, badge + bộ đếm + audit). Còn hoãn: per-output confirm cho set/reset, alarm "còn IO forced".
5. **Adaptive layout 4→20 trục** (flat/tabs/sidebar) — chiến lược render CỦA màn Vận hành tay; build khi dựng màn đó, không build engine trước.
6. ~~**IO actuatorGroup ("▲ giữa"), address-before-name, localize:false/rawName**~~ ✅ **XONG (S60)**: `JsonIoTagMap` nhận schema mảng (địa chỉ/tên đa ngữ/rawName/localize) + `IoChannelDescriptor`/`IoCylinderDescriptor`; màn IO danh sách "địa chỉ·tên" + ô lọc + xi lanh ▲ giữa + Forced ô vuông + pending. Còn hoãn: confirm chạm-2 cho set/reset có hậu quả + alarm "còn IO forced" (increment C).

> ⇒ **Lượt này KHÔNG build màn Vận hành tay.** Chỉ chốt nền role (đã làm) — nền cho mọi guard/override sau. Còn lại cần (a) chốt §9 với chủ dự án, (b) thêm HardwareInputEventBus + guard engine.

### D. Phản biện mâu thuẫn NỘI BỘ bộ tài liệu (sửa khi ra bản sau)
1. **Chiều cao header không nhất quán**: Master Index §3 = 48px, Template §3.1 = 64px, mockup `hmi_manual_operation` = 56px → thống nhất **64px**.
2. **Connection bar**: §3 = 32px, Template §2 = 40px → thống nhất **40px**.
3. **Tham chiếu treo**: §1 trỏ `HMI_Calibration_Model_v1.0.md`, `hmi_calib_wizard.html`, `hmi_motion_v2.html` — KHÔNG có trong bộ giao. Bổ sung hoặc bỏ trỏ.
4. **Override confirm + R2 policy** tự nhận "cần xác nhận" (§9) nhưng config schema §8 đã ghi cứng `confirm:"TwoStep+Countdown(3s)"` và `r2DowngradeToLineLead` — mâu thuẫn "chưa chốt" vs "ví dụ cứng". Đánh dấu là default TẠM.

### E. Phản biện thiết kế (giữ để bản sau cân nhắc)
1. **SuperUser**: bộ tài liệu chỉ nói 4 role, bỏ qua SuperUser đang có trong code. Quyết định: giữ SuperUser là tầng **OEM/bảo trì nội bộ trên Admin**, KHÔNG hiện trong UI chọn role vận hành, chỉ override safety + debug HAL. Tài liệu role nên ghi rõ tầng thứ 5 tồn tại nhưng ngoài mô hình vận hành thường.
2. **"Gộp Manual+Motion/IO thành Vận hành tay"** mâu thuẫn với **màn điều khiển trục** đã build S46 (tab Motion riêng). Khi dựng Vận hành tay, màn Motion S46 nên thành **sub-tab "Điều khiển trục"** nhúng lại (không viết trùng — đúng §3.2 template). Cần refactor nav khi đó.
3. **Risk tier R0–R3 gắn vào QuickAction/RecoveryAction**: hiện `QuickActions` trên Home (S45) chưa đọc `risk`/`guard`/`audit`. Khi có guard engine, QuickAction và RecoveryAction nên dùng CHUNG kiểu `GuardedAction` (id, risk, halCommand, guard, blockReason, audit) — tránh hai schema gần giống.
