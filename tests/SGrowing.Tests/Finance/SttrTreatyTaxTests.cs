using SGrowing.Api.Modules.Finance.SttrTreatyTax;
using Xunit;

namespace SGrowing.Tests.Finance;

public class SttrTreatyTaxTests
{
    private readonly SttrTreatyTaxEngine _engine = new();

    [Fact]
    public void SeededPayments_EvaluatedAgainstNinePercentThreshold()
    {
        var payments = _engine.GetPayments();
        Assert.NotEmpty(payments);

        var evaluations = _engine.GetEvaluations();
        Assert.NotEmpty(evaluations);

        var bermudaPayment = evaluations.FirstOrDefault(e => e.PaymentId == "PAY-STTR-101");
        Assert.NotNull(bermudaPayment);
        Assert.True(bermudaPayment.IsSubjectToSttr);
        Assert.Equal(9.0m, bermudaPayment.SttrTopUpRatePercentage);
        Assert.True(bermudaPayment.TopUpWithholdingTaxEur > 0m);
    }

    [Fact]
    public void AssessPayment_ExemptsWhenNominalRateAboveNinePercent()
    {
        var request = new AssessSttrPaymentRequest(
            SourceJurisdiction: "DE",
            RecipientJurisdiction: "SG",
            Category: PaymentCategory.TechnicalServiceFees,
            GrossPaymentAmountEur: 1000000m,
            RecipientNominalRatePercentage: 17.0m, // Singapore corporate rate 17% > 9%
            BilateralTreatyIncludesSttr: true
        );

        var eval = _engine.AssessPayment(request);

        Assert.NotNull(eval);
        Assert.False(eval.IsSubjectToSttr);
        Assert.Equal(0.0m, eval.SttrTopUpRatePercentage);
        Assert.Equal(0.0m, eval.TopUpWithholdingTaxEur);
        Assert.Equal("ExemptNominalRateAboveThreshold", eval.ClearanceStatus);
    }

    [Fact]
    public void GenerateClearanceCertificate_IssuesCryptographicSha256Signature()
    {
        var cert = _engine.GenerateClearanceCertificate("PAY-STTR-101");

        Assert.NotNull(cert);
        Assert.NotEmpty(cert.CryptographicSignatureSha256);
        Assert.Equal(64, cert.CryptographicSignatureSha256.Length); // Valid SHA-256 hex string
        Assert.Contains("Article 1", cert.LegalArticleReference);
    }
}
