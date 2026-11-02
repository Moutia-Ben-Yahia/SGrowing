namespace SGrowing.Api.Modules.Quantum.ContinuousVariableClusterTeleportation;

public interface ICvClusterTeleportationEngine
{
    CvClusterTeleportationEvaluation AnalyzeCluster(CvClusterTeleportationTelemetry telemetry);
}

public class CvClusterTeleportationEngine : ICvClusterTeleportationEngine
{
    public CvClusterTeleportationEvaluation AnalyzeCluster(CvClusterTeleportationTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Squeezing in dB = 8.686 * r
        double r = Math.Max(0.01, telemetry.SqueezingParameterR);
        double calculatedSqueezingDb = 8.6858896 * r;

        // Teleportation fidelity F = 1 / (1 + exp(-2*r))
        double varianceMin = Math.Exp(-2.0 * r);
        double rawFidelity = 1.0 / (1.0 + varianceMin);

        // Degradations from detector quantum efficiency and phase jitter
        double eta = telemetry.HomodyneDetectorQuantumEfficiencyPercent / 100.0;
        double phaseJitterRad = telemetry.LocalOscillatorPhaseJitterDegrees * Math.PI / 180.0;
        double detectionPenalty = (1.0 - eta) * 0.35 + (phaseJitterRad * phaseJitterRad) * 0.5;

        double fidelityPercent = Math.Clamp((rawFidelity - detectionPenalty) * 100.0, 40.0, 99.5);

        // Cluster entanglement witness: Nullifier variance / classical limit (0.5)
        double nullifierWitness = telemetry.ClusterGraphNullifierVariance / 0.5;

        bool isSqueezingSufficient = r >= 1.0 && calculatedSqueezingDb >= 8.5 && fidelityPercent >= 82.0;
        bool isHomodyneOptimal = telemetry.HomodyneDetectorQuantumEfficiencyPercent >= 90.0 &&
                                  telemetry.LocalOscillatorPhaseJitterDegrees <= 2.0;
        bool isNullifierBound = telemetry.ClusterGraphNullifierVariance <= 0.18;

        if (!isSqueezingSufficient)
        {
            regimes.Add("SQUEEZING_DEFICIT_ENTANGLEMENT_DEGRADATION");
            mitigations.Add("Increase optical parametric oscillator (OPO) cavity escape efficiency and suppress pump phase noise to elevate squeezing r >= 1.15.");
        }

        if (!isHomodyneOptimal)
        {
            regimes.Add("HOMODYNE_DETECTOR_OPTICAL_LOSS_CONTAMINATION");
            mitigations.Add("Upgrade balanced homodyne photodiodes with anti-reflective coated InGaAs sensors (efficiency >= 93%) and tighten local oscillator optical PLL.");
        }

        if (!isNullifierBound)
        {
            regimes.Add("NULLIFIER_VARIANCE_CLUSTER_UNBOUNDED");
            mitigations.Add("Re-align asymmetric fiber beam splitters and Mach-Zehnder interferometers to restore multi-mode cluster graph node nullifiers.");
        }

        bool isMbqcReady = isSqueezingSufficient && isHomodyneOptimal && isNullifierBound && fidelityPercent >= 85.0;

        return new CvClusterTeleportationEvaluation
        {
            ClusterNodeId = telemetry.ClusterNodeId,
            CalculatedTeleportationFidelityPercent = Math.Round(fidelityPercent, 2),
            CalculatedSqueezingDecibels = Math.Round(calculatedSqueezingDb, 2),
            ClusterEntanglementWitnessMetric = Math.Round(nullifierWitness, 3),
            IsSqueezingSufficientForTeleportation = isSqueezingSufficient,
            IsHomodyneDetectionOptimal = isHomodyneOptimal,
            IsClusterNullifierVarianceBound = isNullifierBound,
            IsUniversalMbqcReady = isMbqcReady,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
