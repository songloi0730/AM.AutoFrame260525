// -------------------------------------------------------
// File:    MasterControllerIntegrationTests.cs
// Project: AM.WorkStation.Demo.Tests
// Purpose: Vòng đời ISA-88 của DemoMasterController nối SequenceEngine —
//          Initialize → Start → Pause/Resume → Stop (PackML mapping spec §3)
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.WorkStation.Demo.Controllers;
using AM.WorkStation.Demo.Mechanisms;
using AM.WorkStation.Demo.Stations;
using AM.WorkStation.Demo.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AM.WorkStation.Demo.Tests;

public sealed class MasterControllerIntegrationTests
{
    private static DemoMasterController CreateMaster(ScenarioHarness h)
    {
        // Hạ tầng 3-tier hiển thị — hardware là Moq (loose: Task method trả CompletedTask)
        var pick = new DemoPickMechanism(Mock.Of<IMotionController>(), Mock.Of<IIoModule>(),
            NullLogger<DemoPickMechanism>.Instance);
        var inspect = new DemoInspectMechanism(Mock.Of<ICameraDevice>(),
            NullLogger<DemoInspectMechanism>.Instance);
        var station = new DemoStation(pick, inspect, Mock.Of<IRecipeService>(),
            Mock.Of<IAlarmService>(), NullLogger<DemoStation>.Instance);

        var safety = new Mock<ISafetyInput>();
        safety.SetupGet(s => s.IsAllSafe).Returns(true);

        var source = new AM.WorkStation.Demo.Sequencing.SequenceSource(
            Path.Combine(AppContext.BaseDirectory, "recipes", "DemoPickPlace.sequence.json"),
            h.Resolver, NullLogger<AM.WorkStation.Demo.Sequencing.SequenceSource>.Instance);

        return new DemoMasterController(station, Mock.Of<IStationSyncService>(),
            h.Engine, source, h.Resolver, Mock.Of<IAlarmService>(),
            NullLogger<DemoMasterController>.Instance, safety.Object);
    }

    [Fact]
    public async Task MasterController_FullLifecycle_InitializeStartPauseResumeStop()
    {
        var h = new ScenarioHarness();
        await using var master = CreateMaster(h);

        // Khởi tạo: Uninitialized → Idle (init 3-tier + sequencing stations theo thứ tự khai báo)
        await master.InitializeAsync();
        master.State.Should().Be(MachineState.Idle);

        // Chạy: mỗi cycle = 1 sản phẩm; CycleCompleted của master phát theo từng sản phẩm
        int cycles = 0;
        master.CycleCompleted += (_, _) => Interlocked.Increment(ref cycles);
        await master.StartAsync();
        master.State.Should().Be(MachineState.Running);
        await ScenarioHarness.WaitUntilAsync(() => Volatile.Read(ref cycles) >= 2, 10_000);

        // Tạm dừng: trigger ISA-88 + engine dừng ở ranh giới bước (giữa cycle)
        await master.PauseAsync();
        master.State.Should().Be(MachineState.Paused);

        // Tiếp tục rồi chạy thêm ít nhất 1 cycle nữa
        int atResume = Volatile.Read(ref cycles);
        await master.ResumeAsync();
        master.State.Should().Be(MachineState.Running);
        await ScenarioHarness.WaitUntilAsync(() => Volatile.Read(ref cycles) > atResume, 10_000);

        // Dừng: về Idle, cycle dở KHÔNG được đếm thêm sau khi dừng hẳn
        await master.StopAsync();
        master.State.Should().Be(MachineState.Idle);
        await Task.Delay(300);
        int afterStop = Volatile.Read(ref cycles);
        await Task.Delay(300);
        Volatile.Read(ref cycles).Should().Be(afterStop, "đã Stop — không còn cycle mới");

        h.Records.Count.Should().BeGreaterThanOrEqualTo(cycles, "mỗi CycleCompleted có record tương ứng");
    }
}
