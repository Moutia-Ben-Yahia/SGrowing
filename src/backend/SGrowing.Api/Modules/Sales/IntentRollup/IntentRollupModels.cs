using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Sales.IntentRollup;

public record BuyingCommitteeSignal(
    string SignalId,
    string ContactEmail,
    string JobTitle,
    string IntentSource, // "G2_BuyerIntent", "WebsitePricingPage", "WebinarAttendance", "WhitepaperDownload"
    int IntensityScore, // 1-100
    DateTime DetectedAt
);

public record CorporateAccountNode(
    string AccountId,
    string LegalName,
    string Domain,
    string ParentCorporateEntity,
    string GlobalUltimateDuns,
    int TotalEmployees,
    int AggregateIntentScore, // 0-100
    string ActiveOutreachOwnerRepId,
    bool OutreachLocked,
    IReadOnlyList<BuyingCommitteeSignal> CommitteeSignals
);

public record IntentRollupOverview(
    int TotalMonitoredAccounts,
    int HighIntentAccountsCount,
    int DeDuplicatedSignalsCount,
    double OutboundCollisionPreventionRatePct,
    IReadOnlyList<CorporateAccountNode> Accounts
);

public record IngestIntentSignalCommand(
    string Domain,
    string ContactEmail,
    string JobTitle,
    string IntentSource,
    int IntensityScore
);

public record IngestIntentSignalResult(
    string SignalReceiptId,
    string RollupAccountId,
    string ParentCorporateEntity,
    int UpdatedAggregateIntentScore,
    bool IsDeDuplicatedDuplicate,
    string RecommendedAction
);

public record LockAccountOutreachCommand(
    string AccountId,
    string RepId,
    int ExclusivityWindowDays
);

public record LockAccountOutreachResult(
    string LockId,
    string AccountId,
    string LockedToRepId,
    DateTime ExpiresAt,
    string AuditReceipt
);
