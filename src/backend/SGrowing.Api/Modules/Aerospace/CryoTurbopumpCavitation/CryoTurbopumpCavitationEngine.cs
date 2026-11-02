namespace SGrowing.Api.Modules.Aerospace.CryoTurbopumpCavitation;

public interface ICryoTurbopumpCavitationEngine
{
    TurbopumpThrottleDirective EvaluateCavitation(CavitationEvaluationRequest request);
}

public class CryoTurbopumpCavitationEngine : ICryoTurbopumpCavitationEngine
{
    private const double Gravity = 9.80665;

    public TurbopumpThrottleDirective EvaluateCavitation(CavitationEvaluationRequest request)
    {
        var p = request.Propellant;
        var ind = request.Inducer;

        double deltaPBar = Math.Max(0.01, p.InletPressureBar - p.VaporPressureBar);
        double deltaPPascals = deltaPBar * 100000.0;
        double density = Math.Max(50.0, p.FluidDensityKgPerM3);

        // NPSH Available = (P_inlet - P_vapor) / (rho * g) + V^2 / (2 * g)
        double staticHead = deltaPPascals / (density * Gravity);
        double npshAvailable = staticHead + ind.SuctionHeadMeters;

        // Cavitation Number Sigma = (P_inlet - P_vapor) / (0.5 * rho * V_tip^2)
        double dynamicPressure = 0.5 * density * Math.Max(1.0, ind.TipSpeedMps * ind.TipSpeedMps);
        double sigma = deltaPPascals / dynamicPressure;

        // Required NPSH increases with rotational speed: typical inducer critical sigma is ~0.04 to 0.08
        double criticalSigma = 0.055;
        double requiredNpshMeters = Math.Max(5.0, (ind.RotationalSpeedRpm / 10000.0) * 12.0);
        double npshMarginMeters = npshAvailable - requiredNpshMeters;

        bool cavitationDetected = sigma <= criticalSigma || npshMarginMeters <= 2.0;
        bool chuggingRisk = ind.VibrationRmsG >= 8.5 && cavitationDetected;

        string urgency;
        string directive;

        if (chuggingRisk || npshMarginMeters <= -1.0)
        {
            urgency = "EMERGENCY_THROTTLE_DOWN";
            directive = "CRITICAL_CAVITATION_CHUGGING: Throttle engine down by 15% immediately. Boost ullage tank autogenous helium pressurization by +0.8 bar to avert pump destruction.";
        }
        else if (cavitationDetected || npshMarginMeters <= 5.0)
        {
            urgency = "ADAPTIVE_TRIM";
            directive = $"CAVITATION_MARGIN_TIGHT: NPSH margin ({npshMarginMeters:F1}m) below safety threshold. Command +0.3 bar tank pressurization trim.";
        }
        else
        {
            urgency = "NOMINAL";
            directive = $"TURBOPUMP_NOMINAL: Cavitation margin optimal (sigma {sigma:F3} > {criticalSigma}, NPSH margin {npshMarginMeters:F1}m). Full throttle autorun approved.";
        }

        return new TurbopumpThrottleDirective(
            TurbopumpId: ind.TurbopumpId,
            PropellantName: p.PropellantName,
            CavitationNumberSigma: Math.Round(sigma, 4),
            NpshMarginMeters: Math.Round(npshMarginMeters, 2),
            CavitationDetected: cavitationDetected,
            ChuggingResonanceRisk: chuggingRisk,
            ActionUrgency: urgency,
            ThrottleAndPressurizationDirective: directive
        );
    }
}
