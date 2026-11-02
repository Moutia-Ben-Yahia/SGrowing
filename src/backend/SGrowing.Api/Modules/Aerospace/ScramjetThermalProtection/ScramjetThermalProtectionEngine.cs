namespace SGrowing.Api.Modules.Aerospace.ScramjetThermalProtection;

public interface IScramjetThermalProtectionEngine
{
    ScramjetThermalProtectionPlan EvaluateThermalProtection(ScramjetThermalEvaluationRequest request);
}

public class ScramjetThermalProtectionEngine : IScramjetThermalProtectionEngine
{
    public ScramjetThermalProtectionPlan EvaluateThermalProtection(ScramjetThermalEvaluationRequest request)
    {
        var flight = request.FlightCondition;
        var mach = Math.Max(1.0, flight.MachNumber);
        
        // Atmospheric freestream temperature approximation
        double tInf = flight.AltitudeMeters switch
        {
            <= 11000.0 => 288.15 - (0.0065 * flight.AltitudeMeters),
            <= 25000.0 => 216.65,
            _ => 216.65 + (0.001 * (flight.AltitudeMeters - 25000.0))
        };

        // Stagnation temperature: T0 = Tinf * (1 + 0.2 * M^2) for gamma = 1.4
        double stagnationTempK = tInf * (1.0 + 0.2 * mach * mach);

        // Shock interaction factor increases sharply with angle of attack and Mach number
        double shockFactor = 1.0 + (mach * 0.2) + (Math.Abs(flight.AngleOfAttackDegrees) * 0.15);

        var adjustments = new List<ChannelCoolantAdjustment>();
        double totalDemand = 0.0;
        double maxProjectedTemp = 0.0;

        foreach (var ch in request.Channels)
        {
            // Location sensitivity multiplier
            double locationFactor = ch.Location switch
            {
                "LeadingEdgeNose" => 1.45,
                "CowlLip" => 1.65,
                "CombustorIsolator" => 1.25,
                "CombustorFlameholder" => 1.35,
                "NozzleExpansionRamp" => 1.10,
                _ => 1.0
            };

            // Raw uncooled adiabatic wall temperature with turbulent recovery and shock-shock interaction
            double adiabaticWallTemp = stagnationTempK * (0.86 + 0.08 * locationFactor) * (1.0 + 0.04 * shockFactor);

            // Cooling capability: each kg/s of regenerative coolant suppresses ~450 K per channel segment
            double currentSuppression = ch.CoolantMassFlowKgPerSec * 450.0;
            double baselineWallTemp = Math.Max(tInf, adiabaticWallTemp - currentSuppression);

            // Required cooling to stay comfortably below max allowable temperature with 150K margin
            double targetWallTemp = Math.Min(ch.MaxAllowableTempKelvin - 150.0, 1800.0);
            double requiredSuppression = Math.Max(0.0, adiabaticWallTemp - targetWallTemp);
            double requiredFlow = Math.Max(0.05, requiredSuppression / 450.0);

            // Projected wall temperature with allocated/required flow
            double projectedWall = Math.Max(300.0, adiabaticWallTemp - (requiredFlow * 450.0));
            double margin = ch.MaxAllowableTempKelvin - projectedWall;

            totalDemand += requiredFlow;
            if (projectedWall > maxProjectedTemp)
            {
                maxProjectedTemp = projectedWall;
            }

            string action = requiredFlow > ch.CoolantMassFlowKgPerSec
                ? $"INCREASE_COOLANT: Increase mass flow from {ch.CoolantMassFlowKgPerSec:F2} to {requiredFlow:F2} kg/s"
                : (requiredFlow < ch.CoolantMassFlowKgPerSec * 0.8
                    ? $"OPTIMIZE_FLOW: Trim mass flow to {requiredFlow:F2} kg/s to conserve fuel coolant"
                    : "NOMINAL: Flow rate adequate");

            adjustments.Add(new ChannelCoolantAdjustment(
                SegmentId: ch.SegmentId,
                Location: ch.Location,
                RequiredCoolantFlowKgPerSec: Math.Round(requiredFlow, 2),
                ProjectedWallTempKelvin: Math.Round(projectedWall, 1),
                SafetyMarginKelvin: Math.Round(margin, 1),
                FlowAction: action
            ));
        }

        bool runawayRisk = totalDemand > request.AvailableTotalCoolantFlowKgPerSec || maxProjectedTemp > 2400.0;
        string overallStatus = runawayRisk
            ? "CRITICAL_AEROTHERMAL_OVERLOAD: Total coolant demand exceeds available flow capacity or maximum ceramic wall limits exceeded."
            : (totalDemand > request.AvailableTotalCoolantFlowKgPerSec * 0.85
                ? "WARNING_HIGH_THERMAL_LOAD: Approaching maximum regenerative fuel cooling capacity."
                : "AEROTHERMAL_MARGIN_OPTIMAL: Active cooling within design envelopes.");

        return new ScramjetThermalProtectionPlan(
            VehicleId: flight.VehicleId,
            MachNumber: Math.Round(mach, 2),
            StagnationTempKelvin: Math.Round(stagnationTempK, 1),
            MaxProjectedWallTempKelvin: Math.Round(maxProjectedTemp, 1),
            ShockInteractionAmplificationFactor: Math.Round(shockFactor, 2),
            TotalCoolantDemandKgPerSec: Math.Round(totalDemand, 2),
            AvailableCoolantFlowKgPerSec: Math.Round(request.AvailableTotalCoolantFlowKgPerSec, 2),
            ThermalRunawayRisk: runawayRisk,
            OverallStatus: overallStatus,
            Adjustments: adjustments
        );
    }
}
