using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Finance.TaxExemption;

public class TaxExemptionLedgerEngine
{
    private readonly ConcurrentDictionary<string, TaxExemptionCertificate> _certificates = new();
    private readonly ConcurrentBag<VatClearanceInvoiceRecord> _clearances = new();

    public TaxExemptionLedgerEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var c1 = new TaxExemptionCertificate(
            "CERT-EU-DE-001",
            "Siemens Digital Cloud GmbH",
            "EU_DE",
            "DE811122233",
            "REVERSE_CHARGE_B2B",
            "EU_VIES",
            "PROOF-SHA256-DE811122233-VIES",
            DateTime.UtcNow.AddMonths(-6),
            DateTime.UtcNow.AddMonths(18),
            "VALID"
        );

        var c2 = new TaxExemptionCertificate(
            "CERT-UK-GB-002",
            "Rolls-Royce Digital Systems Ltd",
            "UK_GB",
            "GB987654321",
            "REVERSE_CHARGE_B2B",
            "UK_HMRC",
            "PROOF-SHA256-GB987654321-HMRC",
            DateTime.UtcNow.AddMonths(-3),
            DateTime.UtcNow.AddMonths(21),
            "VALID"
        );

        _certificates[c1.CertificateId] = c1;
        _certificates[c2.CertificateId] = c2;

        _clearances.Add(new VatClearanceInvoiceRecord(
            "CLR-2026-001",
            "CERT-EU-DE-001",
            "INV-2026-8901",
            125000.00m,
            0.0m,
            0.0m,
            "VAT Reverse Charge: Article 196 EU Directive 2006/112/EC applies. Customer liable for VAT.",
            DateTime.UtcNow.AddDays(-5)
        ));

        _clearances.Add(new VatClearanceInvoiceRecord(
            "CLR-2026-002",
            "CERT-UK-GB-002",
            "INV-2026-8902",
            85000.00m,
            0.0m,
            0.0m,
            "Reverse Charge: Section 55A VATA 1994 applies. Customer to account for VAT to HMRC.",
            DateTime.UtcNow.AddDays(-2)
        ));
    }

    public TaxExemptionOverview GetOverview()
    {
        var certsList = _certificates.Values.ToList();
        var clrList = _clearances.ToList();

        // Standard unshielded VAT would be approx 20% average
        decimal totalTaxShielded = clrList.Sum(c => c.NetAmountUsd * 0.20m);

        return new TaxExemptionOverview(
            certsList.Count(c => c.Status == "VALID"),
            clrList.Count,
            totalTaxShielded,
            99.6,
            certsList,
            clrList
        );
    }

    public TaxExemptionCertificate ValidateCertificate(ValidateCertificateRequest request)
    {
        var certId = $"CERT-{request.JurisdictionCode}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var registry = request.JurisdictionCode.StartsWith("EU_") ? "EU_VIES" :
                       request.JurisdictionCode.StartsWith("UK_") ? "UK_HMRC" : "US_SST_GOV";

        var proofHash = $"PROOF-SHA256-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.TaxIdentificationNumber + registry)))[..12]}";

        var cert = new TaxExemptionCertificate(
            certId,
            request.CounterpartyName,
            request.JurisdictionCode,
            request.TaxIdentificationNumber,
            request.ExemptionType,
            registry,
            proofHash,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(request.ValidMonths),
            "VALID"
        );

        _certificates[certId] = cert;
        return cert;
    }

    public VatClearanceInvoiceRecord IssueClearance(IssueVatClearanceRequest request)
    {
        var clrId = $"CLR-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        string notice = "VAT Reverse Charge applied. Customer responsible for self-assessing tax.";

        if (_certificates.TryGetValue(request.CertificateId, out var cert))
        {
            if (cert.JurisdictionCode.StartsWith("EU_"))
            {
                notice = "VAT Reverse Charge: Article 196 EU Directive 2006/112/EC applies. Customer liable for VAT.";
            }
            else if (cert.JurisdictionCode.StartsWith("UK_"))
            {
                notice = "Reverse Charge: Section 55A VATA 1994 applies. Customer to account for VAT to HMRC.";
            }
        }

        var clearance = new VatClearanceInvoiceRecord(
            clrId,
            request.CertificateId,
            request.InvoiceReferenceNumber,
            request.NetAmountUsd,
            0.0m,
            0.0m,
            notice,
            DateTime.UtcNow
        );

        _clearances.Add(clearance);
        return clearance;
    }
}
