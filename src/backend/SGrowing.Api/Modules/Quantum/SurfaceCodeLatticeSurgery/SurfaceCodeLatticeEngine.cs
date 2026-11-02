namespace SGrowing.Api.Modules.Quantum.SurfaceCodeLatticeSurgery;

public interface ISurfaceCodeLatticeEngine
{
    SurfaceCodeLatticeEvaluation AnalyzeLattice(SurfaceCodeLatticeTelemetry telemetry);
}

public class SurfaceCodeLatticeEngine : ISurfaceCodeLatticeEngine
{
    private const double FaultToleranceThreshold = 0.0010; // 0.1% threshold p_th

    public SurfaceCodeLatticeEvaluation AnalyzeLattice(SurfaceCodeLatticeTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Suppression factor Lambda = p_th / p_phys
        double pPhys = Math.Max(1e-6, telemetry.PhysicalQubitErrorRate);
        double lambda = FaultToleranceThreshold / pPhys;

        // Decoder latency headroom: 2.5 us - latency
        double decoderHeadroom = Math.Max(0.0, 2.5 - telemetry.DecoderLatencyMicroseconds);

        bool isPhysicalBelowThreshold = telemetry.PhysicalQubitErrorRate <= FaultToleranceThreshold;
        bool isLatencyRealTime = telemetry.DecoderLatencyMicroseconds <= 1.2;
        bool isDefectTolerated = telemetry.DefectChainWeightSpanRatio < 0.50;

        if (!isDefectTolerated || telemetry.LogicalErrorRatePerRound > 1e-6)
        {
            regimes.Add("SURFACE_CODE_DEFECT_STRING_LEAKAGE");
            mitigations.Add("Execute adaptive weight re-scaling in Minimum Weight Perfect Matching (MWPM) syndrome graph to penalize correlated space-time error paths.");
        }

        if (!isLatencyRealTime)
        {
            regimes.Add("SYNDROME_DECODER_LATENCY_BACKLOG");
            mitigations.Add("Offload syndrome matching to pipelined Union-Find FPGA acceleration cores to bound decoding latency below 1.0 us per round.");
        }

        if (!telemetry.IsBoundaryCommutationVerified || telemetry.LatticeSurgeryMergeFidelityPercent < 98.5)
        {
            regimes.Add("LATTICE_SURGERY_BOUNDARY_MERGE_DISLOCATION");
            mitigations.Add("Re-align gauge operator measurement schedule across boundary ancilla qubits to preserve mutual stabilizer commutation.");
        }

        bool isFaultTolerant = isPhysicalBelowThreshold && isLatencyRealTime && isDefectTolerated &&
                               telemetry.IsBoundaryCommutationVerified &&
                               telemetry.LatticeSurgeryMergeFidelityPercent >= 99.0;

        return new SurfaceCodeLatticeEvaluation
        {
            LogicalQubitPatchUid = telemetry.LogicalQubitPatchUid,
            LogicalSuppressionFactorLambda = Math.Round(lambda, 2),
            DecoderHeadroomMicroseconds = Math.Round(decoderHeadroom, 2),
            IsPhysicalErrorBelowThreshold = isPhysicalBelowThreshold,
            IsDecodingLatencyRealTimeBounded = isLatencyRealTime,
            IsDefectChainTolerated = isDefectTolerated,
            IsLatticeSurgeryFaultTolerant = isFaultTolerant,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
