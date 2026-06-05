// -------------------------------------------------------
// File:    ISafetyInput.cs
// Project: AM.Core.Abstractions
// Purpose: Interface CHỈ ĐỌC trạng thái an toàn (E-Stop/Guard/Light Curtain).
// -------------------------------------------------------

using AM.Core.Exceptions;
using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface CHỈ ĐỌC trạng thái an toàn từ safety terminal (TwinSAFE/safety relay).
/// </summary>
/// <remarks>
/// ⚠️ AN TOÀN: Việc cắt nguồn/dừng khẩn do <b>mạch an toàn phần cứng</b> đảm nhiệm (PL theo ISO 13849-1).
/// Phần mềm C# CHỈ ĐỌC trạng thái để khoá logic chu trình — KHÔNG phải lớp an toàn duy nhất.
/// </remarks>
public interface ISafetyInput : IDisposable, IHardwareDevice
{
    /// <summary>True nếu mạch E-Stop OK (không nhấn).</summary>
    bool IsEStopOk { get; }

    /// <summary>True nếu cửa/guard đang đóng.</summary>
    bool IsGuardClosed { get; }

    /// <summary>True nếu light curtain không bị che.</summary>
    bool IsLightCurtainClear { get; }

    /// <summary>True nếu toàn bộ điều kiện an toàn đang OK.</summary>
    bool IsAllSafe { get; }

    /// <summary>Sự kiện khi bất kỳ trạng thái an toàn nào thay đổi.</summary>
    event EventHandler<SafetyStateChangedEventArgs>? SafetyStateChanged;

}
