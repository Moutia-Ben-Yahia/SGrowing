namespace SGrowing.Api.Modules.Finance.ExportCompliance;

public record ExportScreeningRecord(
    string ScreeningId,
    string TransactionReference,
    string DestinationCountryIso,
    string ConsigneeEntityName,
    string EccnClassification, // e.g. 5A002.a (Information Security hardware), 5D002 (Cryptographic software), EAR99
    bool DeniedPartiesListCleared,
    bool RequiresBisLicense,
    string ClearanceStatus, // CLEARED_EXEMPT, LICENSE_REQUIRED, PROHIBITED_SANCTIONED
    DateTime ScreenedAt
);

public record CryptographicClearanceToken(
    string TokenId,
    string ScreeningId,
    string TransactionReference,
    string CryptographicSignatureHash,
    DateTime ValidUntil,
    string AuthorizingOfficer
);

public record ExportComplianceOverview(
    int TotalScreenedTransactionsCount,
    double AutomatedClearanceRatePct,
    int DualUseItemsTrackedCount,
    int BlockedSanctionedAttemptsCount,
    List<ExportScreeningRecord> RecentScreenings,
    List<CryptographicClearanceToken> ActiveTokens
);

public record ScreenTransactionRequest(
    string TransactionReference,
    string DestinationCountryIso,
    string ConsigneeEntityName,
    string EccnClassification
);

public record IssueClearanceRequest(
    string ScreeningId,
    string TransactionReference,
    string AuthorizingOfficer
);
