# 0008 — Tách process Vision (VisionPro chạy ngoài .NET Framework, trả VisionResult qua IPC)

**Bối cảnh.** Chốt dùng Cognex VisionPro (ADR [0007](0007-vision-module-design.md)). Bản cài: **9.x (59.2.0.0) —
.NET Framework 4.x**; managed API ở `libs/Vision/Cognex/x64/ReferencedAssemblies`, native `x64/bin`, license dongle.
App AM.AutoFrame là **.NET 9 / WPF**.

**Phát hiện thực nghiệm (6 spike, app net9 throwaway — không đụng repo):**

| Khả năng | net9 in-process | Kết quả |
|---|---|---|
| Nạp managed `Cognex.VisionPro.*` | ✅ | `LOADED OK 59.2.0.0` — KHÔNG mixed-mode như lo ban đầu; pure-IL x64, CoreCLR nạp qua shim FW |
| Native interop (cấp `CogImage8Grey`) | ✅ | `64x48` khi trỏ native path vào `x64/bin` + resolve managed từ `ReferencedAssemblies` |
| **Nạp/lưu `.vpp` (`CogSerializer`)** | ❌ | BinaryFormatter (net9 đã gỡ) → shim `System.Runtime.Serialization.Formatters`+cờ được → thiếu `System.Drawing.Common` → thêm được → **`SEHException` native** (STA cũng không cứu) |

→ **Nạp `.vpp` in-process trên .NET 9 không khả thi/không an toàn.** `.vpp` là cốt lõi workflow QuickBuild. Một đường
serialization **crash tầng native** KHÔNG được đưa vào phần mềm điều khiển máy (R01 — an toàn).

> Đính chính: khẳng định sớm "mixed-mode → net9 không load được" là **sai** (spike chứng minh nó load + native chạy).
> Chặn thật là `.vpp`/`CogSerializer` (BinaryFormatter → SEHException native), không phải tầng nạp assembly.

## Phương án
- **A — In-process net9 (reference trực tiếp).** *−* `.vpp` SEHException; Cognex KHÔNG hỗ trợ .NET Core cho 9.x;
  fragile (re-enable BinaryFormatter, dep ngầm System.Drawing). **LOẠI.**
- **B — Process VisionPro tách, .NET Framework 4.8, headless host + Cognex QuickBuild authoring. ✅ CHỌN (nền).**
  Host nạp `.vpp` + RunJob; authoring `.vpp` bằng QuickBuild (app Cognex sẵn có). Main net9 nói chuyện qua IPC.
- **C — App WinForms FW4.8 đầy đủ** (nhúng `CogToolBlockEditV2`/`CogRecordDisplay` làm teach/live in-house).
  ✅ tiến hoá khi cần UI tích hợp; cùng ranh giới IPC với B.
- **D — Nâng VisionPro bản hỗ trợ .NET (Core).** In-process sạch, khỏi IPC. Phụ thuộc license/phiên bản — chưa có; để ngỏ.

## Phương án chọn
Tách Vision thành **process .NET Framework 4.8 riêng** chạy TOÀN BỘ VisionPro (acquire + `.vpp` + calibration), trả
**kết quả trung lập gọn** (`VisionResult`) cho main net9 qua **IPC**. Bắt đầu **B** (headless host + QuickBuild), nâng
**C** (WinForms UI) khi cần. Nếu sau có **D** thì gập về in-process được vì ranh giới đã trung lập.

## Lý do
- **Bằng chứng:** `.vpp` chỉ chạy trong FW4.8 (nơi BinaryFormatter + native + CogSerializer hoạt động, Cognex hỗ trợ chính thức).
- **An toàn (R01):** vision native crash (SEH…) cách ly process, không kéo sập master controller.
- **VisionPro là WinForms-native:** dùng thẳng editor của nó (QuickBuild / `CogToolBlockEditV2` / `CogRecordDisplay`) —
  khỏi tự viết lại teach/ROI, khỏi `WindowsFormsHost`.
- **Vendor-isolation đã có sẵn** (ADR 0001 / 0007 Quyết định 1): ranh giới là `VisionResult`/`FrameData` trung lập.

## Hợp đồng ranh giới ("app vision chạy hết, chỉ trả tham số")
- **Payload** = `VisionResult` + `correlationId`: `pass(OK/NG), score, x, y, angleDeg,
  checks[]{name,value,unit,low,high,passed}, jobName, timestamp, correlationId`.
  `x/y` là **mm đã hiệu chuẩn** trong frame thống nhất (app vision tính px→mm bằng calibration VisionPro).
- **Trigger:** WorkStation Step bắn lệnh inspect qua IPC, chờ kết quả có **timeout (CS03)**; hoặc vision tự trigger
  sensor rồi push. `correlationId` khớp kết quả ↔ part/SN trong pipeline.
- **Camera:** app vision **giữ acquisition + calibration**. HMI muốn xem ảnh → **kênh hiển thị phụ** (thumbnail/ảnh NG
  có overlay, on-demand) — KHÔNG nhét ảnh vào đường control.
- **Vòng đời:** main app spawn + heartbeat + tự reconnect; vision chết → **alarm dải 20xxx**.
- **Transport:** JSON over TCP/named-pipe (kết quả nhỏ, dễ debug). Lệnh: SelectJob/LoadRecipe · Trigger/Inspect ·
  (Result push) · Status/Heartbeat.

## Hệ quả / đánh đổi còn lại
- `AM.Hardware.Vision`: thêm `VisionProProcessor : IVisionProcessor` = **IPC client** (KHÔNG reference Cognex).
  Sim giữ nguyên cho dev/CI.
- Project mới `AM.Vision.VisionProHost` (**net48**): ref `ReferencedAssemblies`, native path `x64/bin`, dongle; nạp
  `.vpp`, map kết quả Cognex → `VisionResult`. Build riêng, KHÔNG vào ràng buộc net9 của solution chính.
- Main app net9 + mọi module **không reference Cognex** — vendor-isolation "trả lãi".
- **V3** (ROI/threshold/calib editor custom) **bị thay** bằng editor VisionPro → giữ làm scaffold sim hoặc bỏ sau.
  **V1/V2** (lưới đo/stats/trend bind `VisionResult.Checks`) **tái dùng** làm UI hiển thị kết quả từ IPC (đổi nguồn sim→IPC).
- Binaries Cognex (license) KHÔNG vào git → `.gitignore libs/Vision/Cognex/`. Deploy 2 runtime (FW4.8 + net9) + dongle.
- **Việc tiếp:** spike host **net48** nạp `.vpp` thật (round-trip CogSerializer mà net9 fail) để chốt FW4.8 chạy; rồi
  thiết kế hợp đồng IPC chi tiết + alarm codes 20xxx.

## Liên kết
[0001](0001-am-autoframe-design-decisions.md) (HAL vendor-isolation, simulation parity) ·
[0006](0006-vision-live-view.md) / [0007](0007-vision-module-design.md) (Vision layering, Quyết định 1) ·
R01 (safety) · R02/R03 (layer/interface) · CS03 (timeout). Phiên: PROJECT_STATUS / CHANGELOG Session 72.
