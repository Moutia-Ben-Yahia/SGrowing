using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Finance.SovereignDst;

public class SovereignDstLedgerEngine
{
    private readonly ConcurrentDictionary<string, LocalDstLiabilityRecord> _dstRecords = new();
    private readonly ConcurrentBag<OecdGlobePillar2Assessment> _assessments = new();

    public SovereignDstLedgerEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var d1 = new LocalDstLiabilityRecord(
            "DST-UK-001",
            "UK_DST_2PCT",
            42000000.00m,
            2.0m,
            840000.00m,
            true,
            true,
            "ACCRUED"
        );

        var d2 = new LocalDstLiabilityRecord(
            "DST-FR-002",
            "FR_DST_3PCT",
            36000000.00m,
            3.0m,
            1080000.00m,
            true,
            true,
            "ACCRUED"
        );

        _dstRecords[d1.DstRecordId] = d1;
        _dstRecords[d2.DstRecordId] = d2;

        _assessments.Add(new OecdGlobePillar2Assessment(
            "GLOBE-2025-01",
            "2025",
            "IE_DUBLIN",
            85000000.00m,
            9775000.00m,
            11.5,
            15.0,
            2975000.00m, // (15.0 - 11.5)% = 3.5% * 85M
            "CLR-PILLAR2-SHA256-48BA11C"
        ));
    }

    public SovereignDstOverview GetOverview()
    {
        var dstList = _dstRecords.Values.ToList();
        var assessList = _assessments.ToList();

        decimal totalDst = dstList.Sum(d => d.CalculatedTaxLiabilityEur);
        decimal totalPillar2 = assessList.Sum(a => a.TopUpTaxLiabilityEur);

        return new SovereignDstOverview(
            152, // 152 countries tracked
            totalDst,
            totalPillar2,
            14.2,
            dstList,
            assessList
        );
    }

    public LocalDstLiabilityRecord ComputeDst(ComputeDstRequest request)
    {
        var dstId = $"DST-{request.JurisdictionCode[..2]}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        decimal liability = request.LocalDigitalTurnoverEur * (request.StatutoryDstRatePct / 100.0m);

        var record = new LocalDstLiabilityRecord(
            dstId,
            request.JurisdictionCode,
            request.LocalDigitalTurnoverEur,
            request.StatutoryDstRatePct,
            liability,
            true,
            true,
            "ASSESSED"
        );

        _dstRecords[dstId] = record;
        return record;
    }

    public OecdGlobePillar2Assessment ComputeGlobeTax(ComputeGlobeTaxRequest request)
    {
        var assessId = $"GLOBE-{request.FiscalYear}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        double etr = request.FinancialNetIncomeEur > 0
            ? Math.Round((double)(request.CoveredTaxesPaidEur / request.FinancialNetIncomeEur) * 100.0, 2)
            : 0;

        double topUpPct = Math.Max(0.0, 15.0 - etr);
        decimal topUpLiability = request.FinancialNetIncomeEur * ((decimal)topUpPct / 100.0m);

        var tokenHash = $"CLR-PILLAR2-SHA256-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(assessId + request.Jurisdiction)))[..12]}";

        var assessment = new OecdGlobePillar2Assessment(
            assessId,
            request.FiscalYear,
            request.Jurisdiction,
            request.FinancialNetIncomeEur,
            request.CoveredTaxesPaidEur,
            etr,
            15.0,
            topUpLiability,
            tokenHash
        );

        _assessments.Add(assessment);
        return assessment;
    }
}
