// -------------------------------------------------------
// File:    GuardService.cs
// Project: AM.Services
// Purpose: Engine phân quyền per-action R0–R3 — trạng thái máy → role (guard condition để mở rộng).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;

namespace AM.Services;

/// <summary>
/// Hiện thực <see cref="IGuardEngine"/>: đánh giá thao tác theo tầng trạng thái máy → role.
/// Map mức rủi ro → cấp quyền tối thiểu cố định (HMI_Manual_Operation_and_Safety §3):
/// R0=Operator · R1=LineLead · R2=Engineer · R3=Engineer (Force IO=Admin xử lý riêng tại call site).
/// Tầng guard condition (điều kiện phần cứng) là bước mở rộng sau (RecoveryActions + HardwareInputEventBus).
/// </summary>
public sealed class GuardService : IGuardEngine
{
    private readonly IUserService _user;
    private readonly IMasterController _master;

    /// <summary>Tạo guard engine từ phiên đăng nhập + bộ điều phối máy.</summary>
    public GuardService(IUserService user, IMasterController master)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(master);
        _user = user;
        _master = master;
    }

    /// <inheritdoc/>
    public UserLevel MinLevelFor(RiskTier risk) => risk switch
    {
        RiskTier.R0 => UserLevel.Operator,
        RiskTier.R1 => UserLevel.LineLead,
        RiskTier.R2 => UserLevel.Engineer,
        RiskTier.R3 => UserLevel.Engineer,
        _           => UserLevel.Administrator
    };

    /// <inheritdoc/>
    public GuardResult Evaluate(RiskTier risk)
    {
        var min = MinLevelFor(risk);

        // Tầng 1 — trạng thái máy: R1+ chỉ khi máy KHÔNG đang chạy/chuyển trạng thái (R0 tiện ích chạy được).
        if (risk >= RiskTier.R1 && IsMachineBusy())
            return new GuardResult(false, GuardBlock.MachineBusy, min);

        // Tầng 2 — role: cấp quyền hiện tại phải ≥ tối thiểu của mức rủi ro.
        if (_user.CurrentLevel < min)
            return new GuardResult(false, GuardBlock.InsufficientRole, min);

        // Tầng 3 (guard condition phần cứng) — mở rộng sau; hiện cho phép.
        return new GuardResult(true, GuardBlock.None, min);
    }

    // Máy "bận" = đang chạy hoặc đang chuyển trạng thái (không cho điều chỉnh).
    private bool IsMachineBusy() => _master.State
        is MachineState.Running or MachineState.Initializing or MachineState.Resetting;
}
