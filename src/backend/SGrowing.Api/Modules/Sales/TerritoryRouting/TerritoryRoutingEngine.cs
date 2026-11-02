using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Sales.TerritoryRouting;

public interface ITerritoryRoutingEngine
{
    List<TargetAccount> ListTargetAccounts();
    List<SalesRepCapacity> ListReps();
    List<TerritoryRule> ListRules();
    TargetAccount IngestSurgeSignal(IngestSurgeEventRequest request);
    RoutingAssignmentResult AssignAccountToOptimalRep(AssignAccountRequest request);
}

public class TerritoryRoutingEngine : ITerritoryRoutingEngine
{
    private readonly ConcurrentDictionary<string, TargetAccount> _accounts = new();
    private readonly ConcurrentDictionary<string, SalesRepCapacity> _reps = new();
    private readonly List<TerritoryRule> _rules = new();

    public TerritoryRoutingEngine()
    {
        SeedDefaultTerritoriesAndReps();
    }

    private void SeedDefaultTerritoriesAndReps()
    {
        var rep1 = new SalesRepCapacity("rep_na_1", "Elena Rostova", "North America Enterprise", 25, 18, 115.0);
        var rep2 = new SalesRepCapacity("rep_emea_1", "Marcus Vance", "EMEA Strategic", 20, 12, 98.5);
        var rep3 = new SalesRepCapacity("rep_apac_1", "Kenji Sato", "APAC & Global Tech", 25, 8, 104.2);

        _reps[rep1.RepId] = rep1;
        _reps[rep2.RepId] = rep2;
        _reps[rep3.RepId] = rep3;

        _rules.Add(new TerritoryRule("rule_na", "North America Enterprise", new List<string> { "SaaS", "Fintech", "Healthcare" }, new List<string> { "US", "CA" }, AccountTier.Tier1_Strategic));
        _rules.Add(new TerritoryRule("rule_emea", "EMEA Strategic", new List<string> { "Fintech", "E-Commerce", "Manufacturing" }, new List<string> { "GB", "DE", "FR", "SE" }, AccountTier.Tier2_Target));
        _rules.Add(new TerritoryRule("rule_apac", "APAC & Global Tech", new List<string> { "Hardware", "Cloud Infrastructure", "Telecom" }, new List<string> { "JP", "SG", "AU" }, AccountTier.Tier2_Target));

        var acc1 = new TargetAccount("acc_stripe", "Stripe Inc", "stripe.com", "Fintech", "US", AccountTier.Tier1_Strategic, 96.0, rep1.RepId, rep1.Territory, DateTime.UtcNow.AddHours(-1));
        var acc2 = new TargetAccount("acc_klarna", "Klarna Bank AB", "klarna.com", "Fintech", "SE", AccountTier.Tier2_Target, 88.5, rep2.RepId, rep2.Territory, DateTime.UtcNow.AddHours(-3));
        var acc3 = new TargetAccount("acc_grab", "Grab Holdings", "grab.com", "Telecom", "SG", AccountTier.Tier2_Target, 74.0, null, null, DateTime.UtcNow.AddHours(-6));

        _accounts[acc1.AccountId] = acc1;
        _accounts[acc2.AccountId] = acc2;
        _accounts[acc3.AccountId] = acc3;
    }

    public List<TargetAccount> ListTargetAccounts() =>
        _accounts.Values.OrderByDescending(a => a.IntentSurgeScore).ToList();

    public List<SalesRepCapacity> ListReps() =>
        _reps.Values.OrderBy(r => (double)r.ActiveAccounts / r.MaxConcurrentAccounts).ToList();

    public List<TerritoryRule> ListRules() => _rules;

    public TargetAccount IngestSurgeSignal(IngestSurgeEventRequest request)
    {
        var existing = _accounts.Values.FirstOrDefault(a => a.Domain.Equals(request.Domain, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            var newScore = Math.Min(100.0, existing.IntentSurgeScore + request.IntentWeight);
            var updated = existing with
            {
                IntentSurgeScore = Math.Round(newScore, 1),
                LastSurgeAtUtc = DateTime.UtcNow
            };
            _accounts[updated.AccountId] = updated;
            return updated;
        }

        var newAcc = new TargetAccount(
            AccountId: "acc_" + Guid.NewGuid().ToString("N")[..8],
            CompanyName: request.Domain.Split('.')[0].ToUpperInvariant(),
            Domain: request.Domain,
            Industry: "Cloud Infrastructure",
            Country: "US",
            Tier: request.IntentWeight > 20 ? AccountTier.Tier1_Strategic : AccountTier.Tier2_Target,
            IntentSurgeScore: Math.Round(request.IntentWeight, 1),
            AssignedRepId: null,
            AssignedTerritory: null,
            LastSurgeAtUtc: DateTime.UtcNow
        );

        _accounts[newAcc.AccountId] = newAcc;
        return newAcc;
    }

    public RoutingAssignmentResult AssignAccountToOptimalRep(AssignAccountRequest request)
    {
        if (!_accounts.TryGetValue(request.AccountId, out var account))
        {
            throw new KeyNotFoundException($"Target account '{request.AccountId}' not found.");
        }

        // Match territory rule based on country & industry
        var matchedRule = _rules.FirstOrDefault(r => r.Countries.Contains(account.Country, StringComparer.OrdinalIgnoreCase))
            ?? _rules[0];

        // Find rep in territory with available capacity (lowest load ratio)
        var availableReps = _reps.Values
            .Where(r => r.Territory.Equals(matchedRule.TerritoryName, StringComparison.OrdinalIgnoreCase) && r.ActiveAccounts < r.MaxConcurrentAccounts)
            .OrderBy(r => (double)r.ActiveAccounts / r.MaxConcurrentAccounts)
            .ToList();

        var selectedRep = availableReps.FirstOrDefault()
            ?? _reps.Values.OrderBy(r => (double)r.ActiveAccounts / r.MaxConcurrentAccounts).First();

        // Increment rep load
        _reps[selectedRep.RepId] = selectedRep with { ActiveAccounts = selectedRep.ActiveAccounts + 1 };

        var updatedAccount = account with
        {
            AssignedRepId = selectedRep.RepId,
            AssignedTerritory = selectedRep.Territory
        };
        _accounts[account.AccountId] = updatedAccount;

        return new RoutingAssignmentResult(
            AccountId: account.AccountId,
            CompanyName: account.CompanyName,
            AssignedRepId: selectedRep.RepId,
            AssignedRepName: selectedRep.RepName,
            Territory: selectedRep.Territory,
            RoutingReason: $"Matched territory rule '{matchedRule.TerritoryName}' with lowest capacity load ({selectedRep.ActiveAccounts}/{selectedRep.MaxConcurrentAccounts} active accounts).",
            AssignedAtUtc: DateTime.UtcNow
        );
    }
}
