namespace SGrowing.Tests.Manufacturing;

using FluentAssertions;
using SGrowing.Api.Modules.Manufacturing.EuvLithographyPellicle;
using Xunit;

public class EuvLithographyPellicleTests
{
    private readonly EuvLithographyPellicleEngine _engine = new();

    [Fact]
    public void EvaluatePellicleLifecycle_NominalFreshPellicle_ReportsNormalUrgency()
    {
        var scanner = new EuvScannerCondition(
            ScannerId: "ASML-NXE3600D-01",
            ReticleId: "MASK-N2-GPU-LOGIC",
            SourcePowerWatts: 350.0,
            ExposureDoseMjPerCm2: 45.0,
            ScanningSpeedMmPerSec: 300.0,
            ChamberHydrogenPressurePa: 3.5
        );

        var pellicle = new PellicleMembraneState(
            PellicleSerialNumber: "PEL-CNT-9912",
            MaterialType: "CarbonNanotube_CNT",
            MembraneThicknessNm: 22.0,
            CurrentTransmittancePercent: 91.5,
            TotalWafersExposed: 500,
            CumulativeThermalCycles: 15000.0
        );

        var request = new PellicleEvaluationRequest(scanner, pellicle);
        var assessment = _engine.EvaluatePellicleLifecycle(request);

        assessment.PellicleSerialNumber.Should().Be("PEL-CNT-9912");
        assessment.ReplacementRequired.Should().BeFalse();
        assessment.ReplacementUrgency.Should().Be("NORMAL");
        assessment.ProjectedRemainingWafers.Should().BeGreaterThan(5000);
        assessment.MaintenanceRecommendation.Should().Contain("NOMINAL_OPERATION");
    }

    [Fact]
    public void EvaluatePellicleLifecycle_SevereThermalStressOverLimit_RequiresImmediateSwap()
    {
        // 500W source power with degraded SiNx (transmittance 85.5% -> absorbing 14.5% power)
        var scanner = new EuvScannerCondition(
            ScannerId: "ASML-EXE5000-HIGH-NA",
            ReticleId: "MASK-A16-PROCESSOR",
            SourcePowerWatts: 500.0,
            ExposureDoseMjPerCm2: 60.0,
            ScanningSpeedMmPerSec: 350.0,
            ChamberHydrogenPressurePa: 5.0
        );

        var pellicle = new PellicleMembraneState(
            PellicleSerialNumber: "PEL-SIN-0044",
            MaterialType: "Polysilicon_SiNx",
            MembraneThicknessNm: 9.0, // Thinned
            CurrentTransmittancePercent: 85.5,
            TotalWafersExposed: 28000,
            CumulativeThermalCycles: 95000.0
        );

        var request = new PellicleEvaluationRequest(scanner, pellicle);
        var assessment = _engine.EvaluatePellicleLifecycle(request);

        assessment.ReplacementRequired.Should().BeTrue();
        assessment.ReplacementUrgency.Should().Be("IMMEDIATE_SWAP_CRITICAL");
        assessment.MaintenanceRecommendation.Should().Contain("CRITICAL_PELLICLE_RUPTURE_RISK");
    }

    [Fact]
    public void EvaluatePellicleLifecycle_LowThicknessRemaining_RecommendsScheduledMaintenance()
    {
        var scanner = new EuvScannerCondition("ASML-3400C", "MASK-SRAM-02", 300.0, 40.0, 250.0, 3.0);
        var pellicle = new PellicleMembraneState("PEL-MOSI-019", "MolybdenumSilicide_MoSi", MembraneThicknessNm: 8.3, CurrentTransmittancePercent: 85.1, 15000, 45000.0);

        var request = new PellicleEvaluationRequest(scanner, pellicle);
        var assessment = _engine.EvaluatePellicleLifecycle(request);

        assessment.ReplacementRequired.Should().BeTrue();
        assessment.ProjectedRemainingWafers.Should().BeLessThanOrEqualTo(500);
    }

    [Fact]
    public void EvaluatePellicleLifecycle_CntMaterial_HasLowerPeakTempDueToHigherEmissivity()
    {
        var scanner = new EuvScannerCondition("ASML-STD", "MASK-TEST", 400.0, 45.0, 300.0, 3.0);
        
        var cntPellicle = new PellicleMembraneState("P-CNT", "CarbonNanotube_CNT", 18.0, 90.0, 1000, 5000.0);
        var sinxPellicle = new PellicleMembraneState("P-SIN", "Polysilicon_SiNx", 18.0, 90.0, 1000, 5000.0);

        var cntAssessment = _engine.EvaluatePellicleLifecycle(new PellicleEvaluationRequest(scanner, cntPellicle));
        var sinxAssessment = _engine.EvaluatePellicleLifecycle(new PellicleEvaluationRequest(scanner, sinxPellicle));

        cntAssessment.PeakMembraneTempCelsius.Should().BeLessThan(sinxAssessment.PeakMembraneTempCelsius);
    }
}
