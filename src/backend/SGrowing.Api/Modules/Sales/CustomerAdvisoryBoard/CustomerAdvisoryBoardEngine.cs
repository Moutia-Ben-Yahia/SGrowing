using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Sales.CustomerAdvisoryBoard;

public class CustomerAdvisoryBoardEngine
{
    private readonly ConcurrentDictionary<string, ExecutiveSponsorPairing> _pairings = new();
    private readonly ConcurrentBag<CabRoadmapVote> _votes = new();

    public CustomerAdvisoryBoardEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var p1 = new ExecutiveSponsorPairing(
            "PAIR-001",
            "Northstar Enterprise Holdings",
            "Dr. Marcus Vance",
            "Chief Information Security Officer",
            "Jonathan Chen",
            "Chief Revenue Officer",
            12,
            94.5,
            "STRONGLY_ALIGNED"
        );

        var p2 = new ExecutiveSponsorPairing(
            "PAIR-002",
            "Global Vanguard Logistics",
            "Elena Rostova",
            "VP Global Supply Chain",
            "Alexander Vance",
            "Chief Executive Officer",
            52,
            58.0,
            "AT_RISK_STALE"
        );

        _pairings[p1.PairingId] = p1;
        _pairings[p2.PairingId] = p2;

        _votes.Add(new CabRoadmapVote(
            "VOTE-001",
            "In-Kernel Cilium WireGuard Cross-Cloud Mesh",
            "Northstar Enterprise Holdings",
            1,
            2400000.00m,
            DateTime.UtcNow.AddDays(-10)
        ));

        _votes.Add(new CabRoadmapVote(
            "VOTE-002",
            "Direct Speech-to-Intent Voice Telephony Fast-Path",
            "Global Vanguard Logistics",
            2,
            1850000.00m,
            DateTime.UtcNow.AddDays(-5)
        ));
    }

    public CabMatrixOverview GetOverview()
    {
        var pairingsList = _pairings.Values.ToList();
        var votesList = _votes.ToList();

        double avgHealth = pairingsList.Count > 0
            ? Math.Round(pairingsList.Average(p => p.EngagementHealthScore), 1)
            : 0;

        int atRisk = pairingsList.Count(p => p.Status == "AT_RISK_STALE");
        decimal influencedArr = votesList.Sum(v => v.AnnualContractValueWeightUsd);

        return new CabMatrixOverview(
            pairingsList.Count,
            avgHealth,
            atRisk,
            influencedArr,
            pairingsList,
            votesList
        );
    }

    public ExecutiveSponsorPairing AlignSponsor(AlignSponsorRequest request)
    {
        var pairingId = $"PAIR-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var pairing = new ExecutiveSponsorPairing(
            pairingId,
            request.AccountName,
            request.CustomerExecutiveName,
            request.CustomerTitle,
            request.VendorSponsorName,
            request.VendorTitle,
            0,
            100.0,
            "STRONGLY_ALIGNED"
        );

        _pairings[pairingId] = pairing;
        return pairing;
    }

    public CabRoadmapVote VoteRoadmap(VoteRoadmapRequest request)
    {
        var voteId = $"VOTE-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var vote = new CabRoadmapVote(
            voteId,
            request.FeatureName,
            request.AccountName,
            Math.Clamp(request.PriorityRank, 1, 5),
            request.AnnualContractValueWeightUsd,
            DateTime.UtcNow
        );

        _votes.Add(vote);
        return vote;
    }
}
