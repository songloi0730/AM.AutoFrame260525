# ROADMAP hoàn thiện AM.AutoFrame

> **Ngày đánh giá:** 2026-07-04 (Session 79) · **Trạng thái nền:** 28 projects, 258 tests pass, 0 warning.
> **Phạm vi:** đánh giá toàn diện giao diện / chức năng / an toàn / bảo mật / hiệu chỉnh và kế hoạch
> thực hiện chi tiết. **Vision NGOÀI PHẠM VI** (làm app riêng — chỉ giữ hợp đồng tích hợp, xem §6).
>
> **Cách dùng:** mỗi phiên làm việc chọn một hạng mục trong §3 theo thứ tự ưu tiên, làm trọn theo DoD
> (không làm nửa vời — nguyên tắc adoption §C). Mục nào cần chủ dự án chốt trước → §5.
> Gap đánh dấu **[✓code]** = đã kiểm chứng trực tiếp trong source; còn lại theo tài liệu/TODO đã ghi.

---

## §1. Hiện trạng — cái đã có (điểm tựa)

| Trục | Đã có |
|------|-------|
| **Giao diện** | Shell v3 (4 vùng, chrome 168px, kiosk config-driven, banner prompt 3 nút) · Home v2.1 (card KQ gần nhất, KPI màu-khi-có-nghĩa, empty state) · 12 module Prism-style tự sinh nav từ `[ModuleNavigation]` · i18n vi/en/zh đổi runtime · palette ISA-101 |
| **Chức năng** | 3-tier + ISA-88 (8 state, 55 test) · **AM.Core.Sequencing** (engine khai báo, coverage 92.7%) · máy mẫu DemoPickPlace end-to-end trên sim (4 kịch bản nghiệm thu) · HAL 16 interface + driver thật (GTS/Advantech/Inovance/Mitsubishi/Siemens/ADAM/Keyence/Cognex-TCP) + sim đầy đủ · Recipe/Parameter/PointTable/AxisMap config-driven |
| **An toàn** | GuardService 3 tầng (state→role→điều kiện) + HardwareSignalBus event-push · interlock Start theo ISafetyInput · hold-to-confirm/2-chạm/2-bước+đếm-ngược theo mức rủi ro R0–R3 · quy tắc Abort-giữ-vacuum có test · tower light tự lái |
| **Bảo mật** | RBAC 5 cấp (Operator→SuperUser) · BCrypt password · CRUD user + bất biến last-admin · AuditService ghi thao tác guard |
| **Hạ tầng** | CPM 28 projects · TreatWarningsAsErrors + AnalysisMode=All · CI build+test · Serilog retention theo ngày · 11 ADR |

---

## §2. Kết quả rà soát — GAP theo trục

### A. An toàn (nghiêm trọng nhất — làm trước)

| # | Gap | Bằng chứng |
|---|-----|-----------|
| A1 | **E-Stop không đổi state machine**: `BaseMasterController.EmergencyStop()` chỉ EmergencyStop từng station + hủy CTS — KHÔNG fire trigger `Error`, không raise alarm 70001. Watchdog gọi khi mất kết nối → máy đã dừng khẩn nhưng UI vẫn hiện "Đang chạy"/badge xanh, operator không biết vì sao | **[✓code]** `BaseMasterController.cs` — không có `FireTrigger` trong `EmergencyStop` |
| A2 | **Nút vật lý Start/Stop/Reset chưa wire**: `DI.Btn.*` chỉ là hằng số trong IoMap, không ai đọc — IPC màn cảm ứng vẫn cần nút cứng theo thói quen vận hành | **[✓code]** grep `DI.Btn` chỉ ra định nghĩa |
| A3 | Guard tầng 3 mới có nguồn `Safety.*` — tín hiệu IO/trục (vị trí Z, chân không) chưa publish lên `HardwareSignalBus` → guard hình học (cấm XY khi Z thấp) chưa hoạt động thật | adoption §C1 ghi rõ "publish thêm khi §6.3 cần" |
| A4 | Jog giữ-để-chạy (velocity-mode + deadman watchdog >200ms→dừng) chưa có — jog hiện là inching từng bước | CLAUDE.md: "chưa có IAxisGroup, IAxisJog" |
| A5 | Station init phát hiện liệu sót đang TỰ thoát (blow-off) — bản đầy đủ phải HỎI operator (lấy tay / máy tự thoát) qua `IOperatorPrompt` (contract có rồi, chưa có UI adapter cho init) | PickStation comment + ADR 0011 §4.2 |
| A6 | `IResumeVerifiable` (resume-check chống cơ cấu bị xê dịch khi pause) engine đã hỗ trợ + có test, nhưng CHƯA station demo nào implement | ADR 0011 §4.1 |

### B. Bảo mật

| # | Gap | Bằng chứng |
|---|-----|-----------|
| B1 | **Không lockout** sau N lần đăng nhập sai, **không password policy** (độ dài tối thiểu), **không bắt đổi mật khẩu seed** lần đầu — chỉ có warning trong log | **[✓code]** grep UserService: không có lockout/MinLength/ForceChange |
| B2 | **Không auto-logout** sau thời gian idle — Engineer đăng nhập rồi rời máy = ai cũng jog được trục | **[✓code]** không có inactivity timer |
| B3 | **users.json schema cũ → re-seed GHI ĐÈ không backup** — user đã tạo mất im lặng (đã xảy ra thật trong log 2026-07-02 09:10) | **[✓code]** `UserService.Load()` → `SeedDefaults()` → `Save()` đè file |
| B4 | Mật khẩu mặc định (operator123…) không có cảnh báo runtime — chỉ dòng hint ở màn login | UserService line 265 chỉ LogWarning |
| B5 | Audit chưa xem/export được trên UI (ghi rồi để đó) | AuditService có, Settings không có màn audit |
| B6 | File cấu hình quan trọng (recipes/sequence/users/points) không backup tự động, không checksum | — |

### C. Chức năng

| # | Gap | Bằng chứng |
|---|-----|-----------|
| C1 | **`DataRetentionDays` KHÔNG được thực thi** — `DeleteOlderThanAsync` có sẵn ở cả 2 repository nhưng không service nào gọi → DB SQLite phình vô hạn (LogRetentionDays thì Serilog đã lo) | **[✓code]** grep: 0 caller |
| C2 | ~~Màn Vận hành tay chưa dựng~~ **ĐÍNH CHÍNH (S81): màn này ĐÃ TỒN TẠI từ S48** — MotionView = tab "Vận hành tay" (`[ModuleNavigation(Nav.ManualOp, minLevel: LineLead)]`) đủ 5 sub-tab Trục/Điểm/IO/Thao tác trạm/Override + dải khoá IsAdjustAllowed. Gap thật chỉ là nút Manual trên action bar disabled → ĐÃ NỐI (S81) | đánh giá S79 ghi quá tay |
| C3 | Sequence giai đoạn 2: single-step · pipeline >1 sản phẩm · khai báo resources · resume-from-crash (persist-step sink) — đã ghi lý do hoãn | ADR 0011 §6 |
| C4 | Sequence gắn 1 file config chung — chưa **per-recipe** thật (đổi recipe không đổi sequence, không Invalidate) | SequenceSource v1 |
| C5 | Settings còn 4 ô placeholder "Đang phát triển": Phần cứng · Host · Sao lưu & phục hồi · Hiệu chuẩn; chưa có nút thoát kiosk (đang chỉ Ctrl+Shift+F11) | S52 GridMenu + ADR 0009 TODO |
| C6 | Host/MES: chưa có module SECS/GEM (CLAUDE.md ghi "optional" nhưng chưa tồn tại); OPC UA/EtherNet-IP chỉ có sim; ReportStation upload đang giả lập delay | **[✓code]** ls modules — không có SecsGem |
| C7 | Production module: KPI cơ bản, chưa SPC chart/trend/export; cửa sổ ca cứng 8h | DashboardVM `ShiftHours = 8` hằng số |
| C8 | Các mục nhỏ đã hoãn có chủ đích: Stop popup 2 lựa chọn · Start pre-check popup · heartbeat amber >3s · billboard mode · UiScale · MachineId trên header · ngưỡng yield đổi màu KPI | Dashboard spec §5 + ADR 0009/0010 |

### D. Hiệu chỉnh (calibration) — trục TRẮNG hoàn toàn

| # | Gap | Bằng chứng |
|---|-----|-----------|
| D1 | `HMI_Calibration_Model_v1.0.md` là **tham chiếu treo** — Master Index trỏ tới nhưng file không tồn tại (đã ghi ở phản biện D3) | Master Index §1 "(chưa có — TBD)" |
| D2 | Không có framework calib: routine/rare, wizard 2 nhánh theo sai số, `requiresCalibAfterChange`, lưu kết quả vào recipe + audit | — |
| D3 | Máy demo không có routine calib mẫu để nghiệm thu framework | — |

### E. Giao diện & tài liệu

| # | Gap |
|---|-----|
| E1 | `HMI_UI_Architecture_Template` + Master Index §3 vẫn mô tả 7 vùng (Shell đã 4 vùng — ADR 0009); `HMI_Dashboard_Spec` chưa nâng v2.1 (card KQ — ADR 0010); 3 nguyên tắc (màu-khi-có-nghĩa · empty-state-có-hướng-dẫn · xếp-theo-tần-suất-liếc) chưa vào template |
| E2 | CLAUDE.md ghi "README chưa có — TODO" nhưng README.md đã tồn tại (stale) **[✓code]** |
| E3 | Chưa có UI automation smoke test (mọi nghiệm thu UI đang bằng mắt/app run) |

### F. Tích hợp thật / triển khai

| # | Gap |
|---|-----|
| F1 | **Máy reference thật** (TODO từ S43): driver P/Invoke GTS/Advantech chưa từng chạy phần cứng thật; watchdog/reconnect chưa đo thực tế |
| F2 | Chưa có quy trình đóng gói: publish self-contained + auto-start kiosk + watchdog process + hướng dẫn cài IPC |
| F3 | Vòng review phản biện ADR 0011 + engine (bước cuối quy trình Sequencing_NextSteps §5) chưa chạy |

---

## §3. Kế hoạch thực hiện chi tiết (P0 → P5)

> Ước lượng theo **phiên** (1 phiên ≈ 1 session làm việc trọn một hạng mục có test + docs + commit).

### P0 — Sửa ngay: đúng đắn & an toàn nền (2 phiên, không phụ thuộc gì)

| Mục | Việc cụ thể | DoD |
|-----|-------------|-----|
| **P0.1 E-Stop vào state machine** (gap A1) | `EmergencyStop()`: fire `Error` trigger (Running/Initializing → RunAlarm/InitAlarm) + raise alarm 70001 qua AlarmService (fire-and-forget an toàn, KHÔNG throw); E-Stop khi Idle → alarm + interlock chặn Start (đã có `IsAllSafe`). Wire `ISafetyInput.SafetyStateChanged` (E-Stop kích) → master EmergencyStop (hiện chỉ watchdog gọi) | Test: EStop lúc Running → State=RunAlarm + alarm 70001 active + banner đỏ; Reset → re-init sạch |
| **P0.2 Retention job** (gap C1) | `RetentionCleanupService` (IHostedService-style, timer 1 lần/ngày + lúc khởi động): gọi `DeleteOlderThanAsync(now − DataRetentionDays)` cho Alarm + Production qua scope; log số record xoá | Test với repo fake; chạy app thấy log dọn |
| **P0.3 users.json an toàn re-seed** (gap B3) | Trước khi `SeedDefaults()` ghi đè: copy file cũ → `users.json.bak-{yyyyMMdd}` + LogError rõ "đã backup"; (tuỳ chọn) alarm 40003 config invalid | Test: file schema cũ → có .bak + seed mới |
| **P0.4 Dọn tài liệu** (E1, E2, D1-một-phần) | Nâng `HMI_UI_Architecture_Template` lên v3 (4 vùng) + Master Index §3 + Dashboard spec v2.1 + 3 nguyên tắc; sửa dòng README stale trong CLAUDE.md; gỡ/đánh dấu tham chiếu treo calib | Grep không còn mô tả 7 vùng lệch thực tế |

### P1 — An toàn & Vận hành tay (4–5 phiên; P1.1 cần chốt §5 trước)

| Mục | Việc cụ thể | DoD |
|-----|-------------|-----|
| **P1.1 Chốt chính sách §9** | Chủ dự án trả lời 4 câu §5 (override confirm, R2, ngưỡng lệch Set–Confirm, trục/trạm máy chủ lực) → cập nhật `HMI_Manual_Operation_and_Safety` từ "default TẠM" thành chốt | Doc cập nhật, đánh dấu ĐÃ CHỐT |
| **P1.2 Màn Vận hành tay v1** (gap C2) | Tab mới gộp (LineLead+): dải khóa trạng thái `IsAdjustAllowed` + giám sát rút gọn cố định + sub-tab **Thao tác trạm (S63) / Điều khiển trục (S46) / Bảng điểm / Giám sát IO (S60) / ⚠ Override (S64)** — NHÚNG lại module có sẵn (đúng §3.2 template, không viết trùng); refactor nav (Motion/IoMonitor thành sub-tab, bỏ tab riêng); bật nút Manual trên action bar → điều hướng tab này | Checklist HMI_Manual_Operation §7; đổi role thấy đúng tab; không mất chức năng cũ |
| **P1.3 Nút vật lý** (gap A2) | `PhysicalButtonMonitor` (Demo project): poll/edge-detect `DI.Btn.*` qua IIoService → gọi master Start/Stop/Reset (debounce, chỉ khi hợp lệ theo state); Start vật lý tôn trọng interlock | Test sim: set DI → master đổi state đúng |
| **P1.4 Guard hình học** (gap A3) | SimIoService/HAL publish tín hiệu trục+IO chọn lọc lên `HardwareSignalBus` (Z-height, vacuum); khai `GuardCondition` cho jog/xi lanh trong Vận hành tay (cấm XY khi Z chưa an toàn — predicate như RefSeq-A §10b.3) | Test guard: điều kiện sai → blockReason hiện trên nút |
| **P1.5 Jog deadman** (gap A4) | `IAxisJog` (velocity-mode): StartJog/StopJog + watchdog tick (UI giữ nút gửi tick ≤100ms, mất tick >200ms → HAL tự dừng); sim implement; jog pad màn trục dùng giữ-để-chạy | Test: ngừng tick → trục dừng; nhả nút → dừng |
| **P1.6 Prompt liệu sót + resume-check demo** (gap A5, A6) | `OperatorPromptService : IOperatorPrompt` (UI adapter — tái dùng banner prompt); PickStation init hỏi "Lấy tay / Máy tự thoát" thay vì tự blow; PickStation implement `IResumeVerifiable` (snapshot vị trí XYZ + trạng thái vacuum) | Kịch bản (d) mở rộng: init dừng chờ operator; pause→xê dịch (sim SetDi)→Resume bị từ chối + prompt |

### P2 — Hiệu chỉnh / Calibration (3 phiên)

| Mục | Việc cụ thể | DoD |
|-----|-------------|-----|
| **P2.1 Tài liệu mô hình calib** (gap D1) | Viết `docs/HMI_Calibration_Model_v1.0.md`: phân loại `frequency` routine/rare; calib ≠ setting; wizard 2 nhánh theo `autoThreshold`; `requiresCalibAfterChange` + usage counter; ADR-style lựa chọn | Master Index hết tham chiếu treo |
| **P2.2 Calib framework** (gap D2) | `AM.Core.Sequencing`-style module nhỏ: `ICalibrationRoutine` (Id, DisplayKey, Frequency, AutoThreshold, MeasureAsync/ApplyAsync/GuideSteps), `CalibrationRegistry` config-driven, kết quả ghi recipe (qua IRecipeService) + audit + lịch sử JSON; wizard state machine (đo → trong ngưỡng? áp : hướng dẫn tay → đo lại) | Unit test wizard 2 nhánh; không đụng IStation |
| **P2.3 UI + demo routine** (gap D3) | Sub-tab "Hiệu chỉnh" trong Vận hành tay (routine, tự ẩn nếu rỗng) + ô "Bảo trì & Hiệu chuẩn" trong Settings (rare, Admin, thay placeholder); demo: calib offset điểm pick trên sim (đo lệch giả lập → áp vào recipe PickPositionX/Y) | Chạy wizard end-to-end trên sim; recipe đổi + audit ghi |

### P3 — Bảo mật & phiên đăng nhập (2 phiên)

| Mục | Việc cụ thể | DoD |
|-----|-------------|-----|
| **P3.1 Password policy + lockout** (gap B1, B4) | UserService: MinLength (config, mặc định 8) khi tạo/đổi; `MustChangePassword` flag — seed user bật sẵn, login lần đầu ép đổi (IdentityView thêm bước); lockout sau N lần sai (config, mặc định 5 → khoá 5 phút + audit + alarm nhẹ); banner cảnh báo khi còn user dùng mật khẩu mặc định | Test: sai 5 lần → khoá; seed login → bắt đổi; cảnh báo tắt sau khi đổi hết |
| **P3.2 Auto-logout + audit UI** (gap B2, B5) | Inactivity timer (config phút, đếm input toàn cửa sổ) → tự logout về "Chưa đăng nhập" (máy VẪN chạy — chỉ hạ quyền); Settings thêm màn Audit: xem bảng + lọc ngày/user + export CSV | Idle quá hạn → UserText đổi, tab quyền cao biến mất; export mở được |
| **P3.3 Backup cấu hình** (gap B6, C5-một-phần) | Settings "Sao lưu & phục hồi" (thay placeholder): backup zip (db + recipes + points + users + sequence + io.map) vào thư mục chọn, restore có confirm 2 bước + backup-trước-restore; backup tự động hàng ngày giữ N bản (config) | Backup → xoá file → restore → app chạy lại đúng |

### P4 — Chức năng máy & dữ liệu (3–4 phiên)

| Mục | Việc cụ thể | DoD |
|-----|-------------|-----|
| **P4.1 Single-step mode** (gap C3a) | Engine: gate chờ xác nhận sau mỗi nhóm `order` khi `SingleStep=true` (bất biến 5 — không đổi IStation); UI: toggle Engineer trên action bar + nút "Bước tiếp" trên banner/work area | Test engine; chạy app từng bước bằng tay |
| **P4.2 Sequence per-recipe** (gap C4) | Trường `SequenceFile` trong RecipeBase (hoặc convention `recipes/{RecipeName}.sequence.json`); `SequenceSource.Invalidate()` khi `RecipeChanged`; validate lúc nạp recipe — recipe fail giữ recipe cũ (đã đúng thiết kế ADR §1) | Đổi recipe → sequence đổi; recipe hỏng → alarm 60005 + giữ cũ |
| **P4.3 Settings hoàn thiện** (gap C5) | Ô "Phần cứng": bảng thiết bị từ machine.json/HardwareManager (read-only + reconnect từng cái); ô "Host": trạng thái + endpoint config; nút thoát/vào kiosk (Engineer) đặt ở Cài đặt (giữ Ctrl+Shift+F11 làm dự phòng) | Không còn ô "Đang phát triển" nào |
| **P4.4 Production nâng cấp** (gap C7, C8-một-phần) | Ca làm việc config (giờ bắt đầu/độ dài, thay hằng 8h — dashboard + production cùng dùng); export CSV; trend/SPC đơn giản (X̄ chart cycle time + yield theo giờ); ngưỡng yield đổi màu KPI (config — đã hoãn từ ADR 0010) | KPI theo ca thật; export đúng; ngưỡng hoạt động |
| **P4.5 Chi tiết vận hành nhỏ** (gap C8) | Stop popup 2 lựa chọn (dừng hết cycle / dừng ngay) · Start pre-check (điều kiện chưa đạt liệt kê lý do) · heartbeat amber khi tick >3s · MachineId (machine.json) cạnh logo · UiScale theo màn 21.5" | Mỗi item theo Button_Spec; gộp 1 phiên |
| *(sau P4)* Pipeline >1 + resources + resume-from-crash | Chỉ làm khi có máy thật cần — giữ nguyên lý do hoãn ADR 0011 §6 | — |

### P5 — Tích hợp thật & triển khai (theo tiến độ phần cứng)

| Mục | Việc cụ thể | DoD |
|-----|-------------|-----|
| **P5.1 Máy reference thật** (gap F1) | Dựng DemoPickPlace vật lý (hoặc máy khách đầu tiên): thay SimIoService bằng adapter IIoService/IMotionService trên IIoModule/IMotionController thật; test P/Invoke GTS/Advantech; đo watchdog + reconnect + timeout thật | 4 kịch bản nghiệm thu pass TRÊN PHẦN CỨNG |
| **P5.2 Host thật** (gap C6) | Chốt giao thức với nhà máy (§5.Q5): OPC UA client thật (thư viện OPCFoundation đang comment trong csproj) hoặc module SECS/GEM (HSMS — cân nhắc secs4net); ReportStation upload thật + retry + CSV backup đã có | Upload thành công + mất mạng không giết cycle |
| **P5.3 Đóng gói triển khai** (gap F2) | `dotnet publish` self-contained + script cài IPC (auto-start, kiosk=true, quyền thư mục, firewall); watchdog process (Task Scheduler restart-on-crash); hướng dẫn `docs/DEPLOYMENT.md` | Máy sạch cài từ zip chạy được trong 15 phút |
| **P5.4 Review phản biện** (gap F3) | Vòng ChatGPT/Gemini cho ADR 0011 + engine + máy demo, lọc bằng chứng theo SequenceEngine_Spec + requirements local (đúng quy trình chương sách) | Danh sách finding đã lọc + fix những cái CONFIRMED |
| **P5.5 UI smoke automation** (gap E3, tuỳ chọn) | FlaUI: mở app → Initialize → Start → 3 cycle → Pause/Resume → Stop → so KPI; chạy trong CI windows | CI xanh có UI smoke |

---

## §4. Bảng ưu tiên tổng hợp

| Ưu tiên | Mục | Trục | Phiên | Phụ thuộc |
|---------|-----|------|-------|-----------|
| ✅ 1 | P0.1 E-Stop state machine — XONG S80 (`EmergencyStop` fire Error → RunAlarm/InitAlarm + alarm 70001 + wire ISafetyInput; cửa mở KHÔNG estop; +transition Paused→RunAlarm; 5 test) | An toàn | 0.5 | — |
| ✅ 2 | P0.2 Retention job — XONG S80 (`RetentionCleanupService`: dọn ngay lúc boot + mỗi 24h; 4 test) | Dữ liệu | 0.5 | — |
| ✅ 3 | P0.3 users.json backup — XONG S80 (backup `.bak-{timestamp}` trước re-seed; 2 test) | Bảo mật | 0.25 | — |
| ✅ 4 | P0.4 Sync tài liệu HMI v3 — XONG S80 (Template v3 mới + Master Index 4 vùng + 3 nguyên tắc + Dashboard spec v2.1 + CLAUDE.md) | Docs | 0.75 | — |
| ✅ 5 | P1.1 Chốt §9 — XONG S81: Override = 1 người 2 bước+đếm ngược 3s (giữ S64) · R2 cứng Engineer · ngưỡng Set–Confirm 0.05mm config | An toàn | (đã chốt) | — |
| ✅ 6 | P1.2 Màn Vận hành tay — XONG S81: màn đã có từ S48 (đính chính C2); nối nút Manual action bar → tab Vận hành tay (gate LineLead+, tooltip 3 ngữ) | UI+An toàn | 0.25 | — |
| ✅ 7 | P1.3 Nút vật lý — XONG S81: `PhysicalButtonMonitor` poll 50ms edge-detect DI.Btn.* → Start/Stop/Reset (master tự kiểm điều kiện); 3 test | An toàn | 0.5 | — |
| ✅ 8 | P1.4 Guard hình học — XONG S82: `MotionSignalPublisher` poll Z 100ms → publish `Motion.ZAtSafe` (fail-safe false khi chưa kết nối/lỗi đọc); `GeometricGuardFor` khai `GuardCondition` cho jog/move/hold X/Y/U (Z được miễn); blockReason `Manual.ZNotSafe` 3 ngữ; 2 test | An toàn | 1 | — |
| ✅ 9 | P1.5 Jog deadman — XONG S82: `IAxisJog` (StartJog/KeepAlive/StopJog, watchdog 200ms) sim implement (vòng tích phân 25ms, mất KeepAlive → TỰ DỪNG); jog pad giữ-để-chạy qua `JogHoldBehavior` (nhả nút/rời nút/mất capture → Stop; UI nuôi KeepAlive 80ms); HAL không có IAxisJog → fallback inching; 4 test | An toàn | 1 | — |
| ✅ 10 | P1.6 Prompt liệu sót + resume-check — XONG S81: `BannerOperatorPromptService` (IOperatorPrompt → nút động trên banner; không subscriber → chọn lựa chọn an toàn nhất đứng đầu); PickStation init HỎI operator (lấy tay/tự thoát, lặp tới khi sạch); PickStation + `IResumeVerifiable` kiểm BẤT BIẾN HÌNH HỌC Z-ở-độ-cao-an-toàn (không so snapshot — gantry dùng chung làm snapshot per-station stale); 3 test | An toàn | 0.5 | — |
| 🟡 11 | P2.1–P2.3 Calibration (doc+framework+UI) | Hiệu chỉnh | 3 | P1.2 (sub-tab) |
| 🟠 12 | P3.1 Password policy + lockout | Bảo mật | 1 | — |
| 🟡 13 | P3.2 Auto-logout + audit UI | Bảo mật | 1 | — |
| 🟡 14 | P3.3 Backup & restore | Bảo mật/Ops | 1 | — |
| 🟡 15 | P4.1 Single-step | Chức năng | 1 | — |
| 🟡 16 | P4.2 Sequence per-recipe | Chức năng | 0.5 | — |
| 🟡 17 | P4.3 Settings hoàn thiện | UI | 1 | — |
| 🟡 18 | P4.4 Production/SPC/ca | Chức năng | 1 | — |
| 🟢 19 | P4.5 Chi tiết vận hành nhỏ | UI | 1 | — |
| 🟢 20 | P5.1–P5.5 Tích hợp thật + deploy | Tích hợp | theo phần cứng | P0–P4 |

**Tổng ước lượng P0–P4: ~17 phiên.** Sau P0+P1 là dùng được an toàn trong xưởng thử nghiệm;
sau P3 mới nên đưa ra môi trường có nhiều người dùng; P5 gắn với phần cứng/nhà máy thật.

---

## §5. Câu hỏi cần chủ dự án chốt (chặn các mục tương ứng)

| # | Câu hỏi | Chặn mục |
|---|---------|----------|
| Q1 | Supervised Override confirm: giữ **1 người (2 bước + đếm ngược — đang chạy)** hay nâng thành 2 người giữ-nút? | P1.1/P1.2 |
| Q2 | R2 (move-to-point gỡ kẹt): cứng ở Engineer hay cho hạ LineLead per-machine? | P1.1 |
| Q3 | Ngưỡng cảnh báo lệch Set–Confirm theo loại trục = bao nhiêu (mm)? | P1.2 bảng điểm |
| Q4 | Ngưỡng yield đổi màu KPI (vd <98% vàng, <95% đỏ)? Ca làm việc thật (giờ bắt đầu/độ dài)? | P4.4 |
| Q5 | Host nhà máy dùng gì: SECS/GEM, OPC UA, hay chỉ CSV/DB? (quyết P5.2 làm gì) | P5.2 |
| Q6 | Auto-logout sau bao nhiêu phút idle? Lockout bao nhiêu lần sai/khoá bao lâu? | P3.1/P3.2 |
| Q7 | Máy reference thật: dùng bo motion nào (GTS/Advantech) + IO nào để ưu tiên test P/Invoke? | P5.1 |

---

## §6. Vision — ngoài phạm vi (app riêng), giữ hợp đồng

- **Không làm** trong roadmap này: VisionPro host FW4.8, `.vpp`, teach/ROI nâng cao (ADR 0007/0008 giữ nguyên trạng).
- **Giữ lại trong AM.AutoFrame** để app vision riêng cắm vào:
  1. `IVisionProcessor`/`VisionResult` (Abstractions) — app vision là một implementation trả kết quả qua IPC (hợp đồng ranh giới ADR 0008: payload + correlationId).
  2. `VisionStation` của sequence chỉ cần `StationResult` + score — thay sim bằng IPC client là xong, KHÔNG đổi engine.
  3. Card "Kết quả gần nhất" trên Home nhận ảnh cycle qua đường `IProductionService`/event hiện có — app vision đẩy ảnh về theo SN.
- Khi bắt đầu app vision riêng: viết ADR hợp đồng IPC chi tiết (transport, timeout, vòng đời process) — đã phác ở ADR 0008.

---

*File này là kế hoạch sống: mỗi phiên xong một mục → tick vào bảng §4 + cập nhật PROJECT_STATUS như thường lệ. Đánh giá dựa trên source tại commit `7373fb4`.*
