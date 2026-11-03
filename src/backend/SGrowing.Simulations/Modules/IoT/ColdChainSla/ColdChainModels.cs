namespace SGrowing.Api.Modules.IoT.ColdChainSla;

public enum ColdChainCargoType
{
    VaccinesAndBiologics, // 2C to 8C
    FrozenSeafoodAndMeat, // -18C or colder
    InsulinInjectables,   // 2C to 8C strictly (freezing ruins insulin)
    AmbientPharmaceuticals // 15C to 25C
}

public sealed record TemperatureSensorSample(
    string SensorId,
    string ShipmentId,
    string CarrierId,
    DateTime TimestampUtc,
    double TemperatureCelsius,
    double RelativeHumidityPercent,
    double ShockGForce
);

public sealed record ColdChainSlaPolicy(
    ColdChainCargoType CargoType,
    double MinAllowedTempCelsius,
    double MaxAllowedTempCelsius,
    double CriticalMaxMktCelsius,
    int MaxPermissibleExcursionMinutes = 60,
    double MaxPermissibleShockG = 4.0
);

public sealed record ColdChainAuditReport(
    string AuditId,
    string ShipmentId,
    string CarrierId,
    ColdChainCargoType CargoType,
    double MeanKineticTemperatureCelsius,
    double MinObservedTempCelsius,
    double MaxObservedTempCelsius,
    int TotalExcursionMinutes,
    double MaxShockGObserved,
    bool SlaBreached,
    double SpoilageRiskPercent,
    decimal CarrierPenaltyDeductionEur,
    string AdjudicationVerdict,
    string ImmutableAuditProofHex,
    DateTime AuditedAtUtc
);

public sealed record AuditShipmentColdChainRequest(
    string ShipmentId,
    string CarrierId,
    ColdChainCargoType CargoType,
    decimal TotalCargoInvoiceValueEur,
    IReadOnlyList<TemperatureSensorSample> TelemetrySamples
);
