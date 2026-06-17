// -------------------------------------------------------
// File:    GuardService.cs
// Project: AM.Services
// Purpose: Engine phân quyền per-action R0–R3 — trạng thái máy → role (guard condition để mở rộng).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
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
    private readonly IHardwareSignalBus? _signals;

    /// <summary>
    /// Tạo guard engine từ phiên đăng nhập + bộ điều phối máy + (tuỳ chọn) bus tín hiệu phần cứng cho tầng 3.
    /// </summary>
    public GuardService(IUserService user, IMasterController master, IHardwareSignalBus? signals = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(master);
        _user = user;
        _master = master;
        _signals = signals;
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
    public GuardResult Evaluate(RiskTier risk, GuardCondition? condition = null)
    {
        var min = MinLevelFor(risk);

        // Tầng 1 — trạng thái máy: R1+ chỉ khi máy KHÔNG đang chạy/chuyển trạng thái (R0 tiện ích chạy được).
        if (risk >= RiskTier.R1 && IsMachineBusy())
            return new GuardResult(false, GuardBlock.MachineBusy, min);

        // Tầng 2 — role: cấp quyền hiện tại phải ≥ tối thiểu của mức rủi ro.
        if (_user.CurrentLevel < min)
            return new GuardResult(false, GuardBlock.InsufficientRole, min);

        // Tầng 3 — điều kiện phần cứng (đọc HardwareInputEventBus); chưa thoả → chặn + blockReason.
        if (condition is not null && !IsConditionSatisfied(condition))
            return new GuardResult(false, GuardBlock.ConditionNotMet, min, condition.BlockReason);

        return new GuardResult(true, GuardBlock.None, min);
    }

    // Máy "bận" = đang chạy hoặc đang chuyển trạng thái (không cho điều chỉnh).
    private bool IsMachineBusy() => _master.State
        is MachineState.Running or MachineState.Initializing or MachineState.Resetting;

    // Thoả khi CÓ ÍT NHẤT một nhóm mà MỌI yêu cầu khớp bus. Không có bus → coi như chưa đạt (fail-safe).
    // AnyOf rỗng → luôn thoả (điều kiện trống).
    private bool IsConditionSatisfied(GuardCondition condition)
    {
        if (condition.AnyOf.Count == 0) return true;
        if (_signals is null) return false;
        return condition.AnyOf.Any(GroupSatisfied);
    }

    private bool GroupSatisfied(IReadOnlyList<SignalRequirement> group)
        => group.All(req => _signals!.GetSignal(req.Key) == req.Expected);
}
