// -------------------------------------------------------
// File:    AlarmEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs wrapper cho AlarmModel — dùng với EventHandler<AlarmEventArgs>
// -------------------------------------------------------

namespace AM.Core.Models.EventArgs;

/// <summary>
/// EventArgs cho các sự kiện alarm: AlarmRaised, AlarmCleared.
/// </summary>
public sealed class AlarmEventArgs : System.EventArgs
{
    /// <summary>Alarm liên quan đến sự kiện này.</summary>
    public AlarmModel Alarm { get; }

    public AlarmEventArgs(AlarmModel alarm)
    {
        Alarm = alarm ?? throw new ArgumentNullException(nameof(alarm));
    }
}
