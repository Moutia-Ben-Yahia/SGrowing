using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Logistics.DrayageScheduler;
using Xunit;

namespace SGrowing.Tests.Logistics;

public sealed class DrayageSchedulerTests
{
    private readonly IntermodalDrayageSchedulerEngine _engine = new();

    [Fact]
    public void GenerateOptimalSchedule_PrioritizesUrgentDemurrageExpiry_SavesDemurrageFees()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var urgentContainer = new ContainerCargoRecord(
            ContainerId: "MSCU1234567",
            Size: ContainerSize.FortyFoot,
            GrossWeightKg: 18000.0,
            IsHazmat: false,
            RequiresGensetReefer: false,
            TerminalCode: "TERM-APM",
            VesselName: "MSC Oscar",
            DischargeTimeUtc: now.AddDays(-3),
            FreeTimeExpiryUtc: now.AddHours(2), // Expiry imminent in 2 hours!
            DailyDemurrageRateUsd: 350.0m
        );

        var normalContainer = new ContainerCargoRecord(
            ContainerId: "MAEU9876543",
            Size: ContainerSize.FortyFoot,
            GrossWeightKg: 16000.0,
            IsHazmat: false,
            RequiresGensetReefer: false,
            TerminalCode: "TERM-APM",
            VesselName: "Maersk Mc-Kinney",
            DischargeTimeUtc: now.AddDays(-1),
            FreeTimeExpiryUtc: now.AddDays(3), // 3 days remaining free time
            DailyDemurrageRateUsd: 250.0m
        );

        var trucker = new DrayageTrucker(
            TruckerId: "TRK-01",
            DriverName: "Ali Benmoussa",
            CurrentChassis: ChassisType.StandardTandem,
            RemainingHoursOfServiceMinutes: 480,
            CurrentDistanceToTerminalKm: 15.0
        );

        var gateSlot = new TerminalGateSlot(
            SlotId: "SLOT-0900",
            TerminalCode: "TERM-APM",
            WindowStartUtc: now.AddMinutes(30),
            WindowEndUtc: now.AddMinutes(90),
            CongestionIndex: 1.2,
            AvailableCapacity: 1 // Only 1 container slot available
        );

        var request = new ScheduleDrayageRequest(
            Containers: [urgentContainer, normalContainer],
            Truckers: [trucker],
            GateSlots: [gateSlot],
            PlanningHorizonUtc: now
        );

        // Act
        var plan = _engine.GenerateOptimalSchedule(request);

        // Assert
        plan.Should().NotBeNull();
        plan.TotalContainersScheduled.Should().Be(1);
        plan.Missions.Should().ContainSingle();
        plan.Missions[0].ContainerId.Should().Be("MSCU1234567", "Urgent container with 2hr slack should be dispatched first.");
        plan.Missions[0].IncurredDemurrageUsd.Should().Be(0m);
        plan.TotalDemurrageSavedUsd.Should().BeGreaterThan(0m);
        plan.UnassignedContainerIds.Should().Contain("MAEU9876543");
    }

    [Fact]
    public void GenerateOptimalSchedule_HeavyWeightContainer_RequiresTriAxleChassis()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var heavyContainer = new ContainerCargoRecord(
            ContainerId: "CMAU5556667",
            Size: ContainerSize.FortyFoot,
            GrossWeightKg: 28500.0, // Exceeds standard tandem 22t limit
            IsHazmat: false,
            RequiresGensetReefer: false,
            TerminalCode: "TERM-DPW",
            VesselName: "CMA CGM Palais",
            DischargeTimeUtc: now.AddDays(-2),
            FreeTimeExpiryUtc: now.AddDays(1),
            DailyDemurrageRateUsd: 300.0m
        );

        // Standard chassis trucker - cannot legally haul 28.5t
        var standardTrucker = new DrayageTrucker(
            TruckerId: "TRK-STD",
            DriverName: "Karim Ziani",
            CurrentChassis: ChassisType.StandardTandem,
            RemainingHoursOfServiceMinutes: 400,
            CurrentDistanceToTerminalKm: 10.0
        );

        // Tri-axle heavy chassis trucker - eligible
        var triAxleTrucker = new DrayageTrucker(
            TruckerId: "TRK-TRI",
            DriverName: "Mourad Meghni",
            CurrentChassis: ChassisType.TriAxleHeavy,
            RemainingHoursOfServiceMinutes: 400,
            CurrentDistanceToTerminalKm: 20.0
        );

        var slot = new TerminalGateSlot(
            SlotId: "SLOT-DPW-1",
            TerminalCode: "TERM-DPW",
            WindowStartUtc: now.AddMinutes(30),
            WindowEndUtc: now.AddHours(3),
            CongestionIndex: 1.0,
            AvailableCapacity: 5
        );

        var request = new ScheduleDrayageRequest(
            Containers: [heavyContainer],
            Truckers: [standardTrucker, triAxleTrucker],
            GateSlots: [slot],
            PlanningHorizonUtc: now
        );

        // Act
        var plan = _engine.GenerateOptimalSchedule(request);

        // Assert
        plan.TotalContainersScheduled.Should().Be(1);
        plan.Missions[0].TruckerId.Should().Be("TRK-TRI", "Heavy container requires tri-axle chassis for road safety compliance.");
    }

    [Fact]
    public void GenerateOptimalSchedule_ReeferContainer_RequiresGensetChassis()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var reeferContainer = new ContainerCargoRecord(
            ContainerId: "HLXU7778889",
            Size: ContainerSize.ReeferFortyFoot,
            GrossWeightKg: 21000.0,
            IsHazmat: false,
            RequiresGensetReefer: true,
            TerminalCode: "TERM-MED",
            VesselName: "Hapag-Lloyd Berlin",
            DischargeTimeUtc: now.AddDays(-1),
            FreeTimeExpiryUtc: now.AddDays(2),
            DailyDemurrageRateUsd: 400.0m
        );

        var tandemTrucker = new DrayageTrucker("TRK-1", "Driver 1", ChassisType.StandardTandem, 400, 10.0);
        var triAxleTrucker = new DrayageTrucker("TRK-2", "Driver 2", ChassisType.TriAxleHeavy, 400, 10.0);

        var slot = new TerminalGateSlot("SLOT-1", "TERM-MED", now.AddMinutes(30), now.AddHours(2), 1.0, 5);

        var request = new ScheduleDrayageRequest(
            Containers: [reeferContainer],
            Truckers: [tandemTrucker, triAxleTrucker], // Neither has genset
            GateSlots: [slot],
            PlanningHorizonUtc: now
        );

        // Act
        var plan = _engine.GenerateOptimalSchedule(request);

        // Assert
        plan.TotalContainersScheduled.Should().Be(0);
        plan.UnassignedContainerIds.Should().Contain("HLXU7778889", "Cannot dispatch reefer without genset power unit.");
    }

    [Fact]
    public void AddDrayageSchedulerServices_RegistersEngineAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDrayageSchedulerServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(IntermodalDrayageSchedulerEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
