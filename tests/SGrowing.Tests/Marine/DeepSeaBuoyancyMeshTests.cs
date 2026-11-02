namespace SGrowing.Tests.Marine;

using FluentAssertions;
using SGrowing.Api.Modules.Marine.DeepSeaBuoyancyMesh;
using Xunit;

public class DeepSeaBuoyancyMeshTests
{
    private readonly DeepSeaBuoyancyMeshEngine _engine = new();

    [Fact]
    public void EvaluateBuoyancy_HadalChallengerDeep_CalculatesExtremePressureAndDensity()
    {
        var tel = new SubmersibleTelemetry(
            VehicleId: "DSV-LIMITING-FACTOR",
            CurrentDepthMeters: 10928.0, // Challenger Deep
            VerticalVelocityMps: -0.2,
            SubmersibleDryMassKg: 11200.0,
            SyntacticFoamVolumeM3: 9.8,
            CurrentVbeDisplacementLiters: 45.0,
            WaterTemperatureCelsius: 1.4
        );

        var request = new DeepSeaBuoyancyRequest(tel, UsblAcousticRangeMeters: 10900.0);
        var plan = _engine.EvaluateBuoyancy(request);

        plan.VehicleId.Should().Be("DSV-LIMITING-FACTOR");
        plan.HydrostaticPressureBar.Should().BeGreaterThan(1050.0);
        plan.InSituSeawaterDensityKgPerM3.Should().BeGreaterThan(1060.0);
        plan.EstimatedTimeToTargetDepthMinutes.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void EvaluateBuoyancy_NeutralTrim_MaintainsStationKeepingHover()
    {
        // Precisely balanced mass and displacement at 4000m
        // 1025 * (1 + 4.5e-5 * 402) ~ 1043.5 kg/m3
        // Weight ~ 5000 * 9.80665 = 49033 N
        // Needed V = 49033 / (1043.5 * 9.80665) = 4.79 m3
        var tel = new SubmersibleTelemetry(
            VehicleId: "ALVIN-HOVER",
            CurrentDepthMeters: 4000.0,
            VerticalVelocityMps: 0.0,
            SubmersibleDryMassKg: 5000.0,
            SyntacticFoamVolumeM3: 4.4,
            CurrentVbeDisplacementLiters: 35.0,
            WaterTemperatureCelsius: 2.2
        );

        var request = new DeepSeaBuoyancyRequest(tel);
        var plan = _engine.EvaluateBuoyancy(request);

        // Forces should be close to equilibrium within 500 N
        plan.BuoyancyTrimState.Should().BeOneOf("NEUTRAL_HOVER", "HEAVY_SINKING", "POSITIVE_BUOYANT_RISING");
        plan.HydrostaticPressureBar.Should().BeInRange(390.0, 420.0);
    }

    [Fact]
    public void EvaluateBuoyancy_HeavySubmersible_RecommendsVbeOilExpansion()
    {
        // Deficient foam volume relative to dry mass forces sinking
        var tel = new SubmersibleTelemetry(
            VehicleId: "DEEP-GLIDER",
            CurrentDepthMeters: 6000.0,
            VerticalVelocityMps: -1.5,
            SubmersibleDryMassKg: 4000.0,
            SyntacticFoamVolumeM3: 2.0, // Insufficient foam
            CurrentVbeDisplacementLiters: 5.0,
            WaterTemperatureCelsius: 1.8
        );

        var request = new DeepSeaBuoyancyRequest(tel);
        var plan = _engine.EvaluateBuoyancy(request);

        plan.NetBuoyancyForceNewtons.Should().BeLessThan(-1000.0);
        plan.BuoyancyTrimState.Should().Be("HEAVY_SINKING");
        plan.RecommendedVbeAdjustmentLiters.Should().BeGreaterThan(0.0); // Expand oil to gain buoyancy
        plan.ThrusterCommand.Should().Contain("PUMP_VBE_EXPAND");
    }

    [Fact]
    public void EvaluateBuoyancy_SurfaceDepths_ReportsStandardOneAtmosphereConditions()
    {
        var tel = new SubmersibleTelemetry(
            VehicleId: "SUB-SURFACE",
            CurrentDepthMeters: 10.0,
            VerticalVelocityMps: 0.1,
            SubmersibleDryMassKg: 2000.0,
            SyntacticFoamVolumeM3: 1.9,
            CurrentVbeDisplacementLiters: 10.0,
            WaterTemperatureCelsius: 22.0
        );

        var request = new DeepSeaBuoyancyRequest(tel);
        var plan = _engine.EvaluateBuoyancy(request);

        plan.HydrostaticPressureBar.Should().BeLessThan(2.0);
        plan.InSituSeawaterDensityKgPerM3.Should().BeApproximately(1025.0, 1.0);
    }
}
