namespace SGrowing.Api.Modules.Sustainability.CarbonExchange;

public enum EmissionScope
{
    Scope1DirectFuel,
    Scope2PurchasedElectricity,
    Scope3LogisticsSupplyChain
}

public enum TransportModeEmissions
{
    DieselTruck,
    ElectricFreight,
    FreightRail,
    MaritimeContainer,
    AirCargo
}

public enum CreditCertificateStandard
{
    GoldStandard,
    VerraVCS,
    IrecRenewableEnergy,
    EecertEuropean
}

public enum OrderBookSide
{
    Buy,
    Sell
}

public enum TradeStatus
{
    Open,
    PartiallyFilled,
    Filled,
    Cancelled,
    Retired
}

public sealed record CarbonFootprintItem(
    string ItemId,
    EmissionScope Scope,
    string Description,
    double ActivityValue, // e.g., liters, kWh, ton-km
    string Unit,
    double EmissionFactorKgCo2ePerUnit,
    double TotalEmissionsKgCo2e
);

public sealed record GhgAssessmentResult(
    string AssessmentId,
    string TenantId,
    DateTime TimestampUtc,
    double Scope1TotalKgCo2e,
    double Scope2TotalKgCo2e,
    double Scope3TotalKgCo2e,
    double TotalGrossEmissionsKgCo2e,
    double Scope2RecOffsetKgCo2e,
    double NetEmissionsKgCo2e,
    double CbamTaxExposureEur,
    IReadOnlyList<CarbonFootprintItem> LineItems
);

public sealed record CalculateFootprintRequest(
    string TenantId,
    double DieselLitersBurned,
    double GridElectricityKwh,
    double GridRecCoverageRatio, // 0.0 to 1.0
    double FreightTonKilometers,
    TransportModeEmissions FreightMode,
    double EuEtsPriceEurPerTonne = 85.0,
    double EmbeddedEmissionsBenchmarkTonne = 5.0
);

public sealed record RecCertificate(
    string CertificateId,
    CreditCertificateStandard Standard,
    string OriginFacility,
    string GenerationType, // Solar, Wind, Hydro
    double MegawattHours,
    double Co2eOffsetTonnes,
    string IssuerCountry,
    DateTime VintageYear,
    string ProofHashHex,
    bool IsRetired,
    string? RetiredByTenantId = null,
    DateTime? RetiredAtUtc = null
);

public sealed record RecTradeOrder(
    string OrderId,
    string TenantId,
    OrderBookSide Side,
    double QuantityTonnes,
    decimal LimitPriceEurPerTonne,
    CreditCertificateStandard Standard,
    DateTime CreatedAtUtc,
    TradeStatus Status = TradeStatus.Open,
    double FilledQuantityTonnes = 0.0
);

public sealed record MatchedTrade(
    string TradeId,
    string BuyOrderId,
    string SellOrderId,
    string BuyerTenantId,
    string SellerTenantId,
    double MatchedTonnes,
    decimal ExecutionPriceEurPerTonne,
    CreditCertificateStandard Standard,
    DateTime ExecutedAtUtc
);

public sealed record RetirementCertificateReceipt(
    string ReceiptId,
    string CertificateId,
    string TenantId,
    double RetiredTonnes,
    string Reason,
    string ImmutableAuditProofHex,
    DateTime RetiredAtUtc
);

public sealed record DecarbonizationRecommendation(
    string ActionTitle,
    string Description,
    double PotentialAbatementTonnesCo2e,
    decimal EstimatedCostEur,
    decimal AbatementCostEurPerTonne,
    string Priority
);
