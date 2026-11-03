using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Sales.BuyingCommittee;

public class BuyingCommitteeGraphEngine
{
    private readonly ConcurrentDictionary<string, StakeholderMember> _stakeholders = new();
    private readonly ConcurrentDictionary<string, CommitteeInterventionRecord> _interventions = new();

    public BuyingCommitteeGraphEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var s1 = new StakeholderMember(
            "STK-001",
            "Dr. Marcus Vance",
            "Chief Information Security Officer",
            "TECHNICAL_EVALUATOR",
            0.85,
            0.65,
            "InfoSec & Compliance",
            ["STK-002", "STK-004"],
            "ALIGNED"
        );

        var s2 = new StakeholderMember(
            "STK-002",
            "Alicia Thorne",
            "VP Procurement & Strategic Sourcing",
            "PROCUREMENT_BLOCKER",
            0.90,
            -0.45,
            "Global Sourcing",
            ["STK-003"],
            "BLOCKED"
        );

        var s3 = new StakeholderMember(
            "STK-003",
            "Jonathan Chen",
            "Chief Revenue Officer",
            "ECONOMIC_BUYER",
            0.95,
            0.88,
            "Executive Leadership",
            ["STK-001", "STK-002"],
            "ALIGNED"
        );

        var s4 = new StakeholderMember(
            "STK-004",
            "Sarah Jenkins",
            "Director of Revenue Operations",
            "CHAMPION",
            0.75,
            0.92,
            "RevOps",
            ["STK-001"],
            "ALIGNED"
        );

        _stakeholders[s1.StakeholderId] = s1;
        _stakeholders[s2.StakeholderId] = s2;
        _stakeholders[s3.StakeholderId] = s3;
        _stakeholders[s4.StakeholderId] = s4;

        var int1 = new CommitteeInterventionRecord(
            "INT-001",
            "STK-002",
            "EXEC_SPONSOR_ALIGNMENT",
            "Jonathan Chen (CRO)",
            DateTime.UtcNow.AddDays(2),
            "Unblock net-60 payment terms exception and deliver ROI impact case study",
            "PENDING"
        );

        _interventions[int1.InterventionId] = int1;
    }

    public BuyingCommitteeOverview GetOverview(string accountId = "ACC-NORTHSTAR")
    {
        var members = _stakeholders.Values.ToList();
        var interventions = _interventions.Values.ToList();

        double totalWeight = members.Sum(m => m.InfluenceWeight);
        double weightedSentiment = totalWeight > 0
            ? members.Sum(m => m.InfluenceWeight * m.SentimentScore) / totalWeight
            : 0;

        // Scale sentiment (-1.0 .. +1.0) to consensus score (0 .. 100)
        double consensusScore = Math.Round(((weightedSentiment + 1.0) / 2.0) * 100.0, 1);
        int alignedCount = members.Count(m => m.CurrentStatus == "ALIGNED" || m.SentimentScore > 0.3);
        int blockerCount = members.Count(m => m.CurrentStatus == "BLOCKED" || m.SentimentScore < -0.2);

        return new BuyingCommitteeOverview(
            accountId,
            "Northstar Enterprise Systems",
            consensusScore,
            members.Count,
            alignedCount,
            blockerCount,
            members,
            interventions
        );
    }

    public StakeholderMember AddStakeholder(AddStakeholderRequest request)
    {
        var id = $"STK-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        string status = request.SentimentScore switch
        {
            > 0.3 => "ALIGNED",
            < -0.2 => "BLOCKED",
            _ => "UNDECIDED"
        };

        var member = new StakeholderMember(
            id,
            request.FullName,
            request.RoleTitle,
            request.BuyingRole,
            Math.Clamp(request.InfluenceWeight, 0.1, 1.0),
            Math.Clamp(request.SentimentScore, -1.0, 1.0),
            request.Department,
            [],
            status
        );

        _stakeholders[id] = member;
        return member;
    }

    public CommitteeInterventionRecord TriggerIntervention(TriggerInterventionRequest request)
    {
        var intId = $"INT-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var intervention = new CommitteeInterventionRecord(
            intId,
            request.StakeholderId,
            request.PlaybookType,
            request.AssignedExecutive,
            DateTime.UtcNow.AddDays(3),
            $"Automated intervention playbook {request.PlaybookType} scheduled to resolve objection",
            "PENDING"
        );

        _interventions[intId] = intervention;
        return intervention;
    }
}
