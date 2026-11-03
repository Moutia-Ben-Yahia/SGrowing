namespace SGrowing.Api.Modules.Sustainability.CircularRemanufacturing;

public enum PartDisposition
{
    DirectReuse,
    RefurbishRemanufacture,
    MaterialRecyclingScrap,
    HazardousDisposal
}

public sealed record ComponentDisassemblyNode(
    string PartNumber,
    string PartName,
    string MaterialType,
    double OperatingHoursUsed,
    double WeibullShapeBeta,
    double WeibullScaleEtaHours,
    decimal RefurbishCostEur,
    decimal ResaleValueEur,
    decimal RecyclingMeltValueEur,
    int DisassemblyLaborMinutes,
    double VirginMaterialCarbonKg
);

public sealed record DisassemblyPathRecommendation(
    string PartNumber,
    string PartName,
    double WeibullReliabilityScore,
    PartDisposition RecommendedDisposition,
    decimal NetEconomicValueEur,
    double AvoidedVirginCarbonKgCo2,
    string EngineeringRationale
);

public sealed record ProductRemanufacturingYieldReport(
    string ProductSerialNumber,
    string ModelNumber,
    int TotalDisassemblyLaborMinutes,
    decimal TotalGrossRecoveryValueEur,
    decimal TotalRemanufacturingCostEur,
    decimal NetCircularProfitEur,
    double OverallCircularYieldPercent,
    double TotalVirginCarbonAvoidedKg,
    IReadOnlyList<DisassemblyPathRecommendation> PartRecommendations,
    DateTime GeneratedAtUtc
);

public sealed record AnalyzeProductYieldRequest(
    string ProductSerialNumber,
    string ModelNumber,
    decimal HourlyLaborRateEur,
    IReadOnlyList<ComponentDisassemblyNode> Components
);
