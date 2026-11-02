namespace SGrowing.Tests.Pharma;

using FluentAssertions;
using SGrowing.Api.Modules.Pharma.HotMeltExtrusionPat;
using Xunit;

public class HotMeltExtrusionPatTests
{
    private readonly HotMeltExtrusionPatEngine _engine = new();

    [Fact]
    public void EvaluateExtrusionStream_AllParametersWithinDesignSpace_ApprovesRealTimeRelease()
    {
        var zones = new List<ExtruderZoneTelemetry>
        {
            new(1, 140.0, 140.0, 45.0, 250.0),
            new(2, 160.0, 160.0, 52.0, 250.0),
            new(3, 175.0, 175.0, 48.0, 250.0)
        };

        var nir = new NearInfraredSpectra("NIR-001", ApiConcentrationWeightPercent: 20.1, TargetApiPercent: 20.0, TolerancePercent: 0.5, CrystallinityIndex: 0.015, DateTimeOffset.UtcNow);
        var request = new HmeBatchMonitoringRequest("BATCH-HME-100", "RITONAVIR-ASD-20", zones, nir, MeltPressureBar: 65.0, DieTemperatureCelsius: 175.0);

        var result = _engine.EvaluateExtrusionStream(request);

        result.BatchId.Should().Be("BATCH-HME-100");
        result.ProcessInStatisticalControl.Should().BeTrue();
        result.DivertStatus.DivertGateActuated.Should().BeFalse();
        result.QualityReleaseVerdict.Should().Contain("GMP In-Spec");
        result.QualityReleaseVerdict.Should().Contain("Real-time release testing (RTRT) approved");
    }

    [Fact]
    public void EvaluateExtrusionStream_ApiConcentrationDeviation_ActuatesDivertGate()
    {
        var zones = new List<ExtruderZoneTelemetry>
        {
            new(1, 150.0, 150.0, 50.0, 200.0)
        };

        // API is 22.5% vs target 20.0% ± 0.5% (OOS!)
        var oosNir = new NearInfraredSpectra("NIR-OOS", 22.5, 20.0, 0.5, 0.02, DateTimeOffset.UtcNow);
        var request = new HmeBatchMonitoringRequest("BATCH-OOS-01", "DRUG-ASD", zones, oosNir, 60.0, 150.0);

        var result = _engine.EvaluateExtrusionStream(request);

        result.ProcessInStatisticalControl.Should().BeFalse();
        result.DivertStatus.DivertGateActuated.Should().BeTrue();
        result.DivertStatus.DivertReason.Should().Contain("API Concentration OOS");
        result.QualityReleaseVerdict.Should().Contain("GMP Out-of-Spec");
    }

    [Fact]
    public void EvaluateExtrusionStream_RecrystallizationSpike_ActuatesDivertGate()
    {
        var zones = new List<ExtruderZoneTelemetry>
        {
            new(1, 150.0, 150.0, 50.0, 200.0)
        };

        // Crystallinity is 8.5% > 5% threshold -> crystalline defect
        var crystalNir = new NearInfraredSpectra("NIR-CRYSTAL", 20.0, 20.0, 0.5, CrystallinityIndex: 0.085, DateTimeOffset.UtcNow);
        var request = new HmeBatchMonitoringRequest("BATCH-CRYSTAL", "DRUG-ASD", zones, crystalNir, 70.0, 150.0);

        var result = _engine.EvaluateExtrusionStream(request);

        result.ProcessInStatisticalControl.Should().BeFalse();
        result.DivertStatus.DivertGateActuated.Should().BeTrue();
        result.DivertStatus.DivertReason.Should().Contain("Recrystallization Risk");
    }

    [Fact]
    public void EvaluateExtrusionStream_AbnormalMeltPressure_ActuatesDivertGate()
    {
        var zones = new List<ExtruderZoneTelemetry>
        {
            new(1, 160.0, 160.0, 45.0, 200.0)
        };

        var nir = new NearInfraredSpectra("NIR-OK", 20.0, 20.0, 0.5, 0.01, DateTimeOffset.UtcNow);
        // Melt pressure 220 bar > 180 bar limit
        var request = new HmeBatchMonitoringRequest("BATCH-PRESSURE", "DRUG-ASD", zones, nir, MeltPressureBar: 220.0, 160.0);

        var result = _engine.EvaluateExtrusionStream(request);

        result.ProcessInStatisticalControl.Should().BeFalse();
        result.DivertStatus.DivertGateActuated.Should().BeTrue();
        result.DivertStatus.DivertReason.Should().Contain("Abnormal Melt Pressure");
    }
}
