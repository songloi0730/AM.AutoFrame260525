---
name: am-testing
description: xUnit + Moq + FluentAssertions test templates cho AM.AutoFrame (.NET 9)
---

# AM Testing Patterns

## NuGet packages (test project)

```xml
<ItemGroup>
  <PackageReference Include="xunit" Version="2.*" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  <PackageReference Include="Moq" Version="4.*" />
  <PackageReference Include="FluentAssertions" Version="6.*" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.*" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.*" />
  <PackageReference Include="coverlet.collector" Version="6.*" />
</ItemGroup>
```

## Test project csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <!-- Relax rules cho test project -->
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

---

## Service Test Template

```csharp
// -------------------------------------------------------
// File:    AlarmServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Unit tests cho AlarmService
// -------------------------------------------------------

using AM.Core.Models;
using AM.Data.Repositories;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AM.Services.Tests;

public sealed class AlarmServiceTests
{
    private readonly Mock<IAlarmRepository> _repoMock;
    private readonly AlarmService _sut;

    public AlarmServiceTests()
    {
        _repoMock = new Mock<IAlarmRepository>();
        _sut = new AlarmService(
            _repoMock.Object,
            NullLogger<AlarmService>.Instance);
    }

    [Fact]
    public async Task RaiseAlarmAsync_Should_SaveToRepository()
    {
        // Arrange
        var alarm = new AlarmModel { Code = 10001, Message = "Axis timeout" };

        // Act
        await _sut.RaiseAlarmAsync(alarm);

        // Assert
        _repoMock.Verify(r => r.AddAsync(alarm, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RaiseAlarmAsync_Should_FireAlarmRaisedEvent()
    {
        // Arrange
        var alarm = new AlarmModel { Code = 10001, Message = "Axis timeout" };
        AlarmModel? received = null;
        _sut.AlarmRaised += (_, e) => received = e.Alarm;

        // Act
        await _sut.RaiseAlarmAsync(alarm);

        // Assert
        received.Should().NotBeNull();
        received!.Code.Should().Be(10001);
    }

    [Fact]
    public async Task GetActiveAlarmsAsync_Should_ReturnOnlyActive()
    {
        // Arrange
        var alarms = new List<AlarmModel>
        {
            new() { Code = 10001, IsActive = true },
            new() { Code = 10002, IsActive = false },
        };
        _repoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(alarms.FindAll(a => a.IsActive));

        // Act
        var result = await _sut.GetActiveAlarmsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Code.Should().Be(10001);
    }
}
```

---

## Step Test Template

```csharp
// -------------------------------------------------------
// File:    Step01InitializeTests.cs
// Project: AM.WorkStation.Demo.Tests
// Purpose: Unit tests cho Step01Initialize
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.WorkStation.Demo.Steps;
using FluentAssertions;
using Moq;

namespace AM.WorkStation.Demo.Tests.Steps;

public sealed class Step01InitializeTests
{
    private readonly Mock<IMotionController> _motionMock;
    private readonly Step01Initialize _sut;

    public Step01InitializeTests()
    {
        _motionMock = new Mock<IMotionController>();
        _sut = new Step01Initialize(_motionMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_Should_HomeAllAxes()
    {
        // Arrange
        _motionMock
            .Setup(m => m.HomeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ExecuteAsync(CancellationToken.None);

        // Assert
        _motionMock.Verify(
            m => m.HomeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Cancel_WhenTokenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _motionMock
            .Setup(m => m.HomeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(5000, ct);
            });

        // Act
        cts.CancelAfter(50);
        var act = () => _sut.ExecuteAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
```

---

## Mechanism Test Template

```csharp
// -------------------------------------------------------
// File:    PickMechanismTests.cs
// Project: AM.WorkStation.Demo.Tests
// Purpose: Unit tests cho PickMechanism
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.WorkStation.Demo.Mechanisms;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AM.WorkStation.Demo.Tests.Mechanisms;

public sealed class PickMechanismTests
{
    private readonly Mock<IHardwareManagerService> _hwManagerMock;
    private readonly Mock<IMotionController> _motionMock;
    private readonly PickMechanism _sut;

    public PickMechanismTests()
    {
        _motionMock = new Mock<IMotionController>();
        _hwManagerMock = new Mock<IHardwareManagerService>();
        _hwManagerMock
            .Setup(h => h.Resolve<IMotionController>("AxisX"))
            .Returns(_motionMock.Object);

        _sut = new PickMechanism(
            _hwManagerMock.Object,
            NullLogger<PickMechanism>.Instance);
    }

    [Fact]
    public async Task InitializeAsync_Should_ConnectMotionController()
    {
        // Act
        await _sut.InitializeAsync();

        // Assert
        _motionMock.Verify(m => m.ConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
        _sut.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task PickAsync_Should_ThrowWhenBusy()
    {
        // Arrange — set IsBusy = true via reflection or dedicated test hook
        // (hoặc dùng SemaphoreSlim trick)
        await _sut.InitializeAsync();

        // Act & Assert
        // Nếu đang pick, gọi pick lần nữa → InvalidOperationException
        var pick1 = _sut.PickAsync(CancellationToken.None);
        var act = () => _sut.PickAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*busy*");

        await pick1; // cleanup
    }

    [Fact]
    public void EmergencyStop_Should_NotThrow()
    {
        // EmergencyStop must NEVER throw
        var act = _sut.EmergencyStop;
        act.Should().NotThrow();
    }
}
```

---

## Integration Test với In-Memory SQLite

```csharp
// -------------------------------------------------------
// File:    ProductionRepositoryIntegrationTests.cs
// Project: AM.Data.Tests
// Purpose: Integration tests với EF Core InMemory
// -------------------------------------------------------

using AM.Core.Models;
using AM.Data;
using AM.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AM.Data.Tests;

public sealed class ProductionRepositoryIntegrationTests : IAsyncDisposable
{
    private readonly AutoMachineDbContext _context;
    private readonly ProductionRepository _sut;

    public ProductionRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AutoMachineDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AutoMachineDbContext(options);
        _sut = new ProductionRepository(_context);
    }

    [Fact]
    public async Task AddAsync_Should_PersistRecord()
    {
        // Arrange
        var record = new ProductionRecord
        {
            ProductId = "P001",
            Result = true,
            Timestamp = DateTime.UtcNow,
        };

        // Act
        await _sut.AddAsync(record);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.ProductionRecords.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.ProductId.Should().Be("P001");
    }

    [Fact]
    public async Task GetByDateRangeAsync_Should_FilterCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        await _sut.AddAsync(new ProductionRecord { Timestamp = now.AddHours(-2) });
        await _sut.AddAsync(new ProductionRecord { Timestamp = now.AddHours(-1) });
        await _sut.AddAsync(new ProductionRecord { Timestamp = now.AddHours(+1) });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByDateRangeAsync(now.AddHours(-3), now);

        // Assert
        result.Should().HaveCount(2);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();
}
```

---

## Coverage Check Commands

```bash
# Chạy tests với coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Xem coverage report (cần reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:./coverage/**/*.xml -targetdir:./coverage/report -reporttypes:Html

# Quick coverage check trong terminal
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:Threshold=80
```

---

## Test Naming Convention

```
{ClassUnderTest}Tests.cs
  {MethodName}_Should_{ExpectedBehavior}
  {MethodName}_Should_{ExpectedBehavior}_When{Condition}

Ví dụ:
  RaiseAlarmAsync_Should_SaveToRepository
  RaiseAlarmAsync_Should_ThrowAlarmException_WhenCodeInvalid
  GetActiveAlarmsAsync_Should_ReturnEmpty_WhenNoActiveAlarms
```

## Test Structure: AAA Pattern

```csharp
// Arrange — chuẩn bị data, mocks
// Act     — gọi method cần test
// Assert  — kiểm tra kết quả
```

## Mock Verification Patterns

```csharp
// Verify called once
mock.Verify(x => x.Method(It.IsAny<string>()), Times.Once);

// Verify called with specific args
mock.Verify(x => x.Method("expected"), Times.Once);

// Verify never called
mock.Verify(x => x.Method(It.IsAny<string>()), Times.Never);

// Capture argument
var captured = new List<string>();
mock.Setup(x => x.Method(Capture.In(captured)));
```
