# HMI_Naming_and_Axis_Point_Model — v1.0

Quy ước đặt tên biến/IO và mô hình dữ liệu trục–điểm cho AM.AutoFrame.
Tổng hợp từ phân tích phần mềm thật (Secote/Mesa của dự án, Hanmi tham chiếu).
Đi kèm: mockup `hmi_axis_detail_v1.html`.

---

## 1. Nguyên tắc gốc

1. **Tên vật lý và tên biến code là hai thứ tách biệt**, map qua bảng `IOMap` trong config.
   Tên vật lý cho kỹ thuật viên dò dây (khớp tem nhãn trên máy); tên biến cho lập trình.
2. **Trục và điểm là hai thực thể tách rời.** Điểm tham chiếu trục (`axisId`), không lưu trong trục.
   Một trục tham gia nhiều điểm; nhét điểm vào trục sẽ khiến sửa một trục đụng toàn bộ điểm.
3. **Tên có nghĩa làm khóa hiển thị, index/id chỉ nội bộ.** Tránh "Index 37" trần như Hanmi —
   người đọc phải tra nghĩa. Giữ "SafePoint", "AssemblyPoint" như chính Mesa đã làm.
4. **Set Pos vs Confirm Pos** (học từ Hanmi): mỗi điplaceholder lưu giá trị *đặt* (mong muốn) và
   *xác nhận* (đã teach thực tế); lệch quá ngưỡng → cảnh báo khi teach/chạy.

---

## 2. Đặt tên IO

Cấu trúc tên vật lý — bốn lớp **vị trí vật lý → địa chỉ logic → chức năng** (mở rộng từ quy ước Secote):

```
{Node}.{Slot}.{Channel}_{LogicAddr}_{Function}
  1  .  2  .  17        _ X017      _ AdjPlatformVacuumReached
  │     │     │           │           └ chức năng (cái gì)
  │     │     │           └ địa chỉ logic PLC
  │     │     └ số kênh trên module
  │     └ slot/module
  └ node/station
```

Tên biến code tương ứng (gom theo cụm để IntelliSense gợi ý đúng nhóm):

| Loại | Prefix | Ví dụ |
|------|--------|-------|
| Digital input | `DI_` | `DI_AdjPlatform_VacuumReached`, `DI_Safety_DoorClosed`, `DI_EStop_Pressed` |
| Digital output | `DO_` | `DO_AdjPlatform_Vacuum`, `DO_Lamp_Green`, `DO_Buzzer1` |
| Analog in/out | `AI_` / `AO_` | `AI_AirPressure`, `AO_LightSource1` |
| Axis | `AX_` | `AX_X_Adjust`, `AX_Z1_Tap` |
| Point | `PT_` | `PT_Screw_SafePoint` |

Quy tắc hậu tố trạng thái (input): `_Reached`, `_Extended`, `_Retracted`, `_Present`, `_Ack`, `_Pressed`, `_Closed`.
Tránh hậu tố vô nghĩa kiểu `_Sensor1`. Mỗi tín hiệu phải đọc được nghĩa từ tên.

Cụm chức năng (ghép sau prefix): `AdjPlatform`, `TapHead`, `Loader`, `Unloader`, `Safety`, `LightSource`, `Lamp`, `EStop`, `Vacuum`, `Cylinder`…

`IOMap` (config) nối tên vật lý ↔ tên biến ↔ địa chỉ thật:
```json
{ "physical": "1.2.17_X017_AdjPlatformVacuumReached",
  "var": "DI_AdjPlatform_VacuumReached",
  "address": "X017", "type": "DI", "station": "AdjPlatform", "normallyOpen": true }
```

---

## 3. Mô hình dữ liệu Trục

Một trục = định nghĩa phần cứng (tham khảo bảng 轴配置 của Mesa, ảnh 1):

```json
{ "id": "AX_X_Adjust", "displayName": "Trục X điều chỉnh", "cardType": "PCIeM60", "axisNo": 1,
  "pulsePerMm": 1000, "homingMode": 1021,
  "homingSpeedMin": 1000, "homingSpeedMax": 20000,
  "homingAccelTime": 0.1, "homingDecelTime": 0.1,
  "maxSpeed": 500000, "accelTime": 0.1, "decelTime": 0.1, "smoothFactor": 0.1,
  "inPosError": 200,
  "softLimitPosEnable": false, "softLimitPos": 10000000,
  "softLimitNegEnable": false, "softLimitNeg": -10000000,
  "homeReturnEnable": false, "slaveAxis": false }
```

Tín hiệu trục hiển thị (bảng đèn 8 cột, chuẩn công nghiệp — Mesa ảnh 3):
`Alarm(报警) · +Limit(正限位) · −Limit(负限位) · Origin(原点) · EStop(急停) · Zero(零位) · InPosition(到位) · Servo/Excited(励磁)`.

---

## 4. Mô hình dữ liệu Điểm

Điểm = một vị trí có toạ độ trên NHIỀU trục (Mesa ảnh 3, Hanmi ảnh 4):

```json
{ "id": "PT_Screw_AssemblyPoint", "displayName": "Điểm lắp ráp / Assembly Point",
  "station": "Screw", "index": 3,
  "axes": {
    "AX_X_Adjust":  { "setPos": 125.52,  "confirmPos": 125.52,  "speedPct": 100 },
    "AX_Y_Adjust":  { "setPos": 320.41,  "confirmPos": 320.41,  "speedPct": 100 },
    "AX_Z1_Adjust": { "setPos": 112.56,  "confirmPos": 112.30,  "speedPct": 100 }
  } }
```

- `setPos` = giá trị mong muốn/lập trình; `confirmPos` = giá trị teach thực tế.
- |setPos − confirmPos| > ngưỡng → cảnh báo (điểm 3 ở mockup: Z lệch 0.26 mm).
- Điểm thuộc `PointGroup`/recipe; teach xong PHẢI lưu vào recipe (nhắc "Lưu ngay?").

**Hiển thị bảng điểm (gọn cho 20–50 điểm)**: mỗi điểm MỘT hàng ~32 px, mỗi ô trục hiện set/confirm xếp dọc. Bỏ cột nút. Tương tác = chọn-rồi-thực-thi (an toàn 2 chạm):
- Chạm ô toạ độ → chọn riêng trục đó của điểm; chạm tên điểm → chọn cả điểm (mọi trục).
- Chọn KHÔNG chạy ngay. Một cặp **Tới / Teach** duy nhất ở thanh dưới sáng lên kèm chỉ báo phạm vi ("Tới: Z của P03" / "Tới: cả điểm P03"); cú chạm thứ hai vào nút đó mới thực thi.
- Teach mở popup so cũ→mới; chỉ cho trục đã Servo ON + Home.
- Lý do 2 chạm: lệnh chuyển động/ghi đè toạ độ là hậu quả vật lý, một-chạm-là-chạy quá rủi ro trên màn cảm ứng cạnh máy mở cửa.

---

## 5. Nút vận hành trục cần thiết (tổng hợp từ ảnh thật)

Bắt buộc có (mockup `hmi_axis_detail_v1.html`):

| Nút | Phạm vi | Ghi chú |
|-----|---------|---------|
| Servo ON/OFF | mỗi trục | hàng ngang mỗi trục (Mesa ảnh 3) |
| Home / 原点 | mỗi trục + tất cả | giữ 1 s; "tất cả" theo thứ tự an toàn |
| **Clear Error / 清错** | mỗi trục + tất cả | xóa servo alarm RIÊNG, KHÔNG gộp vào Reset máy |
| Di chuyển tới đích + tốc độ % | mỗi trục | tốc độ giới hạn an toàn |
| Jog deadman (giữ-để-chạy) | trục đang chọn | watchdog HAL, STOP dừng mọi motion |
| Inching 3 mức + ô nhập tùy ý | — | tinh/vừa/thô (0.001/0.01/0.1) + ô nhập số cho bước đặc biệt + nút +/− nhích từng nấc. KHÔNG để 5 mức (mắt quét lâu) |
| Nhóm trục XYZU / Tap | — | chuyển nhóm khi máy >4 trục (máy có 7 trục) |
| Tương đối / Tuyệt đối | jog mode | Mesa ảnh 3 |
| **Chọn ô → Tới / Teach** | bảng điểm | chạm ô toạ độ = chọn 1 trục; chạm tên điểm = chọn cả điểm. CHỌN không chạy ngay; một cặp Tới/Teach duy nhất ở thanh dưới (hiện rõ phạm vi) là cú chạm thứ hai xác nhận. Thay cho 2 nút/điểm — tiết kiệm cả cột, hiển thị được 20–50 điểm |
| Lưu bảng điểm vào recipe | — | 保存点位 |
| Dừng chuyển động | toàn cục | khác Stop chu trình máy |

"Dừng chuyển động" (STOP đỏ giữa jog pad) dừng jog/move-to-point, KHÁC Stop của action bar (dừng chu trình).

---

## 6. Phản biện các pattern trong ảnh (lấy gì, bỏ gì)

| Pattern trong ảnh | Quyết định |
|-------------------|-----------|
| Tên IO 4 lớp của Secote (`1.17_2-X01_…`) | **Lấy** — rõ vị trí vật lý → logic → chức năng |
| `X000` trần của Hanmi (ảnh 6) | **Bỏ** — thiếu ngữ cảnh, khó dò |
| Set/Confirm Pos của Hanmi | **Lấy** — pattern an toàn chống teach sai |
| Index số trần làm khóa điểm (Hanmi) | **Bỏ** — dùng tên có nghĩa, index chỉ id nội bộ |
| Bảng đèn 8 tín hiệu/trục (Mesa) | **Lấy** — chuẩn công nghiệp |
| Clear Error per-axis (Mesa 清错) | **Lấy** — mockup cũ thiếu, đã bổ sung |
| Update-this-axis (Mesa 更新此轴) | **Lấy** — teach một trục giữ trục khác |
| Nút tròn + gradient (cả hai) | **Bỏ** — ISA-101, dùng chữ nhật phẳng |
| Bảng config trục dày đặc (Mesa ảnh 1) | **Lấy cấu trúc, bỏ mật độ** — đây là màn Cài đặt kỹ thuật (Admin), không phải màn vận hành; OK để dày vì ít dùng |

---

*Cần xác nhận: (a) số trục thực tế và cách nhóm (XYZU + Tap 3 trục?); (b) ngưỡng cảnh báo lệch Set–Confirm cho từng loại trục.*


---

## 7. Quyết định adoption — AM.AutoFrame (Session 46, 13/06/2026)

> Mục này do team AM.AutoFrame thêm. Spec gốc giữ nguyên ở trên. Hiện thực ở
> `AM.Modules.Motion` (MotionView/MotionViewModel/AxisVm/PointRowVm) + `AM.Hardware.Motion`.

### Áp ngay (đã code, sim "sống" đầy đủ)
- **Bảng đèn 8 tín hiệu/trục** (Alarm·+Limit·−Limit·Origin·E-Stop·Zero·In-Pos·Servo) — qua interface
  TUỲ CHỌN `IAxisDiagnostics` (xanh=đạt, đỏ=Alarm/Limit/E-Stop, xám=không active).
- **Điều khiển từng trục**: Servo ON/OFF · Home · Clear Error (清错 riêng, KHÔNG gộp Reset máy) · Move-to + tốc độ %.
- **Jog pad + inching**: nhóm trục XYZU; mode Tương đối/Tuyệt đối; STOP đỏ dừng MỌI trục (khác Stop chu trình);
  inching 3 mức (0.001/0.01/0.1) + ô tuỳ ý + nudge +/−.
- **Phản hồi servo**: following error / feedback velocity / torque / motor load (trục đang chọn).
- **Bảng điểm Set/Confirm, 2 chạm**: chạm ô = chọn 1 trục · chạm tên = chọn cả điểm · Tới/Teach ở thanh
  dưới là cú chạm thứ hai. Teach một trục chỉ cho trục đã Servo ON + Home (更新此轴). ▲ khi confirm lệch set
  > 0.05 mm. Lưu bảng điểm vào recipe (file points.json).

### Kiến trúc (non-breaking — theo tiền lệ ISafetyInput)
| Spec yêu cầu | Hiện thực |
|--------------|-----------|
| Servo/8 tín hiệu/phản hồi | Interface **tuỳ chọn** `IAxisDiagnostics` — controller MAY implement; UI cast runtime (`motion as IAxisDiagnostics`). Sim implement đầy đủ; driver thật (Gts/Advantech P/Invoke) CHƯA → UI ẩn cột tín hiệu/servo, hiện "—". KHÔNG sửa `IMotionController` (giữ 31 hardware-test xanh). |
| Set/Confirm trong điểm | `MotionPoint` thêm `SetPositions` (additive, mặc định rỗng) — delta chỉ hiện khi recipe có set-point; không bịa. `Positions` = confirm (teach thực tế). |
| Tên trục có nghĩa (AX_X_Adjust) | Hiện dùng `AX_{i}` (index). Tên có nghĩa cần `IAxisMap`/AxisMap config — hoãn (đã có JsonAxisMap, sẽ nối sau). |

### Phản biện / hoãn có chủ đích
1. **Deadman "giữ-để-chạy" liên tục**: `IMotionController` chỉ có MoveAbs/MoveRel/Stop, KHÔNG có velocity-jog
   (JogStart/JogStop). Tự dựng vòng lặp MoveRel khi giữ nút là rủi ro (queue lệnh, dừng trễ) — **không làm nửa vời**.
   Hiện: mỗi lần bấm jog/nudge = nhích đúng MỘT bước inching (an toàn, rõ ràng). Deadman thật cần bổ sung
   `IAxisJog` (velocity-mode + watchdog HAL) — hoãn.
2. **Tên IO 4 lớp + IOMap** (§2 tài liệu): thuộc màn **Cài đặt → IO mapping**, không phải màn điều khiển trục —
   tách ra, làm khi xây màn IOMap. Màn này chỉ dùng tên trục/điểm.
3. **Bảng config trục dày đặc** (Mesa ảnh 1): đúng là màn Cài đặt kỹ thuật (Admin) riêng, không nhồi vào màn vận hành.
4. Nút emoji trong mockup (💾 ✎) → sản phẩm dùng Segoe MDL2 một màu (giữ nhất quán palette v2); tạm để glyph
   trong nhãn i18n, thay bằng icon khi gom `Icons.xaml`.

### Đã chốt 2 câu hỏi cuối tài liệu
- (a) Số trục/nhóm: nhóm theo lô 4 trục (XYZU, rồi AX_4–AX_n…) — tự sinh từ `AxisCount`, không hardcode.
- (b) Ngưỡng lệch Set–Confirm: mặc định **0.05 mm** (`PointCellVm.DeltaThresholdMm`) — chỉnh theo loại trục sau.
