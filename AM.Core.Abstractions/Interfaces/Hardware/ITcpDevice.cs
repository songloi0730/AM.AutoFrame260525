// -------------------------------------------------------
// File:    ITcpDevice.cs
// Project: AM.Core.Abstractions
// Purpose: Interface cho generic TCP client — giao thức tùy chỉnh qua TCP/IP
// -------------------------------------------------------

using AM.Core.Exceptions;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface cho generic TCP client device.
/// Dùng khi thiết bị dùng giao thức TCP tùy chỉnh: cân kết nối mạng,
/// máy đọc mã vạch Ethernet, custom PLC ASCII protocol...
/// </summary>
/// <remarks>
/// Với thiết bị có giao thức chuẩn (Modbus, EthernetIP, OPC UA), dùng interface chuyên biệt.
/// <c>ITcpDevice</c> chỉ dùng khi không có interface phù hợp hơn.
/// Implementations: <c>GenericTcpDevice</c> (System.Net.Sockets), <c>SimulatedTcpDevice</c> (echo/loopback).
/// </remarks>
public interface ITcpDevice : IDisposable, IHardwareDevice
{
    /// <summary>Host/IP của thiết bị.</summary>
    string Host { get; }

    /// <summary>Port TCP của thiết bị.</summary>
    int Port { get; }


    /// <summary>
    /// Gửi raw bytes qua TCP.
    /// </summary>
    Task SendAsync(byte[] data, CancellationToken ct = default);

    /// <summary>
    /// Nhận đúng <paramref name="count"/> bytes. Block cho đến khi đủ bytes hoặc timeout.
    /// </summary>
    Task<byte[]> ReceiveAsync(int count, int timeoutMs, CancellationToken ct = default);

    /// <summary>
    /// Gửi request và đợi response (request-response pattern).
    /// Dùng khi thiết bị trả lời ngay sau khi nhận command.
    /// </summary>
    /// <param name="request">Bytes gửi đi.</param>
    /// <param name="expectedResponseLength">Số bytes response dự kiến (0 = đọc đến timeout).</param>
    /// <param name="timeoutMs">Timeout chờ response (ms).</param>
    /// <exception cref="AlarmException">
    /// Ném khi timeout — code CommTimeout.
    /// Ném khi socket lỗi — code CommTcpSocketError.
    /// </exception>
    Task<byte[]> SendAndReceiveAsync(byte[] request, int expectedResponseLength,
        int timeoutMs, CancellationToken ct = default);
}
