using System;

namespace SGrowing.Api.Modules.Energy.SmesCryogenicQuenchFastDump;

public sealed record SmesQuenchTelemetry(
    string CoilId,
    double StoredEnergyMegajoules,
    double OperatingCurrentAmperes,
    double CoilInductanceHenries,
    double MeasuredTotalVoltageMillivolts,
    double CoWoundInductiveVoltageMillivolts,
    double ColdHeadTemperatureKelvin,
    double HotSpotHeatingRateKelvinPerSec,
    double DumpResistorOhms,
    double CurrentRampRateAmpsPerSec);

public sealed record SmesQuenchAnalysisResult(
    string CoilId,
    double ResistiveVoltageMicrovolts,
    double EstimatedHotSpotTemperatureKelvin,
    double EnergyDumpTimeConstantSeconds,
    string QuenchProtectionStatus,
    string ProtectionActionRecommendation,
    bool IsFastDumpTriggered,
    DateTimeOffset Timestamp);
