// -------------------------------------------------------
// File:    IMachineConfigProvider.cs
// Project: AM.Core.Abstractions
// Purpose: Cung cấp layout máy (MachineConfig) nạp từ file cấu hình.
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>Cung cấp <see cref="MachineConfig"/> (layout máy) đã nạp từ file.</summary>
public interface IMachineConfigProvider
{
    /// <summary>Layout máy hiện tại.</summary>
    MachineConfig Machine { get; }

    /// <summary>Lấy cấu hình station theo tên (null nếu không có).</summary>
    StationConfig? GetStation(string name);
}
