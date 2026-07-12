// -------------------------------------------------------
// File:    IKioskService.cs
// Project: AM.Core.Abstractions
// Purpose: Vào/thoát kiosk mode từ UI (P4.3 — nút trong Cài đặt, Engineer+)
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Điều khiển kiosk mode của app (borderless + maximize che taskbar — IPC sản xuất).
/// Shell implement (nắm MainWindow); module Cài đặt gọi qua interface.
/// Caller TỰ gate quyền (Engineer+) trước khi Toggle — service không biết user.
/// </summary>
public interface IKioskService
{
    /// <summary>True nếu đang ở kiosk mode.</summary>
    bool IsKiosk { get; }

    /// <summary>Đảo kiosk mode (marshal về UI thread bên trong).</summary>
    void Toggle();
}
