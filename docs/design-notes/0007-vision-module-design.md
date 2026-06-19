# 0007 — Thiết kế module Vision (phản biện 2 tài liệu tham khảo + adoption)

**Bối cảnh.** Vision là UI module DUY NHẤT còn thiếu. Trước khi build, chủ dự án đưa 2 tài liệu tham khảo để
"lấy cái hay, bỏ cái không hợp":

1. **`hmi_vision_station_v1_1.html`** — mockup HMI một trạm vision (dark theme, full-chrome, tab theo trạm SMT).
2. **SECPC_Vision** — skeleton 14 thành phần của một phần mềm vision inspection **Cognex VisionPro / C# WinForms**
   đang chạy thật (StaticClass/FObject singleton, CogToolBlock, iStep_hand state machine, INI/CSV config…).

Note này chốt: (1) Vision logic nằm ở tầng nào, (2) lấy/bỏ gì từ mỗi tài liệu, (3) mô hình Vision recipe,
(4) mở rộng `ILightController`. Nối tiếp [0006](0006-vision-live-view.md) (đã có live-view sim + converter).

> **Nguyên tắc lọc**: 2 tài liệu là **checklist năng lực**, KHÔNG phải blueprint để copy. Mockup va chạm tầng
> *trình bày* (Persistent Frame, light theme, ISA-101, cảm ứng); SECPC va chạm tầng *kiến trúc* (DI, HAL
> vendor-isolation, ISA-88, WPF/MVVM). Giữ *cái cần làm*, bỏ *cách họ làm*.

---

## Quyết định 1 — Vision logic nằm ở tầng nào (quan trọng nhất)

**Bối cảnh.** SECPC gộp tất cả vào một app: camera + calibrate + tool-run + IO + conveyor + barcode + đèn +
lưu ảnh + flow + PLC/MES. Cám dỗ là bê nguyên khối đó vào `AM.Modules.Vision`.

### Phương án
- **A — Monolith "Vision Station" (như SECPC):** một module ôm trọn flow máy + IO + vision.
  *+* giống tài liệu, ít file. *−* phá R02 (Module không gọi hardware/IO/flow trực tiếp), không tái dùng, trộn
  capability với machine-specific. **Loại.**
- **B — Tách 4 tầng theo kiến trúc dự án ✅ CHỌN:**
  ```
  AM.Hardware.Vision   → driver VisionPro/Cognex (bọc SDK) → trả FrameData/VisionResult (DLL ở libs/)
  AM.Modules.Vision    → UI: live view · Result · VisionTeachView (ROI/threshold/calib) — chỉ ICameraDevice/IVisionProcessor
  WorkStation Steps/Mechanism → flow: đọc barcode → kích Inspect → đọc conveyor/stopper → xuất PLC
  AM.Services          → lưu/xoá ảnh, export data, đẩy MES
  ```
  *+* đúng R02/R03, vision thành **capability tái dùng**, flow máy ở đúng chỗ. *−* nhiều project hơn (đã là chuẩn dự án).

**Lý do.** Trong AM.AutoFrame vision là **một capability của máy**, không phải toàn bộ máy. Flow đọc-barcode →
inspect → xuất-PLC là **machine-specific**, thuộc WorkStation; module Vision chỉ lo *camera + teach + hiển thị kết quả*.

**Hệ quả.** Module Vision KHÔNG được tham chiếu IO/conveyor/PLC. Khi dựng máy vision thật, flow viết bằng `IStep`
(async, timeout CS03) trong WorkStation — KHÔNG phải `iStep_hand[]`/Thread/Stopwatch của SECPC.

---

## Quyết định 2 — Adoption từ mockup HTML (tầng trình bày)

### Bỏ (không phù hợp)
| Nội dung mockup | Vì sao bỏ |
|---|---|
| Zone 1 Chrome / Zone 2 Tab-theo-trạm / Zone 3 Run-Stop / Zone 5 Status / Zone 6 Alarm | **Trùng Persistent Frame** — Shell đã sở hữu (Template §2). Module chỉ nạp vào vùng làm việc (vùng 4). Tab phải theo *chức năng*, không theo *trạm* (Master Index §6) |
| Dark theme toàn cục `#1A1D23` | Dự án **Light theme** (Template §5). *Ngoại lệ giữ:* riêng vùng ảnh nền tối `#1F1F1F` |
| Glow/box-shadow/gradient, `scanline`, `pulse-dot`, `pass-flash` | ISA-101 phẳng + "yên tĩnh khi bình thường". Nhấp nháy CHỈ cho alarm chưa ACK (EEMUA 201) |
| Màu bão hòa thường trực (`#22C55E`/`#EF4444`) | Dùng token dịu: OK `#1E7E46`, NG `#C0392B` (Template §5) |
| Kích thước desktop (nút 20–28px, dòng 24px) | Cảm ứng + găng: nút ≥48px, dòng ≥44px, ACK ≥40px (Template §8) |
| Infobar GPU%/Temp/Proc-ms | Số liệu chẩn đoán → tab Diagnostics, không lên màn vận hành |
| Icon-only (⊕⊖⊞) + emoji | Phải Segoe MDL2 một màu; emoji chỉ ở mockup (Template §5) |
| Lặp verdict 3 chỗ (badge ảnh + verdict block + meas grid) | Nhiễu — một sự thật một chỗ |

### Giữ (chuyển sang light theme + phóng to cảm ứng)
- Bố cục **ảnh lớn trái + tab phải** (Result / Log / Tool) — khớp tinh thần VisionTeachView v1.1.
- Camera toolbar: Overlay · ROI · Caliper · Histogram · Freeze · Zoom/Fit (đổi MDL2).
- Result tab: verdict → đo từng ROI kèm limit → stats ca → trend OK/NG.
- Log NG table (SN · time · defect · ROI · thumbnail) — chỉ dòng NG tô màu.
- **Tool tab = VisionTeachView, gate Engineer** (camera settings / ROI / threshold / output).
- ROI overlay trên ảnh → WPF **Adorner/Canvas** trên `Image` (KHÔNG `CogRecordDisplay`).

---

## Quyết định 3 — Adoption từ SECPC (tầng kiến trúc)

### Bỏ (cách hiện thực)
| SECPC | Vì sao bỏ | Thay bằng |
|---|---|---|
| `StaticClass` + `FObject` (singleton toàn cục) | Phản đề R02/R03 | Service đăng ký DI, sau interface |
| Type Cognex rò mọi tầng (`CogImage8Grey`, `CogToolBlock`, `CogRecordDisplay`) | Phá HAL vendor-isolation (§0) | Bọc trong `AM.Hardware.Vision`; tầng trên chỉ thấy `FrameData`/`VisionResult` |
| `iStep_hand[0..2]` + Thread + Stopwatch busy-wait | Phá R04 + đã có ISA-88 + `IStep` | Async Steps, timeout CS03 (`CancelAfter`) |
| INI/CSV config | Dự án dùng JSON + EF/SQLite | `JsonIoTagMap`, `machine.config.json`, `RecipeBase` |
| WinForms (`Invoke`, `FormMain`) | Dự án WPF/MVVM | View/VM + `SynchronizationContext` |
| `Tool视觉Run` tên lẫn ngôn ngữ | CA1707 | `RunInspectionAsync` |

### Giữ (năng lực — dưới dạng *yêu cầu*)
1. **Xác nhận abstraction đúng**: split acquire (`ICameraDevice`) ↔ tool/job (`IVisionProcessor`) khớp camera↔calibrate↔tool-run của SECPC.
2. **Mô hình Vision recipe** (`FunctionPRMs[]/CalibPRMs[]/ToolBlocks[]` theo mã SP) → Quyết định 4.
3. **Light control per-channel + delay** (LightData{CH, intensity, delay}) → Quyết định 5.
4. **Calibration px→mm + lịch sử offset** → VisionTeachView.
5. **Lưu ảnh OK/NG** thư mục theo ngày + auto-purge N ngày → service bảo trì.
6. **Năng lực khác**: multi-camera theo SN, barcode/QR gating, TCP command PLC/MES — map vào interface/service sẵn có (KHÔNG vào module Vision UI).

---

## Quyết định 4 — Mô hình Vision recipe (model only, chưa build engine)

**Bối cảnh.** SECPC: mỗi mã SP có danh sách function (mỗi function = 1 inspection step) + calib params + mảng ToolBlock.
Dự án đã có `RecipeBase` (đa hình, validate attribute-driven).

**Chọn:** `VisionRecipe : RecipeBase` chứa `IReadOnlyList<VisionJobSpec>` (id, jobPath .vpp, cameraId, lightId,
ROI list, ngưỡng) + tham chiếu calibration. KHÔNG nhúng type Cognex — chỉ đường dẫn job + tham số trung lập.

**Đánh đổi.** Chưa build "engine chạy nhiều job song song" (SECPC `ThreadCameraRun`) — chỉ định nghĩa *model* để
VisionTeachView đọc/sửa. Engine để khi dựng máy vision thật (tránh abstraction chưa cần — CLAUDE.md "Simple").

---

## Quyết định 5 — Mở rộng `ILightController` (gap thật)

**Bối cảnh.** `ICameraDevice.SetLightAsync(bool)` quá nhị phân. Vision thật cần per-channel intensity + delay strobe
trước grab (SECPC LightData; khớp field "Strobe sync / Trigger delay" của mockup).

### Phương án
- **A — nhồi thêm tham số vào `SetLightAsync`:** *−* phá call site cũ, trộn vai trò camera vs đèn. Loại.
- **B — mở rộng `ILightController` riêng ✅ CHỌN:** thêm `SetChannelAsync(int ch, int intensity, ct)` +
  (tuỳ chọn) `StrobeAsync(channel, intensity, delayMs, ct)`. Camera giữ `SetLightAsync(bool)` cho on/off đơn giản;
  máy cần đèn phức tạp resolve `ILightController` qua `IHardwareManagerService`.
  *+* đúng "interface theo domain", sim implement được, không phá call site. *−* 1 interface nở thêm.

**Hệ quả.** Cần `SimulatedLightController` (đã có sim TowerLight, thêm channel). Alarm dải 30xxx (IO) hoặc 20xxx (vision) tuỳ gắn.

---

## Kế hoạch hiện thực (roadmap — build tăng dần, mỗi bước 1 commit)

> Build **giao diện trước** (light theme, touch-sized, dùng `ICameraDevice` sẵn có) rồi mới mở rộng HAL.
> KHÔNG build engine multi-job/flow máy ở lượt này.

- **V1 — Nâng `VisionView` (work-area, không chrome):** layout ảnh trái + tab phải (Result/Log/Tool). Light theme,
  nút ≥48px, dòng ≥44px. Live-view (đã có 0006) + Grab/Inspect/Light/Calibrate (đã có VM). Result tab bind
  `VisionResult` thật. **Done = chạy Shell→tab Vision: ảnh sống, Inspect ra verdict, không vi phạm Persistent Frame.**
- **V2 — Result/Log tab đầy đủ:** verdict + đo từng ROI (cần `VisionResult` mang measurements) + stats ca + bảng NG.
  (Có thể cần mở rộng `VisionResult` thêm `Measurements[]`.)
- **V3 — VisionTeachView (tab Tool, gate Engineer) ✅ Session 70:** ROI editor (Canvas + `Thumb` kéo/đổi cỡ) +
  ngưỡng từng ROI + calib px→mm (form + lịch sử). Save → **`VisionTeachConfig` JSON nhẹ** (KHÔNG `VisionRecipe` —
  xem "Quyết định V3" bên dưới). *Hoãn:* camera settings + output config (chưa cần khi chưa có engine).
- **V4 — `ILightController` per-channel** (Quyết định 5) + `SimulatedLightController` + test.
- **V5 — `VisionRecipe` model** (Quyết định 4) + validate attribute-driven + test.

Mỗi bước: 0 warning (TreatWarningsAsErrors), test xanh, theo workflow `am-commit.sh` + điền hash.

---

## Quyết định V3 (Session 70) — đã hiện thực

Build với 3 fork đã chốt cùng chủ dự án (giới thiệu phương án + đánh đổi trước khi chọn):

| Fork | Chọn | Vì sao (đánh đổi) |
|---|---|---|
| **Lưu cấu hình** | `VisionTeachConfig` JSON nhẹ qua `IVisionTeachStore` (trong module) | "Save → VisionRecipe" của roadmap chỉ khả thi khi V5 có `VisionRecipe:RecipeBase`. Chọn model trung lập nhẹ (ROI + ngưỡng + calib) → Save có nghĩa ngay + test round-trip được; **hoãn** ràng RecipeBase. *Bỏ:* kéo V5 lên (nặng, ràng trước khi có engine) · chỉ-bộ-nhớ (mất khi đổi tab). |
| **Sửa ROI** | Kéo-thả/đổi cỡ trên ảnh (Canvas + `Thumb`) | Đúng tinh thần "ROI editor" + cảm ứng. `Thumb` nằm trong `Viewbox` Uniform → **delta đã ở không gian pixel ảnh** (1 đơn vị Canvas = 1 px), không cần chia scale. *Bỏ:* chỉ-nhập-số (kém trực quan). |
| **Hiệu chuẩn** | Form (mm thật + khoảng pixel → mm/px) + lịch sử | Đơn giản, không phụ thuộc thao tác vẽ. Tách `CalibrationMath` thuần để unit-test. *Bỏ:* vẽ đường trên ảnh (nhiều việc, để sau). |

**Chốt thêm / hệ quả:**
- **Nơi đặt model + store:** trong `AM.Modules.Vision` (cohesive, "ở trong module Vision"). **V5** sẽ promote model
  trung lập này lên Core khi `VisionRecipe` chính thức (gói/tham chiếu `VisionTeachConfig`).
- **KHÔNG thêm method hợp đồng phần cứng** — `ICameraDevice`/`IVisionProcessor` giữ nguyên; V3 là *authoring*,
  engine vẫn hoãn (Quyết định 1). ROI/ngưỡng khớp hình dạng `VisionMeasurement` đã có.
- **Bố cục:** `VisionTeachView` là UserControl **phủ toàn vùng làm việc** khi Engineer mở tab Công cụ (ẩn bố cục
  thường qua `MainAreaVisible`); nút ✕ quay về tab Kết quả. Operator vào tab Công cụ chỉ thấy thông báo cần Engineer.
- **Gate Engineer 2 lớp:** overlay chỉ hiện khi `CanEditTool` (View) + mọi lệnh ghi kiểm tra `EnsureEngineer` (VM).
- **Test:** project mới `AM.Modules.Vision.Tests` — round-trip `VisionTeachStore` (xác nhận STJ round-trip
  `IReadOnlyList<T>{get;init;}`) + `CalibrationMath`. Đặt ở module để giữ cohesive (không đụng Core/Services).

---

## Hệ quả / đánh đổi còn lại
- `VisionResult` hiện chỉ có score + X/Y/Angle; Result tab per-ROI cần `Measurements[]` → quyết định khi tới V2.
- Overlay ROI là WPF Adorner — chưa có; dựng ở V3 (không dùng SVG như mockup, không `CogRecordDisplay`).
- Driver Cognex thật cần DLL VisionPro (licensed) ở `libs/` — ngoài phạm vi sim; chỉ implement interface khi có máy.
- Flow máy vision (barcode/conveyor/PLC) KHÔNG thuộc note này — thuộc WorkStation khi dựng máy thật.

## Liên kết
- Nền: [0001](0001-am-autoframe-design-decisions.md) (HAL vendor-isolation, simulation parity, R02/R03) ·
  [0006](0006-vision-live-view.md) (live-view sim + converter, R-UI).
- Chuẩn UI: `docs/HMI_UI_Architecture_Template_v2.md` (§2 vùng, §5 màu, §8 cảm ứng) ·
  `docs/HMI_Master_Index.md` (§2 bất biến, §6 trạm không lên nav).
- Tài liệu nguồn: `hmi_vision_station_v1_1.html`, SECPC_Vision skeleton (tham khảo, không vào repo).
