using SGrowing.Api.Modules.Sales.TerritoryRouting;

namespace SGrowing.Tests.Sales;

public class TerritoryRoutingTests
{
    private readonly ITerritoryRoutingEngine _engine = new TerritoryRoutingEngine();

    [Fact]
    public void ListTargetAccounts_ReturnsSeededPrioritizedAccounts()
    {
        var accounts = _engine.ListTargetAccounts();

        Assert.NotEmpty(accounts);
        Assert.Equal("acc_stripe", accounts[0].AccountId);
        Assert.True(accounts[0].IntentSurgeScore >= 90.0);
    }

    [Fact]
    public void IngestSurgeSignal_UpdatesExistingAccountScore()
    {
        var updated = _engine.IngestSurgeSignal(new IngestSurgeEventRequest(
            Domain: "stripe.com",
            SignalSource: "PricingPageSurge",
            IntentWeight: 3.5,
            TopicKey: "enterprise-licensing"
        ));

        Assert.NotNull(updated);
        Assert.True(updated.IntentSurgeScore >= 96.0);
    }

    [Fact]
    public void AssignAccountToOptimalRep_RoutesToLowestLoadRepInTerritory()
    {
        var result = _engine.AssignAccountToOptimalRep(new AssignAccountRequest("acc_grab"));

        Assert.NotNull(result);
        Assert.Equal("acc_grab", result.AccountId);
        Assert.NotEmpty(result.AssignedRepId);
        Assert.Contains("capacity load", result.RoutingReason);

        var accounts = _engine.ListTargetAccounts();
        var grab = accounts.First(a => a.AccountId == "acc_grab");
        Assert.NotNull(grab.AssignedRepId);
    }
}
