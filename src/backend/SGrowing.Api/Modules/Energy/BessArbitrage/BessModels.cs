namespace SGrowing.Api.Modules.Energy.BessArbitrage;

public enum BatteryOperatingMode
{
    Charge,
    Discharge,
    Idle
}

public sealed record BessSystemSpecs(
    string BatteryId,
    double TotalCapacityKwh,
    double MaxChargePowerKw,
    double MaxDischargePowerKw,
    double RoundTripEfficiency = 0.88,
    double MinAllowedSoC = 0.10,
    double MaxAllowedSoC = 0.90,
    double InitialSoC = 0.40
);

public sealed record HourlyTariffAndDemand(
    int HourOfDay,
    double FacilityDemandKw,
    decimal GridEnergyPricePerKwhEur,
    bool IsPeakTariffWindow
);

public sealed record BessHourlyDispatch(
    int HourOfDay,
    double GrossFacilityDemandKw,
    BatteryOperatingMode Mode,
    double BatteryPowerKw,
    double NetGridDemandKw,
    double ResultingSoC,
    decimal CostWithoutBessEur,
    decimal CostWithBessEur
);

public sealed record BessArbitrageSchedulePlan(
    string PlanId,
    string BatteryId,
    decimal TotalEnergyCostBeforeEur,
    decimal TotalEnergyCostAfterEur,
    decimal PeakDemandChargeReductionEur,
    decimal TotalDailyNetSavingsEur,
    double PeakDemandBeforeKw,
    double PeakDemandAfterKw,
    double PeakShavedKw,
    double TotalDischargedKwh,
    IReadOnlyList<BessHourlyDispatch> HourlySchedule,
    DateTime GeneratedAtUtc
);

public sealed record OptimizeBessScheduleRequest(
    BessSystemSpecs Specs,
    IReadOnlyList<HourlyTariffAndDemand> Tariffs,
    decimal MonthlyPeakChargePerKwEur
);
