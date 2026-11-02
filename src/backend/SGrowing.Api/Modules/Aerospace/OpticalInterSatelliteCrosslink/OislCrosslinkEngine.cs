namespace SGrowing.Api.Modules.Aerospace.OpticalInterSatelliteCrosslink;

public interface IOislCrosslinkEngine
{
    OislCrosslinkEvaluation AnalyzeCrosslink(OislCrosslinkTelemetry telemetry);
}

public class OislCrosslinkEngine : IOislCrosslinkEngine
{
    public OislCrosslinkEvaluation AnalyzeCrosslink(OislCrosslinkTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // 3-sigma pointing jitter
        double threeSigmaJitter = telemetry.FastSteeringMirrorJitterMicroRadians * 3.0;

        // BER log margin: -log10(BER)
        double ber = Math.Max(1e-15, telemetry.CoherentBitErrorRate);
        double berLogMargin = -Math.Log10(ber);

        bool isMarginAdequate = telemetry.OpticalLinkMarginDecibels >= 4.0;
        bool isJitterTolerated = telemetry.FastSteeringMirrorJitterMicroRadians <= 1.0;
        bool isDopplerLocked = telemetry.DopplerFrequencySlewRateMegaHertzPerSecond <= 15.0;

        if (telemetry.FastSteeringMirrorJitterMicroRadians > 1.5 || ber > 1e-4)
        {
            regimes.Add("OISL_POINTING_JITTER_BURST_FADE");
            mitigations.Add("Increase piezoelectric fast steering mirror (FSM) closed-loop bandwidth to 2.5 kHz to reject reaction wheel micro-vibrations.");
        }

        if (telemetry.BeaconAcquisitionLockTimeSeconds > 20.0)
        {
            regimes.Add("BEACON_ACQUISITION_SPIRAL_SEARCH_TIMEOUT");
            mitigations.Add("Update GPS/GNSS orbital state vectors and compress beacon spiral cone angle from 500 urad to 250 urad.");
        }

        if (!isDopplerLocked)
        {
            regimes.Add("DOPPLER_CARRIER_FREQUENCY_SLEW_SLIP");
            mitigations.Add("Engage feedforward digital signal processing carrier recovery loop with orbit ephemeris line-of-sight acceleration compensation.");
        }

        bool isPatGo = isMarginAdequate && isJitterTolerated && isDopplerLocked &&
                       telemetry.BeaconAcquisitionLockTimeSeconds <= 15.0 &&
                       telemetry.CoherentBitErrorRate <= 1e-6;

        return new OislCrosslinkEvaluation
        {
            OpticalTerminalPairId = telemetry.OpticalTerminalPairId,
            ThreeSigmaPointingJitterMicroRadians = Math.Round(threeSigmaJitter, 2),
            BitErrorRateLogMargin = Math.Round(berLogMargin, 2),
            IsLinkMarginAdequate = isMarginAdequate,
            IsPointingJitterTolerated = isJitterTolerated,
            IsDopplerTrackingLocked = isDopplerLocked,
            IsCrosslinkPatGoForTransmission = isPatGo,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
