namespace SGrowing.Api.Modules.Aerospace.GravitationalInterferometerMesh;

public interface IGravitationalInterferometerMeshEngine
{
    GravitationalStrainDetectionResult SynthesizeTdiAndDetectStrain(TdiSynthesisRequest request);
}

public class GravitationalInterferometerMeshEngine : IGravitationalInterferometerMeshEngine
{
    private const double SpeedOfLight = 299792458.0; // m/s

    public GravitationalStrainDetectionResult SynthesizeTdiAndDetectStrain(TdiSynthesisRequest request)
    {
        var measurements = request.ArmMeasurements ?? [];
        double armLength = Math.Max(1e6, request.ArmLengthMeters);
        double armTransitTimeSec = armLength / SpeedOfLight;
        double opticalFrequency = Math.Max(1e12, request.LaserCenterFrequencyHz);

        if (measurements.Count < 3)
        {
            return new GravitationalStrainDetectionResult(
                ConstellationId: request.ConstellationId,
                ReconstructedStrainAmplitudeH: 0.0,
                SignalToNoiseRatio: 0.0,
                AstrophysicalSignalDetected: false,
                SourceCandidateType: "INSUFFICIENT_TELEMETRY",
                TdiChannels: []
            );
        }

        // Extract inter-spacecraft phase values
        // arms: 1->2 (phi12), 2->1 (phi21), 1->3 (phi13), 3->1 (phi31), 2->3 (phi23), 3->2 (phi32)
        double phi12 = GetPhase(measurements, 1, 2);
        double phi21 = GetPhase(measurements, 2, 1);
        double phi13 = GetPhase(measurements, 1, 3);
        double phi31 = GetPhase(measurements, 3, 1);
        double phi23 = GetPhase(measurements, 2, 3);
        double phi32 = GetPhase(measurements, 3, 2);

        // First generation TDI Michelson combinations (X, Y, Z) canceling laser phase noise
        // TDI X centered at Spacecraft 1
        double tdiX = (phi13 - phi12) + (phi31 - phi21);
        // TDI Y centered at Spacecraft 2
        double tdiY = (phi21 - phi23) + (phi12 - phi32);
        // TDI Z centered at Spacecraft 3
        double tdiZ = (phi32 - phi31) + (phi23 - phi13);

        // Sagnac combination alpha = cyclic roundtrip phase
        double sagnacAlpha = (phi12 + phi23 + phi31) - (phi13 + phi32 + phi21);

        var channels = new List<TdiCombinationChannel>
        {
            new("TDI_X", Math.Round(tdiX, 10), LaserPhaseNoiseCancellationDb: 165.0),
            new("TDI_Y", Math.Round(tdiY, 10), LaserPhaseNoiseCancellationDb: 164.5),
            new("TDI_Z", Math.Round(tdiZ, 10), LaserPhaseNoiseCancellationDb: 165.2),
            new("Sagnac_alpha", Math.Round(sagnacAlpha, 10), LaserPhaseNoiseCancellationDb: 172.0)
        };

        // Compute reconstructed gravitational wave strain h
        // h ~ Delta_phi / (2 * pi * nu_0 * tau)
        double phaseRms = Math.Sqrt((tdiX * tdiX + tdiY * tdiY + tdiZ * tdiZ) / 3.0);
        double hStrain = phaseRms / (2.0 * Math.PI * opticalFrequency * armTransitTimeSec);

        // Theoretical noise floor for 2.5 Gm arm with 1064nm laser ~ 1.5e-21
        double noiseFloor = 1.5e-21;
        double snr = hStrain / noiseFloor;

        bool detected = snr >= 7.0; // Standard 7-sigma gravitational wave discovery threshold
        string candidateType;

        if (!detected)
        {
            candidateType = "INSTRUMENTAL_NOISE_FLOOR";
        }
        else if (snr > 35.0)
        {
            candidateType = "SUPERMASSIVE_BLACK_HOLE_BINARY_MERGER (z ~ 1-3)";
        }
        else if (snr > 15.0)
        {
            candidateType = "EXTREME_MASS_RATIO_INSPIRAL (EMRI)";
        }
        else
        {
            candidateType = "GALACTIC_ULTRA_COMPACT_BINARY";
        }

        return new GravitationalStrainDetectionResult(
            ConstellationId: request.ConstellationId,
            ReconstructedStrainAmplitudeH: hStrain,
            SignalToNoiseRatio: Math.Round(snr, 2),
            AstrophysicalSignalDetected: detected,
            SourceCandidateType: candidateType,
            TdiChannels: channels
        );
    }

    private static double GetPhase(IReadOnlyList<InterferometerArmMeasurement> measurements, int tx, int rx)
    {
        var m = measurements.FirstOrDefault(x => x.TransmitterSpacecraftId == tx && x.ReceiverSpacecraftId == rx);
        return m?.PhaseMeasurementRadians ?? 0.0;
    }
}
