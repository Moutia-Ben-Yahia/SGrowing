namespace SGrowing.Api.Modules.Manufacturing.EuvLithographyPellicle;

public interface IEuvLithographyPellicleEngine
{
    PellicleLifecycleAssessment EvaluatePellicleLifecycle(PellicleEvaluationRequest request);
}

public class EuvLithographyPellicleEngine : IEuvLithographyPellicleEngine
{
    public PellicleLifecycleAssessment EvaluatePellicleLifecycle(PellicleEvaluationRequest request)
    {
        var scanner = request.Scanner;
        var pellicle = request.Pellicle;

        // Peak temperature modeling: Absorbed power radiates as Stefan-Boltzmann epsilon * sigma * (T^4 - T0^4)
        // CNT has high emissivity (~0.85), SiNx lower (~0.35)
        double emissivity = pellicle.MaterialType switch
        {
            "CarbonNanotube_CNT" => 0.88,
            "Polysilicon_SiNx" => 0.36,
            "MolybdenumSilicide_MoSi" => 0.52,
            _ => 0.50
        };

        // Absorbed thermal power density ~ (1 - Transmittance) * SourcePower
        double absorptionFraction = Math.Max(0.02, (100.0 - pellicle.CurrentTransmittancePercent) / 100.0);
        double absorbedPowerWatts = scanner.SourcePowerWatts * absorptionFraction;

        // Peak membrane temperature under slit illumination (approx 400C to 1000C)
        double peakTempC = 150.0 + (absorbedPowerWatts * 12.0 / emissivity);

        // Cyclic thermal tensile stress (E * alpha * DeltaT)
        // Tensile strength limits: CNT ~ 2500 MPa, SiNx ~ 1200 MPa, MoSi ~ 1500 MPa
        double maxAllowableStressMpa = pellicle.MaterialType switch
        {
            "CarbonNanotube_CNT" => 2500.0,
            "Polysilicon_SiNx" => 1200.0,
            "MolybdenumSilicide_MoSi" => 1500.0,
            _ => 1200.0
        };

        double thermalStressMpa = (peakTempC - 25.0) * 1.65; // ~1.65 MPa / degC mismatch with invar frame

        // Hydrogen radical chemical etch rate in nm per 1000 wafers
        double etchRate = (scanner.ChamberHydrogenPressurePa * 0.25) + (peakTempC * 0.002);

        // Projected remaining wafers before membrane breaches critical thickness (< 8nm) or transmittance drops < 85%
        double thicknessMarginNm = Math.Max(0.0, pellicle.MembraneThicknessNm - 8.0);
        int remainingByEtch = (int)Math.Floor((thicknessMarginNm / Math.Max(0.01, etchRate)) * 1000.0);
        int remainingByTransmittance = (int)Math.Max(0, (pellicle.CurrentTransmittancePercent - 85.0) * 3500.0);
        int projectedRemainingWafers = Math.Min(remainingByEtch, remainingByTransmittance);

        bool stressExceeded = thermalStressMpa >= (maxAllowableStressMpa * 0.85);
        bool lifeExhausted = projectedRemainingWafers <= 500;
        bool replacementRequired = stressExceeded || lifeExhausted;

        string urgency;
        string recommendation;

        if (stressExceeded || projectedRemainingWafers <= 150)
        {
            urgency = "IMMEDIATE_SWAP_CRITICAL";
            recommendation = $"CRITICAL_PELLICLE_RUPTURE_RISK: Thermal stress ({thermalStressMpa:F1} MPa) near tensile limit or remaining life ({projectedRemainingWafers} wafers) exhausted. Halting scanner for emergency robotic pellicle exchange.";
        }
        else if (replacementRequired)
        {
            urgency = "SCHEDULED_MAINTENANCE";
            recommendation = $"SCHEDULE_PELLICLE_EXCHANGE: Pellicle degradation active. Schedule automated swap within next {projectedRemainingWafers} wafer exposures.";
        }
        else
        {
            urgency = "NORMAL";
            recommendation = $"NOMINAL_OPERATION: Pellicle integrity sound. {projectedRemainingWafers} wafer exposures remaining. Transmittance {pellicle.CurrentTransmittancePercent:F1}%.";
        }

        return new PellicleLifecycleAssessment(
            PellicleSerialNumber: pellicle.PellicleSerialNumber,
            ReticleId: scanner.ReticleId,
            PeakMembraneTempCelsius: Math.Round(peakTempC, 1),
            ThermalTensileStressMegaPascals: Math.Round(thermalStressMpa, 1),
            HydrogenEtchRateNmPer1000Wafers: Math.Round(etchRate, 3),
            ProjectedRemainingWafers: Math.Max(0, projectedRemainingWafers),
            ReplacementRequired: replacementRequired,
            ReplacementUrgency: urgency,
            MaintenanceRecommendation: recommendation
        );
    }
}
