// -------------------------------------------------------
// File:    IIoTagMap.cs
// Project: AM.Core.Abstractions
// Purpose: Ánh xạ tag IO logic (vd "DO_Vac_A") → kênh vật lý, để logic máy không dùng số kênh.
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Bảng ánh xạ tag IO (tên logic) ↔ số kênh vật lý.
/// Logic máy gọi IO bằng tag; đổi đấu dây chỉ sửa file map, không sửa code.
/// </summary>
public interface IIoTagMap
{
    /// <summary>Phân giải tag DI → số kênh.</summary>
    /// <exception cref="KeyNotFoundException">Ném khi tag không tồn tại.</exception>
    int ResolveDi(string tag);

    /// <summary>Phân giải tag DO → số kênh.</summary>
    /// <exception cref="KeyNotFoundException">Ném khi tag không tồn tại.</exception>
    int ResolveDo(string tag);

    /// <summary>True nếu có tag DI này.</summary>
    bool ContainsDi(string tag);

    /// <summary>True nếu có tag DO này.</summary>
    bool ContainsDo(string tag);
}
