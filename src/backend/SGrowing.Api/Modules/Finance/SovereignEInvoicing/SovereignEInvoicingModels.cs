using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.SovereignEInvoicing;

public sealed record SovereignEInvoicingOverview(
    int TotalInvoicesClearedCount,
    double ClearanceSuccessRatePct,
    decimal TotalVatTaxRemittedUsd,
    int ActiveSovereignPortalsCount,
    IReadOnlyList<SovereignTaxPortalProfile> SupportedPortals,
    IReadOnlyList<ClearedEInvoiceRecord> RecentClearedInvoices
);

public sealed record SovereignTaxPortalProfile(
    string PortalCode, // PEPPOL_EU, ZATCA_SA, SAT_MX, CHORUS_PRO_FR, KSEF_PL
    string CountryName,
    string StandardFormat, // UBL 2.1 XML, CII, CFDI 4.0
    string RegulatoryMandate,
    string ClearanceType // PRE_CLEARANCE_REAL_TIME, POST_ISSUANCE_CTC
);

public sealed record ClearedEInvoiceRecord(
    string InvoiceNumber,
    string PortalCode,
    string BuyerTaxId,
    decimal TotalAmount,
    decimal VatAmount,
    string Currency,
    string SovereignClearanceUuid,
    string QrCodePayload,
    string Status,
    DateTime ClearedAt
);

public sealed record GenerateClearanceInvoiceRequest(
    string InvoiceNumber,
    string PortalCode,
    string SellerTaxId,
    string BuyerTaxId,
    decimal LineItemAmount,
    decimal VatRatePct,
    string Currency
);

public sealed record ClearanceInvoiceResult(
    string InvoiceNumber,
    string PortalCode,
    string SovereignClearanceUuid,
    string SignedXmlUblPayload,
    string QrCodePayload,
    string CryptographicTaxSeal,
    string ClearanceStatus,
    long LatencyMs
);

public sealed record VerifyClearanceRequest(
    string InvoiceNumber,
    string SovereignClearanceUuid
);

public sealed record ClearanceVerificationReceipt(
    string InvoiceNumber,
    string SovereignClearanceUuid,
    bool IsValid,
    string SovereignAuthorityConfirmation,
    DateTime VerifiedAt
);
