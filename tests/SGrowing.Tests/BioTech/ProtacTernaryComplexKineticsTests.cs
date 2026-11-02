using System;
using SGrowing.Api.Modules.BioTech.ProtacTernaryComplexKinetics;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class ProtacTernaryComplexKineticsTests
{
    private readonly ProtacTernaryComplexKineticsEngine _engine = new();

    [Fact]
    public void AnalyzeProtacKinetics_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeProtacKinetics(null!));
    }

    [Fact]
    public void AnalyzeProtacKinetics_EmptyCandidateId_ThrowsArgumentException()
    {
        var telemetry = new ProtacTernaryComplexTelemetry(
            ProtacCandidateId: "",
            TargetProtein: "BRD4",
            E3Ligase: "VHL",
            BinaryKdPoiNanomolar: 100.0,
            BinaryKdE3Nanomolar: 50.0,
            TernaryKdPoiNanomolar: 20.0,
            MeasuredDc50Nanomolar: 15.0,
            MaximalDegradationPercent: 92.0,
            CellularHookConcentrationMicromolar: 2.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeProtacKinetics(telemetry));
    }

    [Fact]
    public void AnalyzeProtacKinetics_OptimalPositiveCooperativity_ReturnsOptimalRegime()
    {
        // Binary Kd = 150 nM, Ternary Kd = 15 nM -> alpha = 10.0 >= 5.0
        // DC50 = 12.0 nM <= 25.0, Dmax = 95.0% >= 90.0%
        var telemetry = new ProtacTernaryComplexTelemetry(
            ProtacCandidateId: "ARV-110-ANALOG",
            TargetProtein: "AR-LBD",
            E3Ligase: "VHL",
            BinaryKdPoiNanomolar: 150.0,
            BinaryKdE3Nanomolar: 60.0,
            TernaryKdPoiNanomolar: 15.0,
            MeasuredDc50Nanomolar: 12.0,
            MaximalDegradationPercent: 95.0,
            CellularHookConcentrationMicromolar: 5.0
        );

        var result = _engine.AnalyzeProtacKinetics(telemetry);

        Assert.NotNull(result);
        Assert.Equal("ARV-110-ANALOG", result.ProtacCandidateId);
        Assert.True(result.CooperativityFactorAlpha >= 5.0);
        Assert.True(result.IsProtacLeadViable);
        Assert.Equal("OPTIMAL_POSITIVE_COOPERATIVITY_HIGH_DEGRADATION_REGIME", result.ProtacEfficacyRegime);
        Assert.Contains("Prime preclinical candidate", result.MedicinalChemistryGuidance);
    }

    [Fact]
    public void AnalyzeProtacKinetics_NegativeCooperativity_ReturnsStericClashHazard()
    {
        // Binary Kd = 50 nM, Ternary Kd = 500 nM -> alpha = 0.10 < 0.2
        var telemetry = new ProtacTernaryComplexTelemetry(
            ProtacCandidateId: "PROTAC-SHORT-LINKER-02",
            TargetProtein: "KRAS-G12C",
            E3Ligase: "CRBN",
            BinaryKdPoiNanomolar: 50.0,
            BinaryKdE3Nanomolar: 100.0,
            TernaryKdPoiNanomolar: 500.0,
            MeasuredDc50Nanomolar: 150.0,
            MaximalDegradationPercent: 60.0,
            CellularHookConcentrationMicromolar: 1.0
        );

        var result = _engine.AnalyzeProtacKinetics(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SEVERE_NEGATIVE_COOPERATIVITY_STERIC_CLASH_HAZARD", result.ProtacEfficacyRegime);
        Assert.False(result.IsProtacLeadViable);
        Assert.Contains("modify linker length", result.MedicinalChemistryGuidance);
    }

    [Fact]
    public void AnalyzeProtacKinetics_LowDegradation_ReturnsInsufficientDegradation()
    {
        var telemetry = new ProtacTernaryComplexTelemetry(
            ProtacCandidateId: "PROTAC-INERT-03",
            TargetProtein: "MYC",
            E3Ligase: "VHL",
            BinaryKdPoiNanomolar: 100.0,
            BinaryKdE3Nanomolar: 80.0,
            TernaryKdPoiNanomolar: 80.0,
            MeasuredDc50Nanomolar: 850.0, // > 500 nM!
            MaximalDegradationPercent: 35.0, // < 50%!
            CellularHookConcentrationMicromolar: 0.5
        );

        var result = _engine.AnalyzeProtacKinetics(telemetry);

        Assert.NotNull(result);
        Assert.Equal("INSUFFICIENT_PROTEASOMAL_DEGRADATION_EFFICACY", result.ProtacEfficacyRegime);
        Assert.False(result.IsProtacLeadViable);
    }
}
