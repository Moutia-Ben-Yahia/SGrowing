using SGrowing.Api.Modules.BioTech.DropletMicrofluidicSingleCell;
using Xunit;

namespace SGrowing.Tests.BioTech;

public class DropletMicrofluidicTests
{
    private readonly DropletMicrofluidicEngine _engine = new();

    [Fact]
    public void AnalyzeEmulsion_NominalSingleCellRun_ReturnsPartitioningCertified()
    {
        var telemetry = new DropletMicrofluidicTelemetry
        {
            MicrofluidicChipRunId = "CHIP-GEM-001",
            SurfactantChemistry = "Krytox_PEG_Fluorosurfactant_2wtPct",
            DropletGenerationFrequencyHertz = 3200.0,
            DropletDiameterMicrons = 62.0,
            CellPoissonLambda = 0.08,
            MeasuredDoubletRatePercent = 1.65,
            BarcodedBeadLoadingFractionPercent = 88.5,
            EmulsionCoalescenceRatePercent = 0.045,
            InputCellConcentrationCellsPerMicroliter = 850.0,
            PostThermocyclingRnaRecoveryPercent = 91.2
        };

        var result = _engine.AnalyzeEmulsion(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CHIP-GEM-001", result.MicrofluidicChipRunId);
        Assert.True(result.IsEmulsionPhysicallyStable);
        Assert.True(result.IsPoissonLoadingOptimal);
        Assert.True(result.IsBeadOccupancySufficient);
        Assert.True(result.IsSingleCellPartitioningCertified);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.DropletVolumePicoLiters > 100.0);
        Assert.True(result.SingletPurityIndex > 98.0);
    }

    [Fact]
    public void AnalyzeEmulsion_ExcessiveCoalescence_TriggersEmulsionCollapseAlert()
    {
        var telemetry = new DropletMicrofluidicTelemetry
        {
            MicrofluidicChipRunId = "CHIP-GEM-002",
            EmulsionCoalescenceRatePercent = 0.55
        };

        var result = _engine.AnalyzeEmulsion(telemetry);

        Assert.False(result.IsEmulsionPhysicallyStable);
        Assert.False(result.IsSingleCellPartitioningCertified);
        Assert.Contains("DROPLET_COALESCENCE_EMULSION_COLLAPSE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeEmulsion_HighDoubletConcentration_TriggersMultipletSaturationAlert()
    {
        var telemetry = new DropletMicrofluidicTelemetry
        {
            MicrofluidicChipRunId = "CHIP-GEM-003",
            MeasuredDoubletRatePercent = 4.8,
            InputCellConcentrationCellsPerMicroliter = 1600.0
        };

        var result = _engine.AnalyzeEmulsion(telemetry);

        Assert.False(result.IsPoissonLoadingOptimal);
        Assert.False(result.IsSingleCellPartitioningCertified);
        Assert.Contains("MULTIPLET_CELL_DOUBLET_SATURATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeEmulsion_LowBeadOccupancy_TriggersEmptyDropletAlert()
    {
        var telemetry = new DropletMicrofluidicTelemetry
        {
            MicrofluidicChipRunId = "CHIP-GEM-004",
            BarcodedBeadLoadingFractionPercent = 68.0
        };

        var result = _engine.AnalyzeEmulsion(telemetry);

        Assert.False(result.IsBeadOccupancySufficient);
        Assert.False(result.IsSingleCellPartitioningCertified);
        Assert.Contains("BARCODED_BEAD_EMPTY_DROPLET_DEPLETION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeEmulsion_LowRnaRecovery_FailsPartitioningCertification()
    {
        var telemetry = new DropletMicrofluidicTelemetry
        {
            MicrofluidicChipRunId = "CHIP-GEM-005",
            PostThermocyclingRnaRecoveryPercent = 74.0
        };

        var result = _engine.AnalyzeEmulsion(telemetry);

        Assert.False(result.IsSingleCellPartitioningCertified);
    }
}
