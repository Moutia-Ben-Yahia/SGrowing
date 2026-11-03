namespace SGrowing.Api.Modules.Manufacturing.HighNaEuvStochasticDefects;

public class HighNaEuvStochasticDefectsEngine
{
    private const double EuvWavelengthNm = 13.5;
    private const double K1Factor = 0.35;
    private const double K2Factor = 0.50;

    public HighNaEuvAnalysisResult AnalyzeWaferField(HighNaEuvTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.ScannerId))
        {
            throw new ArgumentException("Scanner ID cannot be null or empty.", nameof(telemetry));
        }

        if (telemetry.NumericalAperture <= 0.33)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "High-NA EUV requires NA > 0.33 (nominal 0.55).");
        }

        if (telemetry.MeasuredDoseMilliJoulesPerCm2 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Dose must be positive.");
        }

        if (telemetry.TargetCriticalDimensionNm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Target critical dimension must be positive.");
        }

        // Theoretical Rayleigh minimum CD and Depth of Focus
        double minCdNm = K1Factor * (EuvWavelengthNm / telemetry.NumericalAperture);
        double depthOfFocusNm = K2Factor * (EuvWavelengthNm / Math.Pow(telemetry.NumericalAperture, 2));

        // Stochastic defect density calculation (micro-bridges vs pinholes)
        // DefectDensity = 10 * exp(-(Dose - 35) / 4) + LER * 0.25 + Flare * 20
        double doseExponent = Math.Clamp((telemetry.MeasuredDoseMilliJoulesPerCm2 - 35.0) / 4.0, -10.0, 10.0);
        double shotNoiseComponent = 10.0 * Math.Exp(-doseExponent);
        double lerComponent = telemetry.MeasuredLineEdgeRoughness3SigmaNm * 0.25;
        double flareComponent = telemetry.EuvFlareFraction * 20.0;
        double predictedDefectDensity = Math.Max(0.01, shotNoiseComponent + lerComponent + flareComponent);

        bool isMicroBridgeRisk = telemetry.MeasuredDoseMilliJoulesPerCm2 < 38.0 || telemetry.MeasuredLineEdgeRoughness3SigmaNm >= 2.8 || predictedDefectDensity >= 8.0;
        bool isPinholeFlareRisk = telemetry.EuvFlareFraction >= 0.075 || telemetry.MeasuredDoseMilliJoulesPerCm2 > 75.0;

        string status;
        string correction;
        string advisory;

        if (isMicroBridgeRisk)
        {
            status = "STOCHASTIC_MICRO_BRIDGE_WARNING";
            correction = "BOOST_EUV_SOURCE_POWER_12_PERCENT";
            advisory = $"STOCHASTIC DEFECT RISK: Low dose ({telemetry.MeasuredDoseMilliJoulesPerCm2:F1} mJ/cm²) and high LER ({telemetry.MeasuredLineEdgeRoughness3SigmaNm:F2} nm) produce {predictedDefectDensity:F2} defects/cm². Boost source power by 12% to prevent micro-bridging.";
        }
        else if (isPinholeFlareRisk)
        {
            status = "CRITICAL_NANO_PINHOLE_FLARE_TRIP";
            correction = "APPLY_ANAMORPHIC_SLIT_FLARE_MASK_ATTENUATION";
            advisory = $"EUV FLARE TRIP: Optical flare ({telemetry.EuvFlareFraction:P1}) exceeds 7.5% specification. Contrast washed out (NILS={telemetry.AerialImageContrastNils:F2}). Applying anamorphic slit mask attenuation.";
        }
        else
        {
            status = "NOMINAL_SUB_2NM_YIELD";
            correction = "HOLD_CURRENT_ILLUMINATION_PUPIL";
            advisory = $"Nominal High-NA EUV lithography (NA={telemetry.NumericalAperture:F2}, CDmin={minCdNm:F2} nm, DoF={depthOfFocusNm:F1} nm). Stochastic defect density {predictedDefectDensity:F3}/cm² within sub-2nm yield budget.";
        }

        bool isYieldBreached = isMicroBridgeRisk || isPinholeFlareRisk;

        return new HighNaEuvAnalysisResult(
            telemetry.ScannerId,
            telemetry.WaferFieldId,
            Math.Round(minCdNm, 2),
            Math.Round(depthOfFocusNm, 1),
            Math.Round(predictedDefectDensity, 3),
            isYieldBreached,
            status,
            correction,
            advisory);
    }
}
