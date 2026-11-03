using SGrowing.Api.Modules.Finance.ExportCompliance;
using Xunit;

namespace SGrowing.Tests.Finance;

public class ExportComplianceTests
{
    [Fact]
    public void GetOverview_ReportsScreeningsRateAndTokens()
    {
        var engine = new ExportComplianceLedgerEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalScreenedTransactionsCount > 400);
        Assert.True(overview.AutomatedClearanceRatePct > 90.0);
        Assert.True(overview.DualUseItemsTrackedCount >= 10);
        Assert.True(overview.BlockedSanctionedAttemptsCount >= 1);
        Assert.NotEmpty(overview.RecentScreenings);
        Assert.NotEmpty(overview.ActiveTokens);
    }

    [Fact]
    public void ScreenTransaction_ValidatesEccnAndProhibitsSanctionedJurisdictions()
    {
        var engine = new ExportComplianceLedgerEngine();
        var safeRequest = new ScreenTransactionRequest(
            "TX-DEFENSE-SW-4410",
            "NL",
            "ASML Operations B.V.",
            "5D002"
        );

        var safeRecord = engine.ScreenTransaction(safeRequest);

        Assert.NotNull(safeRecord);
        Assert.StartsWith("SCR-EXP-", safeRecord.ScreeningId);
        Assert.Equal("CLEARED_EXEMPT", safeRecord.ClearanceStatus);
        Assert.True(safeRecord.DeniedPartiesListCleared);

        var sanctionedRequest = new ScreenTransactionRequest(
            "TX-ILLEGAL-ROUTING-001",
            "KP",
            "Embargoed Entity",
            "5A002.a"
        );

        var sanctionedRecord = engine.ScreenTransaction(sanctionedRequest);

        Assert.NotNull(sanctionedRecord);
        Assert.Equal("PROHIBITED_SANCTIONED", sanctionedRecord.ClearanceStatus);
        Assert.False(sanctionedRecord.DeniedPartiesListCleared);
    }

    [Fact]
    public void IssueClearance_GeneratesCryptographicToken()
    {
        var engine = new ExportComplianceLedgerEngine();
        var request = new IssueClearanceRequest(
            "SCR-EXP-001",
            "TX-CLOUD-INFRA-8801",
            "OFFICER-TEST-LEGAL"
        );

        var token = engine.IssueClearance(request);

        Assert.NotNull(token);
        Assert.StartsWith("TOK-CLR-", token.TokenId);
        Assert.StartsWith("SIG-SHA256-", token.CryptographicSignatureHash);
        Assert.True(token.ValidUntil > DateTime.UtcNow);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActiveTokens, t => t.TokenId == token.TokenId);
    }
}
