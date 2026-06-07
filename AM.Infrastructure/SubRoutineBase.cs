// -------------------------------------------------------
// File:    SubRoutineBase.cs
// Project: AM.Infrastructure
// Purpose: Base cho ISubRoutine — busy-guard + logging; subclass chỉ viết ExecuteCoreAsync.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces;
using AM.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AM.Infrastructure;

/// <summary>
/// Base class cho subroutine: chặn chạy đồng thời (busy-guard) + log. Subclass khai báo
/// Name/Description/RequiredLevel và viết logic trong <see cref="ExecuteCoreAsync"/>.
/// </summary>
public abstract class SubRoutineBase : ISubRoutine
{
    /// <summary>Logger.</summary>
    protected ILogger Logger { get; }

    private volatile bool _busy;

    /// <summary>Base constructor.</summary>
    protected SubRoutineBase(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public abstract UserLevel RequiredLevel { get; }

    /// <inheritdoc/>
    public bool IsBusy => _busy;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (_busy) throw new InvalidOperationException($"Subroutine '{Name}' đang chạy");
        _busy = true;
        try
        {
            Logger.LogInformation("[SubRoutine] {Name} — bắt đầu", Name);
            await ExecuteCoreAsync(ct).ConfigureAwait(false);
            Logger.LogInformation("[SubRoutine] {Name} — xong", Name);
        }
        finally { _busy = false; }
    }

    /// <summary>Subclass triển khai logic thực tế tại đây.</summary>
    protected abstract Task ExecuteCoreAsync(CancellationToken ct);
}
