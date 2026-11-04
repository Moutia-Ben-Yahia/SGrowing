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

    [Fact]
    public void ListReps_ReturnsRepsOrderedByCapacityLoad()
    {
        var reps = _engine.ListReps();

        Assert.NotEmpty(reps);
        Assert.True(reps.Count >= 3);
        // Ordering check: load ratio should be ascending
        for (int i = 0; i < reps.Count - 1; i++)
        {
            var ratioA = (double)reps[i].ActiveAccounts / reps[i].MaxConcurrentAccounts;
            var ratioB = (double)reps[i + 1].ActiveAccounts / reps[i + 1].MaxConcurrentAccounts;
            Assert.True(ratioA <= ratioB);
        }
    }

    [Fact]
    public void ListRules_ReturnsConfiguredTerritoryRules()
    {
        var rules = _engine.ListRules();

        Assert.NotEmpty(rules);
        Assert.Contains(rules, r => r.TerritoryName == "North America Enterprise");
        Assert.Contains(rules, r => r.TerritoryName == "EMEA Strategic");
    }

    [Fact]
    public void IngestSurgeSignal_WhenAccountDoesNotExist_CreatesNewTargetAccount()
    {
        var newAccount = _engine.IngestSurgeSignal(new IngestSurgeEventRequest(
            Domain: "brandnewstartup.ai",
            SignalSource: "PricingCalculatorSurge",
            IntentWeight: 25.0,
            TopicKey: "ai-copilot"
        ));

        Assert.NotNull(newAccount);
        Assert.Equal("brandnewstartup.ai", newAccount.Domain);
        Assert.Equal(25.0, newAccount.IntentSurgeScore);
        Assert.Equal(AccountTier.Tier1_Strategic, newAccount.Tier); // weight > 20 -> Tier1_Strategic

        var all = _engine.ListTargetAccounts();
        Assert.Contains(all, a => a.Domain == "brandnewstartup.ai");
    }

    [Fact]
    public void AssignAccountToOptimalRep_ThrowsKeyNotFoundException_WhenAccountNotFound()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            _engine.AssignAccountToOptimalRep(new AssignAccountRequest("acc_nonexistent_999")));
    }

    [Fact]
    public void IngestSurgeSignal_WhenSurgePushesScoreBeyond100_ClampsScoreTo100()
    {
        // acc_stripe starts at 96.0; adding weight 15.0 exceeds 100.0
        var updated = _engine.IngestSurgeSignal(new IngestSurgeEventRequest(
            Domain: "stripe.com",
            SignalSource: "PricingPageSurge",
            IntentWeight: 15.0,
            TopicKey: "enterprise-licensing"
        ));

        Assert.NotNull(updated);
        Assert.Equal(100.0, updated.IntentSurgeScore);
    }
}
