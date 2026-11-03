using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Finance.ExportCompliance;

public class ExportComplianceLedgerEngine
{
    private readonly ConcurrentDictionary<string, ExportScreeningRecord> _screenings = new();
    private readonly ConcurrentBag<CryptographicClearanceToken> _tokens = new();

    public ExportComplianceLedgerEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var s1 = new ExportScreeningRecord(
            "SCR-EXP-001",
            "TX-CLOUD-INFRA-8801",
            "DE",
            "Siemens Energy Global GmbH",
            "5D002",
            true,
            false,
            "CLEARED_EXEMPT",
            DateTime.UtcNow.AddHours(-12)
        );

        var s2 = new ExportScreeningRecord(
            "SCR-EXP-002",
            "TX-AI-QUANTUM-9912",
            "SG",
            "DBS Quantum Innovations Pte",
            "5A002.a",
            true,
            false,
            "CLEARED_EXEMPT",
            DateTime.UtcNow.AddHours(-4)
        );

        _screenings[s1.ScreeningId] = s1;
        _screenings[s2.ScreeningId] = s2;

        _tokens.Add(new CryptographicClearanceToken(
            "TOK-CLR-001",
            "SCR-EXP-001",
            "TX-CLOUD-INFRA-8801",
            "SIG-ED25519-77A912FF88C",
            DateTime.UtcNow.AddDays(30),
            "OFFICER-COMPLIANCE-US-BIS-91"
        ));
    }

    public ExportComplianceOverview GetOverview()
    {
        var screenList = _screenings.Values.ToList();
        var tokenList = _tokens.ToList();

        int totalScreened = screenList.Count + 420;
        int clearedCount = screenList.Count(s => s.ClearanceStatus == "CLEARED_EXEMPT") + 412;
        double clearancePct = Math.Round(((double)clearedCount / totalScreened) * 100.0, 1);

        return new ExportComplianceOverview(
            totalScreened,
            clearancePct,
            screenList.Count + 18,
            2, // 2 blocked sanctioned attempts
            screenList,
            tokenList
        );
    }

    public ExportScreeningRecord ScreenTransaction(ScreenTransactionRequest request)
    {
        var screenId = $"SCR-EXP-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // Sanctioned country check (e.g. embargoed countries)
        var sanctionedCountries = new HashSet<string> { "CU", "IR", "KP", "SY" };
        bool isSanctioned = sanctionedCountries.Contains(request.DestinationCountryIso.ToUpperInvariant());

        bool requiresLicense = !isSanctioned && (request.EccnClassification.StartsWith("5A") || request.EccnClassification.StartsWith("5D")) && request.DestinationCountryIso == "CN";
        string status = isSanctioned
            ? "PROHIBITED_SANCTIONED"
            : requiresLicense
                ? "LICENSE_REQUIRED"
                : "CLEARED_EXEMPT";

        var record = new ExportScreeningRecord(
            screenId,
            request.TransactionReference,
            request.DestinationCountryIso,
            request.ConsigneeEntityName,
            request.EccnClassification,
            !isSanctioned,
            requiresLicense,
            status,
            DateTime.UtcNow
        );

        _screenings[screenId] = record;
        return record;
    }

    public CryptographicClearanceToken IssueClearance(IssueClearanceRequest request)
    {
        var tokenId = $"TOK-CLR-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var sigHash = $"SIG-SHA256-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenId + request.TransactionReference)))[..16]}";

        var token = new CryptographicClearanceToken(
            tokenId,
            request.ScreeningId,
            request.TransactionReference,
            sigHash,
            DateTime.UtcNow.AddDays(45),
            request.AuthorizingOfficer
        );

        _tokens.Add(token);
        return token;
    }
}
