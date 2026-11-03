using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.TransferPricing;

public record IntercompanyEntity(
    string EntityId,
    string LegalName,
    string Jurisdiction, // "UnitedStates_Delaware", "Ireland_Dublin", "Singapore", "UnitedKingdom_London"
    string TaxIdentificationNumber,
    string FunctionalProfile, // "Principal_RiskTaker", "Distributor_LimitedRisk", "RndServiceProvider", "SharedServices"
    string FunctionalCurrency
);

public record TransferPricingAgreement(
    string AgreementId,
    string ProviderEntityId,
    string RecipientEntityId,
    string ServiceType, // "SoftwareDevelopmentCostSharing", "ManagementServicesMarkup", "IpLicensingRoyalty"
    decimal AppliedMarkupPct,
    decimal ArmLengthMinPct,
    decimal ArmLengthMaxPct,
    string ComplianceStatus // "Compliant_ArmLength", "AuditReviewRequired", "OutOfTolerance"
);

public record IntercompanyInvoice(
    string InvoiceId,
    string AgreementId,
    string ProviderEntityId,
    string RecipientEntityId,
    string BillingPeriod, // "2026-Q1", "2026-M03"
    decimal BaseCostAmount,
    decimal AppliedMarkupPct,
    decimal TotalInvoiceAmount,
    string Currency,
    string FxNettingReference,
    string SettlementStatus // "Settled_NettingPool", "Approved_PendingSettlement", "Disputed"
);

public record TransferPricingOverview(
    int TotalEntities,
    int ActiveAgreementsCount,
    decimal TotalIntercompanyVolumeQuarter,
    double ArmLengthComplianceRatePct,
    IReadOnlyList<IntercompanyEntity> Entities,
    IReadOnlyList<TransferPricingAgreement> Agreements,
    IReadOnlyList<IntercompanyInvoice> RecentInvoices
);

public record CalculateMarkupCommand(
    string ProviderEntityId,
    string RecipientEntityId,
    string ServiceType,
    decimal BaseDirectCost
);

public record CalculateMarkupResult(
    string CalculationId,
    string ServiceType,
    decimal BaseDirectCost,
    decimal RecommendedMarkupPct,
    decimal TotalArmLengthPrice,
    decimal ArmLengthLowerQuartile,
    decimal ArmLengthUpperQuartile,
    bool IsBepsCompliant
);

public record GenerateInvoicesCommand(
    string BillingPeriod,
    string NettingCurrency,
    string ApprovedBy
);

public record GenerateInvoicesResult(
    string SettlementBatchId,
    string BillingPeriod,
    int InvoicesGeneratedCount,
    decimal GrossIntercompanyVolume,
    decimal NetSettlementPayable,
    string FxNettingReceipt
);
