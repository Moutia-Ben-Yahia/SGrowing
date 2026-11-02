namespace SGrowing.Api.Modules.BioTech.DropletMicrofluidicSingleCell;

public interface IDropletMicrofluidicEngine
{
    DropletMicrofluidicEvaluation AnalyzeEmulsion(DropletMicrofluidicTelemetry telemetry);
}

public class DropletMicrofluidicEngine : IDropletMicrofluidicEngine
{
    public DropletMicrofluidicEvaluation AnalyzeEmulsion(DropletMicrofluidicTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Droplet sphere volume V = (4/3) * pi * r^3
        // r in um -> V in um^3 -> 1 um^3 = 10^-3 pL (since 1 pL = 1000 um^3)
        double radiusUm = telemetry.DropletDiameterMicrons / 2.0;
        double volumeUm3 = (4.0 / 3.0) * Math.PI * Math.Pow(radiusUm, 3);
        double volumePicoLiters = volumeUm3 / 1000.0;

        // Singlet purity = 100 - DoubletRate
        double singletPurity = Math.Max(0.0, 100.0 - telemetry.MeasuredDoubletRatePercent);

        bool isStable = telemetry.EmulsionCoalescenceRatePercent <= 0.15;
        bool isPoissonOptimal = telemetry.CellPoissonLambda >= 0.04 && telemetry.CellPoissonLambda <= 0.12 && telemetry.MeasuredDoubletRatePercent <= 2.5;
        bool isBeadSufficient = telemetry.BarcodedBeadLoadingFractionPercent >= 80.0;

        if (telemetry.EmulsionCoalescenceRatePercent > 0.35)
        {
            regimes.Add("DROPLET_COALESCENCE_EMULSION_COLLAPSE");
            mitigations.Add("Increase fluorosurfactant weight percent to 2.5 wt% and passivate microfluidic channel walls with fluorophilic silane coating.");
        }

        if (telemetry.MeasuredDoubletRatePercent > 3.0 || telemetry.InputCellConcentrationCellsPerMicroliter > 1200.0)
        {
            regimes.Add("MULTIPLET_CELL_DOUBLET_SATURATION");
            mitigations.Add("Dilute input single-cell suspension and apply 35-micron cell strainer filtration to eliminate multicellular aggregates.");
        }

        if (telemetry.BarcodedBeadLoadingFractionPercent < 75.0)
        {
            regimes.Add("BARCODED_BEAD_EMPTY_DROPLET_DEPLETION");
            mitigations.Add("Increase hydrogel bead packing density in syringe pump and align microfluidic flow focusing junction shear rates.");
        }

        bool isCertified = isStable && isPoissonOptimal && isBeadSufficient &&
                           telemetry.PostThermocyclingRnaRecoveryPercent >= 85.0;

        return new DropletMicrofluidicEvaluation
        {
            MicrofluidicChipRunId = telemetry.MicrofluidicChipRunId,
            DropletVolumePicoLiters = Math.Round(volumePicoLiters, 1),
            SingletPurityIndex = Math.Round(singletPurity, 2),
            IsEmulsionPhysicallyStable = isStable,
            IsPoissonLoadingOptimal = isPoissonOptimal,
            IsBeadOccupancySufficient = isBeadSufficient,
            IsSingleCellPartitioningCertified = isCertified,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
