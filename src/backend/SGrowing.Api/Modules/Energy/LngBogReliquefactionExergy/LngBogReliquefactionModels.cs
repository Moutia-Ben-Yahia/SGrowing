using System;

namespace SGrowing.Api.Modules.Energy.LngBogReliquefactionExergy;

public sealed record LngBogReliquefactionTelemetry(
    string CarrierTankId,
    double TankUllagePressureBar,
    double MeasuredBogGenerationRateKgPerHour,
    double NitrogenRefrigerantMassFlowKgPerSec,
    double CompressorPowerConsumptionKilowatts,
    double ReliquefactionColdBoxTempInletCelsius,
    double ReliquefactionSubcoolerTempOutletCelsius,
    double MaxDesignReliquefactionCapacityKgPerHour,
    double DualFuelBoilerAvailableCapacityKw);

public sealed record LngBogReliquefactionResult(
    string CarrierTankId,
    double SpecificRefrigerationWorkKwhPerKg,
    double ExergyRateDeliveredKilowatts,
    double SecondLawExergyEfficiencyPercent,
    string ReliquefactionOperatingState,
    string OptimalPlantControlAction,
    bool IsFlaringSuppressed,
    DateTimeOffset Timestamp);
