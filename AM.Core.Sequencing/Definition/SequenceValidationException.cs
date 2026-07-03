// -------------------------------------------------------
// File:    SequenceValidationException.cs
// Project: AM.Core.Sequencing
// Purpose: Exception khi sequence không hợp lệ lúc nạp — chứa toàn bộ lỗi
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Ném khi nạp sequence thất bại — chứa TOÀN BỘ danh sách lỗi để sửa một lần.
/// Lỗi lúc nạp = mọi thứ đọc được từ file + container (tên station, kiểu, ràng buộc số học);
/// lúc chạy chỉ còn lỗi thế giới thật (ADR 0011 §1).
/// </summary>
public sealed class SequenceValidationException : Exception
{
    /// <summary>Danh sách lỗi validate.</summary>
    public IReadOnlyList<string> Errors { get; } = [];

    /// <summary>Constructor mặc định (CA1032).</summary>
    public SequenceValidationException()
        : base("Sequence không hợp lệ") { }

    /// <summary>Constructor với message (CA1032).</summary>
    /// <param name="message">Thông điệp lỗi.</param>
    public SequenceValidationException(string message)
        : base(message) { }

    /// <summary>Constructor với message + inner (CA1032).</summary>
    /// <param name="message">Thông điệp lỗi.</param>
    /// <param name="innerException">Exception gốc.</param>
    public SequenceValidationException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>Tạo exception từ danh sách lỗi validate.</summary>
    /// <param name="errors">Toàn bộ lỗi thu được lúc nạp.</param>
    public SequenceValidationException(IReadOnlyList<string> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    private static string BuildMessage(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return $"Sequence không hợp lệ ({errors.Count} lỗi): {string.Join(" | ", errors)}";
    }
}
