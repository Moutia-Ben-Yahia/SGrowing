using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Finance.SttrTreatyTax;

public interface ISttrTreatyTaxEngine
{
    IReadOnlyList<SttrPayment> GetPayments();
    IReadOnlyList<SttrEvaluation> GetEvaluations();
    SttrEvaluation AssessPayment(AssessSttrPaymentRequest request);
    SttrClearanceCertificate GenerateClearanceCertificate(string paymentId);
}

public class SttrTreatyTaxEngine : ISttrTreatyTaxEngine
{
    private const decimal SttrMinimumRate = 9.0m;

    private readonly List<SttrPayment> _payments = new()
    {
        new SttrPayment(
            PaymentId: "PAY-STTR-101",
            SourceJurisdiction: "DE",
            RecipientJurisdiction: "BM", // Bermuda (0% nominal tax)
            Category: PaymentCategory.Royalties,
            GrossAmountEur: 2500000m,
            RecipientNominalTaxRatePercentage: 0.0m,
            BilateralTreatyIncludesSttr: true,
            TransactionDateUtc: DateTime.UtcNow.AddDays(-2)
        ),
        new SttrPayment(
            PaymentId: "PAY-STTR-102",
            SourceJurisdiction: "FR",
            RecipientJurisdiction: "SG", // Singapore (17% nominal tax)
            Category: PaymentCategory.TechnicalServiceFees,
            GrossAmountEur: 1200000m,
            RecipientNominalTaxRatePercentage: 17.0m,
            BilateralTreatyIncludesSttr: true,
            TransactionDateUtc: DateTime.UtcNow.AddDays(-1)
        ),
        new SttrPayment(
            PaymentId: "PAY-STTR-103",
            SourceJurisdiction: "NL",
            RecipientJurisdiction: "CY", // Cyprus (12.5% nominal tax > 9%)
            Category: PaymentCategory.FinancingFees,
            GrossAmountEur: 850000m,
            RecipientNominalTaxRatePercentage: 12.5m,
            BilateralTreatyIncludesSttr: true,
            TransactionDateUtc: DateTime.UtcNow
        )
    };

    private readonly List<SttrEvaluation> _evaluations = new();

    public SttrTreatyTaxEngine()
    {
        // Pre-evaluate seeded payments
        foreach (var p in _payments)
        {
            _evaluations.Add(EvaluateInternal(p));
        }
    }

    public IReadOnlyList<SttrPayment> GetPayments() => _payments.AsReadOnly();

    public IReadOnlyList<SttrEvaluation> GetEvaluations() => _evaluations.AsReadOnly();

    public SttrEvaluation AssessPayment(AssessSttrPaymentRequest request)
    {
        var tempPayment = new SttrPayment(
            PaymentId: $"PAY-{Guid.NewGuid().ToString("N")[..8]}",
            SourceJurisdiction: request.SourceJurisdiction,
            RecipientJurisdiction: request.RecipientJurisdiction,
            Category: request.Category,
            GrossAmountEur: request.GrossPaymentAmountEur,
            RecipientNominalTaxRatePercentage: request.RecipientNominalRatePercentage,
            BilateralTreatyIncludesSttr: request.BilateralTreatyIncludesSttr,
            TransactionDateUtc: DateTime.UtcNow
        );

        var eval = EvaluateInternal(tempPayment);
        _payments.Add(tempPayment);
        _evaluations.Add(eval);
        return eval;
    }

    public SttrClearanceCertificate GenerateClearanceCertificate(string paymentId)
    {
        var eval = _evaluations.FirstOrDefault(e => e.PaymentId == paymentId);
        if (eval == null)
        {
            throw new InvalidOperationException($"Evaluation for payment {paymentId} not found");
        }

        var rawData = $"{eval.PaymentId}:{eval.SourceJurisdiction}:{eval.RecipientJurisdiction}:{eval.TopUpWithholdingTaxEur}:{DateTime.UtcNow:O}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        var signatureHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return new SttrClearanceCertificate(
            CertificateId: $"CERT-STTR-{Guid.NewGuid().ToString("N")[..8]}",
            PaymentId: paymentId,
            SourceJurisdiction: eval.SourceJurisdiction,
            RecipientJurisdiction: eval.RecipientJurisdiction,
            TopUpTaxWithheldEur: eval.TopUpWithholdingTaxEur,
            CryptographicSignatureSha256: signatureHex,
            LegalArticleReference: "OECD Multilateral Convention to Implement the Subject to Tax Rule (Article 1 - Bilateral Covered Payments)",
            IssuedAtUtc: DateTime.UtcNow
        );
    }

    private static SttrEvaluation EvaluateInternal(SttrPayment p)
    {
        if (!p.BilateralTreatyIncludesSttr)
        {
            return new SttrEvaluation(
                EvaluationId: $"EVAL-{Guid.NewGuid().ToString("N")[..8]}",
                PaymentId: p.PaymentId,
                SourceJurisdiction: p.SourceJurisdiction,
                RecipientJurisdiction: p.RecipientJurisdiction,
                Category: p.Category,
                GrossAmountEur: p.GrossAmountEur,
                RecipientNominalRatePercentage: p.RecipientNominalTaxRatePercentage,
                IsSubjectToSttr: false,
                SttrTopUpRatePercentage: 0.0m,
                TopUpWithholdingTaxEur: 0.0m,
                ClearanceStatus: "TreatyNotCovered",
                EvaluatedAtUtc: DateTime.UtcNow
            );
        }

        if (p.RecipientNominalTaxRatePercentage >= SttrMinimumRate)
        {
            return new SttrEvaluation(
                EvaluationId: $"EVAL-{Guid.NewGuid().ToString("N")[..8]}",
                PaymentId: p.PaymentId,
                SourceJurisdiction: p.SourceJurisdiction,
                RecipientJurisdiction: p.RecipientJurisdiction,
                Category: p.Category,
                GrossAmountEur: p.GrossAmountEur,
                RecipientNominalRatePercentage: p.RecipientNominalTaxRatePercentage,
                IsSubjectToSttr: false,
                SttrTopUpRatePercentage: 0.0m,
                TopUpWithholdingTaxEur: 0.0m,
                ClearanceStatus: "ExemptNominalRateAboveThreshold",
                EvaluatedAtUtc: DateTime.UtcNow
            );
        }

        var topUpRate = SttrMinimumRate - p.RecipientNominalTaxRatePercentage;
        var topUpTax = p.GrossAmountEur * (topUpRate / 100m);

        return new SttrEvaluation(
            EvaluationId: $"EVAL-{Guid.NewGuid().ToString("N")[..8]}",
            PaymentId: p.PaymentId,
            SourceJurisdiction: p.SourceJurisdiction,
            RecipientJurisdiction: p.RecipientJurisdiction,
            Category: p.Category,
            GrossAmountEur: p.GrossAmountEur,
            RecipientNominalRatePercentage: p.RecipientNominalTaxRatePercentage,
            IsSubjectToSttr: true,
            SttrTopUpRatePercentage: topUpRate,
            TopUpWithholdingTaxEur: topUpTax,
            ClearanceStatus: "SubjectToTopUpWithholding",
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }
}
