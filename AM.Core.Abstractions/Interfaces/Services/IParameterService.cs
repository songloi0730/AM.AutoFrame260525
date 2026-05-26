// -------------------------------------------------------
// File:    IParameterService.cs
// Project: AM.Core.Abstractions
// Purpose: Interface cho ParameterService — lưu/đọc tham số hệ thống
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Service lưu/đọc tham số hệ thống (không phải recipe parameter).
/// Dùng cho: IP controller, cổng kết nối, thông số cố định của máy.
/// </summary>
public interface IParameterService
{
    /// <summary>
    /// Đọc tham số theo key.
    /// Đổi tên từ 'Get' sang 'GetValue' để tránh conflict keyword VB.NET (CA1716).
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của tham số.</typeparam>
    /// <param name="key">Key của tham số.</param>
    /// <param name="defaultValue">Giá trị mặc định nếu key không tồn tại.</param>
    T GetValue<T>(string key, T defaultValue = default!);

    /// <summary>
    /// Ghi tham số theo key và ghi audit log.
    /// </summary>
    /// <param name="key">Key của tham số.</param>
    /// <param name="value">Giá trị mới.</param>
    /// <param name="operatorId">ID người thay đổi.</param>
    Task SetAsync<T>(string key, T value, string operatorId, CancellationToken ct = default);

    /// <summary>Lấy tất cả keys hiện có.</summary>
    IReadOnlyList<string> GetAllKeys();

    /// <summary>Reload parameters từ storage.</summary>
    Task ReloadAsync(CancellationToken ct = default);

    /// <summary>Lưu tất cả parameters vào storage.</summary>
    Task SaveAsync(CancellationToken ct = default);
}
