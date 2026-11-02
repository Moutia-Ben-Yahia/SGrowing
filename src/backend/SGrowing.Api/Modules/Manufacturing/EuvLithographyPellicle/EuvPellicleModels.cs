namespace SGrowing.Api.Modules.Manufacturing.EuvLithographyPellicle;

public record EuvScannerCondition(
    string ScannerId,
    string ReticleId,
    double SourcePowerWatts,
    double ExposureDoseMjPerCm2,
    double ScanningSpeedMmPerSec,
    double ChamberHydrogenPressurePa
);

public record PellicleMembraneState(
    string PellicleSerialNumber,
    string MaterialType, // "CarbonNanotube_CNT", "Polysilicon_SiNx", "MolybdenumSilicide_MoSi"
    double MembraneThicknessNm,
    double CurrentTransmittancePercent,
    int TotalWafersExposed,
    double CumulativeThermalCycles
);

public record PellicleEvaluationRequest(
    EuvScannerCondition Scanner,
    PellicleMembraneState Pellicle
);

public record PellicleLifecycleAssessment(
    string PellicleSerialNumber,
    string ReticleId,
    double PeakMembraneTempCelsius,
    double ThermalTensileStressMegaPascals,
    double HydrogenEtchRateNmPer1000Wafers,
    int ProjectedRemainingWafers,
    bool ReplacementRequired,
    string ReplacementUrgency, // "NORMAL", "SCHEDULED_MAINTENANCE", "IMMEDIATE_SWAP_CRITICAL"
    string MaintenanceRecommendation
);
