using SGrowing.Api.Modules.Quantum.ContinuousVariableClusterTeleportation;
using Xunit;

namespace SGrowing.Tests.Quantum;

public class CvClusterTeleportationTests
{
    private readonly CvClusterTeleportationEngine _engine = new();

    [Fact]
    public void AnalyzeCluster_NominalTeleportation_ReturnsUniversalMbqcReady()
    {
        var telemetry = new CvClusterTeleportationTelemetry
        {
            ClusterNodeId = "CV-NODE-1550-001",
            OpticalFrequencyBand = "Telecom_1550nm",
            SqueezingParameterR = 1.30,
            SqueezingDecibels = 11.2,
            HomodyneDetectorQuantumEfficiencyPercent = 95.0,
            OpticalFiberPropagationLossDbPerKm = 0.18,
            ClusterGraphNullifierVariance = 0.075,
            CoincidentPhotonPumpPowerMilliWatts = 40.0,
            LocalOscillatorPhaseJitterDegrees = 0.8
        };

        var result = _engine.AnalyzeCluster(telemetry);

        Assert.Equal("CV-NODE-1550-001", result.ClusterNodeId);
        Assert.True(result.IsUniversalMbqcReady);
        Assert.True(result.IsSqueezingSufficientForTeleportation);
        Assert.True(result.IsHomodyneDetectionOptimal);
        Assert.True(result.IsClusterNullifierVarianceBound);
        Assert.True(result.CalculatedTeleportationFidelityPercent >= 85.0);
        Assert.True(result.CalculatedSqueezingDecibels >= 10.0);
        Assert.True(result.ClusterEntanglementWitnessMetric < 0.5);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCluster_LowSqueezing_TriggersSqueezingDeficit()
    {
        var telemetry = new CvClusterTeleportationTelemetry
        {
            ClusterNodeId = "CV-LOW-SQUEEZE",
            SqueezingParameterR = 0.55 // Below 1.0 threshold
        };

        var result = _engine.AnalyzeCluster(telemetry);

        Assert.False(result.IsSqueezingSufficientForTeleportation);
        Assert.False(result.IsUniversalMbqcReady);
        Assert.Contains("SQUEEZING_DEFICIT_ENTANGLEMENT_DEGRADATION", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeCluster_DegradedHomodyneDetector_TriggersLossAlert()
    {
        var telemetry = new CvClusterTeleportationTelemetry
        {
            ClusterNodeId = "CV-DETECTOR-LOSS",
            HomodyneDetectorQuantumEfficiencyPercent = 82.0 // Below 90%
        };

        var result = _engine.AnalyzeCluster(telemetry);

        Assert.False(result.IsHomodyneDetectionOptimal);
        Assert.Contains("HOMODYNE_DETECTOR_OPTICAL_LOSS_CONTAMINATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCluster_UnboundedNullifier_TriggersNullifierAlert()
    {
        var telemetry = new CvClusterTeleportationTelemetry
        {
            ClusterNodeId = "CV-UNBOUNDED-NULLIFIER",
            ClusterGraphNullifierVariance = 0.35 // Exceeds 0.18
        };

        var result = _engine.AnalyzeCluster(telemetry);

        Assert.False(result.IsClusterNullifierVarianceBound);
        Assert.Contains("NULLIFIER_VARIANCE_CLUSTER_UNBOUNDED", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCluster_TeleportationFidelity_IncreasesWithSqueezing()
    {
        var lowSqueeze = new CvClusterTeleportationTelemetry
        {
            ClusterNodeId = "LOW-R",
            SqueezingParameterR = 0.75
        };

        var highSqueeze = new CvClusterTeleportationTelemetry
        {
            ClusterNodeId = "HIGH-R",
            SqueezingParameterR = 1.45
        };

        var lowRes = _engine.AnalyzeCluster(lowSqueeze);
        var highRes = _engine.AnalyzeCluster(highSqueeze);

        Assert.True(highRes.CalculatedTeleportationFidelityPercent > lowRes.CalculatedTeleportationFidelityPercent);
    }
}
