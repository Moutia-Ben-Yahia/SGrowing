using System;
using SGrowing.Api.Modules.Energy.SolidStateBatteryDendriteSuppression;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class SolidStateBatteryDendriteTests
{
    private readonly SolidStateBatteryDendriteEngine _engine = new();

    [Fact]
    public void AnalyzeDendriteSuppression_OptimalEnvelope_ReturnsDendriteSuppressed()
    {
        var telemetry = new SolidStateBatteryDendriteTelemetry
        {
            CellPackId = "SSB-LLZO-CELL-01",
            SolidElectrolyteType = "LLZO",
            AppliedCurrentDensityMaPerCm2 = 1.8,
            UniaxialStackPressureMpa = 3.0, // within 1.5 - 5.0 MPa
            SolidElectrolyteShearModulusGpa = 60.0,
            ElectrolyteFractureToughnessMpaM12 = 1.25,
            GrainBoundaryFlawRadiusMicrons = 0.5,
            CellTemperatureCelsius = 35.0,
            BaselineCriticalCurrentDensityMaPerCm2 = 2.4
        };

        var result = _engine.AnalyzeDendriteSuppression(telemetry);

        Assert.Equal("SSB-LLZO-CELL-01", result.CellPackId);
        Assert.True(result.IsStackPressureWithinOptimalEnvelope);
        Assert.True(result.IsDendritePropagationSuppressed);
        Assert.True(result.IsFastChargeSafe);
        Assert.True(result.EffectiveCriticalCurrentDensityMaPerCm2 > telemetry.AppliedCurrentDensityMaPerCm2);
        Assert.Equal("OPTIMAL_DENDRITE_SUPPRESSED_FAST_CHARGE", result.BatteryIntegrityRegime);
    }

    [Fact]
    public void AnalyzeDendriteSuppression_LowStackPressure_TriggersVoidFormation()
    {
        var telemetry = new SolidStateBatteryDendriteTelemetry
        {
            CellPackId = "SSB-VOID-02",
            SolidElectrolyteType = "LLZO",
            AppliedCurrentDensityMaPerCm2 = 1.2,
            UniaxialStackPressureMpa = 0.8, // Below 1.5 MPa
            SolidElectrolyteShearModulusGpa = 60.0,
            ElectrolyteFractureToughnessMpaM12 = 1.25,
            GrainBoundaryFlawRadiusMicrons = 0.6,
            CellTemperatureCelsius = 25.0,
            BaselineCriticalCurrentDensityMaPerCm2 = 2.0
        };

        var result = _engine.AnalyzeDendriteSuppression(telemetry);

        Assert.False(result.IsStackPressureWithinOptimalEnvelope);
        Assert.Equal("INTERFACIAL_VOID_FORMATION_DELAMINATION", result.BatteryIntegrityRegime);
        Assert.Contains("Increase fixture spring compression", result.PressureManagementProtocol);
    }

    [Fact]
    public void AnalyzeDendriteSuppression_ExcessiveStackPressure_TriggersLiCreep()
    {
        var telemetry = new SolidStateBatteryDendriteTelemetry
        {
            CellPackId = "SSB-CREEP-03",
            SolidElectrolyteType = "LLZO",
            AppliedCurrentDensityMaPerCm2 = 1.5,
            UniaxialStackPressureMpa = 6.5, // Exceeds 5.0 MPa
            SolidElectrolyteShearModulusGpa = 60.0,
            ElectrolyteFractureToughnessMpaM12 = 1.25,
            GrainBoundaryFlawRadiusMicrons = 0.6,
            CellTemperatureCelsius = 25.0,
            BaselineCriticalCurrentDensityMaPerCm2 = 2.4
        };

        var result = _engine.AnalyzeDendriteSuppression(telemetry);

        Assert.False(result.IsStackPressureWithinOptimalEnvelope);
        Assert.Equal("EXCESSIVE_STACK_PRESSURE_LI_PLASTIC_CREEP", result.BatteryIntegrityRegime);
        Assert.Contains("Ease mechanical clamp tension", result.PressureManagementProtocol);
    }

    [Fact]
    public void AnalyzeDendriteSuppression_HighCurrentRatio_TriggersCriticalCurrentSurcharge()
    {
        var telemetry = new SolidStateBatteryDendriteTelemetry
        {
            CellPackId = "SSB-HIGH-J-04",
            SolidElectrolyteType = "LPS",
            AppliedCurrentDensityMaPerCm2 = 3.6, // Near/exceeds j_CCD
            UniaxialStackPressureMpa = 2.5,
            SolidElectrolyteShearModulusGpa = 20.0,
            ElectrolyteFractureToughnessMpaM12 = 0.95,
            GrainBoundaryFlawRadiusMicrons = 0.4,
            CellTemperatureCelsius = 25.0,
            BaselineCriticalCurrentDensityMaPerCm2 = 2.0
        };

        var result = _engine.AnalyzeDendriteSuppression(telemetry);

        Assert.False(result.IsFastChargeSafe);
        Assert.True(result.CurrentDensitySafetyRatio > 0.80);
        Assert.Contains("Throttle fast-charge profile", result.PressureManagementProtocol);
    }

    [Fact]
    public void AnalyzeDendriteSuppression_ZeroOrNegativeCurrent_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeDendriteSuppression(null!));

        var invalidTelemetry = new SolidStateBatteryDendriteTelemetry
        {
            CellPackId = "SSB-INV",
            AppliedCurrentDensityMaPerCm2 = 0.0,
            UniaxialStackPressureMpa = 2.0
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeDendriteSuppression(invalidTelemetry));
    }
}
