// -------------------------------------------------------
// File:    Guard.cs
// Project: AM.CommonTools
// Purpose: Utility guard clauses — validate input sớm và clean code
// -------------------------------------------------------

namespace AM.CommonTools;

/// <summary>
/// Guard clause utilities cho clean validation code.
/// Dùng ở đầu method để validate input ngay lập tức.
/// </summary>
public static class Guard
{
    /// <summary>Ném ArgumentOutOfRangeException nếu value nằm ngoài [min, max].</summary>
    public static void InRange(double value, double min, double max, string paramName)
    {
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(paramName,
                $"Value {value} must be between {min} and {max}");
    }

    /// <summary>Ném ArgumentOutOfRangeException nếu value nằm ngoài [min, max].</summary>
    public static void InRange(int value, int min, int max, string paramName)
    {
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(paramName,
                $"Value {value} must be between {min} and {max}");
    }

    /// <summary>Ném ArgumentException nếu string null hoặc whitespace.</summary>
    public static void NotNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Parameter '{paramName}' must not be null or whitespace.", paramName);
    }

    /// <summary>Ném ArgumentOutOfRangeException nếu value âm.</summary>
    public static void NotNegative(double value, string paramName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, $"Value {value} must not be negative");
    }

    /// <summary>Ném ArgumentOutOfRangeException nếu value không dương (≤ 0).</summary>
    public static void Positive(double value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, $"Value {value} must be positive (> 0)");
    }

    /// <summary>Ném ArgumentOutOfRangeException nếu value không dương (≤ 0).</summary>
    public static void Positive(int value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, $"Value {value} must be positive (> 0)");
    }

    /// <summary>Ném ArgumentException nếu from >= to (khoảng thời gian không hợp lệ).</summary>
    public static void ValidDateRange(DateTime from, DateTime to, string fromParam = "from", string toParam = "to")
    {
        if (from >= to)
            throw new ArgumentException($"'{fromParam}' ({from:O}) must be earlier than '{toParam}' ({to:O})");
    }
}
