namespace SGrowing.Api.Modules.Energy.FusionDivertorDetachment;

public interface IFusionDivertorDetachmentEngine
{
    DivertorDetachmentDecision EvaluateDetachment(DivertorDetachmentRequest request);
}

public class FusionDivertorDetachmentEngine : IFusionDivertorDetachmentEngine
{
    public DivertorDetachmentDecision EvaluateDetachment(DivertorDetachmentRequest request)
    {
        var tel = request.Telemetry;
        var lim = request.Limits;

        // Two-point model: scaling expectation for ion saturation current roll-over
        // j_sat,scaling ~ k * (n_upstream)^2
        double scaledJsAtExpectation = 1.2e-38 * Math.Pow(tel.UpstreamSolDensityM3, 2.0);
        double measuredJsAt = Math.Max(1.0, tel.IonSaturationCurrentAmpsPerCm2);
        double degreeOfDetachment = scaledJsAtExpectation / measuredJsAt;
        degreeOfDetachment = Math.Clamp(degreeOfDetachment, 0.5, 25.0);

        bool temperatureDetached = tel.TargetElectronTemperatureElectronVolts <= lim.MaxAllowableElectronTemperatureEv;
        bool heatFluxSafe = tel.TargetHeatFluxMegaWattsPerM2 <= lim.MaxAllowableHeatFluxMegaWattsPerM2;
        bool detachmentAchieved = temperatureDetached && heatFluxSafe && (degreeOfDetachment >= 2.0);

        bool coreCollapseDanger = tel.CoreRadiatedPowerFraction >= lim.MaxCoreRadiatedFraction;

        double recommendedPuffRate;
        string regime;
        string directive;

        if (coreCollapseDanger)
        {
            regime = "CORE_RADIATIVE_COLLAPSE_WARNING";
            recommendedPuffRate = Math.Max(2.0, tel.CurrentGasPuffFlowRateBarLitersPerSec * 0.55);
            directive = $"THROTTLE_IMPURITY_SEEDING: Core radiated fraction ({tel.CoreRadiatedPowerFraction * 100:F1}%) exceeds {lim.MaxCoreRadiatedFraction * 100:F1}% safety limit. Lower {tel.SeededImpurityGas} gas puff to {recommendedPuffRate:F1} bar-L/s to prevent core plasma thermal quench.";
        }
        else if (detachmentAchieved)
        {
            regime = "STABLE_PARTIAL_DETACHMENT";
            recommendedPuffRate = tel.CurrentGasPuffFlowRateBarLitersPerSec;
            directive = $"DETACHMENT_MAINTAINED: Tungsten monoblock heat flux ({tel.TargetHeatFluxMegaWattsPerM2:F1} MW/m2) and Te ({tel.TargetElectronTemperatureElectronVolts:F1} eV) optimal. Maintain {tel.SeededImpurityGas} puff rate at {recommendedPuffRate:F1} bar-L/s.";
        }
        else
        {
            regime = "ATTACHED_HOT_SHEATH_HAZARD";
            recommendedPuffRate = tel.CurrentGasPuffFlowRateBarLitersPerSec * 1.45;
            directive = $"BOOST_DIVERTOR_COOLING: High target electron temperature ({tel.TargetElectronTemperatureElectronVolts:F1} eV) or heat flux ({tel.TargetHeatFluxMegaWattsPerM2:F1} MW/m2) risks tungsten erosion. Increase {tel.SeededImpurityGas} seeding to {recommendedPuffRate:F1} bar-L/s to induce radiative detachment.";
        }

        return new DivertorDetachmentDecision(
            DivertorId: tel.DivertorId,
            DegreeOfDetachment: Math.Round(degreeOfDetachment, 2),
            DetachmentAchieved: detachmentAchieved && !coreCollapseDanger,
            CoreRadiativeCollapseRiskDetected: coreCollapseDanger,
            RecommendedPuffFlowRateBarLitersPerSec: Math.Round(recommendedPuffRate, 1),
            PlasmaExhaustRegime: regime,
            ActuatorDirective: directive
        );
    }
}
