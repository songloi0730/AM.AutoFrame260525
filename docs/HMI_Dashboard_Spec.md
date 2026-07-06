# HMI Dashboard Spec — Màn hình chính Home (v2.1, IPC 24" 1920×1080)

> **Mục đích:** Spec nghiệm thu cho **màn Home** của AM.AutoFrame theo
> `HMI_UI_Architecture_Template_v3.md` (shell 4 vùng) + nội dung Home v2.1 (ADR 0010).
> v2.1 (S79/P0.4) cập nhật theo Shell v3 (S73), Home v2.1 (S74) và sequence engine (S78).
>
> **Đọc cùng:** `.claude/skills/am-hmi-design/SKILL.md` · `docs/HMI_UI_Architecture_Template_v3.md`
> · `docs/HMI_Button_Spec.md` · ADR 0009/0010/0011.
>
> **Hiện thực:** Shell = `AM.Application.Shell/MainWindow.xaml` + `ShellViewModel.cs`;
> Home = `AM.Modules.Dashboard/DashboardView.xaml` + `DashboardViewModel.cs` + `DashboardTileVms.cs`.

---

## 1. Phân công Shell ↔ Home (Persistent Frame — shell v3, 4 vùng)

| Vùng | Cao | Thuộc | Nội dung đã hiện thực |
|------|-----|-------|----------------------|
| 1 Header+Nav | 56 | Shell | Logo (tooltip tên máy) · chip **AUTO/DRY · LOCAL · state ISA-88** (26px) · tab điều hướng RadioButton tự sinh từ `[ModuleNavigation]` (gạch chân 3px, ScrollViewer ngang) · Recipe · Clock (MinWidth) + **heartbeat** 1Hz · Ngôn ngữ · User |
| 2 Alarm banner | 36→52 | Shell | Co giãn: sạch 36px xám; **1 alarm ưu tiên cao nhất chưa ACK** + ACK 40px + chip `+N` (52px) HOẶC **operator prompt** của sequence engine: nội dung + 3 nút Thử lại · Bỏ qua (Engineer+) · Dừng máy |
| 3 Content | * | Home | **Work area**: card "Kết quả gần nhất" (thumb camera chấm-trạng-thái + chip KQ OK/NG 22px + SN/Cycle/Recipe) → bảng truy vết SN (empty state có hướng dẫn, Cycle căn phải, KQ chip màu, counter trên header). **Right rail 560px**: KPI ca 3×2 (số 26px, màu chỉ khi >0, "—" khi trống, cycle tự đổi ms→s) → Thao tác nhanh (2 hàng tiện ích/rủi ro, tooltip lý do + icon khoá, Gọi KT = Andon viền hổ phách) → Trạm & an toàn → Nhật ký (empty state) |
| 4 Action bar | 76 | Shell | `Init · Start · Pause/Resume · Stop │(divider) Reset · Dry run · Manual` — lệnh máy 64px nằm ngang, CHỈ Start viền xanh; mờ + tooltip khi khoá · **chip "● Thiết bị n/m · Host n/m"** (44px) mở Popup 2 cột + version footer |

## 2. Nguồn dữ liệu (interface-only — đổi máy không sửa XAML)

| Thành phần | Nguồn | Cập nhật |
|------------|-------|----------|
| Badge state + enable nút action bar | `IMasterController.State/StateChanged` (`CanExecute` ≙ CanFire) | event |
| Badge AUTO/DRY | `IMasterController.OperationMode` (`Mode.{enum}` i18n) | RefreshState |
| Alarm banner | `IAlarmService.ActiveAlarms` lọc `!IsAcknowledged`, sort `Level` desc → `RaisedAt` desc | event |
| KPI ca (3×2) | `IProductionService.GetStatisticsAsync(now-8h, now)` qua scope — **record do ReportStation của sequence ghi** (SN scanner, OK/NG, vision score) | CycleCompleted + 10s |
| Bảng sản phẩm + card KQ gần nhất | `IProductionRepository.GetByDateRangeAsync` → 14 dòng mới nhất; card = record[0] | CycleCompleted + 10s |
| Thumb camera trong card KQ | `IHardwareManagerService` Category=Camera (tên + chấm trạng thái; ảnh cycle = chờ app vision riêng) | poll 2s |
| Trạm & an toàn | `IMasterController.Stations` (event) + `ISafetyInput` (event `SafetyStateChanged` — push, không poll) | event |
| Thao tác nhanh | `QuickActionVm` — Tắt còi/đèn/ion/cửa qua HAL + guard R0–R3, hold-to-confirm 1s cửa; lý do khoá ở tooltip + icon khoá | poll DO 2s |
| Nhật ký | In-memory từ events (state/alarm/cycle) + **sự kiện sequence engine** (`StepCompleted` lỗi/NG, `ProductCompleted`, prompt) — một nguồn, cap 30 | event |
| Chip kết nối (action bar) | `IHardwareManagerService.GetMonitoredDevices()` — Host = OPC-UA + DB, còn lại = Thiết bị; `DeviceOnlineText/HostOnlineText/AllConnectionsOk` | poll 1s |
| Operator prompt banner | `ISequenceEngine.OperatorPromptRequired` — Respond ngay trong args; Skip lọc theo `UserLevel ≥ Engineer` | event |

## 3. Quy tắc đã áp (theo template v2 §1)

- Lệnh máy KHÔNG ở nửa trên màn hình; một lệnh một chỗ; nút khoá thì **mờ + tooltip lý do**, không ẩn.
- Palette v2 duy nhất (`App.xaml` — GIỮ TÊN token cũ, đổi value); màu chỉ mang nghĩa trạng thái.
- Icon: Segoe MDL2 một màu (quyết định adoption — không thêm package MDI); emoji không vào code.
- Nút: action bar/quick action ≥64px; thường ≥48px; dòng bảng ≥40px; ACK ≥40px; gap ≥8px.
- ACK ≠ tắt còi (EEMUA 201) — ACK ở banner, Tắt còi ở Thao tác nhanh.
- i18n: mọi chuỗi qua `Loc.Strings` key vi/en/zh, đổi runtime.

## 4. Checklist nghiệm thu Home v2

```
□ Liếc 2 giây: badge state + banner alarm + KPI ca + trạm & an toàn đọc được ngay
□ Banner: chỉ 1 alarm (ưu tiên cao nhất chưa ACK); ACK xong alarm kế trồi lên; +N đúng số
□ Action bar: nút trắng phẳng icon-trên; đúng CanExecute theo state; mờ + tooltip khi khoá
□ Pause/Resume một nút tự đổi nhãn; Dry run đổi badge header sang DRY
□ Bảng sản phẩm: chỉ dòng NG có màu; footer đếm đúng; 14 dòng mới nhất
□ Quick action không có HAL → mờ + lý do; Tắt còi chỉ sáng khi còi đang kêu; KHÔNG ACK alarm
□ Mất kết nối thiết bị → chip đổi ✕ đỏ ở connection bar (+ camera tile đổi trạng thái)
□ E-Stop/cửa qua ISafetyInput event (không poll); đổi trạng thái hiện ngay ở rail
□ Đổi ngôn ngữ runtime: toàn bộ nhãn đổi ngay, layout không vỡ
□ Đổi máy mới: chỉ đổi đăng ký DI/máy — Shell + Home không sửa code
```

## 5. TODO theo adoption §9 (không làm nửa vời trong S45)

LOCAL/REMOTE + popup GEM · tiến độ lô (MES) · Stop popup 2 lựa chọn · Start pre-check popup ·
Manual overlay · QuickActions HoldToConfirm + audit · UiScale · billboard mode · heartbeat amber >3s ·
vision thumbnail ảnh kết quả thật (gRPC/vision service) · popup chẩn đoán chip kết nối.

---

*Phiên bản 2.1 — Session 79/P0.4 (04/07/2026): shell v3 4 vùng + card KQ gần nhất + KPI màu-khi-có-nghĩa + prompt banner + mini-log sequence. Bản 2.0 — Session 45 (thay bản 1.0 S44).*
