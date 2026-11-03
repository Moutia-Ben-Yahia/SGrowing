using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Finance.TaxRdCredit;

public class TaxRdCreditLedgerEngine
{
    private readonly ConcurrentDictionary<string, QualifyingRdProject> _projects = new();
    private readonly ConcurrentBag<Form6765TaxCreditClaim> _claims = new();

    public TaxRdCreditLedgerEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var p1 = new QualifyingRdProject(
            "PRJ-RD-001",
            "Real-Time Telephony Dual-Channel Voice Neural Beamformer",
            "Synthesizing sub-10ms acoustic filter removing non-stationary noise without phase distortion",
            true,
            true,
            true,
            true,
            1450000.00m,
            380000.00m,
            1830000.00m,
            "FULLY_QUALIFIED"
        );

        var p2 = new QualifyingRdProject(
            "PRJ-RD-002",
            "Zero-Trust eBPF Kernel Packet Inspection & Micro-Segmenter",
            "Eliminating socket lookup contention in multi-tenant Linux 6.x kernels under 100Gbps line rates",
            true,
            true,
            true,
            true,
            980000.00m,
            240000.00m,
            1220000.00m,
            "FULLY_QUALIFIED"
        );

        _projects[p1.ProjectId] = p1;
        _projects[p2.ProjectId] = p2;

        _claims.Add(new Form6765TaxCreditClaim(
            "CLM-6765-2025",
            "2025",
            3050000.00m,
            1200000.00m,
            259000.00m, // 14% * (3.05M - 1.2M)
            250000.00m,
            DateTime.UtcNow.AddMonths(-2),
            "BINDER-SHA256-48BA001C92D"
        ));
    }

    public TaxRdCreditOverview GetOverview()
    {
        var projectsList = _projects.Values.ToList();
        var claimsList = _claims.ToList();

        decimal totalQre = projectsList.Sum(p => p.TotalQreUsd);
        decimal estimatedCredit = totalQre * 0.10m; // ~10% effective blended R&D credit
        decimal payrollOffset = Math.Min(500000.00m, estimatedCredit);

        return new TaxRdCreditOverview(
            "2026",
            projectsList.Count(p => p.FourPartTestStatus == "FULLY_QUALIFIED"),
            totalQre,
            estimatedCredit,
            payrollOffset,
            projectsList,
            claimsList
        );
    }

    public QualifyingRdProject QualifyProject(QualifyProjectRequest request)
    {
        var projId = $"PRJ-RD-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        decimal totalQre = request.QualifiedWagesUsd + request.QualifiedCloudComputeUsd;

        var project = new QualifyingRdProject(
            projId,
            request.ProjectName,
            request.TechnicalHypothesis,
            true,
            true,
            request.MeetsUncertaintyAndExperimentation,
            request.MeetsUncertaintyAndExperimentation,
            request.QualifiedWagesUsd,
            request.QualifiedCloudComputeUsd,
            totalQre,
            request.MeetsUncertaintyAndExperimentation ? "FULLY_QUALIFIED" : "PARTIALLY_QUALIFIED"
        );

        _projects[projId] = project;
        return project;
    }

    public Form6765TaxCreditClaim ComputeCredit(ComputeCreditRequest request)
    {
        var claimId = $"CLM-6765-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        decimal totalQre = _projects.Values.Where(p => p.FourPartTestStatus == "FULLY_QUALIFIED").Sum(p => p.TotalQreUsd);
        decimal excess = Math.Max(0.0m, totalQre - request.BaseAmountUsd);
        decimal ascCredit = excess * 0.14m;
        decimal payrollOffset = Math.Min(500000.00m, ascCredit);

        var binderHash = $"BINDER-SHA256-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(claimId + request.TaxYear)))[..12]}";

        var claim = new Form6765TaxCreditClaim(
            claimId,
            request.TaxYear,
            totalQre,
            request.BaseAmountUsd,
            ascCredit,
            payrollOffset,
            DateTime.UtcNow,
            binderHash
        );

        _claims.Add(claim);
        return claim;
    }
}
