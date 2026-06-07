// -------------------------------------------------------
// File:    IAxisMap.cs
// Project: AM.Core.Abstractions
// Purpose: Tra cứu trục logic → cấu hình + IAxis đã bind (logical → physical).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Bản đồ trục: tra <see cref="AxisConfig"/> theo tên logic và trả về <see cref="IAxis"/> đã bind
/// vào đúng controller + index (qua HardwareManager). Mechanism nhận <c>IAxis</c> theo TÊN, không
/// hardcode controller/index.
/// </summary>
public interface IAxisMap
{
    /// <summary>Toàn bộ cấu hình trục đã nạp.</summary>
    IReadOnlyList<AxisConfig> All { get; }

    /// <summary>Lấy cấu hình trục theo tên logic.</summary>
    /// <exception cref="KeyNotFoundException">Ném khi không có trục tên này.</exception>
    AxisConfig GetConfig(string logicalName);

    /// <summary>Thử lấy cấu hình trục theo tên logic.</summary>
    bool TryGet(string logicalName, out AxisConfig? config);

    /// <summary>
    /// Trả về <see cref="IAxis"/> đã bind vào controller + index theo cấu hình (cache theo tên).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Ném khi không có trục tên này.</exception>
    IAxis ResolveAxis(string logicalName);
}
