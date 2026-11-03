using SGrowing.Api.Modules.Finance.IntercompanyNetting;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class IntercompanyNettingTests
{
    private readonly IntercompanyNettingEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededInvoicesAndNettingEfficiency()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalSubsidiariesParticipating >= 3);
        Assert.True(overview.TotalGrossInvoicedVolumeUsd > 5000000.0);
        Assert.True(overview.HistoricalEfficiencyPercentage > 70.0);
        Assert.NotEmpty(overview.RecentInvoices);
        Assert.NotEmpty(overview.ParticipantSummaries);
    }

    [Fact]
    public void AddInvoice_ValidSubsidiaryTrade_RegistersInvoice()
    {
        var inv = _engine.AddInvoice(
            "SUB-UK-LTD",
            "SGrowing UK Limited",
            "SUB-US-HQ",
            "SGrowing Technologies Inc (US)",
            750000.0,
            "USD",
            "SALES_COMMISSION_FEE"
        );

        Assert.NotNull(inv);
        Assert.StartsWith("IC-INV-", inv.InvoiceId);
        Assert.Equal(750000.0, inv.InvoiceAmountUsd);
        Assert.Equal(SettlementStatus.PendingInvoiced, inv.Status);
    }

    [Fact]
    public void ExecuteMultilateralNettingCycle_GeneratesCryptographicAuditHash()
    {
        var receipt = _engine.ExecuteMultilateralNettingCycle(12);

        Assert.NotNull(receipt);
        Assert.StartsWith("NET-RCPT-", receipt.ReceiptId);
        Assert.Equal(12, receipt.NettingCycleNumber);
        Assert.Equal(64, receipt.CryptographicAuditHashSha256.Length);
        Assert.True(receipt.SettlementEfficiencyPercentage >= 75.0);
    }
}
