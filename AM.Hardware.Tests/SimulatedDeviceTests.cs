// -------------------------------------------------------
// File:    SimulatedDeviceTests.cs
// Project: AM.Hardware.Tests
// Purpose: Test SimulatedPlcDevice + SimulatedRobotDevice + AdvantechAdamIoModule.
// -------------------------------------------------------

using AM.Core.Models;
using AM.Hardware.Comm.Modbus;
using AM.Hardware.Comm.Plc;
using AM.Hardware.Comm.Robot;
using AM.Hardware.IO.Advantech;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Hardware.Tests;

public sealed class SimulatedDeviceTests
{
    [Fact]
    public async Task SimPlc_WordRoundTrip()
    {
        var plc = new SimulatedPlcDevice(NullLogger<SimulatedPlcDevice>.Instance);
        await plc.ConnectAsync();
        await plc.WriteWordAsync("D1", 42);
        (await plc.ReadWordAsync("D1")).Should().Be(42);
    }

    [Fact]
    public async Task SimPlc_DWordAndFloatRoundTrip()
    {
        var plc = new SimulatedPlcDevice(NullLogger<SimulatedPlcDevice>.Instance);
        await plc.ConnectAsync();
        await plc.WriteDWordAsync("D10", 123456);
        (await plc.ReadDWordAsync("D10")).Should().Be(123456);
        await plc.WriteFloatAsync("D20", 2.5f);
        (await plc.ReadFloatAsync("D20")).Should().Be(2.5f);
    }

    [Fact]
    public async Task SimRobot_MoveUpdatesPose()
    {
        var robot = new SimulatedRobotDevice(NullLogger<SimulatedRobotDevice>.Instance);
        await robot.ConnectAsync();
        await robot.MoveToAsync(new RobotPose(10, 20, 30));
        var pose = await robot.GetCurrentPoseAsync();
        pose.X.Should().Be(10);
        pose.Z.Should().Be(30);
    }

    [Fact]
    public async Task SimRobot_DigitalIo()
    {
        var robot = new SimulatedRobotDevice(NullLogger<SimulatedRobotDevice>.Instance);
        await robot.ConnectAsync();
        await robot.SetDigitalOutputAsync(2, true);
        robot.ForceDigitalInput(5, true);
        (await robot.GetDigitalInputAsync(5)).Should().BeTrue();
        (await robot.GetDigitalInputAsync(0)).Should().BeFalse();
    }

    [Fact]
    public async Task AdamIo_WriteAndReadBackThroughModbus()
    {
        var modbus = new SimulatedModbusClient(NullLogger<SimulatedModbusClient>.Instance, simulatedDelayMs: 0);
        var io = new AdvantechAdamIoModule(modbus, NullLogger<AdvantechAdamIoModule>.Instance, diCount: 8, doCount: 8);
        await io.ConnectAsync();

        await io.WriteDiAsync(3, true);           // ghi DO coil 3
        (await modbus.ReadCoilsAsync(1, 3, 1))[0].Should().BeTrue();

        modbus.SetDiscreteInput(1, 5, true);      // giả lập DI 5
        (await io.ReadDiAsync(5)).Should().BeTrue();
    }

    [Fact]
    public async Task AdamIo_WriteAndWaitConfirm_Succeeds()
    {
        var modbus = new SimulatedModbusClient(NullLogger<SimulatedModbusClient>.Instance, simulatedDelayMs: 0);
        var io = new AdvantechAdamIoModule(modbus, NullLogger<AdvantechAdamIoModule>.Instance, diCount: 8, doCount: 8);
        await io.ConnectAsync();

        modbus.SetDiscreteInput(1, 2, true); // confirm sẵn sàng
        Func<Task> act = () => io.WriteAndWaitConfirmAsync(0, 2, expectedValue: true, timeoutMs: 1000);
        await act.Should().NotThrowAsync();
    }
}
