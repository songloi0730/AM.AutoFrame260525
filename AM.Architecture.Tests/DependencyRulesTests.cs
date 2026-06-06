// -------------------------------------------------------
// File:    DependencyRulesTests.cs
// Project: AM.Architecture.Tests
// Purpose: Enforce Dependency Inversion — UI/Services KHÔNG được ref driver hardware concrete.
//          Chặn "lách luật" gọi SDK hãng trực tiếp từ UI/logic (chỉ qua AM.Core.Abstractions).
// -------------------------------------------------------

using System.Reflection;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Modules.Alarm;
using AM.Modules.Dashboard;
using AM.Services;
using FluentAssertions;
using NetArchTest.Rules;

namespace AM.Architecture.Tests;

public sealed class DependencyRulesTests
{
    // Các assembly driver hardware concrete — tầng trên KHÔNG được phụ thuộc.
    private static readonly string[] ConcreteHardware =
    [
        "AM.Hardware.Motion",
        "AM.Hardware.IO",
        "AM.Hardware.Vision",
        "AM.Hardware.Comm",
        "AM.Hardware.Scanner"
    ];

    private static void AssertNoConcreteHardwareDependency(Assembly assembly)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ConcreteHardware)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "tầng trên chỉ được phụ thuộc AM.Core.Abstractions; vi phạm: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Services_DoNotDependOnConcreteHardware()
        => AssertNoConcreteHardwareDependency(typeof(AlarmService).Assembly);

    [Fact]
    public void DashboardModule_DoesNotDependOnConcreteHardware()
        => AssertNoConcreteHardwareDependency(typeof(DashboardViewModel).Assembly);

    [Fact]
    public void AlarmModule_DoesNotDependOnConcreteHardware()
        => AssertNoConcreteHardwareDependency(typeof(AlarmListViewModel).Assembly);

    [Fact]
    public void Abstractions_DoNotDependOnConcreteHardware()
        => AssertNoConcreteHardwareDependency(typeof(IMotionController).Assembly);

    [Fact]
    public void Infrastructure_DoesNotDependOnConcreteHardware()
        => AssertNoConcreteHardwareDependency(typeof(AM.Infrastructure.BaseMechanism).Assembly);
}
