// -------------------------------------------------------
// File:    AuditService.cs
// Project: AM.Services
// Purpose: Ghi audit log thao tác R1+ qua Serilog (marker [AUDIT], có cấu trúc để lọc/xuất).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Hiện thực <see cref="IAuditService"/> bằng structured logging (Serilog). Mỗi bản ghi có marker
/// <c>[AUDIT]</c> + thuộc tính user/action/result/detail để lọc và xuất phục vụ truy vết.
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;

    /// <summary>Tạo audit service.</summary>
    public AuditService(ILogger<AuditService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Record(string user, string action, bool allowed, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        _logger.LogInformation("[AUDIT] user={User} action={Action} result={Result} detail={Detail}",
            string.IsNullOrEmpty(user) ? "?" : user,
            action,
            allowed ? "OK" : "DENIED",
            detail ?? string.Empty);
    }
}
