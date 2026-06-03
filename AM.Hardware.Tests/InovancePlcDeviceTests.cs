// -------------------------------------------------------
// File:    InovancePlcDeviceTests.cs
// Project: AM.Hardware.Tests
// Purpose: Test InovancePlcDevice ánh xạ địa chỉ + read/write qua SimulatedModbusClient.
// -------------------------------------------------------

using AM.Hardware.Comm.Inovance;
using AM.Hardware.Comm.Modbus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Hardware.Tests;

public sealed class InovancePlcDeviceTests
{
    private static async Task<(InovancePlcDevice Plc, SimulatedModbusClient Modbus)> CreateConnectedAsync()
    {
        var modbus = new SimulatedModbusClient(NullLogger<SimulatedModbusClient>.Instance, simulatedDelayMs: 0);
        var plc = new InovancePlcDevice(modbus, NullLogger<InovancePlcDevice>.Instance, slaveId: 1);
        await plc.ConnectAsync();
        return (plc, modbus);
    }

    [Fact]
    public async Task WriteThenReadWord_RoundTrips()
    {
        var (plc, _) = await CreateConnectedAsync();
        await plc.WriteWordAsync("D100", 1234);
        (await plc.ReadWordAsync("D100")).Should().Be(1234);
    }

    [Fact]
    public async Task DRegister_MapsToHoldingRegister()
    {
        var (plc, modbus) = await CreateConnectedAsync();
        await plc.WriteWordAsync("D50", unchecked((short)0xABCD));
        ushort[] r = await modbus.ReadHoldingRegistersAsync(1, 50, 1);
        r[0].Should().Be(0xABCD);
    }

    [Fact]
    public async Task WriteThenReadDWord_LowWordFirst()
    {
        var (plc, modbus) = await CreateConnectedAsync();
        await plc.WriteDWordAsync("D200", 0x00010002);
        (await plc.ReadDWordAsync("D200")).Should().Be(0x00010002);
        (await modbus.ReadHoldingRegistersAsync(1, 200, 2))[0].Should().Be(0x0002); // low word first
    }

    [Fact]
    public async Task WriteThenReadFloat_RoundTrips()
    {
        var (plc, _) = await CreateConnectedAsync();
        await plc.WriteFloatAsync("D300", 3.14159f);
        (await plc.ReadFloatAsync("D300")).Should().BeApproximately(3.14159f, 1e-5f);
    }

    [Fact]
    public async Task MBit_MapsToCoil()
    {
        var (plc, modbus) = await CreateConnectedAsync();
        await plc.WriteBitAsync("M10", true);
        (await plc.ReadBitAsync("M10")).Should().BeTrue();
        (await modbus.ReadCoilsAsync(1, 10, 1))[0].Should().BeTrue();
    }

    [Fact]
    public async Task XInput_IsReadOnly_ThrowsOnWrite()
    {
        var (plc, _) = await CreateConnectedAsync();
        Func<Task> act = () => plc.WriteBitAsync("X0", true);
        await act.Should().ThrowAsync<AM.Core.Exceptions.AlarmException>();
    }

    [Fact]
    public async Task DBaseOffset_IsApplied()
    {
        var modbus = new SimulatedModbusClient(NullLogger<SimulatedModbusClient>.Instance, simulatedDelayMs: 0);
        var plc = new InovancePlcDevice(modbus, NullLogger<InovancePlcDevice>.Instance) { DBase = 1000 };
        await plc.ConnectAsync();
        await plc.WriteWordAsync("D5", 77);
        (await modbus.ReadHoldingRegistersAsync(1, 1005, 1))[0].Should().Be(77);
    }
}
