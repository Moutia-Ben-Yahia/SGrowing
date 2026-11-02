using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Energy.BessArbitrage;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class BessArbitrageTests
{
    private readonly BessPeakShavingArbitrageEngine _engine = new();

    private static IReadOnlyList<HourlyTariffAndDemand> Create24HourProfile()
    {
        var list = new List<HourlyTariffAndDemand>();
        for (var h = 0; h < 24; h++)
        {
            // Night hours (0-5): Low demand (150kW), low price (0.08 EUR/kWh)
            if (h < 6)
            {
                list.Add(new(h, 150.0, 0.08m, false));
            }
            // Afternoon peak (14-18): High demand (650kW), peak price (0.35 EUR/kWh)
            else if (h is >= 14 and <= 18)
            {
                list.Add(new(h, 650.0, 0.35m, true));
            }
            // Standard day hours (6-13, 19-23): Normal demand (300kW), normal price (0.18 EUR/kWh)
            else
            {
                list.Add(new(h, 300.0, 0.18m, false));
            }
        }
        return list;
    }

    [Fact]
    public void OptimizeSchedule_ShavesPeakDemandAndGeneratesNetDailySavings()
    {
        // Arrange: 1000 kWh / 250 kW LFP battery system
        var specs = new BessSystemSpecs(
            BatteryId: "BESS-LFP-01",
            TotalCapacityKwh: 1000.0,
            MaxChargePowerKw: 250.0,
            MaxDischargePowerKw: 250.0,
            RoundTripEfficiency: 0.88,
            MinAllowedSoC: 0.10,
            MaxAllowedSoC: 0.90,
            InitialSoC: 0.50
        );

        var request = new OptimizeBessScheduleRequest(
            Specs: specs,
            Tariffs: Create24HourProfile(),
            MonthlyPeakChargePerKwEur: 18.0m // 18 EUR/kW monthly demand charge
        );

        // Act
        var plan = _engine.OptimizeSchedule(request);

        // Assert
        plan.Should().NotBeNull();
        plan.PeakDemandBeforeKw.Should().Be(650.0);
        plan.PeakDemandAfterKw.Should().BeLessThan(650.0);
        plan.PeakShavedKw.Should().BeGreaterThan(50.0);
        plan.TotalDailyNetSavingsEur.Should().BeGreaterThan(0m);
        plan.PeakDemandChargeReductionEur.Should().BeGreaterThan(0m);
        plan.HourlySchedule.Should().HaveCount(24);
    }

    [Fact]
    public void OptimizeSchedule_MaintainsSoCWithinSafeLimits()
    {
        // Arrange
        var specs = new BessSystemSpecs("BESS-02", 500.0, 150.0, 150.0, 0.88, 0.10, 0.90, 0.20);
        var request = new OptimizeBessScheduleRequest(specs, Create24HourProfile(), 15.0m);

        // Act
        var plan = _engine.OptimizeSchedule(request);

        // Assert: Every single hourly resulting SoC must strictly obey safety bounds
        foreach (var dispatch in plan.HourlySchedule)
        {
            dispatch.ResultingSoC.Should().BeInRange(0.10, 0.90);
        }
    }

    [Fact]
    public void OptimizeSchedule_ChargesDuringOffPeakAndDischargesDuringPeak()
    {
        // Arrange
        var specs = new BessSystemSpecs("BESS-03", 800.0, 200.0, 200.0, 0.88, 0.10, 0.90, 0.30);
        var request = new OptimizeBessScheduleRequest(specs, Create24HourProfile(), 20.0m);

        // Act
        var plan = _engine.OptimizeSchedule(request);

        // Assert: Early night hours should charge
        var nightDispatches = plan.HourlySchedule.Where(d => d.HourOfDay < 6).ToList();
        nightDispatches.Should().Contain(d => d.Mode == BatteryOperatingMode.Charge);

        // Afternoon peak hours should discharge
        var peakDispatches = plan.HourlySchedule.Where(d => d.HourOfDay is >= 14 and <= 18).ToList();
        peakDispatches.Should().Contain(d => d.Mode == BatteryOperatingMode.Discharge);
    }

    [Fact]
    public void AddBessArbitrageServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBessArbitrageServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(BessPeakShavingArbitrageEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
