// -------------------------------------------------------
// File:    IRecoveryActionRegistry.cs
// Project: AM.Core.Abstractions
// Purpose: Sổ đăng ký handler thao tác trạm theo id (WorkStation đăng ký lệnh HAL thật — Approach C hybrid).
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Sổ đăng ký handler cho "thao tác trạm": metadata khai trong config, còn lệnh phần cứng do WorkStation
/// đăng ký theo <c>id</c> (xem docs/design-notes/0002). UI tra <see cref="Has"/> để biết thao tác đã có HAL chưa.
/// </summary>
public interface IRecoveryActionRegistry
{
    /// <summary>Đăng ký handler thực thi cho một id (gọi lúc bootstrap máy). Đăng ký trùng id sẽ ghi đè.</summary>
    void Register(string id, Func<CancellationToken, Task> handler);

    /// <summary>True nếu id đã có handler (UI: chưa có → "chưa cấu hình HAL").</summary>
    bool Has(string id);

    /// <summary>Thực thi handler theo id; id chưa đăng ký → no-op (không ném).</summary>
    Task ExecuteAsync(string id, CancellationToken ct = default);
}
