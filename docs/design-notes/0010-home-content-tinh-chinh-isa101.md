# 0010 — Home v2.1: tinh chỉnh nội dung theo phản biện ISA-101 (card KQ gần nhất, empty state, màu-khi-có-nghĩa)

**Ngày:** 2026-07-02 (Session 74)
**Trạng thái:** Đã chốt, đã triển khai
**Liên quan:** `AM.Modules.Dashboard/DashboardView.xaml(.cs)`, `DashboardViewModel.cs`, `DashboardTileVms.cs`,
`AM.Application.Shell/MainWindow.xaml`, `docs/HMI_Dashboard_Spec.md` (v2 — cần nâng v2.1), ADR 0009

## Bối cảnh

Sau Shell v3 (ADR 0009), chủ dự án nhận bản phản biện 7 điểm cho **vùng nội dung Home** theo tiêu chí
ISA-101 Level 1: *"operator liếc 3 giây phải trả lời được — máy đang thế nào, kết quả gần nhất ra sao,
có gì cần tôi làm không"*. Kèm wireframe HTML. Nhiệm vụ: đánh giá từng điểm, áp cái hợp lý.

## Đánh giá từng điểm

| # | Đề xuất | Quyết định | Ghi chú |
|---|---------|-----------|---------|
| 1 | Thumbnail camera → card "Kết quả gần nhất" nằm ngang (thumb + OK/NG lớn + SN/cycle/recipe) | ✅ Áp | Đúng bệnh: tile camera 140px + nghìn px trống chết. Card bind record mới nhất trong ca; thumb camera thu gọn 96×62 giữ trạng thái kết nối (chấm màu, chữ xám — hết "Sẵn sàng" xanh). Live view vẫn ở tab Vision. Ảnh cycle thật chờ vision service (ADR 0008) |
| 2 | Bảng sản phẩm: empty state + Cycle căn phải + KQ chip màu + footer gộp lên header | ✅ Áp | Empty state "Chưa có sản phẩm trong ca — Khởi tạo → Chạy để bắt đầu ghi nhận"; KQ = `DataGridTemplateColumn` chip nền Ok/Ng. KHÔNG đảo thứ tự cột (giữ SN·Vào·Cycle·Data·Recipe·KQ — Data `Width=*` là cột co giãn hợp lý khi có dữ liệu thật) |
| 3 | KPI: số 26-28px, Lỗi=0 phải xám, yield "—" khi trống, cycle ms→s | ✅ Áp | Số 17→26px; Đạt/Lỗi mặc định có màu + DataTrigger về xám khi =0; `YieldText`/`AvgCycleText` ("—" khi Total=0, tự đổi đơn vị). Ngưỡng đổi màu yield: CHƯA áp — chưa có ngưỡng cấu hình theo máy, tránh magic number (R10) |
| 4 | Quick actions: bỏ 6 dòng "cần quyền…", lock icon + tooltip; tách hàng tiện ích/rủi ro; Gọi KT = Andon viền amber | ✅ Áp | SubText chuyển vào tooltip (style, tự ẩn khi rỗng); `NeedsRole` → icon khoá góc nút; reorder BuzzerOff·WorkLight·Ionizer / SafetyDoor·FeedDoor·CallTech; `IsAndon` viền `Status.WarningBrush` |
| 5 | "B.thường" → viết đủ; thẳng cột key-value | ✅/— | `Safety.OK` → "Không kích hoạt"/"Not triggered"/"未触发" (chỉ dùng cho E-Stop). Layout key-value giữ nguyên — đã là DockPanel tên-trái/giá-trị-phải thẳng hàng |
| 6 | Nhật ký "đất chết" → đổ 4-5 dòng sự kiện | —/✅ | Đã có sẵn từ S45 (OpLog bind state/alarm/cycle events) — phản biện xem mockup tĩnh lúc chưa có event. Chỉ bổ sung **empty state** "Sự kiện vận hành sẽ hiện ở đây" |
| 7 | Tách Reset khỏi cụm vận hành + guard theo state | ✅/— | Thêm divider trước Reset ở action bar Shell. Guard state đã có sẵn (`CanReset` chỉ InitAlarm/RunAlarm — ISA-88 không có Stopped/Aborted như PackML) |
| — | Rail phải 560 → 400-420px | ❌ Không áp | 560px là quyết định spec v2 (S45): quick action 3 cột ≥64px thoải mái, KPI 3×2 không vỡ khi đổi ngôn ngữ (nhãn zh/en dài ngắn khác nhau). Wireframe 244px là tỉ lệ thu nhỏ, không phải số đo thật. Xét lại khi sync template v3 |

## Ba nguyên tắc rút ra (đưa vào template khi nâng v3)

1. **Màu chỉ xuất hiện khi có ý nghĩa trạng thái** — Lỗi=0 xám, KQ OK/NG mới có màu, camera bình thường = chấm xanh nhỏ chứ không chữ xanh.
2. **Mọi vùng trống phải nói cho operator bước tiếp theo** — empty state có hướng dẫn (bảng sản phẩm, nhật ký, card KQ).
3. **Thông tin xếp theo tần suất liếc nhìn** — KQ gần nhất > KPI ca > thao tác > log.

## Hệ quả

- `DashboardViewModel`: +`HasLatest`/`LatestSn`/`LatestCycleText`/`LatestRecipeName`/`LatestResultText`/`LatestIsPassed` (từ record mới nhất), +`YieldText`/`AvgCycleText` (`FormatCycle`); `QuickActionVm` +`IsAndon`/`NeedsRole`.
- i18n +4 key (`Dash.EmptyTitle/EmptyHint/NoCycle/LogEmpty`) vi/en/zh; sửa `Safety.OK`.
- **Nợ tài liệu:** `HMI_Dashboard_Spec.md` v2 mô tả "dải thumbnail vision" — cần nâng v2.1 cùng đợt sync template v3 (TODO đã ghi ở ADR 0009).
- Ảnh cycle trong card KQ là placeholder tối — nối ảnh thật khi vision service IPC (ADR 0008) xong.
