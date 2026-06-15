# HMI_Naming_and_Axis_Point_Model — v1.0

Quy ước đặt tên biến/IO và mô hình dữ liệu trục–điểm cho AM.AutoFrame.
Tổng hợp từ phân tích phần mềm điều khiển máy công nghiệp tham khảo.
Đi kèm: mockup `hmi_axis_detail_v1.html`.

---

## 1. Nguyên tắc gốc

1. **Tên vật lý và tên biến code là hai thứ tách biệt**, map qua bảng `IOMap` trong config.
   Tên vật lý cho kỹ thuật viên dò dây (khớp tem nhãn trên máy); tên biến cho lập trình.
2. **Trục và điểm là hai thực thể tách rời.** Điểm tham chiếu trục (`axisId`), không lưu trong trục.
   Một trục tham gia nhiều điểm; nhét điểm vào trục sẽ khiến sửa một trục đụng toàn bộ điểm.
3. **Tên có nghĩa làm khóa hiển thị, index/id chỉ nội bộ.** Tránh "Index 37" trần —
   người đọc phải tra nghĩa. Giữ tên có nghĩa kiểu "SafePoint", "AssemblyPoint".
4. **Set Pos vs Confirm Pos**: mỗi điểm lưu giá trị *đặt* (mong muốn) và
   *xác nhận* (đã teach thực tế); lệch quá ngưỡng → cảnh báo khi teach/chạy.

---

## 2. Đặt tên IO

Cấu trúc tên vật lý — bốn lớp **vị trí vật lý → địa chỉ logic → chức năng** (quy ước đặt tên vật lý 4 lớp):

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
  "address": "X017", "type": "DI", "station": "AdjPlatform", "normallyOpen": true,
  "displayName": { "vi": "Chân không đầu hút 1", "zh": "吸嘴真空1" },
  "localize": true, "rawName": "吸嘴真空1" }
```

**Hiển thị trên HMI — địa chỉ luôn đứng trước tên:** `X017 · Chân không đầu hút 1`.
- **Địa chỉ vật lý (X017) dùng font mono, KHÔNG dịch, KHÔNG đổi theo ngôn ngữ** — là mỏ neo khớp nhãn trên tủ điện. Khi dò dây, người vận hành đọc địa chỉ, bỏ qua phần chữ. Đây là thứ cứu họ khi bản dịch khó hiểu.
- **`localize: false` → giữ tên gốc một ngôn ngữ cố định** bất kể UI đang ngôn ngữ nào. Tên IO do nhà SX đặt (thường quy ước nội bộ, đôi khi tiếng Trung/Anh gốc) có thể rõ hơn bản dịch máy móc với người đã quen. `rawName` hiện kèm sau dấu `/` để người quen tên gốc vẫn nhận ra.
- **Phản biện nguyên tắc "mọi chuỗi qua ILocalizationService"**: quá tuyệt đối. Đúng hơn — *chuỗi UI hướng người dùng cuối* (nhãn nút, thông báo) bắt buộc dịch; *định danh kỹ thuật* (địa chỉ, tên biến, và tên IO khi `localize:false`) được phép giữ nguyên gốc. Bản dịch tốt nhất vẫn là một lớp giữa người và phần cứng; địa chỉ vật lý xuyên qua lớp đó.

### Bộ trạng thái IO (mockup `hmi_io_states.html`)

Phân biệt bằng **hình + màu** (an toàn mù màu). Output cần tách 3 khái niệm dễ lẫn:

| Trạng thái | Ký hiệu | Áp dụng |
|------------|---------|---------|
| OFF / chưa kích | đèn tròn xám | in/out mức 0 |
| ON / đang kích (do logic) | đèn tròn xanh | trạng thái thực tế hiện tại |
| Chờ / đang chuyển | đèn tròn vàng nhấp nháy | vừa ấn, chờ cảm biến xác nhận |
| **FORCED** | **ô vuông đỏ chữ F** + badge | bị cưỡng bức đè — KHÁC bật do logic |
| Đã ấn (momentary) | đèn xanh khi giữ | nút nhấn đang được giữ |
| Giữa hành trình | tam giác hổ phách "▲" | xi lanh 2 cảm biến đều off (nghi kẹt) |

- **Force phải có dấu hiệu riêng tuyệt đối** (ô vuông, không phải đèn tròn): output bật *do logic* và output bật *do force* là hai việc khác hẳn, lẫn lộn là nguy hiểm.
- **Cảm biến xi lanh hai đầu**: đọc cả cảm biến KẸP và NHẢ → suy ra KẸP / NHẢ / GIỮA. Cả hai off = "▲ giữa" = nghi kẹt, cảnh báo.
- **Input nút nhấn**: phân biệt chưa-ấn / đang-ấn (momentary giữ).

### Điều khiển output: set/reset thường vs Force (phương án A)

Set/reset và Force là HAI việc khác bản chất — set/reset ra lệnh nhưng logic vẫn kiểm soát (có thể ghi đè lại); Force *đóng băng* output, cắt quyền logic (quên gỡ = output chết kể cả khi sản xuất → tai nạn kinh điển). Không gộp hai cái vào một cú bấm mơ hồ.

Mô hình (mockup `hmi_io_states.html`):
- **Mặc định — chế độ thường**: mỗi dòng output là MỘT NÚT bấm-được. Màu/hình = trạng thái hiện tại; bấm = set/reset (logic vẫn kiểm soát). Output có hậu quả cần cú chạm xác nhận thứ hai. KHÔNG còn nút "Force" riêng cho từng dòng (đó là cái thừa) → bảng gọn.
- **Chế độ Force** (toggle ở đầu bảng, Admin, ngoài EXECUTE, audit): bật lên → nền bảng đỏ cảnh báo, bấm dòng = ĐÓNG BĂNG output. Dòng đã force hiện ô vuông đỏ chữ F + badge + bấm để gỡ. Bộ đếm "đang force N IO" luôn hiển thị + nhắc gỡ trước khi rời màn/chạy máy.
- Ranh giới hiển nhiên: ở chế độ Force thì MỌI cú bấm là force; ở chế độ thường thì MỌI cú bấm là set/reset. Người dùng luôn biết mình đang "bật tạm" hay "đóng băng" — không nhầm.
- *(Trường phái an-toàn-tối-đa: bỏ luôn set/reset thường, IO chỉ để xem trừ khi vào Force mode. Máy ưu tiên an toàn hơn linh hoạt có thể chọn cấu hình này. Dự án dùng phương án A — có set/reset thường.)*

> **✅ Đã triển khai (S59)** — không còn chỉ là mockup. `IIoModule` có `ForceDoAsync`/`UnforceDoAsync`/`IsDoForced`/
> `ForcedOutputs`/`ReadAllDoAsync`; kênh bị force thì `WriteDiAsync` (kể cả logic máy qua `WriteDoByTagAsync`)
> **bị bỏ qua** → đúng nghĩa cắt logic, hiện thực ở cả `SimulatedIoModule` lẫn `AdvantechAdamIoModule` (software-layer).
> UI `AM.Modules.IoMonitor` (sub-tab Giám sát I/O): set/reset = Engineer + máy dừng (guard R3); toggle **Chế độ Force** =
> Administrator + máy dừng; force = chạm-2-bước; badge "F" + bộ đếm "đang FORCE N IO"; mọi thao tác audit.
> Mất quyền/máy chạy khi đang ở Force mode → tự thoát chế độ (force trên HAL vẫn giữ tới khi gỡ thủ công).
>
> **✅ S60 — layout + trạng thái phong phú**: IOMap mở rộng (`IIoTagMap` +`IoChannelDescriptor`/`IoCylinderDescriptor` +
> `Describe*`/`DiChannels`/`DoChannels`/`Cylinders`; `JsonIoTagMap` nhận schema mảng có địa chỉ/tên đa ngữ/rawName/localize +
> seed `io.map.json`). Màn IO: danh sách 2 cột "địa chỉ · tên có nghĩa (+rawName)" + ô lọc; chỉ báo đủ bộ
> (Off/On đèn · Pending vàng nhấp nháy theo `confirmDi` · **Forced ô vuông đỏ F**) + nhóm **Xi lanh** suy KẸP/NHẢ/**▲ giữa**
> từ cặp DI. *(Lệch nhẹ mockup: gom xi lanh thành nhóm riêng thay vì badge trên 1 DI thô — rõ hơn.)*

---

## 3. Mô hình dữ liệu Trục

Một trục = định nghĩa phần cứng (cấu trúc bảng cấu hình trục công nghiệp):

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

Tín hiệu trục hiển thị (bảng đèn 8 cột, chuẩn công nghiệp):
`Alarm(报警) · +Limit(正限位) · −Limit(负限位) · Origin(原点) · EStop(急停) · Zero(零位) · InPosition(到位) · Servo/Excited(励磁)`.

---

## 4. Mô hình dữ liệu Điểm

Điểm = một vị trí có toạ độ trên NHIỀU trục:

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
| Servo ON/OFF | mỗi trục | hàng ngang mỗi trục |
| Home / 原点 | mỗi trục + tất cả | giữ 1 s; "tất cả" theo thứ tự an toàn |
| **Clear Error / 清错** | mỗi trục + tất cả | xóa servo alarm RIÊNG, KHÔNG gộp vào Reset máy |
| Di chuyển tới đích + tốc độ % | mỗi trục | tốc độ giới hạn an toàn |
| Jog deadman (giữ-để-chạy) | trục đang chọn | watchdog HAL, STOP dừng mọi motion |
| Inching 3 mức + ô nhập tùy ý | — | tinh/vừa/thô (0.001/0.01/0.1) + ô nhập số cho bước đặc biệt + nút +/− nhích từng nấc. KHÔNG để 5 mức (mắt quét lâu) |
| Nhóm trục XYZU / Tap | — | chuyển nhóm khi máy >4 trục (máy có 7 trục) |
| Tương đối / Tuyệt đối | jog mode | chuẩn jog công nghiệp |
| **Chọn ô → Tới / Teach** | bảng điểm | chạm ô toạ độ = chọn 1 trục; chạm tên điểm = chọn cả điểm. CHỌN không chạy ngay; một cặp Tới/Teach duy nhất ở thanh dưới (hiện rõ phạm vi) là cú chạm thứ hai xác nhận. Thay cho 2 nút/điểm — tiết kiệm cả cột, hiển thị được 20–50 điểm |
| Lưu bảng điểm vào recipe | — | 保存点位 |
| Dừng chuyển động | toàn cục | khác Stop chu trình máy |

"Dừng chuyển động" (STOP đỏ giữa jog pad) dừng jog/move-to-point, KHÁC Stop của action bar (dừng chu trình).

---

## 6. Quyết định pattern (lấy gì, bỏ gì)

| Pattern tham khảo | Quyết định |
|-------------------|-----------|
| Tên IO 4 lớp (`1.17_2-X01_…`) | **Lấy** — rõ vị trí vật lý → logic → chức năng |
| Địa chỉ trần `X000` (không ngữ cảnh) | **Bỏ** — thiếu ngữ cảnh, khó dò |
| Set/Confirm Pos | **Lấy** — pattern an toàn chống teach sai |
| Index số trần làm khóa điểm | **Bỏ** — dùng tên có nghĩa, index chỉ id nội bộ |
| Bảng đèn 8 tín hiệu/trục | **Lấy** — chuẩn công nghiệp |
| Clear Error per-axis (清错) | **Lấy** — mockup cũ thiếu, đã bổ sung |
| Update-this-axis (更新此轴) | **Lấy** — teach một trục giữ trục khác |
| Nút tròn + gradient | **Bỏ** — ISA-101, dùng chữ nhật phẳng |
| Bảng config trục dày đặc | **Lấy cấu trúc, bỏ mật độ** — đây là màn Cài đặt kỹ thuật (Admin), không phải màn vận hành; OK để dày vì ít dùng |

---

## 7. Layout thích ứng theo quy mô (4 trục → 20 trục)

Không thiết kế layout cố định; cùng một cấu trúc dữ liệu render khác nhau theo số lượng khai báo. Mockup: `hmi_adaptive_layout.html`.

### 7.0 Nhất quán ba tầng (giải mâu thuẫn "thích ứng" vs "vị trí nhất quán")

Layout thích ứng KHÔNG phá nguyên tắc nhất quán, vì nhất quán được phân theo ba tầng tách biệt:

**Tầng 1 — Nhất quán vị trí TUYỆT ĐỐI (cho Operator, hành động phản xạ).**
Persistent Frame (header, nav, banner, action bar, thanh kết nối) giống hệt nhau trên MỌI máy, MỌI quy mô. Start/Stop/ACK/E-stop luôn ở đúng vị trí — đây là hành động tần suất cao, áp lực thời gian, cần vị trí cố định cứu phần nghìn giây và tránh bấm nhầm. Layout thích ứng KHÔNG bao giờ chạm tới tầng này. Operator cũng không mở màn Vận hành tay nên không gặp layout thích ứng.

**Tầng 2 — Nhất quán MÔ HÌNH (cho Engineer, hành động có chủ đích).**
Khu thao tác kỹ thuật (Vận hành tay) thay đổi *khung chứa* theo quy mô nhưng giữ bất biến:
- **Thứ tự sub-tab** không đổi: Thao tác trạm → Điều khiển trục → Bảng điểm → Override.
- **Quy luật điều hướng** không đổi: chọn-phạm-vi một phía → nội-dung phía kia. Phẳng/nút-ngang/sidebar khác *hình* nhưng cùng *quy luật* "chọn trạm rồi nội dung hiện".
- **Đơn vị nội dung lặp lại** không đổi: một hàng trục luôn là (tên trái · vị trí phải · ON/Home/Clear giữa) dù máy 4 hay 20 trục. Layout nút bên trong một trạm giống hệt mọi máy.
- Cái co giãn là *khung* (số trạm, số trục); cái cố định là *đơn vị* và *thứ tự*. Engineer làm việc có cân nhắc, không phản xạ → nhất quán mô hình là đủ.

**Tầng 3 — Van ép nhất quán tuyệt đối (`layoutHint`).**
Nhà máy có cả máy nhỏ lẫn lớn, muốn kỹ sư thấy giao diện đồng nhất, có thể ép một kiểu render cho mọi máy (vd `layoutHint: "sidebar"` kể cả máy 4 trục — chấp nhận hơi trống để đổi lấy nhất quán tuyệt đối). Mặc định `auto` tối ưu từng máy; ép tay khi nhà máy coi nhất quán > tối ưu không gian.

Lý do KHÔNG ép một kiểu cho tất cả làm mặc định: ép sidebar → máy 4 trục lãng phí ~150 px cho 1 trạm, trống vô lý; ép phẳng → máy 20 trục thành danh sách trộn lẫn không cuộn nổi. Nhất quán-tuyệt-đối ở tầng 2 sẽ *phá* tính dùng được; nên mặc định là thích ứng-có-quy-luật, van ép để tùy chọn.

### 7.1 Mô hình dữ liệu &amp; ngưỡng render

**Mô hình phân cấp chung (mọi máy):**
```
Machine
 └─ Station[] (Loader, Adjust, Tap…) → Axes[] + Cylinders[] + IO[]
 └─ SharedAxes[] (trục station:null — gantry dùng chung)
```

**Engine chọn cách render (áp nhất quán cho cả 3 sub-tab Thao tác/Trục/Bảng điểm):**

| Quy mô | Điều kiện | Render |
|--------|-----------|--------|
| Nhỏ | stationCount ≤ 1, axes ≤ 6 | FLAT — bỏ lớp trạm, danh sách phẳng |
| Vừa | stationCount ≤ 4 và axes ≤ 12 | HORIZONTAL TABS — nút trạm ngang đầu pane |
| Lớn | ≥ 5 trạm hoặc > 12 trục | SIDEBAR trái |

`layoutHint: auto|flat|tabs|sidebar` — mặc định auto, cho ép tay khi cần.

**Trục thuộc trạm hay chung — config quyết định**, không cố định:
- `"station": "Adjust"` → trục hiện trong nhóm trạm đó.
- `"station": null` → gom vào nhóm "Trục chung" (gantry phục vụ nhiều trạm).
- Máy die-bonder (mỗi chuck table có cụm trục riêng) → khai station từng trục, sidebar tự gom. Máy gantry XY dùng chung → khai null.

**Trạm KHÔNG lên thanh nav chính.** Nav phân theo *chức năng* (Home, Vision, Vận hành tay…); trạm là *vị trí vật lý*, thuộc bên trong màn thao tác. Trộn hai trục phân loại vào một thanh gây rối.

Người viết máy mới chỉ khai station/axes/io; engine chọn layout. Máy 4 trục tự ra giao diện gọn, máy 20 trục tự ra sidebar — không sửa code HMI.


---

> **Adoption AM.AutoFrame**: phản biện + quyết định "build gì / map gì / hoãn gì" cho codebase thật
> nằm tập trung ở `docs/HMI_Master_Index.md §11` (nguồn DUY NHẤT). Đọc đó trước khi hiện thực màn này.
