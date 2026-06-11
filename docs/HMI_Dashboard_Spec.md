# HMI Dashboard Spec — Màn hình chính L1 (IPC 1920×1080, 21–24")

> **Mục đích:** Spec chuẩn cho **màn hình chính (Dashboard, ISA-101 Level 1)** của AM.AutoFrame —
> tài liệu nghiệm thu + tham chiếu khi đổi máy mới (layout giữ nguyên, chỉ đổi data binding).
>
> **Đọc cùng:** `.claude/skills/am-hmi-design/SKILL.md` (quy tắc thiết kế) ·
> `docs/HMI_UI_Architecture_Template.md` (khung Shell) · `docs/HMI_Components_Catalog.md` §1 (checklist Dashboard).
>
> **Hiện thực:** `AM.Modules.Dashboard/DashboardView.xaml` + `DashboardViewModel.cs` + `DashboardTileVms.cs`.

---

## 1. Vai trò & nguyên tắc

| Mục | Giá trị |
|-----|---------|
| Level (ISA-101) | **L1 — Process Overview**: đánh giá toàn máy trong 1–2 giây |
| Target | IPC 1920×1080, 21–24", chuột + cảm ứng (găng tay — SEMI S8) |
| Triết lý | High-Performance HMI: **yên tĩnh khi bình thường** — màu chỉ cho bất thường |
| Bề rộng nội dung | **MaxWidth = 1400px**, căn trái, KHÔNG giãn hết 1920px |
| Điều hướng | Là màn mặc định (nav order 10); mọi màn khác ≤ 3 click từ đây |

Phân công với Shell (tránh trùng lặp):
- **Shell header** (cố định mọi màn): tên máy, state chip, mode, recipe, user, clock + lệnh toàn cục Init/Start/Stop/Reset.
- **Shell alarm bar / status bar**: alarm mới nhất + Acknowledge; dãy chip kết nối thu gọn.
- **Dashboard** (content L1): chi tiết hơn — KPI sản xuất, trạng thái từng station, panel kết nối đầy đủ
  (kèm banner cảnh báo), danh sách alarm active, hàng nút nhanh đủ Pause/Resume (header không có).

---

## 2. Layout (5 hàng, lưới 8px)

```
┌─ Row 0: STATE BANNER ───────────────────────────────────────────────┐
│ ●(màu state 24px)  TÊN TRẠNG THÁI (22pt bold)   Chu kỳ: N | Cảnh báo: N │ [Đang xử lý...] │
├─ Row 1: KPI SẢN XUẤT — 1 GIỜ QUA (6 tile, UniformGrid) ─────────────┤
│ [Tổng] [Đạt(xanh)] [Lỗi(đỏ)] [Tỉ lệ đạt %] [UPH] [Cycle TB ms]      │
├─ Row 2: BANNER MẤT KẾT NỐI (đỏ, chỉ hiện khi có thiết bị rớt) ──────┤
├─ Row 3: 2 cột (2* | 1*) ────────────────────────────────────────────┤
│ ┌ Trạm sản xuất (tile/station: ●state + tên + n cụm + nhãn state) ┐ │ ┌ Kết nối thiết bị ┐ │
│ ├ Cảnh báo đang active (DataGrid: Mã|Mức|Trạm|Thông điệp|Giờ|Ack) ┤ │ │ ●/✕ + tên + nhãn  │ │
│ └──────────────────────────────────────────────────────────────────┘ │ └ (scroll khi dài) ┘ │
├─ Row 4: NÚT NHANH 60px (SEMI S8 nút lệnh chính ≥60×60) ─────────────┤
│ [Khởi tạo][Chạy][Tạm dừng][Tiếp tục]   ←48px→   [DỪNG][Reset]       │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 3. Thành phần ↔ nguồn dữ liệu (interface-only, không vendor type)

| Thành phần | Nguồn | Cập nhật |
|------------|-------|----------|
| State banner + CanExecute nút | `IMasterController.State/StateChanged` | event, tức thì |
| Chu kỳ | `IMasterController.CycleCount/CycleCompleted` | event |
| KPI Total/OK/NG/Yield/UPH/CycleTB | `IProductionService.GetStatisticsAsync(now-1h, now)` qua `IServiceScopeFactory` (Scoped EF — tránh captive dependency) | CycleCompleted + 10s |
| Station tiles | `IMasterController.Stations` (`IStation.Name/State/Mechanisms.Count/StateChanged`) | event/station |
| Connection panel + banner | `IHardwareManagerService.GetMonitoredDevices()` → `IsConnected` | poll 2s |
| Active alarms | `IAlarmService.ActiveAlarms/AlarmRaised/AlarmCleared` | event |
| Lệnh Init/Start/Pause/Resume/Stop/Reset | `IMasterController.*Async()` | — |

ViewModel **không import System.Windows.\*** (R-UI-01): UI thread qua `SynchronizationContext`,
vòng poll dùng `PeriodicTimer` (không `DispatcherTimer`).

## 4. Quy tắc màu / mù màu / i18n đã áp

- State → màu qua `MachineStateToColorConverter` (token `Status.*`, semantic = StaticResource).
- Kết nối: **màu (xanh/đỏ) + chữ ("Kết nối"/"Mất kết nối") + vị trí** — không chỉ màu (mù màu OK).
- Equipment bình thường hiển thị xám/yên tĩnh; đỏ/vàng chỉ cho alarm/warning.
- Live value (KPI 28pt bold) > label (12pt) — ISA-101 typography; mọi giá trị có **đơn vị** (`%`, `ms`).
- Không hardcode chuỗi: mọi text qua `Loc.Strings[key]` (`Dash.*`, `Prod.*`, `Conn.*`, `State.*`),
  đổi ngôn ngữ runtime cập nhật cả nhãn state trong tile.
- Không hardcode hex: nền/chữ `DynamicResource`, semantic `StaticResource`.

## 5. Nút lệnh (SEMI S8 — đeo găng)

| Quy tắc | Áp dụng |
|---------|---------|
| Nút lệnh chính ≥ 60×60px | Hàng nút nhanh Height=60 |
| Nút nguy hiểm cách ≥ 48px | Stop có `Margin="48,0,4,0"` |
| Disable khi điều kiện chưa đạt | `CanExecute` theo state machine (ngăn lỗi thay vì báo lỗi) |
| Title-Case, không TOÀN HOA | Nhãn từ i18n catalog |

## 6. Checklist nghiệm thu màn hình chính

```
□ Nhìn 2 giây biết: máy đang state gì, có alarm không, sản lượng ra sao
□ Mất kết nối 1 thiết bị → banner đỏ hiện ngay trên Dashboard (không cần mở Diagnostics)
□ Station nào lỗi → tile đổi màu + nhãn state đổi (màu + chữ, không chỉ màu)
□ KPI có đơn vị, live value đậm > label; không NaN khi chưa có dữ liệu (ProductionStatistics.Empty)
□ Nút đúng CanExecute theo 8-state ISA-88; Stop cách nút thường ≥48px; nút chính ≥60px
□ Đổi ngôn ngữ runtime: mọi nhãn (kể cả state trong tile, status chip) đổi ngay, layout không vỡ
□ Nội dung ≤1400px, không giãn hết màn; screenshot grayscale vẫn đọc được
□ Đổi máy mới: KHÔNG sửa XAML — station tiles/connection chips tự sinh từ IMasterController/IHardwareManagerService
```

## 7. Phần thay đổi khi đổi máy

**Không có.** Toàn bộ Dashboard data-driven qua interface: máy mới chỉ cần đăng ký
MasterController/Stations/hardware devices trong Bootstrapper — tiles và chips tự sinh.
(Đúng nguyên tắc vàng `HMI_UI_Architecture_Template.md` §0.)

---

*Phiên bản 1.0 — Session 44. Chuẩn tham chiếu: ISA-101.01, SEMI E95, SEMI S8, ISA-88/PackML, EEMUA 201.*
