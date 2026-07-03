// -------------------------------------------------------
// File:    IStationResolver.cs
// Project: AM.Core.Sequencing
// Purpose: Resolve station theo tên logic — abstraction trên DI container (ADR 0011 §2)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Resolve <see cref="IStation"/> theo tên logic. Implementation DryIoc keyed nằm ở
/// composition root (Bootstrapper) — engine và test KHÔNG reference container
/// (bất biến 1: engine không biết trạm cụ thể nào tồn tại).
/// </summary>
public interface IStationResolver
{
    /// <summary>True nếu tên station đã được đăng ký — validator dùng NGAY LÚC NẠP sequence.</summary>
    /// <param name="name">Tên logic của station.</param>
    bool Contains(string name);

    /// <summary>Resolve station theo tên. Chỉ gọi với tên đã validate lúc nạp.</summary>
    /// <param name="name">Tên logic của station.</param>
    IStation Resolve(string name);

    /// <summary>Danh sách tên đã đăng ký — dùng cho thông điệp lỗi/chẩn đoán.</summary>
    IReadOnlyList<string> AllNames();
}
