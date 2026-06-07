// -------------------------------------------------------
// File:    ITowerLightService.cs
// Project: AM.Core.Abstractions
// Purpose: Tự lái đèn tháp theo trạng thái máy + alarm + an toàn.
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Dịch vụ tự động đặt đèn tháp theo (an toàn → alarm → trạng thái máy). Gọi <see cref="Start"/>
/// một lần lúc khởi động (sau khi đã connect hardware).
/// </summary>
public interface ITowerLightService : IDisposable
{
    /// <summary>Bắt đầu lắng nghe sự kiện và đặt đèn theo trạng thái hiện tại.</summary>
    void Start();
}
