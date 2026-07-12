// -------------------------------------------------------
// File:    SequenceEngine.cs
// Project: AM.Core.Sequencing
// Purpose: Engine chạy sequence khai báo — vòng lặp sản phẩm, nhóm order song song,
//          timeout/retry/prompt theo chính sách khai báo, pause ở ranh giới bước (ADR 0011 §3)
// -------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AM.Core.Sequencing;

/// <summary>
/// Triển khai <see cref="ISequenceEngine"/>. Xem pseudocode + quyết định thiết kế ở
/// <c>docs/design-notes/0011-sequencing-engine.md</c> §3–§4.
/// </summary>
public sealed class SequenceEngine : ISequenceEngine
{
    private static readonly StepErrorAction[] ErrorPromptChoices =
        [StepErrorAction.Retry, StepErrorAction.Skip, StepErrorAction.Abort];
    private static readonly StepErrorAction[] ResumePromptChoices =
        [StepErrorAction.Retry, StepErrorAction.Abort];

    private readonly IStationResolver _resolver;
    private readonly ISequenceRuntimeContext _runtime;
    private readonly ILogger<SequenceEngine> _logger;
    private readonly Lock _sync = new();

    private bool _pauseRequested;
    private bool _abortAfterResume;
    private bool _singleStep;
    private bool _singleStepArmed; // gate hiện tại do single-step tạo (không phải RequestPause)
    private TaskCompletionSource<bool>? _resumeTcs;
    private SequenceDefinition? _current;
    private CancellationToken _runCt;

    /// <inheritdoc/>
    public SequenceRunState State { get; private set; } = SequenceRunState.Idle;

    /// <inheritdoc/>
    public bool SingleStep
    {
        get { lock (_sync) { return _singleStep; } }
        set
        {
            lock (_sync) { _singleStep = value; }
            _logger.LogInformation("SingleStep {State} — {Detail}",
                value ? "BẬT" : "TẮT",
                value ? "dừng ở ranh giới sau mỗi nhóm bước" : "chạy liên tục");
        }
    }

    /// <inheritdoc/>
    public bool IsWaitingStep
    {
        get { lock (_sync) { return _singleStepArmed && _pauseRequested && State == SequenceRunState.Paused; } }
    }

    /// <inheritdoc/>
    public event EventHandler<StepEventArgs>? StepStarted;

    /// <inheritdoc/>
    public event EventHandler<StepEventArgs>? StepCompleted;

    /// <inheritdoc/>
    public event EventHandler<ProductEventArgs>? ProductCompleted;

    /// <inheritdoc/>
    public event EventHandler<OperatorPromptEventArgs>? OperatorPromptRequired;

    /// <summary>Tạo engine.</summary>
    /// <param name="resolver">Resolve station theo tên logic (composition root cung cấp).</param>
    /// <param name="runtime">Nguồn HAL + recipe + cờ dry-run để dựng StepContext.</param>
    /// <param name="logger">Logger của engine.</param>
    public SequenceEngine(IStationResolver resolver, ISequenceRuntimeContext runtime,
        ILogger<SequenceEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(logger);
        _resolver = resolver;
        _runtime = runtime;
        _logger = logger;
    }

    // ─── Vòng lặp sản phẩm ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task RunAsync(SequenceDefinition sequence, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        lock (_sync)
        {
            if (State != SequenceRunState.Idle)
                throw new InvalidOperationException($"Engine đang {State} — không thể RunAsync chồng.");
            State = SequenceRunState.Running;
            _current = sequence;
            _runCt = ct;
            _pauseRequested = false;
            _abortAfterResume = false;
            _singleStepArmed = false;
        }
        _logger.LogInformation("Sequence '{Name}' v{Version} bắt đầu ({Steps} bước)",
            sequence.Name, sequence.Version, sequence.Steps.Count);

        // Nhóm theo order — chuẩn bị một lần cho mọi cycle
        var groups = sequence.Steps
            .GroupBy(s => s.Order)
            .OrderBy(g => g.Key)
            .Select(g => g.ToArray())
            .ToArray();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await WaitIfPausedAsync(ct).ConfigureAwait(false); // (i) ranh giới sản phẩm
                await RunSingleProductAsync(groups, ct).ConfigureAwait(false);
                if (sequence.Settings.ContinueMode == ContinueMode.SingleCycle)
                    break;
            }
        }
        catch (OperationCanceledException oce) when (ct.IsCancellationRequested)
        {
            // Stop bình thường — station đã thoát về an toàn trước khi tới đây
            State = SequenceRunState.Stopping;
            _logger.LogInformation(oce, "Sequence '{Name}' dừng theo yêu cầu (token hủy)", sequence.Name);
        }
        finally
        {
            lock (_sync)
            {
                State = SequenceRunState.Idle;
                _current = null;
                _pauseRequested = false;
                _abortAfterResume = false;
                _singleStepArmed = false;
                _resumeTcs = null;
                _runCt = CancellationToken.None;
            }
        }
    }

    /// <summary>Chạy một sản phẩm — một cycle trọn vẹn từ nhóm order đầu tới cuối.</summary>
    private async Task RunSingleProductAsync(SequenceStep[][] groups, CancellationToken ct)
    {
        var product = new ProductContext();
        // ConcurrentDictionary: bước song song cùng order ghi Blackboard an toàn
        var blackboard = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        var sw = Stopwatch.StartNew();

        try
        {
            foreach (var group in groups)
            {
                await WaitIfPausedAsync(ct).ConfigureAwait(false); // (ii) ranh giới bước

                var runnable = group.Where(s => !product.IsNg || s.RunOnNg).ToArray();
                foreach (var bypassed in group.Where(s => product.IsNg && !s.RunOnNg))
                {
                    // Sản phẩm NG → bước không runOnNg bị bỏ, vẫn phát sự kiện để log thấy đủ bước
                    RaiseStepCompleted(bypassed, product, attempt: 0,
                        new StationResult(StationStatus.Skipped, "Bỏ qua vì sản phẩm NG"), TimeSpan.Zero);
                }
                if (runnable.Length == 0) continue;

                await Task.WhenAll(runnable.Select(s =>
                    RunStepAsync(s, product, blackboard, ct))).ConfigureAwait(false);

                ArmSingleStepGate(); // P4.1: từng-bước → gate kế tiếp (ii hoặc i) sẽ đứng lại
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            product.MarkAborted();
            RaiseProductCompleted(product, sw.Elapsed);
            throw; // RunAsync xử lý thành kết thúc sạch
        }
        catch (SequenceAbortException)
        {
            product.MarkAborted();
            RaiseProductCompleted(product, sw.Elapsed);
            throw; // master controller fire trigger Error/Abort
        }

        RaiseProductCompleted(product, sw.Elapsed);
    }

    // ─── Một bước: timeout + onError/retry/prompt ────────────────────────────

    private async Task RunStepAsync(SequenceStep step, ProductContext product,
        ConcurrentDictionary<string, object> blackboard, CancellationToken ct)
    {
        var station = _resolver.Resolve(step.Station); // chắc chắn có — validate lúc nạp
        int attempt = 0;

        while (true)
        {
            RaiseStepStarted(step, product, attempt);
            var sw = Stopwatch.StartNew();
            StationResult result;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(step.TimeoutMs);
            try
            {
                var ctx = new StepContext
                {
                    Product = product,
                    Recipe = _runtime.Recipe,
                    Blackboard = blackboard,
                    IsDryRun = _runtime.IsDryRun,
                    Logger = _logger,
                    Io = _runtime.Io,
                    Motion = _runtime.Motion,
                };
                result = await station.ExecuteAsync(ctx, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // Stop/Abort thật — thoát, không phải timeout
            }
            catch (OperationCanceledException)
            {
                result = StationResult.Fail($"Timeout {step.TimeoutMs} ms"); // lỗi máy → onError
            }
#pragma warning disable CA1031 // exception bất ngờ từ station = lỗi máy — áp onError, không giết engine
            catch (Exception ex) when (ex is not SequenceAbortException)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[{Step}] Station {Station} ném exception", step.Id, step.Station);
                result = StationResult.Fail(ex.Message);
            }

            sw.Stop();
            RaiseStepCompleted(step, product, attempt, result, sw.Elapsed);

            if (result.Status == StationStatus.Ok)
            {
                MergeData(step, result, blackboard);
                return;
            }
            if (result.Status == StationStatus.Skipped)
            {
                ApplySkip(step, product);
                return;
            }
            if (result.Status == StationStatus.Ng)
            {
                // NG nghiệp vụ — KHÔNG áp onError (spec §2); flow chạy tiếp phần runOnNg
                product.MarkNg(result.Message);
                MergeData(step, result, blackboard);
                return;
            }
            // Còn lại: StationStatus.Error → chính sách lỗi máy khai báo

            // Lỗi máy → chính sách khai báo
            StepErrorAction action;
            if (step.OnError == StepErrorAction.Retry)
                action = attempt < step.Retry
                    ? StepErrorAction.Retry
                    : step.OnRetryExhausted ?? StepErrorAction.Pause;
            else
                action = step.OnError;

            switch (action)
            {
                case StepErrorAction.Retry:
                    attempt++;
                    _logger.LogWarning("[{Step}] lỗi máy — retry lần {Attempt}/{Max}",
                        step.Id, attempt, step.Retry);
                    continue;

                case StepErrorAction.Skip:
                    ApplySkip(step, product);
                    return;

                case StepErrorAction.Abort:
                    throw new SequenceAbortException(
                        $"Bước '{step.Id}' yêu cầu Abort: {result.Message}");

                default: // StepErrorAction.Pause (và giá trị lạ) → hỏi operator — lựa chọn an toàn nhất
                    var decision = await PromptOperatorAsync(step, result, ct).ConfigureAwait(false);
                    if (decision == StepErrorAction.Retry)
                    {
                        attempt = 0; // operator đã can thiệp — đếm lại từ đầu (ADR 0011 §3)
                        continue;
                    }
                    if (decision == StepErrorAction.Skip)
                    {
                        ApplySkip(step, product);
                        return;
                    }
                    throw new SequenceAbortException(
                        $"Operator chọn Abort tại bước '{step.Id}': {result.Message}");
            }
        }
    }

    private static void ApplySkip(SequenceStep step, ProductContext product)
    {
        if (step.SkipCountsAsNg)
            product.MarkNg($"Bước '{step.Id}' bị bỏ qua (skipCountsAsNg)");
    }

    private static void MergeData(SequenceStep step, StationResult result,
        ConcurrentDictionary<string, object> blackboard)
    {
        if (result.Data is null) return;
        foreach (var kv in result.Data)
            blackboard[$"{step.Id}.{kv.Key}"] = kv.Value; // key theo convention {stepId}.{field}
    }

    /// <summary>Hỏi operator khi lỗi máy có onError=Pause — engine chỉ await, không biết UI.</summary>
    private async Task<StepErrorAction> PromptOperatorAsync(SequenceStep step,
        StationResult result, CancellationToken ct)
    {
        if (OperatorPromptRequired is null)
        {
            // Không ai trả lời được → không thể chờ vô hạn: coi như Abort (an toàn nhất)
            _logger.LogError("[{Step}] cần operator nhưng không có subscriber OperatorPromptRequired — Abort", step.Id);
            return StepErrorAction.Abort;
        }

        var args = new OperatorPromptEventArgs(step.Id, step.Station,
            result.Message ?? "Lỗi máy", ErrorPromptChoices);
        var previous = State;
        State = SequenceRunState.Paused;
        _logger.LogWarning("[{Step}] chờ operator: {Message}", step.Id, args.Message);
        RaiseSafe(OperatorPromptRequired, args);

        using var reg = ct.Register(args.CancelPrompt); // Stop trong lúc chờ → hủy prompt
        try
        {
            var decision = await args.Decision.ConfigureAwait(false);
            _logger.LogWarning("[{Step}] operator chọn {Decision}", step.Id, decision);
            return decision;
        }
        finally
        {
            State = previous;
        }
    }

    // ─── Pause / Resume ở ranh giới bước ─────────────────────────────────────

    /// <inheritdoc/>
    public void RequestPause()
    {
        lock (_sync)
        {
            if (State is SequenceRunState.Idle or SequenceRunState.Stopping) return;
            _pauseRequested = true;
            if (State == SequenceRunState.Running) State = SequenceRunState.Pausing;
        }
        _logger.LogInformation("RequestPause — sẽ dừng ở ranh giới bước kế tiếp");
    }

    /// <inheritdoc/>
    public void Resume()
    {
        SequenceDefinition? sequence;
        lock (_sync)
        {
            if (!_pauseRequested) return;
            sequence = _current;
        }
        // Fire-and-forget an toàn: ResumeCoreAsync tự bắt mọi exception
        _ = ResumeCoreAsync(sequence);
    }

    /// <summary>Xác minh resume-check rồi mở gate. Cơ cấu lệch → giữ Paused + prompt (ADR 0011 §4.1).</summary>
    private async Task ResumeCoreAsync(SequenceDefinition? sequence)
    {
        try
        {
            if (sequence is not null
                && !await VerifyResumeAsync(sequence, _runCt).ConfigureAwait(false))
            {
                return; // operator chọn Abort — _abortAfterResume đã set, gate mở bên dưới
            }
        }
        catch (OperationCanceledException)
        {
            return; // máy đang Stop — gate không cần mở, RunAsync tự thoát
        }
#pragma warning disable CA1031 // fire-and-forget: không được ném lên thread pool
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Resume-check lỗi bất ngờ — giữ nguyên Paused");
            return;
        }

        lock (_sync)
        {
            _pauseRequested = false;
            _singleStepArmed = false; // Resume cũng mở được gate từng-bước (đường an toàn hơn — có resume-check)
            _resumeTcs?.TrySetResult(true);
            _resumeTcs = null;
        }
        _logger.LogInformation("Resume — chạy tiếp từ ranh giới bước");
    }

    /// <summary>True = mọi trạm xác minh OK (hoặc operator Retry tới khi OK); false = operator Abort.</summary>
    private async Task<bool> VerifyResumeAsync(SequenceDefinition sequence, CancellationToken ct)
    {
        foreach (var name in sequence.Steps.Select(s => s.Station).Distinct(StringComparer.Ordinal))
        {
            if (_resolver.Resolve(name) is not IResumeVerifiable verifiable) continue;

            while (true)
            {
                var check = await verifiable.VerifyResumeAsync(ct).ConfigureAwait(false);
                if (check.Status == StationStatus.Ok) break;

                _logger.LogWarning("Resume-check: trạm {Station} báo lệch — {Message}", name, check.Message);
                if (OperatorPromptRequired is null)
                {
                    lock (_sync) { _abortAfterResume = true; _pauseRequested = false; _singleStepArmed = false; _resumeTcs?.TrySetResult(true); _resumeTcs = null; }
                    return false;
                }

                var args = new OperatorPromptEventArgs("resume", name,
                    check.Message ?? "Cơ cấu không còn đúng trạng thái lúc tạm dừng", ResumePromptChoices);
                RaiseSafe(OperatorPromptRequired, args);
                using var reg = ct.Register(args.CancelPrompt);
                var decision = await args.Decision.ConfigureAwait(false);
                if (decision == StepErrorAction.Abort)
                {
                    lock (_sync) { _abortAfterResume = true; _pauseRequested = false; _singleStepArmed = false; _resumeTcs?.TrySetResult(true); _resumeTcs = null; }
                    return false;
                }
                // Retry → xác minh lại trạm này
            }
        }
        return true;
    }

    /// <inheritdoc/>
    public void StepOnce()
    {
        lock (_sync)
        {
            // Chỉ mở gate DO single-step tạo — gate của RequestPause phải đi đường Resume (có resume-check)
            if (!_singleStepArmed || !_pauseRequested) return;
            _singleStepArmed = false;
            _pauseRequested = false;
            _resumeTcs?.TrySetResult(true);
            _resumeTcs = null;
        }
        _logger.LogInformation("SingleStep — chạy nhóm bước kế tiếp");
    }

    // Sau mỗi nhóm order: nếu đang bật từng-bước thì cài gate cho ranh giới kế tiếp (P4.1).
    // KHÔNG đè gate của RequestPause (pause thật giữ nguyên ngữ nghĩa resume-check).
    private void ArmSingleStepGate()
    {
        lock (_sync)
        {
            if (!_singleStep || _pauseRequested) return;
            _pauseRequested = true;
            _singleStepArmed = true;
        }
    }

    /// <summary>Gate ranh giới bước: đứng lại khi có RequestPause; ném Abort nếu resume bị từ chối.</summary>
    private async Task WaitIfPausedAsync(CancellationToken ct)
    {
        Task? waitTask = null;
        lock (_sync)
        {
            if (_pauseRequested)
            {
                _resumeTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                waitTask = _resumeTcs.Task;
                State = SequenceRunState.Paused;
            }
        }
        if (waitTask is not null)
        {
            _logger.LogInformation("Sequence tạm dừng ở ranh giới bước");
            await waitTask.WaitAsync(ct).ConfigureAwait(false);
        }

        bool abort;
        lock (_sync)
        {
            abort = _abortAfterResume;
            _abortAfterResume = false;
            if (!abort) State = SequenceRunState.Running;
        }
        if (abort)
            throw new SequenceAbortException("Resume bị từ chối (cơ cấu lệch) — operator chọn Abort");
    }

    // ─── Phát sự kiện (một nguồn — consumer lỗi không được giết engine) ───────

    private void RaiseStepStarted(SequenceStep step, ProductContext product, int attempt)
        => RaiseSafe(StepStarted, new StepEventArgs(step.Id, step.Station, step.Order,
            attempt, product.SerialNumber, result: null, duration: null));

    private void RaiseStepCompleted(SequenceStep step, ProductContext product, int attempt,
        StationResult result, TimeSpan duration)
        => RaiseSafe(StepCompleted, new StepEventArgs(step.Id, step.Station, step.Order,
            attempt, product.SerialNumber, result, duration));

    private void RaiseProductCompleted(ProductContext product, TimeSpan total)
        => RaiseSafe(ProductCompleted, new ProductEventArgs(product, total));

    private void RaiseSafe<TArgs>(EventHandler<TArgs>? handler, TArgs args) where TArgs : EventArgs
    {
        if (handler is null) return;
        try
        {
            handler(this, args);
        }
#pragma warning disable CA1031 // consumer (UI/log sink) lỗi không được làm sập vòng chạy máy
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Consumer sự kiện {Event} ném exception — bỏ qua", typeof(TArgs).Name);
        }
    }
}
