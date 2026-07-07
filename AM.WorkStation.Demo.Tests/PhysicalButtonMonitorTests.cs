// -------------------------------------------------------
// File:    PhysicalButtonMonitorTests.cs
// Project: AM.WorkStation.Demo.Tests
// Purpose: Test P1.3 — nút vật lý edge-detect gọi đúng lệnh master, giữ nút không lặp
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Machine;
using AM.WorkStation.Demo.Sequencing;
using AM.WorkStation.Demo.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AM.WorkStation.Demo.Tests;

public sealed class PhysicalButtonMonitorTests
{
    private const string BtnStart = "DI.Btn.Start";
    private const string BtnStop = "DI.Btn.Stop";

    private static (PhysicalButtonMonitor Monitor, SimIoService Sim, Mock<IMasterController> Master) CreateSut()
    {
        var sim = new SimIoService(new DemoSimOptions(), NullLogger<SimIoService>.Instance);
        var master = new Mock<IMasterController>();
        var monitor = new PhysicalButtonMonitor(sim, master.Object,
            NullLogger<PhysicalButtonMonitor>.Instance);
        return (monitor, sim, master);
    }

    [Fact]
    public async Task Start_RisingEdgeOnStartButton_CallsStartOnce()
    {
        var (monitor, sim, master) = CreateSut();
        using (monitor)
        {
            monitor.Start();
            await Task.Delay(150); // vài tick poll với nút chưa nhấn

            sim.SetDi(BtnStart, true); // nhấn và GIỮ nút
            await ScenarioHarness.WaitUntilAsync(
                () => master.Invocations.Count(i => i.Method.Name == nameof(IMasterController.StartAsync)) == 1);
            await Task.Delay(200); // giữ nút qua nhiều tick

            master.Verify(m => m.StartAsync(It.IsAny<CancellationToken>()), Times.Once,
                "edge-detect: giữ nút KHÔNG được lặp lệnh");
        }
    }

    [Fact]
    public async Task Start_PressReleasePressAgain_CallsStartTwice()
    {
        var (monitor, sim, master) = CreateSut();
        using (monitor)
        {
            monitor.Start();
            sim.SetDi(BtnStart, true);
            await ScenarioHarness.WaitUntilAsync(
                () => master.Invocations.Count(i => i.Method.Name == nameof(IMasterController.StartAsync)) == 1);

            sim.SetDi(BtnStart, false);
            await Task.Delay(150); // nhả qua vài tick
            sim.SetDi(BtnStart, true);
            await ScenarioHarness.WaitUntilAsync(
                () => master.Invocations.Count(i => i.Method.Name == nameof(IMasterController.StartAsync)) == 2);

            master.Verify(m => m.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }

    [Fact]
    public async Task StopButton_Pressed_CallsStop()
    {
        var (monitor, sim, master) = CreateSut();
        using (monitor)
        {
            monitor.Start();
            sim.SetDi(BtnStop, true);
            await ScenarioHarness.WaitUntilAsync(
                () => master.Invocations.Any(i => i.Method.Name == nameof(IMasterController.StopAsync)));

            master.Verify(m => m.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
            master.Verify(m => m.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
