# HMI Calibration Model v1.0 — Mô hình hiệu chỉnh AM.AutoFrame

> **Trạng thái:** CHUẨN HIỆN HÀNH (S84, ROADMAP P2.1). Đầu mối: `HMI_Master_Index.md` §6b.
> Code tương ứng: `ICalibrationRoutine`/`ICalibrationWizard`/`ICalibrationService` (AM.Core.Abstractions),
> `CalibrationService`+`CalibrationWizard` (AM.Services), UI `AM.Modules.Calibration`.

## 1. Calib ≠ Setting — ranh giới cứng

| | Setting (cấu hình) | Calibration (hiệu chỉnh) |
|---|---|---|
| Bản chất | Giá trị tĩnh người nhập | Quy trình động: máy đo → so → bù |
| Cần máy chuyển động? | Không | Có (đo thật trên máy) |
| Kết quả | Giá trị config | Kết quả đo + giá trị bù ghi vào **recipe** + bản ghi lịch sử |
| UI | Form nhập (ParamView) | **Wizard** có bước, trạng thái, lịch sử |

Không trộn: mục calib KHÔNG nằm trong form recipe; giá trị bù do calib ghi vào recipe qua `IRecipeService`
(một nguồn sự thật — sequence đọc recipe như mọi tham số khác).

## 2. Phân loại theo `frequency` — quyết định chỗ đứng trên UI

| `frequency` | Ai làm / bao lâu | UI đặt ở đâu | Ví dụ |
|---|---|---|---|
| **`routine`** | Operator/LineLead — đầu ca, đổi lô, sau alarm lệch | Sub-tab **"Hiệu chỉnh"** trong màn Vận hành tay — **tự ẩn nếu máy không có routine nào** | Offset điểm pick, bù chiều cao đầu hút |
| **`rare`** | Engineer/Admin — sau thay cơ khí, định kỳ tháng | **Cài đặt → Bảo trì & Hiệu chuẩn** | Calib camera↔trục (hand-eye), vuông góc gantry |

Phân loại khai trên từng routine (thuộc tính `Frequency`), KHÔNG cố định theo chức năng — cùng một phép đo
có thể là routine ở máy này, rare ở máy khác. Quyền tối thiểu khai riêng (`MinLevel`) — frequency chỉ quyết định
chỗ đứng, không quyết định quyền.

## 3. Wizard 2 nhánh theo `autoThreshold` — mô hình cốt lõi

```
        ┌────────┐  Đo   ┌──────────┐
  ──────►  Idle  ├──────►│ Measuring │───lỗi───► Failed ──Reset──► Idle
        └────────┘       └─────┬────┘
                               │ kết quả |offset|
              ┌────────────────┴───────────────────┐
              ▼ ≤ autoThreshold                     ▼ > autoThreshold
      ┌───────────────┐                    ┌────────────────┐
      │ WithinThreshold│                    │ OutOfThreshold │ → hiện GuideSteps
      └──────┬────────┘                    └───────┬────────┘   (chỉnh tay từng bước)
             │ Áp bù (1 chạm)                      │ "Đã chỉnh xong — đo lại"
             ▼                                     └────────────► Measuring (lặp đến khi đạt)
       ┌──────────┐
       │ Applying │──► Completed  (ghi recipe + audit + lịch sử)
       └──────────┘
```

- **Trong ngưỡng** → phần mềm bù được: một chạm "Áp bù" → ghi recipe + audit + lịch sử. Không hỏi thêm.
- **Vượt ngưỡng** → lệch quá lớn để bù phần mềm (che giấu vấn đề cơ khí): wizard chuyển nhánh **chỉnh tay**,
  hiện `GuideSteps` từng bước (key i18n), operator chỉnh xong bấm đo lại — lặp đến khi vào ngưỡng.
- **Bất biến an toàn**: wizard KHÔNG tự áp khi vượt ngưỡng, KHÔNG áp khi chưa đo, chỉ áp đúng kết quả đo gần nhất.
- Đo/áp là `MeasureAsync`/`ApplyAsync` của routine — framework không biết nội dung đo (vision, chạm cữ, laser...).

## 4. Lịch sử + audit + nhắc hạn

- **Mỗi lần Completed** ghi `CalibrationRecord` (routineId, thời điểm, user, offset, unit, tự-áp hay sau-chỉnh-tay)
  vào `calibration-history.json` (giữ tối đa 200 bản ghi mới nhất) + `IAuditService.Record`.
- **`requiresCalibAfterChange` + usage counter** (khái niệm, code ở P5 khi có máy thật): routine khai "phải calib lại
  sau khi thay X" — nút "đã thay đầu hút" trên màn Kỹ thuật + bộ đếm chu kỳ từ lần calib cuối; quá hạn → banner nhắc
  + deep-link mở đúng wizard. v1.0 mới lưu lịch sử (đủ dữ liệu tính "bao lâu rồi chưa calib"), chưa có bộ nhắc.

## 5. Lựa chọn thiết kế (ADR-style)

**Chỗ đặt framework** — A: project riêng `AM.Core.Calibration` (như Sequencing) · B: contracts vào
`AM.Core.Abstractions` + implementation vào `AM.Services` ✅. Chọn B: engine calib nhỏ (1 registry + 1 wizard
state machine), không đáng một project; UI modules vốn chỉ reference Abstractions nên contracts phải ở đó dù chọn gì.
Sequencing là project riêng vì engine lớn + chạy độc lập — không phải tiền lệ bắt buộc.

**Đăng ký routine** — A: config JSON thuần (như recovery-actions.json) · B: đăng ký code lúc bootstrap ✅.
Chọn B: khác recovery action (metadata + handler tra sổ), MỘT routine calib là MỘT class có logic đo thật —
config JSON chỉ mô tả được metadata, không mô tả được phép đo. Máy mới viết routine class + `Register()` một dòng.
(Bật/tắt theo máy vẫn làm được ở tầng bootstrap.)

**UI hai chỗ, một module** — A: view riêng cho từng chỗ · B: một `AM.Modules.Calibration` dùng chung, VM nhận
`frequency` filter, nhúng vào Vận hành tay (routine) và Settings (rare) ✅. Chọn B: một wizard duy nhất, khác nhau
chỉ ở danh sách lọc — hai view là code đôi.

**Kết quả bù ghi đâu** — A: file calib riêng · B: ghi vào recipe qua `IRecipeService` ✅. Chọn B (theo Master Index
§6b): sequence/mechanism đã đọc recipe — thêm nguồn thứ hai là mời lệch pha. Giá trị bù là tham số vận hành như mọi
tham số khác; lịch sử calib mới là thứ lưu riêng.

## 6. Hợp đồng code (tóm tắt)

```csharp
enum CalibrationFrequency { Routine, Rare }
enum CalibrationWizardState { Idle, Measuring, WithinThreshold, OutOfThreshold, Applying, Completed, Failed }

interface ICalibrationRoutine
{
    string Id { get; }                       // "demo.pick-offset"
    string DisplayKey { get; }               // key i18n tên routine
    CalibrationFrequency Frequency { get; }
    UserLevel MinLevel { get; }              // quyền tối thiểu chạy wizard
    double AutoThreshold { get; }            // |offset| ≤ ngưỡng → cho áp tự động
    string Unit { get; }                     // "mm", "px"...
    IReadOnlyList<string> GuideStepKeys { get; } // hướng dẫn chỉnh tay (key i18n, theo thứ tự)
    Task<CalibrationMeasurement> MeasureAsync(CancellationToken ct = default);
    Task ApplyAsync(CalibrationMeasurement m, string operatorId, CancellationToken ct = default);
}

interface ICalibrationService   // đăng ký + tạo wizard + lịch sử
{
    void Register(ICalibrationRoutine routine);
    IReadOnlyList<ICalibrationRoutine> Routines { get; }
    ICalibrationWizard CreateWizard(ICalibrationRoutine routine);
    IReadOnlyList<CalibrationRecord> GetHistory(string? routineId = null, int max = 50);
}
```

## 7. Demo (máy DemoPickPlace)

`PickOffsetCalibrationRoutine` (`routine`, LineLead+, ngưỡng 0.05mm — khớp ngưỡng Set–Confirm đã chốt §9):
mô phỏng đo lệch điểm pick (dx, dy) so với vị trí thật; trong ngưỡng → áp vào `PickPositionX/Y` của recipe đang
active qua `IRecipeService.SaveRecipeAsync`; vượt ngưỡng → 3 bước hướng dẫn chỉnh tay (sim: mỗi lần đo lại lệch
giảm dần như thể operator đã chỉnh). Chạy end-to-end trên simulation.
