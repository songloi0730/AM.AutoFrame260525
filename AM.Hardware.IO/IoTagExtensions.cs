// -------------------------------------------------------
// File:    IoTagExtensions.cs
// Project: AM.Hardware.IO
// Purpose: Extension cho phép gọi IIoModule bằng tag thay vì số kênh.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;

namespace AM.Hardware.IO;

/// <summary>
/// Extension methods để đọc/ghi IO theo tag logic (qua <see cref="IIoTagMap"/>).
/// </summary>
public static class IoTagExtensions
{
    /// <summary>Đọc DI theo tag.</summary>
    public static Task<bool> ReadDiByTagAsync(this IIoModule io, IIoTagMap map, string tag,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(map);
        return io.ReadDiAsync(map.ResolveDi(tag), ct);
    }

    /// <summary>Ghi DO theo tag.</summary>
    public static Task WriteDoByTagAsync(this IIoModule io, IIoTagMap map, string tag, bool value,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(map);
        return io.WriteDiAsync(map.ResolveDo(tag), value, ct);
    }

    /// <summary>Ghi DO theo tag và chờ DI confirm theo tag.</summary>
    public static Task WriteAndWaitConfirmByTagAsync(this IIoModule io, IIoTagMap map,
        string outputTag, string confirmTag, bool expectedValue, int timeoutMs,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(map);
        return io.WriteAndWaitConfirmAsync(
            map.ResolveDo(outputTag), map.ResolveDi(confirmTag), expectedValue, timeoutMs, ct);
    }
}
