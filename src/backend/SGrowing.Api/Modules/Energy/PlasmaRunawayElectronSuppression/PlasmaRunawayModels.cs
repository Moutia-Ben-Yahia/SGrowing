namespace SGrowing.Api.Modules.Energy.PlasmaRunawayElectronSuppression;

public record PlasmaRunawayTelemetry(
    string TokamakId,
    double PlasmaCurrentMegaAmps,
    double CurrentQuenchRateMaPerSec,
    double InducedToroidalElectricFieldVPerM,
    double ElectronDensity1e19PerM3,
    double CoulombLogarithm,
    double HardXRayPhotonCountsPerMs,
    string ShatteredPelletGasSpecies);

public record PlasmaRunawayAnalysisResult(
    string TokamakId,
    double RelativisticCriticalFieldVPerM,
    double ElectricFieldOverdriveRatio,
    double AvalancheGrowthRatePerMs,
    double RosenbluthDensity1e19PerM3,
    bool IsRunawayAvalancheDetected,
    string MitigationState,
    string MitigationCommand,
    string SuppressionAdvisory);
