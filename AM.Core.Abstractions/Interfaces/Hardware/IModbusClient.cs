// -------------------------------------------------------
// File:    IModbusClient.cs
// Project: AM.Core.Abstractions
// Purpose: Interface chuẩn cho Modbus TCP/RTU client
// -------------------------------------------------------

using AM.Core.Exceptions;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface cho Modbus TCP/RTU client — giao tiếp với PLC, VFD, sensor công nghiệp.
/// Hỗ trợ đầy đủ 8 function codes tiêu chuẩn (FC01–FC06, FC15, FC16).
/// </summary>
/// <remarks>
/// Modbus data model:
/// <list type="bullet">
///   <item><b>Coils</b>: bit read/write — FC01/FC05/FC15</item>
///   <item><b>Discrete Inputs</b>: bit read-only — FC02</item>
///   <item><b>Holding Registers</b>: 16-bit word read/write — FC03/FC06/FC16</item>
///   <item><b>Input Registers</b>: 16-bit word read-only — FC04</item>
/// </list>
/// Implementations: <c>ModbusTcpClient</c> (real, cần NuGet), <c>SimulatedModbusClient</c> (in-memory).
/// </remarks>
public interface IModbusClient : IDisposable, IHardwareDevice
{
    /// <summary>Host/IP của Modbus server (PLC, gateway).</summary>
    string Host { get; }

    /// <summary>Port TCP. Default Modbus: 502.</summary>
    int Port { get; }

    /// <summary>True nếu đã kết nối TCP thành công.</summary>
    bool IsConnected { get; }


    // ─── FC01: Read Coils ─────────────────────────────────────────────────────

    /// <summary>
    /// Đọc trạng thái Output Coils (FC01).
    /// </summary>
    /// <param name="slaveId">Modbus slave/unit ID (1–247).</param>
    /// <param name="startAddress">Địa chỉ coil bắt đầu (0-based).</param>
    /// <param name="count">Số coils cần đọc.</param>
    Task<bool[]> ReadCoilsAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default);

    // ─── FC02: Read Discrete Inputs ──────────────────────────────────────────

    /// <summary>Đọc trạng thái Discrete Inputs — read-only (FC02).</summary>
    Task<bool[]> ReadDiscreteInputsAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default);

    // ─── FC03: Read Holding Registers ────────────────────────────────────────

    /// <summary>
    /// Đọc Holding Registers — read/write, lưu setpoints và config (FC03).
    /// </summary>
    Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default);

    // ─── FC04: Read Input Registers ──────────────────────────────────────────

    /// <summary>Đọc Input Registers — read-only, lưu measurements (FC04).</summary>
    Task<ushort[]> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default);

    // ─── FC05: Write Single Coil ─────────────────────────────────────────────

    /// <summary>Ghi một Coil ON/OFF (FC05).</summary>
    Task WriteSingleCoilAsync(byte slaveId, ushort coilAddress, bool value,
        CancellationToken ct = default);

    // ─── FC06: Write Single Register ─────────────────────────────────────────

    /// <summary>Ghi một Holding Register (FC06).</summary>
    Task WriteSingleRegisterAsync(byte slaveId, ushort registerAddress, ushort value,
        CancellationToken ct = default);

    // ─── FC15: Write Multiple Coils ──────────────────────────────────────────

    /// <summary>Ghi nhiều Coils cùng lúc (FC15).</summary>
    Task WriteMultipleCoilsAsync(byte slaveId, ushort startAddress, bool[] values,
        CancellationToken ct = default);

    // ─── FC16: Write Multiple Registers ──────────────────────────────────────

    /// <summary>Ghi nhiều Holding Registers cùng lúc (FC16).</summary>
    Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] values,
        CancellationToken ct = default);
}
