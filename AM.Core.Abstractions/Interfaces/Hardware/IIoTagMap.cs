// -------------------------------------------------------
// File:    IIoTagMap.cs
// Project: AM.Core.Abstractions
// Purpose: Ánh xạ tag IO logic (vd "DO_Vac_A") → kênh vật lý + metadata mô tả kênh (địa chỉ/tên/xi lanh).
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Bảng ánh xạ tag IO (tên logic) ↔ số kênh vật lý, kèm metadata mô tả kênh để màn Giám sát I/O
/// hiển thị "địa chỉ · tên có nghĩa". Logic máy gọi IO bằng tag; đổi đấu dây chỉ sửa file map, không sửa code.
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

    /// <summary>Mô tả tất cả kênh DI khai trong map (rỗng nếu map không có metadata).</summary>
    IReadOnlyList<IoChannelDescriptor> DiChannels { get; }

    /// <summary>Mô tả tất cả kênh DO khai trong map (rỗng nếu map không có metadata).</summary>
    IReadOnlyList<IoChannelDescriptor> DoChannels { get; }

    /// <summary>Xi lanh hai cảm biến khai trong map (rỗng nếu không có).</summary>
    IReadOnlyList<IoCylinderDescriptor> Cylinders { get; }

    /// <summary>Mô tả kênh DI theo số kênh; null nếu không khai.</summary>
    IoChannelDescriptor? DescribeDi(int channel);

    /// <summary>Mô tả kênh DO theo số kênh; null nếu không khai.</summary>
    IoChannelDescriptor? DescribeDo(int channel);
}
