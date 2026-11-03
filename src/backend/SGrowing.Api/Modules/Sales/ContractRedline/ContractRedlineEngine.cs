using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Sales.ContractRedline;

public class ContractRedlineEngine
{
    private readonly ConcurrentDictionary<string, ClauseRedlineRecord> _redlines = new();
    private readonly ConcurrentBag<ContractDeviationApproval> _approvals = new();

    public ContractRedlineEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var r1 = new ClauseRedlineRecord(
            "RED-LOL-001",
            "MSA-2026-NORTHSTAR",
            "LIMITATION_OF_LIABILITY",
            "Vendor liability shall be unlimited for any gross negligence or data breach incidents.",
            "Vendor liability is strictly capped at the total fees paid in the preceding 12 months.",
            "Vendor liability shall be capped at 2x annual contract value, excluding IP infringement claims.",
            48.5,
            "ACCEPTABLE_COMPROMISE",
            "COMPROMISE_OFFERED"
        );

        var r2 = new ClauseRedlineRecord(
            "RED-IND-002",
            "MSA-2026-NORTHSTAR",
            "INDEMNIFICATION",
            "Customer shall receive full uncapped indemnity for all third-party IP and regulatory claims without reciprocal defense.",
            "Each party shall mutually defend and indemnify the other against third-party IP infringement claims.",
            "Reciprocal indemnification for third-party direct patent or copyright infringement claims with 30-day notice requirement.",
            32.0,
            "ACCEPTABLE_COMPROMISE",
            "PENDING_REVIEW"
        );

        _redlines[r1.ClauseId] = r1;
        _redlines[r2.ClauseId] = r2;

        _approvals.Add(new ContractDeviationApproval(
            "APP-001",
            "RED-LOL-001",
            "Sarah Jenkins (Associate General Counsel)",
            "Approved 2x ACV super-cap in exchange for 3-year commitment and upfront payment",
            DateTime.UtcNow.AddDays(-1),
            "SEAL-SHA256-48C9A0F112B"
        ));
    }

    public ContractRedlineOverview GetOverview()
    {
        var redlinesList = _redlines.Values.ToList();
        var approvalsList = _approvals.ToList();

        double avgRisk = redlinesList.Count > 0
            ? Math.Round(redlinesList.Average(r => r.DeviationRiskScore), 1)
            : 0;

        int pending = redlinesList.Count(r => r.Status == "PENDING_REVIEW");
        int compromises = redlinesList.Count(r => r.RiskClassification == "ACCEPTABLE_COMPROMISE");

        return new ContractRedlineOverview(
            "Northstar Enterprise Holdings",
            redlinesList.Count,
            pending,
            compromises,
            avgRisk,
            redlinesList,
            approvalsList
        );
    }

    public ClauseRedlineRecord NegotiateClause(NegotiateClauseRequest request)
    {
        var clauseId = $"RED-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        double riskScore = request.ClauseCategory switch
        {
            "LIMITATION_OF_LIABILITY" => 45.0,
            "INDEMNIFICATION" => 35.0,
            "IP_OWNERSHIP" => 75.0,
            _ => 20.0
        };

        string riskClass = riskScore switch
        {
            > 60.0 => "UNACCEPTABLE_ESCALATE",
            > 25.0 => "ACCEPTABLE_COMPROMISE",
            _ => "STANDARD_ACCEPT"
        };

        string compromise = $"Balanced reciprocal compromise generated for category {request.ClauseCategory}";
        string standard = $"Standard enterprise master services agreement baseline clause for {request.ClauseCategory}";

        var record = new ClauseRedlineRecord(
            clauseId,
            request.ContractReference,
            request.ClauseCategory,
            request.ProposedRedlineText,
            standard,
            compromise,
            riskScore,
            riskClass,
            "COMPROMISE_OFFERED"
        );

        _redlines[clauseId] = record;
        return record;
    }

    public ContractDeviationApproval ApproveDeviation(ApproveDeviationRequest request)
    {
        var approvalId = $"APP-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var seal = $"SEAL-SHA256-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.ClauseId + request.ApprovedByLegalCounsel)))[..12]}";

        var approval = new ContractDeviationApproval(
            approvalId,
            request.ClauseId,
            request.ApprovedByLegalCounsel,
            request.BusinessJustification,
            DateTime.UtcNow,
            seal
        );

        _approvals.Add(approval);

        if (_redlines.TryGetValue(request.ClauseId, out var record))
        {
            _redlines[request.ClauseId] = record with { Status = "APPROVED" };
        }

        return approval;
    }
}
