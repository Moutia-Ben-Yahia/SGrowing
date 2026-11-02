using SGrowing.Api.Modules.Energy.VrfbShuntCurrentRebalancing;
using Xunit;

namespace SGrowing.Tests.Energy;

public class VrfbShuntCurrentTests
{
    private readonly VrfbShuntCurrentEngine _engine = new();

    [Fact]
    public void AnalyzeStack_NominalVrfbSystem_ReturnsStackCertified()
    {
        var telemetry = new VrfbShuntCurrentTelemetry
        {
            VrfbStorageSystemId = "VRFB-MW-001",
            StackCellCount = 40,
            RatedStackPowerKilowatts = 150.0,
            MeasuredShuntCurrentLossKilowatts = 3.8,
            PositiveTankStateOfChargePercent = 64.5,
            NegativeTankStateOfChargePercent = 63.2,
            VanadiumCrossoverRateMolesPerDay = 0.0085,
            PositiveElectrolyteTemperatureCelsius = 28.5,
            NegativeElectrolyteTemperatureCelsius = 27.8,
            RoundTripEnergyEfficiencyPercent = 83.5,
            CoulombicEfficiencyPercent = 97.2
        };

        var result = _engine.AnalyzeStack(telemetry);

        Assert.NotNull(result);
        Assert.Equal("VRFB-MW-001", result.VrfbStorageSystemId);
        Assert.True(result.IsShuntCurrentLossAcceptable);
        Assert.True(result.IsStateOfChargeSymmetric);
        Assert.True(result.IsElectrolyteTemperatureSafe);
        Assert.True(result.IsVrfbStackHealthCertified);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.ShuntCurrentPowerLossFractionPercent <= 3.0);
        Assert.Equal(1.30, result.StateOfChargeAsymmetryPercent);
    }

    [Fact]
    public void AnalyzeStack_ExcessiveShuntCurrent_TriggersShuntSurgeAlert()
    {
        var telemetry = new VrfbShuntCurrentTelemetry
        {
            VrfbStorageSystemId = "VRFB-MW-002",
            RatedStackPowerKilowatts = 100.0,
            MeasuredShuntCurrentLossKilowatts = 6.5 // 6.5% > 3.8%
        };

        var result = _engine.AnalyzeStack(telemetry);

        Assert.False(result.IsShuntCurrentLossAcceptable);
        Assert.False(result.IsVrfbStackHealthCertified);
        Assert.Contains("MANIFOLD_PARASITIC_SHUNT_CURRENT_SURGE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeStack_AsymmetricSocMismatch_TriggersOxidationStateAlert()
    {
        var telemetry = new VrfbShuntCurrentTelemetry
        {
            VrfbStorageSystemId = "VRFB-MW-003",
            PositiveTankStateOfChargePercent = 75.0,
            NegativeTankStateOfChargePercent = 67.5 // Diff = 7.5% > 3.0%
        };

        var result = _engine.AnalyzeStack(telemetry);

        Assert.False(result.IsStateOfChargeSymmetric);
        Assert.False(result.IsVrfbStackHealthCertified);
        Assert.Contains("ELECTROLYTE_OXIDATION_STATE_ASYMMETRY", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeStack_HighPositiveElectrolyteTemperature_TriggersPrecipitationAlert()
    {
        var telemetry = new VrfbShuntCurrentTelemetry
        {
            VrfbStorageSystemId = "VRFB-MW-004",
            PositiveElectrolyteTemperatureCelsius = 43.5 // > 39 C threshold
        };

        var result = _engine.AnalyzeStack(telemetry);

        Assert.False(result.IsElectrolyteTemperatureSafe);
        Assert.False(result.IsVrfbStackHealthCertified);
        Assert.Contains("VANADIUM_PRECIPITATION_TEMPERATURE_BREACH", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeStack_LowRoundTripEfficiency_FailsCertification()
    {
        var telemetry = new VrfbShuntCurrentTelemetry
        {
            VrfbStorageSystemId = "VRFB-MW-005",
            RoundTripEnergyEfficiencyPercent = 76.0
        };

        var result = _engine.AnalyzeStack(telemetry);

        Assert.False(result.IsVrfbStackHealthCertified);
    }
}
