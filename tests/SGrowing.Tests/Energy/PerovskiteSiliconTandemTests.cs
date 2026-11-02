using System;
using SGrowing.Api.Modules.Energy.PerovskiteSiliconTandemRecombination;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class PerovskiteSiliconTandemTests
{
    private readonly PerovskiteSiliconTandemEngine _engine = new();

    [Fact]
    public void AnalyzeTandemCell_RecordParameters_ReturnsRecordGradeStable()
    {
        var telemetry = new PerovskiteSiliconTandemTelemetry
        {
            TandemCellId = "TANDEM-RECORD-001",
            ArchitectureType = "Monolithic-2T",
            TopPerovskiteCurrentDensityMaPerCm2 = 20.0,
            BottomSiliconCurrentDensityMaPerCm2 = 20.1, // Near-perfect current matching
            TopPerovskiteOpenCircuitVoltageVolts = 1.25,
            BottomSiliconOpenCircuitVoltageVolts = 0.72,
            TunnelJunctionContactResistivityMOhmCm2 = 10.0,
            FillFactorPercent = 82.5,
            BulkPerovskiteSrhRecombinationLossMv = 20.0,
            HalidePhaseSegregationDefectDensityCm3 = 1.5e15
        };

        var result = _engine.AnalyzeTandemCell(telemetry);

        Assert.Equal("TANDEM-RECORD-001", result.TandemCellId);
        Assert.True(result.IsCurrentMatchingOptimal);
        Assert.True(result.IsTunnelJunctionOhmicLossLow);
        Assert.True(result.IsTandemEfficiencyRecordGrade);
        Assert.True(result.PowerConversionEfficiencyPercent >= 30.0);
        Assert.Equal("RECORD_GRADE_PEROVSKITE_SILICON_TANDEM_STABLE", result.TandemOperationRegime);
    }

    [Fact]
    public void AnalyzeTandemCell_CurrentMismatch_TriggersSubCellLimitingRegime()
    {
        var telemetry = new PerovskiteSiliconTandemTelemetry
        {
            TandemCellId = "TANDEM-MISMATCH-02",
            ArchitectureType = "Monolithic-2T",
            TopPerovskiteCurrentDensityMaPerCm2 = 18.2, // Bottleneck in top cell
            BottomSiliconCurrentDensityMaPerCm2 = 21.5, // Mismatch > 3.0%
            TopPerovskiteOpenCircuitVoltageVolts = 1.22,
            BottomSiliconOpenCircuitVoltageVolts = 0.71,
            TunnelJunctionContactResistivityMOhmCm2 = 12.0,
            FillFactorPercent = 80.0
        };

        var result = _engine.AnalyzeTandemCell(telemetry);

        Assert.False(result.IsCurrentMatchingOptimal);
        Assert.Equal("SUB_CELL_CURRENT_MISMATCH_CURRENT_LIMITING", result.TandemOperationRegime);
        Assert.Contains("Top Perovskite", result.TandemOptimizationRecommendation);
    }

    [Fact]
    public void AnalyzeTandemCell_HighTunnelJunctionResistivity_TriggersOhmicBottleneck()
    {
        var telemetry = new PerovskiteSiliconTandemTelemetry
        {
            TandemCellId = "TANDEM-TJ-RESISTIVE-03",
            ArchitectureType = "Monolithic-2T",
            TopPerovskiteCurrentDensityMaPerCm2 = 19.8,
            BottomSiliconCurrentDensityMaPerCm2 = 20.0,
            TopPerovskiteOpenCircuitVoltageVolts = 1.22,
            BottomSiliconOpenCircuitVoltageVolts = 0.71,
            TunnelJunctionContactResistivityMOhmCm2 = 55.0, // High contact resistivity > 25 mOhm*cm^2
            FillFactorPercent = 78.0
        };

        var result = _engine.AnalyzeTandemCell(telemetry);

        Assert.False(result.IsTunnelJunctionOhmicLossLow);
        Assert.Equal("TUNNEL_JUNCTION_HIGH_RESISTIVITY_BOTTLENECK", result.TandemOperationRegime);
        Assert.Contains("contact resistivity", result.TandemOptimizationRecommendation);
    }

    [Fact]
    public void AnalyzeTandemCell_HighSrhLoss_TriggersNonRadiativeRecombinationDeficit()
    {
        var telemetry = new PerovskiteSiliconTandemTelemetry
        {
            TandemCellId = "TANDEM-SRH-LOSS-04",
            ArchitectureType = "Monolithic-2T",
            TopPerovskiteCurrentDensityMaPerCm2 = 19.9,
            BottomSiliconCurrentDensityMaPerCm2 = 20.0,
            TopPerovskiteOpenCircuitVoltageVolts = 1.20,
            BottomSiliconOpenCircuitVoltageVolts = 0.70,
            TunnelJunctionContactResistivityMOhmCm2 = 10.0,
            FillFactorPercent = 80.0,
            BulkPerovskiteSrhRecombinationLossMv = 55.0 // Severe SRH loss > 45 mV
        };

        var result = _engine.AnalyzeTandemCell(telemetry);

        Assert.Equal("PEROVSKITE_NON_RADIATIVE_SRH_RECOMBINATION_DEFICIT", result.TandemOperationRegime);
        Assert.Contains("Shockley-Read-Hall non-radiative recombination", result.TandemOptimizationRecommendation);
    }

    [Fact]
    public void AnalyzeTandemCell_ZeroOrNegativeCurrent_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeTandemCell(null!));

        var invalidTelemetry = new PerovskiteSiliconTandemTelemetry
        {
            TandemCellId = "TANDEM-INV",
            TopPerovskiteCurrentDensityMaPerCm2 = 0.0,
            BottomSiliconCurrentDensityMaPerCm2 = 20.0
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeTandemCell(invalidTelemetry));
    }
}
