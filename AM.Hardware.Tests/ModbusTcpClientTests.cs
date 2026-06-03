// -------------------------------------------------------
// File:    ModbusTcpClientTests.cs
// Project: AM.Hardware.Tests
// Purpose: Test khung MBAP của ModbusTcpClient thật qua một Modbus slave giả loopback.
// -------------------------------------------------------

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using AM.Hardware.Comm.Modbus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Hardware.Tests;

public sealed class ModbusTcpClientTests
{
    [Fact]
    public async Task WriteMultiple_ThenRead_RoundTripsOverWire()
    {
        using var slave = new FakeModbusSlave();
        int port = slave.Start();

        using var client = new ModbusTcpClient(NullLogger<ModbusTcpClient>.Instance, "127.0.0.1", port);
        await client.ConnectAsync();

        await client.WriteMultipleRegistersAsync(1, 100, new ushort[] { 0x1234, 0x5678 });
        ushort[] r = await client.ReadHoldingRegistersAsync(1, 100, 2);

        r.Should().Equal(0x1234, 0x5678);
    }

    [Fact]
    public async Task WriteSingleRegister_RoundTrips()
    {
        using var slave = new FakeModbusSlave();
        int port = slave.Start();

        using var client = new ModbusTcpClient(NullLogger<ModbusTcpClient>.Instance, "127.0.0.1", port);
        await client.ConnectAsync();

        await client.WriteSingleRegisterAsync(1, 7, 0xBEEF);
        (await client.ReadHoldingRegistersAsync(1, 7, 1))[0].Should().Be(0xBEEF);
    }

    /// <summary>Modbus TCP slave giả: tự decode MBAP, hỗ trợ FC03/FC06/FC16.</summary>
    private sealed class FakeModbusSlave : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Dictionary<ushort, ushort> _regs = new();
        private CancellationTokenSource? _cts;

        public int Start()
        {
            _listener.Start();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => RunAsync(_cts.Token));
            return ((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        private async Task RunAsync(CancellationToken ct)
        {
            try
            {
                using TcpClient c = await _listener.AcceptTcpClientAsync(ct);
                NetworkStream s = c.GetStream();
                var head = new byte[6];
                while (!ct.IsCancellationRequested)
                {
                    await s.ReadExactlyAsync(head, ct);
                    ushort tid = BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(0));
                    ushort len = BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(4));
                    var body = new byte[len];
                    await s.ReadExactlyAsync(body, ct);
                    byte unit = body[0];
                    byte fc = body[1];
                    byte[] respPdu = Handle(fc, body.AsSpan(2));
                    await s.WriteAsync(BuildResponse(tid, unit, respPdu), ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }

        private byte[] Handle(byte fc, ReadOnlySpan<byte> data)
        {
            switch (fc)
            {
                case 0x03:
                {
                    ushort start = BinaryPrimitives.ReadUInt16BigEndian(data);
                    ushort qty = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
                    var pdu = new byte[2 + (qty * 2)];
                    pdu[0] = 0x03; pdu[1] = (byte)(qty * 2);
                    for (int i = 0; i < qty; i++)
                        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(2 + (i * 2)),
                            _regs.GetValueOrDefault((ushort)(start + i)));
                    return pdu;
                }
                case 0x06:
                {
                    ushort addr = BinaryPrimitives.ReadUInt16BigEndian(data);
                    ushort val = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
                    _regs[addr] = val;
                    return new byte[] { 0x06, data[0], data[1], data[2], data[3] };
                }
                case 0x10:
                {
                    ushort start = BinaryPrimitives.ReadUInt16BigEndian(data);
                    ushort qty = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
                    for (int i = 0; i < qty; i++)
                        _regs[(ushort)(start + i)] = BinaryPrimitives.ReadUInt16BigEndian(data[(5 + (i * 2))..]);
                    var pdu = new byte[5];
                    pdu[0] = 0x10;
                    BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1), start);
                    BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3), qty);
                    return pdu;
                }
                default:
                    return new byte[] { (byte)(fc | 0x80), 0x01 };
            }
        }

        private static byte[] BuildResponse(ushort tid, byte unit, byte[] pdu)
        {
            var frame = new byte[7 + pdu.Length];
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(0), tid);
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), (ushort)(pdu.Length + 1));
            frame[6] = unit;
            pdu.CopyTo(frame, 7);
            return frame;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _listener.Dispose();
            _cts?.Dispose();
        }
    }
}
