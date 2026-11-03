namespace SGrowing.Api.Modules.Finance.SttrTreatyTax;

public enum PaymentCategory
{
    Royalties,
    Interest,
    FinancingFees,
    TechnicalServiceFees,
    InsurancePremiums
}

public record SttrPayment(
    string PaymentId,
    string SourceJurisdiction,
    string RecipientJurisdiction,
    PaymentCategory Category,
    decimal GrossAmountEur,
    decimal RecipientNominalTaxRatePercentage,
    bool BilateralTreatyIncludesSttr,
    DateTime TransactionDateUtc
);

public record SttrEvaluation(
    string EvaluationId,
    string PaymentId,
    string SourceJurisdiction,
    string RecipientJurisdiction,
    PaymentCategory Category,
    decimal GrossAmountEur,
    decimal RecipientNominalRatePercentage,
    bool IsSubjectToSttr,
    decimal SttrTopUpRatePercentage,
    decimal TopUpWithholdingTaxEur,
    string ClearanceStatus, // SubjectToTopUpWithholding, ExemptNominalRateAboveThreshold, TreatyNotCovered
    DateTime EvaluatedAtUtc
);

public record AssessSttrPaymentRequest(
    string SourceJurisdiction,
    string RecipientJurisdiction,
    PaymentCategory Category,
    decimal GrossPaymentAmountEur,
    decimal RecipientNominalRatePercentage,
    bool BilateralTreatyIncludesSttr
);

public record SttrClearanceCertificate(
    string CertificateId,
    string PaymentId,
    string SourceJurisdiction,
    string RecipientJurisdiction,
    decimal TopUpTaxWithheldEur,
    string CryptographicSignatureSha256,
    string LegalArticleReference,
    DateTime IssuedAtUtc
);
