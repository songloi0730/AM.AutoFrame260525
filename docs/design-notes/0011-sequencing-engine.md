# 0011 — Thiết kế AM.Core.Sequencing (sequence engine khai báo, station plugin)

**Ngày:** 2026-07-02 (Session 76) · duyệt + hiệu chỉnh S77
**Trạng thái:** ĐÃ DUYỆT (chủ dự án ủy quyền tự đánh giá, S77) — kèm 2 hiệu chỉnh triển khai ở cuối file
**Nguồn thiết kế (và CHỈ những nguồn này):** `docs/SequenceEngine_Spec.md` (hợp đồng đã chốt),
`docs/private/Sequence_Requirements_RefSeqA.md` *(local, không commit — trích dẫn "req §n")*,
`docs/DemoMachine_IO_Map.md`. KHÔNG mở lại dự án tham khảo RefSeq-A.
**Phạm vi:** thiết kế phần spec CHƯA quy định. Không đổi hợp đồng §1–§4 của spec.

## Bối cảnh

Spec chốt: *sequence là dữ liệu, engine là generic, station là plugin* — máy mới = station mới
+ file sequence mới, engine/shell không đổi. Spec đã cho hợp đồng `IStation`/`StepContext`/
`StationResult`/`ISequenceEngine`, format JSON, 5 bất biến, 6 test case. Còn bỏ ngỏ: cấu trúc
loader/validator, cách đăng ký-resolve station, vòng lặp chi tiết (retry/prompt/pause), tích hợp
hành vi học từ RefSeq-A, đường sự kiện ra dashboard/log, và ranh giới giai đoạn 2. ADR này chốt
các phần đó ở mức **pseudocode** — code thuộc Prompt C sau khi duyệt.

**Vị trí dự án:** project mới `AM.Core.Sequencing` (net9 class lib) — chỉ reference
`AM.Core` + `AM.Core.Abstractions`. KHÔNG reference DryIoc/Prism/hardware (xem §2 — engine
resolve station qua abstraction, không qua container trực tiếp).

---

## §1. Nạp và validate SequenceDefinition

### Model (record, immutable sau nạp)

```csharp
sealed record SequenceSettings(string ContinueMode /* UntilStopped|SingleCycle */,
                               int MaxProductsInFlight /* v1: phải =1, xem §6 */);

sealed record SequenceStep(string Id, string Station, int Order, int TimeoutMs,
                           StepErrorAction OnError, int Retry,
                           StepErrorAction? OnRetryExhausted,
                           bool RunOnNg, bool SkipCountsAsNg);

sealed record SequenceDefinition(string Name, int Version,
                                 SequenceSettings Settings,
                                 IReadOnlyList<SequenceStep> Steps);
```

### Loader — 2 pha, lỗi gom một lần

`SequenceLoader.Load(json, IStationResolver)` → `SequenceDefinition` hoặc ném
`SequenceValidationException` chứa **toàn bộ** lỗi (không fail-fast từng lỗi — operator/kỹ sư
sửa file 1 lần thay vì thử-sai n lần):

- **Pha 1 — parse + schema** (System.Text.Json, DTO riêng → map sang record):
  thiếu trường bắt buộc (`id`/`station`/`order`/`timeoutMs`), kiểu sai, JSON hỏng.
  Key lạ → **warning** (log, không chặn — spec §2 quy tắc validate).
- **Pha 2 — ngữ nghĩa** (trên model đã parse):

| Quy tắc | Mức |
|---|---|
| `id` trùng nhau | Error |
| `station` không resolve được từ DI (qua `IStationResolver.Contains`) | Error — **fail lúc nạp, không lúc chạy** (test case 6) |
| `order` âm · `timeoutMs ≤ 0` · `retry < 0` | Error |
| `onError = Retry` mà `retry = 0` | Error (retry vô nghĩa) |
| `onError ≠ Retry` mà khai `retry`/`onRetryExhausted` | Warning (bỏ qua) |
| `onError = Retry` thiếu `onRetryExhausted` | Default = `Pause` (an toàn nhất — operator quyết) |
| `maxProductsInFlight ≠ 1` | Error ở v1 (§6) |
| Cùng `order` (chạy song song) — chưa khai `resources` | Warning (v1 không có model tài nguyên — §6) |

**Lỗi lúc nạp vs lúc chạy** — nguyên tắc phân loại: *mọi thứ đọc được từ file + container đều
phải chết lúc nạp* (tên station, kiểu enum, ràng buộc số học); *lúc chạy chỉ còn lỗi thế giới
thật* (timeout, hardware, kết quả NG). Đây là thuốc trực tiếp cho RefSeq-A req §4 — "chính sách
lỗi nằm rải trong code từng bước, không có bảng khai báo".

**Thời điểm nạp:** khi nạp recipe (sequence gắn theo recipe — spec §2 tiêu đề). Nạp fail →
recipe bị từ chối, máy giữ nguyên recipe cũ; alarm nhóm 60000 (Production/Recipe).

## §2. Đăng ký và resolve station — DryIoc keyed, qua abstraction

**Phương án cân nhắc:**
- (a) Engine gọi thẳng `IContainer.Resolve<IStation>(serviceKey)` — ngắn, nhưng
  `AM.Core.Sequencing` phải reference DryIoc, engine không test được bằng fake thuần,
  vi phạm tinh thần R03 (Bootstrapper là nơi DUY NHẤT biết container).
- (b) **(CHỌN)** interface mỏng trong `AM.Core.Sequencing`:

```csharp
public interface IStationResolver
{
    bool Contains(string name);          // validator dùng lúc nạp
    IStation Resolve(string name);       // engine dùng lúc chạy (đã validate nên không miss)
    IReadOnlyList<string> AllNames();    // chẩn đoán/UI liệt kê
}
```

Implementation DryIoc nằm ở `AM.Application.Shell` (composition root):

```csharp
// Bootstrapper — đăng ký station theo tên logic (khớp trường "station" trong JSON)
container.Register<IStation, ScannerStation>(serviceKey: ScannerStation.StationName,
    reuse: Reuse.Singleton);
container.Register<IStation, PickStation>(serviceKey: PickStation.StationName,
    reuse: Reuse.Singleton);
// ...

sealed class DryIocStationResolver(IContainer c) : IStationResolver
{
    public bool Contains(string name) => c.IsRegistered<IStation>(serviceKey: name);
    public IStation Resolve(string name) => c.Resolve<IStation>(serviceKey: name);
    public IReadOnlyList<string> AllNames() =>
        c.GetServiceRegistrations().Where(r => r.ServiceType == typeof(IStation))
         .Select(r => (string)r.OptionalServiceKey!).ToList();
}
```

- Tên key = `const string StationName` trên chính station (một nguồn — không magic string
  lặp; JSON là nơi duy nhất "gõ tay" tên, và validator bắt sai chính tả **ngay lúc nạp** bằng
  `Contains`, kèm gợi ý `AllNames()` trong message lỗi).
- Test: fake `IStationResolver` trả mock `IStation` — engine test không cần DryIoc (Prompt C).
- Giữ bất biến 1: engine không biết trạm cụ thể; thêm máy = thêm đăng ký ở Bootstrapper.

## §3. Vòng lặp sản phẩm — pseudocode

```text
RunAsync(seq, executeCt):
  State = Running
  groups = seq.Steps.GroupBy(Order).OrderBy(Order)     // chuẩn bị 1 lần
  loop:                                                 // mỗi vòng = 1 sản phẩm
    if executeCt.IsCancellationRequested → break
    await PauseGate.WaitAsync(executeCt)                // (i) ranh giới SẢN PHẨM
    product   = new ProductContext(...)                 // SN điền dần (ScannerStation ghi vào)
    blackboard = new Dictionary<string,object>()        // sống đúng 1 cycle — xóa là hết
                                                        //   (thuốc cho req §3 Reset: "bit cũ làm
                                                        //    cycle mới chạy sai" không thể xảy ra)
    foreach group in groups:
      await PauseGate.WaitAsync(executeCt)              // (ii) ranh giới BƯỚC — RequestPause dừng ở đây
      runnable = group.Where(s => !product.IsNg || s.RunOnNg)   // Ng bỏ bước sau, trừ runOnNg
      foreach skipped in group.Except(runnable):
          emit StepCompleted(skipped, Skipped-by-Ng)    // log vẫn thấy đủ bước
      results = await Task.WhenAll(runnable.Select(s => RunStepAsync(s, product, blackboard, executeCt)))
      if results.Any(Aborted) → product.MarkAborted; break
    emit ProductCompleted(product, tổng thời gian)      // 1 nguồn cho KPI/bảng SP/log (§5)
    if seq.Settings.ContinueMode == SingleCycle → break
  State = Idle

RunStepAsync(step, product, blackboard, executeCt):
  station = resolver.Resolve(step.Station)              // chắc chắn có (validated §1)
  attempt = 0
  loop:
    emit StepStarted(step, attempt)
    sw = Stopwatch.StartNew()
    using linked = CancellationTokenSource.CreateLinkedTokenSource(executeCt)   // CA2000: using var
    linked.CancelAfter(step.TimeoutMs)
    try:
      ctx = new StepContext { Product=product, Blackboard=blackboard, IsDryRun=..., Io=..., Motion=..., ... }
      result = await station.ExecuteAsync(ctx, linked.Token)
    catch OperationCanceledException when executeCt.IsCancellationRequested:
      rethrow                                           // Stop/Abort thật — thoát sạch (test case 5)
    catch OperationCanceledException:
      result = StationResult.Fail($"Timeout {step.TimeoutMs}ms")   // timeout = lỗi máy (spec §2)
    catch Exception ex when ex is not SequenceAbortException:      // RSPEC-2139
      result = StationResult.Fail(ex.Message)           // exception bất ngờ = lỗi máy, không giết engine
    emit StepCompleted(step, result, sw.Elapsed, attempt)

    switch result.Status:
      Ok      → merge result.Data vào blackboard; return
      Skipped → if step.SkipCountsAsNg → product.MarkNg("skipped"); return
      Ng      → product.MarkNg(result.Message); return  // NGHIỆP VỤ — KHÔNG áp onError (spec §2)
      Error   →
        action = attempt < step.Retry ? step.OnError
                                      : (step.OnError == Retry ? step.OnRetryExhausted : step.OnError)
        switch action:
          Retry → attempt++; continue loop
          Skip  → if step.SkipCountsAsNg → product.MarkNg; return
          Abort → throw SequenceAbortException           // master controller fire Error trigger
          Pause →                                        // hỏi operator — KHÔNG chặn thread (§4.3)
            State = Paused
            prompt = new OperatorPromptEventArgs(step, result, choices: [Retry, Skip, Abort])
            emit OperatorPromptRequired(prompt)
            decision = await prompt.Decision              // TaskCompletionSource, hủy theo executeCt
            State = Running
            switch decision: Retry → attempt = 0; continue loop   // operator retry = đếm lại từ đầu
                             Skip  → như Skip ở trên
                             Abort → throw SequenceAbortException
```

**Các quyết định trong pseudocode:**

- **PauseGate** = `AsyncManualResetEvent` (SemaphoreSlim-based). `RequestPause()` đóng gate —
  đang giữa `Task.WhenAll` thì nhóm hiện tại **chạy nốt** rồi mới dừng (spec: không cắt giữa
  bước); `Resume()` mở gate **sau khi resume-check đạt** (§4.1). Trạng thái `Pausing` giữ từ
  lúc request tới lúc nhóm hiện tại xong.
- **Timeout** phân biệt với Stop bằng exception filter `when executeCt.IsCancellationRequested`
  — đúng pattern CS03 của dự án.
- **Operator Retry sau prompt đếm lại `attempt = 0`**: operator đã can thiệp vật lý (rút liệu
  kẹt...), ngữ cảnh lỗi cũ không còn — khớp hành vi popup "kiểm lại" lặp vô hạn của RefSeq-A
  (req §4: retry ∞ do operator quyết) nhưng có kiểm soát.
- **Blackboard sống 1 cycle**, tạo mới mỗi sản phẩm; kiểu `IDictionary<string,object>` theo spec.
  Key convention: `"{stepId}.{field}"` (vd `"scan.SN"`) — tránh 2 bước song song ghi đè nhau.

## §4. Hành vi học từ RefSeq-A (trích số mục requirements)

### 4.1 Resume-check — xác minh cơ cấu trước khi chạy tiếp (req §3-Resume, §10b.1)

RefSeq-A trước khi resume so vị trí mọi trục + trạng thái mọi xi lanh với lúc pause — lệch
(bị đẩy tay) thì **từ chối resume**. Đưa vào thiết kế mà KHÔNG đổi `IStation` (bất biến spec):
capability interface tuỳ chọn, theo đúng tiền lệ `IAxisDiagnostics`:

```csharp
public interface IResumeVerifiable   // station nào có cơ cấu thì implement
{
    /// Trả Ok nếu cơ cấu còn đúng trạng thái lúc pause; Fail kèm mô tả nếu bị xê dịch.
    Task<StationResult> VerifyResumeAsync(CancellationToken ct);
}
```

`Resume()` không mở gate ngay: engine chạy `VerifyResumeAsync` trên mọi station của sequence
có implement — **bất kỳ Fail nào → giữ Paused + phát `OperatorPromptRequired`** (nội dung: trạm
X báo lệch, chọn [Kiểm lại] / [Abort]). Chi phí station: lưu snapshot vị trí trong `OnPause`
— v1 đơn giản: engine phát thêm sự kiện nội bộ `Paused` để station snapshot (hoặc station
snapshot cuối `ExecuteAsync` — để Prompt C chọn khi viết demo).

### 4.2 Init phát hiện liệu sót + hỏi operator (req §2.4, §8, §10b.2)

RefSeq-A: init kiểm cảm biến còn liệu trên bàn → hỏi "lấy tay / máy tự thoát"; sau E-Stop bắt
buộc Reset → re-init (req §8). Thiết kế: đây là việc của **station** (`InitializeAsync` của
PickStation kiểm `DI.Nozzle.VacuumOn`, PlaceStation kiểm khay...), KHÔNG phải của engine —
nhưng station cần kênh hỏi operator không dính UI. Tách service:

```csharp
public interface IOperatorPrompt     // AM.Core.Abstractions/Interfaces/Services
{
    /// Hỏi operator, chờ chọn. KHÔNG chặn UI thread — UI subscribe và hiển thị banner/dialog.
    Task<string> AskAsync(OperatorPromptRequest request, CancellationToken ct);
}
```

- Engine dùng CHÍNH service này bên trong nhánh `Pause` (§3) rồi mirror ra event
  `OperatorPromptRequired` (spec giữ event trên `ISequenceEngine` — dashboard/log cùng thấy).
- Station init inject `IOperatorPrompt` qua constructor (station là plugin DI) — dùng được cả
  ngoài lúc engine chạy (Initialize do master controller gọi).
- Implementation UI ở Shell (banner + 2 nút — theo mẫu multi-alarm banner hiện có), sim/test
  dùng fake trả lời sẵn.

### 4.3 OperatorPromptRequired thay popup chặn thread (req §4, anti-pattern §10 dòng 4)

RefSeq-A hiện `MessageBox` NGAY TRONG thread trạm — logic dính chết WinForms, không test được.
Thiết kế trên (§3 nhánh Pause + §4.2) thay bằng: engine/station chỉ `await` một `Task<string>`;
ai trả lời (UI thật, fake test, thậm chí remote) là chuyện của DI. Popup timeout 3 lựa chọn
[chờ tiếp]/[bỏ qua]/[thoát flow] của RefSeq-A map thành choices `[Retry]/[Skip]/[Abort]`,
riêng "bỏ qua chỉ hiện ở chế độ kỹ sư" map thành filter choices theo `UserLevel` ở tầng UI
(engine gửi đủ, UI cắt theo quyền — engine không biết user).

## §5. Luồng sự kiện — một nguồn, hai consumer (bất biến 3; req §9)

RefSeq-A cần 3 đầu ra từ cùng thông tin bước: dashboard, log file, persist-vị-trí (req §9).
Engine phát 4 sự kiện (spec §3) — **không consumer nào được engine biết tên**:

```
SequenceEngine ──StepStarted/StepCompleted/ProductCompleted/OperatorPromptRequired──▶
   ├─ (1) SequenceProductionBridge (AM.Services):
   │      ProductCompleted → IProductionService.Record… → CycleCompleted event
   │      → Dashboard hiện có (card KQ gần nhất, bảng SP, KPI ca) ăn nguyên đường cũ,
   │        KHÔNG tạo đường dữ liệu riêng cho UI.
   │      StepStarted/Completed → OpLog (mini log) qua event đã có của Dashboard VM.
   └─ (2) SequenceLogSink (AM.Infrastructure):
          StepStarted/Completed → ILogger structured ([SN] [step] [status] [duration]) —
          đồng thời persist "bước đang chạy" ra file (req §10b.4 — chẩn đoán sau crash;
          v1 chỉ ghi, chưa làm resume-from-step).
```

Quy tắc: sự kiện phát trên thread pool, `EventArgs` bất biến (record), consumer tự marshal
UI thread (pattern RunOnUi đã dùng toàn dự án). Đo thời gian bước là việc của ENGINE
(req §10b.5) — station không tự đo.

## §6. Để giai đoạn 2 — và lý do

| Hoãn | Lý do |
|---|---|
| **Single-step mode** | Spec bất biến 5 đã chừa chỗ (chèn điểm chờ sau mỗi nhóm order, không đổi `IStation`); máy demo chưa cần; RefSeq-A cũng không có (req §7). Làm sau khi engine chạy ổn để không nở phạm vi test v1. |
| **Pipeline `maxProductsInFlight > 1`** | Cần model chiếm-giữ station + hàng đợi giữa nhóm; RefSeq-A thực tế cũng chỉ 1 sản phẩm/lượt (req §5) — song song thật duy nhất là upload lệch pha, mà ta đã cover bằng `runOnNg` + bước report riêng. Validator v1 khóa `=1` để không ai bật nhầm. |
| **Khai báo `resources` chống tranh chấp order-song-song** | Chỉ có ý nghĩa khi có pipeline hoặc nhiều cơ cấu chung; DemoPickPlace các bước cùng `order` (scan∥feed) không đụng chung cơ cấu. V1: warning lúc nạp (nhắc người viết sequence tự chịu trách nhiệm), model tài nguyên làm cùng pipeline. |
| **Resume-from-step sau crash** | Persist đã có ở §5 sink; phần *đọc lại và nhảy tới bước* đụng ngữ nghĩa an toàn (cơ cấu ở đâu sau crash?) — cần chốt chính sách với chủ dự án trước, tránh làm nửa vời (tiền lệ adoption §9). |
| **Hook `OnPause` cho thiết bị chạy nền** (req §3-Pause ghi chú) | Chưa có station nền nào trong demo; thêm capability interface sau như `IResumeVerifiable`. |

## §7. Bảng đối chiếu anti-pattern RefSeq-A (req §10 — 14 dòng) → thiết kế này tránh bằng cách nào

| # | Anti-pattern RefSeq-A | Thiết kế này |
|---|---|---|
| 1 | Thread-per-station + `Thread.Abort()` | Một vòng lặp async duy nhất; hủy bằng `CancellationToken`, station tự về an toàn (spec `IStation`). |
| 2 | EMG bằng ném exception từ hàm poll rải ~100 chỗ | Token kiểm ở 2 gate ranh giới (§3) + linked token per-step — điểm cắt tập trung, đếm được. |
| 3 | Busy-wait `Thread.Sleep` trong mọi vòng chờ | Engine thuần `await`; chờ IO là việc của HAL (`IIoService` event/TCS), không của engine. |
| 4 | MessageBox chặn ngay trong thread trạm | `IOperatorPrompt.AskAsync` + event `OperatorPromptRequired` (§4.3) — không biết UI. |
| 5 | Magic string ngôn ngữ gốc cho IO/trạm + ép kiểu chéo | Tên station validate lúc nạp qua `IStationResolver` (§2); IO qua hằng `IoMap` (IO map §7). |
| 6 | God base class ~3.400 dòng | Engine/loader/resolver/prompt/log-sink là 5 đơn vị nhỏ, station chỉ implement 4 method. |
| 7 | Singleton `GetInstance()` mọi manager | Mọi phụ thuộc qua constructor DI (DryIoc ở composition root, engine không thấy container). |
| 8 | Trạm A chờ bit cứng của trạm B | Thứ tự là DỮ LIỆU (`order` trong JSON); chia sẻ giá trị qua Blackboard theo key `{stepId}.{field}`. |
| 9 | State machine enum+switch ~90 bước/3.900 dòng | Mỗi bước là 1 lần `ExecuteAsync` phạm vi nhỏ; nhánh lỗi khai báo (`onError`/`retry`) thay nhảy bước bằng gán biến. |
| 10 | Cấu hình rải XML + INI + hằng code | Sequence gắn theo recipe, nạp + validate một chỗ (§1), tham số qua `IRecipeView`. |
| 11 | Song ngữ if/else lặp mọi method | Engine phát dữ liệu thô (id, status, duration); chữ hiển thị là việc của UI + `ILocalizationService`. |
| 12 | Mọi warning mức Error → EMG toàn máy (side-effect ẩn) | Chỉ `onError`/`onRetryExhausted` = `Abort` mới dừng máy — khai báo trong JSON, truy được "vì sao dừng" từ StepCompleted cuối. |
| 13 | Timeout mặc định 600 s | `timeoutMs` bắt buộc per-step, `≤ 0` fail lúc nạp (§1) — không có fallback. |
| 14 | `catch (Exception) {}` nuốt lỗi trong thread log | Exception filter theo CS04; lỗi bất ngờ thành `StationResult.Error` + phát sự kiện, không nuốt. |

---

## Hệ quả / việc mở khi triển khai (Prompt C)

- Contracts mới cần thêm vào codebase: `IIoService`/`IMotionService` (HAL theo tên logic —
  adapter trên `IIoModule`/`IMotionController` hiện có), `IRecipeView`, `ProductContext`,
  `IStationResolver`, `IOperatorPrompt`, `IResumeVerifiable`, các `EventArgs` (record, CA1003).
- Master controller nối engine theo bảng PackML/ISA-88 của spec §3 (Prompt D).
- Test theo spec §4 (6 case) + validator (thiếu trường, order âm, station trùng id) — station
  mock thuần, fake `IStationResolver`.
- Điểm chưa chốt để hỏi khi triển khai: snapshot resume-check do station tự lưu hay engine
  phát sự kiện `Paused` (§4.1 — đề xuất: station tự lưu, đơn giản hơn).

## Hiệu chỉnh khi duyệt (S77 — tự đánh giá được ủy quyền)

1. **Resume-check snapshot: station TỰ LƯU** (chốt phương án đề xuất §4.1). Station là nơi duy nhất
   biết cơ cấu nào cần so; engine phát thêm sự kiện `Paused` nội bộ là rò kiến thức cơ cấu vào
   engine — vi phạm tinh thần bất biến 1–2. Engine chỉ gọi `VerifyResumeAsync` khi Resume.
2. **Nhánh Pause: engine CHỈ phát event** `OperatorPromptRequired`, kênh trả lời (`Respond`)
   nằm ngay trong EventArgs — bỏ tầng "engine gọi service rồi mirror ra event" ở §4.2 (thừa một
   indirection, khó test hơn). `IOperatorPrompt` vẫn được định nghĩa làm contract cho **station**
   dùng lúc `InitializeAsync` (triển khai UI + adapter ở Prompt D) — một nguồn hỏi, hai ngữ cảnh dùng.
