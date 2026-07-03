# SequenceEngine_Spec v1.0 — AM.AutoFrame

> Chuẩn thiết kế cho Claude Code khi triển khai `AM.Core.Sequencing`.
> Nguyên tắc gốc: **sequence là dữ liệu, engine là generic, station là plugin**.
> Máy mới = viết station mới + file sequence mới. Engine và shell không đổi.
> Ràng buộc HAL: mọi thứ trong tài liệu này KHÔNG được tham chiếu vendor type.

## 1. Hợp đồng cốt lõi

```csharp
namespace AM.Core.Sequencing;

public enum StationStatus { Ok, Ng, Skipped, Error }

/// Hành động khi bước gặp LỖI MÁY (không phải NG nghiệp vụ)
public enum StepErrorAction { Retry, Skip, Pause, Abort }

public sealed record StationResult(
    StationStatus Status,
    string? Message = null,
    IReadOnlyDictionary<string, object>? Data = null)
{
    public static StationResult Ok(IReadOnlyDictionary<string, object>? data = null)
        => new(StationStatus.Ok, null, data);
    public static StationResult Ng(string reason,
        IReadOnlyDictionary<string, object>? data = null)
        => new(StationStatus.Ng, reason, data);
    public static StationResult Fail(string message)
        => new(StationStatus.Error, message);
}

/// Ngữ cảnh một bước — mọi truy cập phần cứng đi qua đây (HAL), station không tự resolve
public sealed class StepContext
{
    public required ProductContext Product { get; init; }      // SN, carrier, trạng thái NG tích lũy
    public required IRecipeView Recipe { get; init; }          // tham số recipe, read-only
    public required IDictionary<string, object> Blackboard { get; init; } // chia sẻ dữ liệu giữa các bước trong 1 cycle
    public required bool IsDryRun { get; init; }
    public required ILogger Logger { get; init; }
    public required IIoService Io { get; init; }               // DI/DO/AI theo tên logic (IoMap)
    public required IMotionService Motion { get; init; }       // trục theo tên logic
}

public interface IStation
{
    /// Tên logic — phải khớp trường "station" trong file sequence.
    /// Đăng ký qua DryIoc keyed registration, KHÔNG dùng switch-case.
    string Name { get; }

    /// Homing / self-check khi máy Initialize. Idempotent.
    Task InitializeAsync(CancellationToken ct);

    /// Thực thi một bước cho một sản phẩm. Bắt buộc tôn trọng ct:
    /// khi ct hủy phải đưa cơ cấu về trạng thái an toàn rồi ném OperationCanceledException.
    Task<StationResult> ExecuteAsync(StepContext ctx, CancellationToken ct);

    /// Đưa trạm về trạng thái sẵn sàng sau Stop/Abort.
    Task ResetAsync(CancellationToken ct);
}
```

## 2. Định dạng khai báo sequence (JSON, gắn theo recipe)

```json
{
  "name": "DemoPickPlace",
  "version": 1,
  "settings": {
    "continueMode": "UntilStopped",
    "maxProductsInFlight": 1
  },
  "steps": [
    { "id": "scan",   "station": "ScannerStation", "order": 10,
      "timeoutMs": 3000, "onError": "Retry", "retry": 2, "onRetryExhausted": "Pause" },

    { "id": "feed",   "station": "FeedStation",    "order": 10,
      "timeoutMs": 5000, "onError": "Retry", "retry": 1, "onRetryExhausted": "Pause" },

    { "id": "pick",   "station": "PickStation",    "order": 20,
      "timeoutMs": 4000, "onError": "Pause" },

    { "id": "vision", "station": "VisionStation",  "order": 30,
      "timeoutMs": 2000, "onError": "Retry", "retry": 1, "onRetryExhausted": "Skip",
      "skipCountsAsNg": true },

    { "id": "place",  "station": "PlaceStation",   "order": 40,
      "timeoutMs": 4000, "onError": "Pause", "runOnNg": true },

    { "id": "report", "station": "ReportStation",  "order": 50,
      "timeoutMs": 8000, "onError": "Skip", "runOnNg": true }
  ]
}
```

### Quy tắc ngữ nghĩa

| Trường | Quy tắc |
|---|---|
| `order` | Bước cùng `order` chạy song song (`Task.WhenAll`). Nhóm sau chỉ bắt đầu khi nhóm trước xong toàn bộ. |
| `timeoutMs` | Engine bọc `ExecuteAsync` bằng linked CancellationToken. Hết giờ = lỗi máy, áp `onError`. |
| `onError` | Chỉ áp cho `StationStatus.Error` và timeout. `Retry` chạy lại tối đa `retry` lần, hết thì áp `onRetryExhausted`. |
| `Pause` | Engine dừng ở ranh giới bước, phát sự kiện gọi operator (banner + còi). Operator chọn Retry / Skip / Abort trên UI. |
| `Ng` | Là kết quả nghiệp vụ, KHÔNG áp `onError`. Sản phẩm đánh dấu NG, các bước sau mặc định bị bỏ qua trừ bước có `runOnNg: true` (vd: PlaceStation đặt vào khay NG, ReportStation vẫn ghi dữ liệu). |
| `skipCountsAsNg` | Bước bị Skip thì sản phẩm tính NG (mặc định false — Skip vẫn OK). |
| Validate lúc nạp | Tên station phải resolve được từ DI; `order` trùng phải không tranh chấp tài nguyên (khai báo `resources` nếu cần, v1 có thể bỏ); key lạ → warning, không crash. |

## 3. Engine

```csharp
public interface ISequenceEngine
{
    SequenceRunState State { get; }   // Idle, Running, Pausing, Paused, Stopping

    event EventHandler<StepEventArgs> StepStarted;
    event EventHandler<StepEventArgs> StepCompleted;      // kèm StationResult + thời gian bước
    event EventHandler<ProductEventArgs> ProductCompleted; // KQ cuối + tổng cycle time
    event EventHandler<OperatorPromptEventArgs> OperatorPromptRequired; // từ onError: Pause

    /// Gọi từ trạng thái Execute của master controller. Chạy vòng lặp sản phẩm
    /// cho tới khi ct hủy hoặc điều kiện dừng (hết liệu, đủ số lượng).
    Task RunAsync(SequenceDefinition sequence, CancellationToken ct);

    /// Dừng ở RANH GIỚI BƯỚC kế tiếp (không cắt giữa bước). Ánh xạ PackML Hold/Suspend.
    void RequestPause();
    void Resume();
}
```

### Ánh xạ PackML / ISA-88 (Stateless state machine ở master controller)

| Lệnh UI | Master controller | Engine |
|---|---|---|
| Khởi tạo | Uninitialized → Initializing: gọi `InitializeAsync` mọi station theo thứ tự khai báo | — |
| Chạy | → Execute | `RunAsync(seq, executeCt)` |
| Tạm dừng | → Holding → Held | `RequestPause()` — dừng ở ranh giới bước |
| Tiếp tục | Held → Execute | `Resume()` |
| Dừng | → Stopping → Stopped | hủy `executeCt`; station tự đưa về an toàn; sản phẩm dở đánh dấu Aborted trong log |
| Abort / E-Stop | → Aborting → Aborted | hủy token + tầng an toàn cắt cứng (ngoài engine); chạy lại bắt buộc qua Reset → Khởi tạo |
| Reset | Stopped/Aborted → Idle | `ResetAsync` mọi station |

### Bất biến engine phải giữ

1. Engine KHÔNG biết trạm cụ thể nào tồn tại — chỉ resolve `IStation` theo tên từ DI (DryIoc keyed). Thêm máy mới không sửa engine.
2. Engine KHÔNG gọi phần cứng trực tiếp — chỉ station làm việc đó, qua HAL trong `StepContext`.
3. Mọi thời gian bước, kết quả, retry đều phát ra sự kiện → Dashboard (bảng sản phẩm, KPI, mini log) và log file cùng ăn một nguồn.
4. Dry-run: engine truyền `IsDryRun` xuống; QUYẾT ĐỊNH bỏ gì là của station (vd PickStation không bật vacuum), không phải của engine.
5. Single-step (giai đoạn 2): engine chèn điểm chờ xác nhận sau mỗi nhóm `order` — không đổi hợp đồng `IStation`.

## 4. Yêu cầu test (xUnit + Moq — liên hệ Ch18 của sách)

Engine phải test được KHÔNG cần phần cứng. Bộ test tối thiểu:

- [ ] Chạy tuần tự đúng thứ tự `order`; cùng `order` chạy song song.
- [ ] Timeout bước → áp `onError` đúng nhánh (Retry đếm đúng số lần → `onRetryExhausted`).
- [ ] `Ng` không trigger `onError`; bước sau bị bỏ trừ `runOnNg`.
- [ ] `RequestPause` không cắt giữa bước; Resume chạy tiếp đúng bước kế.
- [ ] Hủy token giữa bước → engine chờ station thoát, trạng thái về Stopping → dừng sạch.
- [ ] Sequence JSON tên station không tồn tại → fail lúc nạp, không fail lúc chạy.
