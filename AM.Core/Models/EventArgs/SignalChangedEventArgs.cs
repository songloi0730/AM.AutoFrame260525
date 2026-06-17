// -------------------------------------------------------
// File:    SignalChangedEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs khi một tín hiệu phần cứng trên HardwareInputEventBus đổi giá trị.
// -------------------------------------------------------

namespace AM.Core.Models.EventArgs;

/// <summary>EventArgs cho <c>SignalChanged</c> của <c>IHardwareSignalBus</c> — một tín hiệu vừa đổi giá trị.</summary>
public sealed class SignalChangedEventArgs : System.EventArgs
{
    /// <summary>Khoá tín hiệu (vd "Safety.EStopOk").</summary>
    public string Key { get; }

    /// <summary>Giá trị mới.</summary>
    public bool Value { get; }

    /// <summary>Tạo EventArgs với khoá + giá trị mới.</summary>
    public SignalChangedEventArgs(string key, bool value)
    {
        Key = key;
        Value = value;
    }
}
