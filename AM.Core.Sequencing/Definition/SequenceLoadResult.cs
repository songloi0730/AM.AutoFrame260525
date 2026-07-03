// -------------------------------------------------------
// File:    SequenceLoadResult.cs
// Project: AM.Core.Sequencing
// Purpose: Kết quả nạp sequence — gom TOÀN BỘ lỗi/cảnh báo một lần (ADR 0011 §1)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Kết quả nạp một file sequence. Loader gom toàn bộ lỗi thay vì fail-fast
/// từng lỗi — người viết sequence sửa một lần.
/// </summary>
public sealed class SequenceLoadResult
{
    /// <summary>Sequence đã nạp — null khi có lỗi.</summary>
    public SequenceDefinition? Definition { get; }

    /// <summary>Danh sách lỗi (rỗng nếu nạp thành công).</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Danh sách cảnh báo (key lạ, khai báo thừa…) — không chặn nạp.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>True nếu nạp thành công (không lỗi).</summary>
    public bool Success => Definition is not null && Errors.Count == 0;

    internal SequenceLoadResult(SequenceDefinition? definition,
        IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        Definition = definition;
        Errors = errors;
        Warnings = warnings;
    }
}
