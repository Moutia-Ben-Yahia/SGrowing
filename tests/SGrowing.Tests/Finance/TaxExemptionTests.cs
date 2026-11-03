using SGrowing.Api.Modules.Finance.TaxExemption;
using Xunit;

namespace SGrowing.Tests.Finance;

public class TaxExemptionTests
{
    [Fact]
    public void GetOverview_ComputesShieldedTaxAndListsCertificates()
    {
        var engine = new TaxExemptionLedgerEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalValidCertificatesCount >= 2);
        Assert.True(overview.ReverseChargeClearancesCount >= 2);
        Assert.True(overview.TotalTaxShieldedUsd > 0);
        Assert.True(overview.AuditComplianceScorePct >= 99.0);
        Assert.NotEmpty(overview.ActiveCertificates);
        Assert.NotEmpty(overview.RecentClearances);
    }

    [Fact]
    public void ValidateCertificate_GeneratesTamperEvidentProofAndRegisters()
    {
        var engine = new TaxExemptionLedgerEngine();
        var request = new ValidateCertificateRequest(
            "Airbus Defence and Space SAS",
            "EU_FR",
            "FR12345678901",
            "REVERSE_CHARGE_B2B",
            24
        );

        var cert = engine.ValidateCertificate(request);

        Assert.NotNull(cert);
        Assert.StartsWith("CERT-EU_FR-", cert.CertificateId);
        Assert.Equal("EU_VIES", cert.VerificationRegistry);
        Assert.StartsWith("PROOF-SHA256-", cert.CryptographicProofHash);
        Assert.Equal("VALID", cert.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActiveCertificates, c => c.CertificateId == cert.CertificateId);
    }

    [Fact]
    public void IssueClearance_AppliesReverseChargeNoticeAndRecordsZeroVat()
    {
        var engine = new TaxExemptionLedgerEngine();
        var request = new IssueVatClearanceRequest(
            "CERT-EU-DE-001",
            "INV-2026-9901",
            250000.00m
        );

        var clearance = engine.IssueClearance(request);

        Assert.NotNull(clearance);
        Assert.StartsWith("CLR-", clearance.InvoiceClearanceId);
        Assert.Equal(0.0m, clearance.AppliedVatRatePct);
        Assert.Equal(0.0m, clearance.CalculatedTaxUsd);
        Assert.Contains("Article 196 EU Directive 2006/112/EC", clearance.MandatoryStatutoryNotice);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentClearances, c => c.InvoiceClearanceId == clearance.InvoiceClearanceId);
    }
}
