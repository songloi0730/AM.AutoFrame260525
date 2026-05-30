// -------------------------------------------------------
// File:    MachineStateChangedEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs cho sự kiện thay đổi trạng thái máy (CA1003)
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Models.EventArgs;

/// <summary>
/// EventArgs cho sự kiện <c>StateChanged</c> của Station và MasterController.
/// Mang cả trạng thái cũ và trạng thái mới để UI/logic phân biệt transition.
/// </summary>
public sealed class MachineStateChangedEventArgs : System.EventArgs
{
    /// <summary>Trạng thái trước khi thay đổi.</summary>
    public MachineState PreviousState { get; }

    /// <summary>Trạng thái mới sau khi thay đổi.</summary>
    public MachineState NewState { get; }

    /// <summary>Trigger đã kích hoạt sự kiện thay đổi.</summary>
    public MachineTrigger Trigger { get; }

    /// <summary>Thời điểm thay đổi xảy ra (UTC).</summary>
    public DateTime ChangedAt { get; } = DateTime.UtcNow;

    public MachineStateChangedEventArgs(
        MachineState previousState,
        MachineState newState,
        MachineTrigger trigger)
    {
        PreviousState = previousState;
        NewState      = newState;
        Trigger       = trigger;
    }
}
