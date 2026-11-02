namespace SGrowing.Api.Modules.Energy.SolidStateBatteryEis;

public record EisSpectrumPoint(
    double FrequencyHz,
    double RealImpedanceOhms,
    double ImaginaryImpedanceOhms,
    double PhaseAngleDegrees
);

public record BatteryCellCondition(
    string CellId,
    string ElectrolyteType, // "LLZO_Garnet", "Argyrodite_Sulfide", "Polymer_PEO"
    double StateOfChargePercent,
    double TemperatureCelsius,
    double NominalCapacityAh,
    double ChargingCurrentAmps
);

public record EisEvaluationRequest(
    BatteryCellCondition Cell,
    IReadOnlyList<EisSpectrumPoint> Spectrum
);

public record BatteryDendriteAssessment(
    string CellId,
    double BulkResistanceOhms,
    double SeiResistanceOhms,
    double ChargeTransferResistanceOhms,
    double DendriteRiskScorePercent,
    bool DendriteNucleationDetected,
    string ChargingProtocolRecommendation,
    string HealthStatus
);
