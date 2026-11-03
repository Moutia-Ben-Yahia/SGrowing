using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.SovereignCbam;

public sealed record CbamDeclarationRecord(
    string DeclarationId,
    string ImporterTaxId,
    string CountryOfOrigin,
    string GoodsCategory, // STEEL, ALUMINIUM, FERTILIZERS, CEMENT, HYDROGEN, ELECTRICITY
    decimal NetMassMetricTons,
    decimal EmbeddedDirectEmissionsTco2e,
    decimal EmbeddedIndirectEmissionsTco2e,
    decimal StatutoryCarbonTaxLiabilityEur,
    string StatutoryXmlSubmissionDigest,
    string Status // CLEARED_BY_CUSTOMS, PENDING_AUDIT, OFFSET_SETTLED
);

public sealed record EmissionsFactorRecord(
    string FactorId,
    string CountryIsoCode,
    string GoodsCategory,
    decimal BenchmarkFactorTco2ePerTon,
    decimal SovereignCarbonPriceEurPerTco2e,
    DateTime ValidUntil
);

public sealed record CbamOverview(
    int TotalDeclarationsCount,
    decimal TotalEmbeddedEmissionsTco2e,
    decimal TotalCarbonTaxLiabilityEur,
    decimal TotalOffsetCreditsSurrenderedEur,
    IReadOnlyList<CbamDeclarationRecord> RecentDeclarations,
    IReadOnlyList<EmissionsFactorRecord> BenchmarkFactors
);

public sealed record GenerateCbamDeclarationRequest(
    string ImporterTaxId,
    string CountryOfOrigin,
    string GoodsCategory,
    decimal NetMassMetricTons
);

public sealed record OffsetEmissionsRequest(
    string DeclarationId,
    decimal CarbonCreditsSurrenderedTco2e,
    string RegistryRegistryName // Verra, Gold Standard, EU ETS
);
