using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Sustainability.CircularRemanufacturing;
using Xunit;

namespace SGrowing.Tests.Sustainability;

public sealed class CircularRemanufacturingTests
{
    private readonly DisassemblyYieldOptimizationEngine _engine = new();

    [Fact]
    public void AnalyzeYield_LowOperatingHours_RecommendsDirectReuseAndCalculatesCarbonSavings()
    {
        // Arrange: Barely used electric vehicle inverter (1000 hrs used vs 25,000 hrs scale parameter)
        var lowHoursPart = new ComponentDisassemblyNode(
            PartNumber: "INV-EV-2026",
            PartName: "Silicon Carbide Traction Inverter",
            MaterialType: "Semiconductor",
            OperatingHoursUsed: 1000.0,
            WeibullShapeBeta: 1.5,
            WeibullScaleEtaHours: 25000.0,
            RefurbishCostEur: 200.0m,
            ResaleValueEur: 1800.0m,
            RecyclingMeltValueEur: 45.0m,
            DisassemblyLaborMinutes: 30, // 0.5 hours labor
            VirginMaterialCarbonKg: 120.0
        );

        var request = new AnalyzeProductYieldRequest(
            ProductSerialNumber: "EV-CHASSIS-0099",
            ModelNumber: "MODEL-E-PRO",
            HourlyLaborRateEur: 60.0m, // 30 EUR labor cost
            Components: [lowHoursPart]
        );

        // Act
        var report = _engine.AnalyzeYield(request);

        // Assert
        report.Should().NotBeNull();
        report.PartRecommendations.Should().ContainSingle();
        var rec = report.PartRecommendations[0];
        rec.RecommendedDisposition.Should().Be(PartDisposition.DirectReuse);
        rec.WeibullReliabilityScore.Should().BeGreaterThan(0.95);
        rec.NetEconomicValueEur.Should().Be(1800.0m - 30.0m); // 1770 EUR
        rec.AvoidedVirginCarbonKgCo2.Should().BeApproximately(114.0, 1.0); // 95% of 120kg
        report.OverallCircularYieldPercent.Should().Be(100.0);
    }

    [Fact]
    public void AnalyzeYield_HighOperatingHours_RecommendsMaterialScrapRecycling()
    {
        // Arrange: Severely fatigued hydraulic pump (35,000 hrs used vs 20,000 hrs scale parameter)
        var fatiguedPart = new ComponentDisassemblyNode(
            PartNumber: "PUMP-HYD-01",
            PartName: "High-Pressure Hydraulic Pump",
            MaterialType: "Steel / Bronze",
            OperatingHoursUsed: 35000.0,
            WeibullShapeBeta: 2.0,
            WeibullScaleEtaHours: 20000.0,
            RefurbishCostEur: 800.0m,
            ResaleValueEur: 900.0m,
            RecyclingMeltValueEur: 85.0m,
            DisassemblyLaborMinutes: 20, // 0.33 hours labor = 20 EUR
            VirginMaterialCarbonKg: 50.0
        );

        var request = new AnalyzeProductYieldRequest("EQ-PUMP-88", "HYD-PUMP", 60.0m, [fatiguedPart]);

        // Act
        var report = _engine.AnalyzeYield(request);

        // Assert
        var rec = report.PartRecommendations[0];
        rec.RecommendedDisposition.Should().Be(PartDisposition.MaterialRecyclingScrap);
        rec.WeibullReliabilityScore.Should().BeLessThan(0.10);
        rec.NetEconomicValueEur.Should().Be(85.0m - 20.0m); // 65 EUR
        rec.EngineeringRationale.Should().Contain("ADVANCED WEAR");
    }

    [Fact]
    public void AnalyzeYield_ModerateWear_RecommendsRefurbishRemanufacture()
    {
        // Arrange: Moderate wear bearing assembly (12,000 hrs used vs 20,000 hrs scale)
        var moderatePart = new ComponentDisassemblyNode(
            PartNumber: "BEAR-01",
            PartName: "Precision Spindle Bearing",
            MaterialType: "Alloy",
            OperatingHoursUsed: 12000.0,
            WeibullShapeBeta: 1.6,
            WeibullScaleEtaHours: 20000.0,
            RefurbishCostEur: 150.0m,
            ResaleValueEur: 650.0m,
            RecyclingMeltValueEur: 25.0m,
            DisassemblyLaborMinutes: 15,
            VirginMaterialCarbonKg: 40.0
        );

        var request = new AnalyzeProductYieldRequest("CNC-LATHE-01", "CNC-5X", 60.0m, [moderatePart]);

        // Act
        var report = _engine.AnalyzeYield(request);

        // Assert
        var rec = report.PartRecommendations[0];
        rec.RecommendedDisposition.Should().Be(PartDisposition.RefurbishRemanufacture);
        rec.WeibullReliabilityScore.Should().BeInRange(0.50, 0.85);
        rec.EngineeringRationale.Should().Contain("MODERATE FATIGUE");
    }

    [Fact]
    public void AddCircularRemanufacturingServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCircularRemanufacturingServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(DisassemblyYieldOptimizationEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
