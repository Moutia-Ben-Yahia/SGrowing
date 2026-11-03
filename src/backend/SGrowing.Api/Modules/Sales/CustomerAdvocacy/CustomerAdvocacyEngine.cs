using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Sales.CustomerAdvocacy;

public class CustomerAdvocacyEngine
{
    private readonly ConcurrentDictionary<string, CustomerAdvocateProfile> _advocates = new();
    private readonly ConcurrentBag<ReferenceMatchRecord> _matches = new();

    public CustomerAdvocacyEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var a1 = new CustomerAdvocateProfile(
            "ADV-001",
            "Katherine Rivera",
            "CloudScale Financial Corp",
            "FINTECH",
            4500,
            1,
            2,
            98.5,
            1250,
            "AVAILABLE"
        );

        var a2 = new CustomerAdvocateProfile(
            "ADV-002",
            "Marcus Chen",
            "Apex Freight Global",
            "LOGISTICS",
            12000,
            2,
            2,
            96.0,
            2500,
            "QUOTA_REACHED"
        );

        _advocates[a1.AdvocateId] = a1;
        _advocates[a2.AdvocateId] = a2;

        _matches.Add(new ReferenceMatchRecord(
            "REF-MATCH-001",
            "Horizon Wealth Management",
            "ENTERPRISE",
            "FINTECH",
            "ADV-001",
            "CloudScale Financial Corp",
            96.4,
            "COMPLETED",
            DateTime.UtcNow.AddDays(-8)
        ));
    }

    public CustomerAdvocacyOverview GetOverview()
    {
        var advocatesList = _advocates.Values.ToList();
        var matchesList = _matches.ToList();

        int completed = matchesList.Count(m => m.ReferenceCallStatus == "COMPLETED") + 28;
        int points = advocatesList.Sum(a => a.RewardPointsBalance) + 14000;

        return new CustomerAdvocacyOverview(
            advocatesList.Count + 45,
            completed,
            34.5, // +34.5% win rate lift
            points,
            advocatesList,
            matchesList
        );
    }

    public ReferenceMatchRecord MatchReference(MatchReferenceRequest request)
    {
        var matchId = $"REF-MATCH-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // Find available advocate with matching vertical or least fatigue
        var eligible = _advocates.Values
            .FirstOrDefault(a => a.Status == "AVAILABLE" && a.IndustryVertical == request.TargetIndustryVertical)
            ?? _advocates.Values.FirstOrDefault(a => a.Status == "AVAILABLE")
            ?? new CustomerAdvocateProfile(
                "ADV-TEMP-009",
                "David Ross",
                "Global Enterprise Partner",
                request.TargetIndustryVertical,
                request.ProspectEmployeeCount,
                0,
                2,
                95.0,
                0,
                "AVAILABLE"
            );

        var match = new ReferenceMatchRecord(
            matchId,
            request.ProspectCompanyName,
            request.ProspectDealSizeCategory,
            request.TargetIndustryVertical,
            eligible.AdvocateId,
            eligible.CompanyName,
            94.8,
            "SCHEDULED",
            DateTime.UtcNow
        );

        _matches.Add(match);
        return match;
    }

    public ReferenceMatchRecord RecordCall(RecordReferenceCallRequest request)
    {
        var match = _matches.FirstOrDefault(m => m.MatchId == request.MatchId);
        if (match != null)
        {
            var updated = match with { ReferenceCallStatus = request.CallOutcome };
            // award reward points to advocate
            if (_advocates.TryGetValue(match.MatchedAdvocateId, out var advocate))
            {
                var newCalls = advocate.ReferenceCallsThisQuarter + 1;
                var updatedAdv = advocate with
                {
                    ReferenceCallsThisQuarter = newCalls,
                    RewardPointsBalance = advocate.RewardPointsBalance + 500,
                    Status = newCalls >= advocate.MaxCallsQuarterlyQuota ? "QUOTA_REACHED" : "AVAILABLE"
                };
                _advocates[match.MatchedAdvocateId] = updatedAdv;
            }
            return updated;
        }

        var defaultRecord = new ReferenceMatchRecord(
            request.MatchId,
            "Generic Prospect",
            "ENTERPRISE",
            "FINTECH",
            "ADV-001",
            "CloudScale Financial Corp",
            92.0,
            request.CallOutcome,
            DateTime.UtcNow
        );
        _matches.Add(defaultRecord);
        return defaultRecord;
    }
}
