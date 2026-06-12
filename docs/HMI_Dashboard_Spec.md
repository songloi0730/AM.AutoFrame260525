# HMI Dashboard Spec — Màn hình chính Home (v2.0, IPC 24" 1920×1080)

> **Mục đích:** Spec nghiệm thu cho **màn Home** của AM.AutoFrame theo
> `HMI_UI_Architecture_Template_v2.md` (bố cục 7 vùng, mockup `hmi_home_v2.html`).
> Bản này THAY THẾ spec layout 5 hàng cũ (S44).
>
> **Đọc cùng:** `.claude/skills/am-hmi-design/SKILL.md` · `docs/HMI_UI_Architecture_Template_v2.md`
> (kèm mục Quyết định adoption §9) · `docs/HMI_Button_Spec.md`.
>
> **Hiện thực:** Shell = `AM.Application.Shell/MainWindow.xaml` + `ShellViewModel.cs`;
> Home = `AM.Modules.Dashboard/DashboardView.xaml` + `DashboardViewModel.cs` + `DashboardTileVms.cs`.

---

## 1. Phân công Shell ↔ Home (Persistent Frame)

| Vùng | Cao | Thuộc | Nội dung đã hiện thực |
|------|-----|-------|----------------------|
| 1 Header | 64 | Shell | Logo + tên máy · badge **AUTO/DRY** · badge **LOCAL** (tĩnh — GEM TODO) · badge **state ISA-88** · Recipe · Clock + **heartbeat** (chấm nháy 1Hz) · nút Ngôn ngữ · User |
| 2 Nav tabs | 48 | Shell | Tab ngang icon+chữ tự sinh từ `[ModuleNavigation]`, active = nền đậm + chữ đậm |
| 3 Alarm banner | 40 | Shell | **Duy nhất alarm ưu tiên cao nhất chưa ACK** + ACK (40px) + chip `+N khác`; xám khi sạch, hổ phách = Warning, đỏ = Error/Critical |
| 4 Work area | * | Home | Sub-tab "Sản phẩm": dải thumbnail vision + bảng truy vết SN (dòng NG tô `#F9E6E3`) + footer đếm |
| 5 Right rail | 560px | Home | KPI ca (3×2) → Thao tác nhanh (lưới 2–3 cột, ≥64px) → Trạm & an toàn (2 cột) → Nhật ký (1 dòng/entry) |
| 6 Action bar | 84 | Shell | `Start · Pause/Resume · Stop · Reset │ Dry run · Manual` — nút trắng phẳng 64px, icon MDL2 trên + nhãn dưới, CHỈ Start viền xanh; mờ + tooltip lý do khi không khả dụng |
| 7 Connection bar | 40 | Shell | Nhóm **Thiết bị** │ **Host** (ký hiệu ●/✕, ○ chưa cấu hình) + version góc phải |

## 2. Nguồn dữ liệu (interface-only — đổi máy không sửa XAML)

| Thành phần | Nguồn | Cập nhật |
|------------|-------|----------|
| Badge state + enable nút action bar | `IMasterController.State/StateChanged` (`CanExecute` ≙ CanFire) | event |
| Badge AUTO/DRY | `IMasterController.OperationMode` (`Mode.{enum}` i18n) | RefreshState |
| Alarm banner | `IAlarmService.ActiveAlarms` lọc `!IsAcknowledged`, sort `Level` desc → `RaisedAt` desc | event |
| KPI ca (3×2) | `IProductionService.GetStatisticsAsync(now-8h, now)` qua scope | CycleCompleted + 10s |
| Bảng sản phẩm | `IProductionRepository.GetByDateRangeAsync` → 14 dòng mới nhất | CycleCompleted + 10s |
| Thumbnail vision | `IHardwareManagerService` Category=Camera (tên + IsConnected; ảnh kết quả = TODO vision service) | poll 2s |
| Trạm & an toàn | `IMasterController.Stations` (event) + `ISafetyInput` (event `SafetyStateChanged` — push, không poll) | event |
| Thao tác nhanh | `QuickActionVm` list — **Tắt còi** wired `ILightController.SetAsync(Current with {Buzzer=false})`; còn lại disabled + lý do (HAL/audit TODO) | poll buzzer 2s |
| Nhật ký | In-memory từ events (state/alarm/cycle), 1 dòng/entry, cap 30 | event |
| Connection bar | `IHardwareManagerService.GetMonitoredDevices()` — Host = OPC-UA + DB, còn lại = Thiết bị | poll 1s |

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

*Phiên bản 2.0 — Session 45 (12/06/2026). Thay thế bản 1.0 (S44, layout 5 hàng).*
