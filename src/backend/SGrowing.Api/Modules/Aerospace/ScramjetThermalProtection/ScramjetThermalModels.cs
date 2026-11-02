namespace SGrowing.Api.Modules.Aerospace.ScramjetThermalProtection;

public record HypersonicFlightCondition(
    string VehicleId,
    double MachNumber,
    double AltitudeMeters,
    double AngleOfAttackDegrees,
    double DynamicPressureKPa,
    double FreestreamVelocityMps
);

public record CoolingChannelSegment(
    string SegmentId,
    string Location, // e.g. "LeadingEdgeNose", "CowlLip", "CombustorIsolator", "CombustorFlameholder", "NozzleExpansionRamp"
    double WallThicknessMm,
    double MaxAllowableTempKelvin,
    double CurrentWallTempKelvin,
    double CoolantMassFlowKgPerSec
);

public record ScramjetThermalEvaluationRequest(
    HypersonicFlightCondition FlightCondition,
    IReadOnlyList<CoolingChannelSegment> Channels,
    double AvailableTotalCoolantFlowKgPerSec,
    bool ActiveFuelRegeneration = true
);

public record ChannelCoolantAdjustment(
    string SegmentId,
    string Location,
    double RequiredCoolantFlowKgPerSec,
    double ProjectedWallTempKelvin,
    double SafetyMarginKelvin,
    string FlowAction
);

public record ScramjetThermalProtectionPlan(
    string VehicleId,
    double MachNumber,
    double StagnationTempKelvin,
    double MaxProjectedWallTempKelvin,
    double ShockInteractionAmplificationFactor,
    double TotalCoolantDemandKgPerSec,
    double AvailableCoolantFlowKgPerSec,
    bool ThermalRunawayRisk,
    string OverallStatus,
    IReadOnlyList<ChannelCoolantAdjustment> Adjustments
);
