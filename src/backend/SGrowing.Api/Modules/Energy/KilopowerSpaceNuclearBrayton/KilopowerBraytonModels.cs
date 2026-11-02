using System;

namespace SGrowing.Api.Modules.Energy.KilopowerSpaceNuclearBrayton;

public sealed record KilopowerTelemetry(
    string ReactorId,
    double CoreSodiumHeatPipeTempCelsius,
    double BraytonTurbineInletTempCelsius,
    double BraytonCompressorInletTempCelsius,
    double WorkingFluidMassFlowKgPerSec,
    double RadiatorReturnFluidTempCelsius,
    double RadiatorFreezeThresholdTempCelsius,
    double RadiatorPanelAreaM2,
    double DeepSpaceSinkTempKelvin,
    double RadiatorReturnTempRateCelsiusPerMin,
    double ElectricalLoadDemandKilowatts);

public sealed record KilopowerAnalysisResult(
    string ReactorId,
    double NetElectricalPowerKilowatts,
    double RadiatorRejectedThermalPowerKilowatts,
    double RadiatorFreezeOutMarginKelvin,
    string ThermalSystemOperatingState,
    string AutonomousControlRecommendation,
    bool IsPowerAdequate,
    DateTimeOffset Timestamp);
