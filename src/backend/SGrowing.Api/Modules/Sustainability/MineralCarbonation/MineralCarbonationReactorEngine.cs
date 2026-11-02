namespace SGrowing.Api.Modules.Sustainability.MineralCarbonation;

public class MineralCarbonationReactorEngine
{
    private const double StoichiometricConversionRatio = 1.092; // 44.01 (CO2) / 40.30 (MgO equivalent)
    private const double ExothermicHeatFactorMwhPerTonCo2 = 0.55;

    public MineralCarbonationBatchResult ProcessMineralCarbonation(MineralCarbonationBatchRequest request)
    {
        var f = request.Feedstock;
        var p = request.Parameters;

        // Theoretical maximum CO2 capacity
        double theoreticalMaxCo2Tons = f.MassTons * f.ReactiveOxideFraction * StoichiometricConversionRatio;

        // Reaction kinetics efficiency
        double pressureFactor = p.PartialPressureCo2Bar * 0.015;
        double tempFactor = Math.Max(0.0, (p.ReactorTemperatureCelsius - 100.0) * 0.002);
        double particleSizePenalty = Math.Min(0.25, f.ParticleSizeMicrons * 0.002);

        double efficiency = Math.Clamp(0.50 + pressureFactor + tempFactor - particleSizePenalty, 0.10, 0.96);
        double sequesteredCo2Tons = Math.Round(theoreticalMaxCo2Tons * efficiency, 2);

        double heatRecoveryMwh = Math.Round(sequesteredCo2Tons * ExothermicHeatFactorMwhPerTonCo2, 2);
        double carbonCredits = Math.Round(sequesteredCo2Tons * 0.95, 2); // 5% lifecycle parasitic energy penalty

        string disposition = "Inert Carbonate Aggregate: Certified non-leachable calcium/magnesium carbonate suitable for green cement clinker replacement.";
        string certStatus = efficiency >= 0.70
            ? "ISO 14064-2 Compliant: High-efficiency permanent ex-situ mineral carbonation approved for tier-1 CDR credits."
            : "Conditional Certification: Carbonation yield sub-optimal; increase milling fineness or CO2 partial pressure.";

        return new MineralCarbonationBatchResult(
            BatchId: request.BatchId,
            Co2SequesteredTons: sequesteredCo2Tons,
            CarbonationEfficiencyPercent: Math.Round(efficiency * 100.0, 1),
            NetExothermicHeatRecoveryMwh: heatRecoveryMwh,
            CarbonCreditsGenerated: carbonCredits,
            SolidProductDisposition: disposition,
            RegulatoryCertificationStatus: certStatus
        );
    }
}
