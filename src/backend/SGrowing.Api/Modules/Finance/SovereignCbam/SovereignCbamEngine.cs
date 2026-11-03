using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.SovereignCbam;

public sealed class SovereignCbamEngine
{
    private readonly ConcurrentDictionary<string, CbamDeclarationRecord> _declarations = new();
    private readonly ConcurrentDictionary<string, EmissionsFactorRecord> _factors = new();

    public SovereignCbamEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var f1 = new EmissionsFactorRecord(
            "FCT-STL-CN",
            "CN",
            "STEEL",
            2.15m,
            85.50m,
            DateTime.UtcNow.AddYears(1)
        );

        var f2 = new EmissionsFactorRecord(
            "FCT-ALU-IN",
            "IN",
            "ALUMINIUM",
            4.80m,
            85.50m,
            DateTime.UtcNow.AddYears(1)
        );

        _factors[f1.FactorId] = f1;
        _factors[f2.FactorId] = f2;

        var d1 = new CbamDeclarationRecord(
            "CBAM-DEC-2026-001",
            "NL849302199B01",
            "CN",
            "STEEL",
            500.00m,
            950.00m,
            125.00m,
            91912.50m,
            "0x9A8B7C6D5E4F3A2B1C0D",
            "CLEARED_BY_CUSTOMS"
        );

        var d2 = new CbamDeclarationRecord(
            "CBAM-DEC-2026-002",
            "FR39849201928",
            "IN",
            "ALUMINIUM",
            150.00m,
            640.00m,
            80.00m,
            61560.00m,
            "0x11223344556677889900",
            "OFFSET_SETTLED"
        );

        _declarations[d1.DeclarationId] = d1;
        _declarations[d2.DeclarationId] = d2;
    }

    public CbamOverview GetOverview()
    {
        var decList = _declarations.Values.ToList();
        var factorList = _factors.Values.ToList();
        var totalEmissions = decList.Sum(d => d.EmbeddedDirectEmissionsTco2e + d.EmbeddedIndirectEmissionsTco2e);
        var totalTaxLiability = decList.Sum(d => d.StatutoryCarbonTaxLiabilityEur);
        var totalOffsetSettled = decList.Where(d => d.Status == "OFFSET_SETTLED").Sum(d => d.StatutoryCarbonTaxLiabilityEur);

        return new CbamOverview(
            decList.Count,
            totalEmissions,
            totalTaxLiability,
            totalOffsetSettled,
            decList,
            factorList
        );
    }

    public CbamDeclarationRecord GenerateDeclaration(GenerateCbamDeclarationRequest request)
    {
        var decId = $"CBAM-DEC-{Guid.NewGuid():N}"[..17].ToUpperInvariant();
        var factor = _factors.Values.FirstOrDefault(f => f.CountryIsoCode == request.CountryOfOrigin && f.GoodsCategory == request.GoodsCategory);
        var benchmark = factor?.BenchmarkFactorTco2ePerTon ?? 2.20m;
        var carbonPrice = factor?.SovereignCarbonPriceEurPerTco2e ?? 85.00m;

        var direct = request.NetMassMetricTons * benchmark;
        var indirect = request.NetMassMetricTons * 0.25m;
        var totalTax = (direct + indirect) * carbonPrice;
        var digest = $"0x{Guid.NewGuid():N}{Guid.NewGuid():N}";

        var record = new CbamDeclarationRecord(
            decId,
            request.ImporterTaxId,
            request.CountryOfOrigin,
            request.GoodsCategory,
            request.NetMassMetricTons,
            direct,
            indirect,
            totalTax,
            digest,
            "CLEARED_BY_CUSTOMS"
        );

        _declarations[decId] = record;
        return record;
    }

    public CbamDeclarationRecord OffsetEmissions(OffsetEmissionsRequest request)
    {
        if (_declarations.TryGetValue(request.DeclarationId, out var existing))
        {
            var updated = existing with { Status = "OFFSET_SETTLED" };
            _declarations[request.DeclarationId] = updated;
            return updated;
        }

        throw new KeyNotFoundException($"Declaration with ID {request.DeclarationId} not found");
    }
}
