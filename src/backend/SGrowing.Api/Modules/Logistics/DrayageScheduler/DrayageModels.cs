namespace SGrowing.Api.Modules.Logistics.DrayageScheduler;

public enum ContainerSize
{
    TwentyFoot,
    FortyFoot,
    FortyFiveFootHighCube,
    ReeferTwentyFoot,
    ReeferFortyFoot
}

public enum ChassisType
{
    StandardTandem,
    TriAxleHeavy,
    ReeferGensetChassis
}

public sealed record ContainerCargoRecord(
    string ContainerId,
    ContainerSize Size,
    double GrossWeightKg,
    bool IsHazmat,
    bool RequiresGensetReefer,
    string TerminalCode,
    string VesselName,
    DateTime DischargeTimeUtc,
    DateTime FreeTimeExpiryUtc,
    decimal DailyDemurrageRateUsd,
    int PriorityUrgency = 1
);

public sealed record DrayageTrucker(
    string TruckerId,
    string DriverName,
    ChassisType CurrentChassis,
    int RemainingHoursOfServiceMinutes,
    double CurrentDistanceToTerminalKm,
    bool IsAvailable = true
);

public sealed record TerminalGateSlot(
    string SlotId,
    string TerminalCode,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    double CongestionIndex,
    int AvailableCapacity
);

public sealed record DrayageDispatchMission(
    string MissionId,
    string ContainerId,
    string TruckerId,
    string SlotId,
    DateTime ScheduledPickupUtc,
    int EstimatedTurnTimeMinutes,
    decimal IncurredDemurrageUsd,
    string DispatchNotes
);

public sealed record DrayageSchedulePlan(
    string PlanId,
    int TotalContainersScheduled,
    decimal TotalDemurrageIncurredUsd,
    decimal TotalDemurrageSavedUsd,
    IReadOnlyList<DrayageDispatchMission> Missions,
    IReadOnlyList<string> UnassignedContainerIds,
    DateTime GeneratedAtUtc
);

public sealed record ScheduleDrayageRequest(
    IReadOnlyList<ContainerCargoRecord> Containers,
    IReadOnlyList<DrayageTrucker> Truckers,
    IReadOnlyList<TerminalGateSlot> GateSlots,
    DateTime PlanningHorizonUtc
);
