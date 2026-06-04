// -------------------------------------------------------
// File:    SafetyStateChangedEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs cho thay đổi trạng thái an toàn (E-Stop/Guard/Light Curtain).
// -------------------------------------------------------

namespace AM.Core.Models.EventArgs;

/// <summary>EventArgs cho sự kiện <c>SafetyStateChanged</c> của ISafetyInput.</summary>
public sealed class SafetyStateChangedEventArgs : System.EventArgs
{
    /// <summary>True nếu mạch E-Stop đang OK (không nhấn).</summary>
    public bool IsEStopOk { get; }

    /// <summary>True nếu cửa/guard đang đóng.</summary>
    public bool IsGuardClosed { get; }

    /// <summary>True nếu light curtain không bị che.</summary>
    public bool IsLightCurtainClear { get; }

    /// <summary>True nếu toàn bộ điều kiện an toàn đang OK.</summary>
    public bool IsAllSafe => IsEStopOk && IsGuardClosed && IsLightCurtainClear;

    public SafetyStateChangedEventArgs(bool isEStopOk, bool isGuardClosed, bool isLightCurtainClear)
    {
        IsEStopOk = isEStopOk;
        IsGuardClosed = isGuardClosed;
        IsLightCurtainClear = isLightCurtainClear;
    }
}
