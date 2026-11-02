namespace SGrowing.Api.Modules.Aerospace.HypersonicWaveriderShock;

public interface IHypersonicWaveriderShockEngine
{
    WaveriderAerothermalPlan EvaluateAerothermalState(HypersonicWaveriderEvaluationRequest request);
}

public class HypersonicWaveriderShockEngine : IHypersonicWaveriderShockEngine
{
    public WaveriderAerothermalPlan EvaluateAerothermalState(HypersonicWaveriderEvaluationRequest request)
    {
        var st = request.FlightState;
        var env = request.SafetyEnvelope;

        // 1. Shock attachment condition:
        // Supersonic waverider relies on attached planar shock wave beneath leading edge
        bool shockAttached = st.MachNumber >= env.DesignShockAttachmentMach && st.AngleOfAttackDegrees is >= 2.0 and <= 12.0;

        // 2. Lift-to-drag ratio L/D:
        // Attached waverider yields high aerodynamic efficiency (L/D ~ 4.8 - 5.4)
        // Detached shock suffers massive spillage wave drag (L/D drops below 3.0)
        double liftToDrag = shockAttached
            ? Math.Clamp(5.2 - (Math.Abs(st.AngleOfAttackDegrees - 6.0) * 0.15) - ((st.MachNumber - 7.0) * 0.08), 3.8, 5.6)
            : Math.Clamp(2.8 - ((st.AngleOfAttackDegrees - 12.0) * 0.05), 1.8, 3.2);

        // 3. Oblique shock wave angle beta:
        double obliqueShockAngle = Math.Clamp((st.AngleOfAttackDegrees * 1.35) + (18.0 / Math.Max(2.0, st.MachNumber)), 8.0, 45.0);

        // 4. Stagnation aerothermal heat flux approximation:
        double stagnationHeatFlux = 75.0 * Math.Sqrt(Math.Max(5.0, st.DynamicPressureKPa)) * Math.Pow(st.MachNumber / 6.0, 2.2);

        // 5. Thermal safety and transpiration cooling modulation:
        bool thermalWarning = st.LeadingEdgeTemperatureKelvin >= (env.MaxAllowableLeadingEdgeTempKelvin - 150.0);
        bool dynamicPressureExceeded = st.DynamicPressureKPa > env.MaxAllowableDynamicPressureKPa;

        double recommendedCoolingFlowRate;
        string regime;
        string directive;

        if (thermalWarning)
        {
            regime = "AEROTHERMAL_BOUNDARY_EXCURSION";
            recommendedCoolingFlowRate = Math.Max(25.0, st.ActiveTranspirationFlowRateGramsPerSecM2 * 1.6);
            directive = $"SURGE_TRANSPIRATION_COOLING: Leading edge temperature ({st.LeadingEdgeTemperatureKelvin:F0} K) is within 150K of UHTC ceramic structural limit ({env.MaxAllowableLeadingEdgeTempKelvin:F0} K). Boost transpiration coolant to {recommendedCoolingFlowRate:F1} g/s/m2 and pitch up 1.5 deg to ascend into thinner upper atmosphere.";
        }
        else if (dynamicPressureExceeded)
        {
            regime = "DYNAMIC_PRESSURE_CEILING_BREACH";
            recommendedCoolingFlowRate = st.ActiveTranspirationFlowRateGramsPerSecM2 * 1.2;
            directive = $"CLIMB_CORRIDOR_TRIM: Dynamic pressure ({st.DynamicPressureKPa:F1} kPa) exceeds {env.MaxAllowableDynamicPressureKPa:F1} kPa airframe limit. Command positive elevator trim to climb to {st.AltitudeKm + 3.0:F0} km.";
        }
        else if (shockAttached)
        {
            regime = "WAVERIDER_ATTACHED_CRUISE";
            recommendedCoolingFlowRate = st.ActiveTranspirationFlowRateGramsPerSecM2;
            directive = $"CRUISE_NOMINAL: Planar shock firmly attached beneath leading edge. Aerodynamic efficiency optimal (L/D = {liftToDrag:F2}). Stagnation thermal margins verified for sustained hypersonic cruise.";
        }
        else
        {
            regime = "SHOCK_DETACHED_SPILLAGE_DRAG";
            recommendedCoolingFlowRate = st.ActiveTranspirationFlowRateGramsPerSecM2 * 1.1;
            directive = $"REALIGN_SHOCK: Shock detached due to off-design Mach ({st.MachNumber:F1}) or AoA ({st.AngleOfAttackDegrees:F1} deg). Adjust attitude to 6.0 deg alpha to re-establish compressed lower shock layer.";
        }

        return new WaveriderAerothermalPlan(
            VehicleId: st.VehicleId,
            LiftToDragRatio: Math.Round(liftToDrag, 2),
            ObliqueShockAngleDegrees: Math.Round(obliqueShockAngle, 1),
            StagnationHeatFluxKiloWattsPerM2: Math.Round(stagnationHeatFlux, 1),
            ShockAttachedToLeadingEdge: shockAttached,
            ThermalExcursionWarningDetected: thermalWarning,
            RecommendedTranspirationCoolingFlowRate: Math.Round(recommendedCoolingFlowRate, 1),
            AerodynamicFlightRegime: regime,
            FlightGuidanceDirective: directive
        );
    }
}
