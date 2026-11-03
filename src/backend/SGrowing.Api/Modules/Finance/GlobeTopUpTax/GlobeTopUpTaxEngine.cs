using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Finance.GlobeTopUpTax;

public interface IGlobeTopUpTaxEngine
{
    JurisdictionTaxProfile AssessJurisdiction(AssessJurisdictionTaxRequest request);
    IReadOnlyList<JurisdictionTaxProfile> GetJurisdictions();
    SafeHarborEvaluation EvaluateSafeHarbor(string jurisdictionCode);
    TopUpTaxCalculation CalculateTopUpTax(string jurisdictionCode);
    GlobeTaxLedgerSummary GetLedgerSummary();
}

public class GlobeTopUpTaxEngine : IGlobeTopUpTaxEngine
{
    private readonly ConcurrentDictionary<string, JurisdictionTaxProfile> _profiles = new();
    private readonly ConcurrentDictionary<string, TopUpTaxCalculation> _calculations = new();

    public GlobeTopUpTaxEngine()
    {
        SeedInitialJurisdictions();
    }

    private void SeedInitialJurisdictions()
    {
        // Bermuda: 0% statutory rate, low covered tax -> fails safe harbor, incurs top-up
        var bm = new JurisdictionTaxProfile(
            JurisdictionCode: "BM",
            JurisdictionName: "Bermuda",
            ConsolidatedRevenueEur: 45000000m,
            GlobeNetIncomeEur: 22000000m,
            AdjustedCoveredTaxesEur: 440000m, // ~2.0% ETR
            EligiblePayrollCostsEur: 1500000m,
            EligibleTangibleAssetsEur: 800000m,
            StatutoryRatePercentage: 0.0m,
            FiscalYearEndUtc: DateTime.UtcNow.AddMonths(1)
        );

        // Ireland: 12.5% statutory rate, high tangible/payroll -> evaluated for QDMTT
        var ie = new JurisdictionTaxProfile(
            JurisdictionCode: "IE",
            JurisdictionName: "Ireland",
            ConsolidatedRevenueEur: 320000000m,
            GlobeNetIncomeEur: 98000000m,
            AdjustedCoveredTaxesEur: 12250000m, // 12.5% ETR
            EligiblePayrollCostsEur: 28000000m,
            EligibleTangibleAssetsEur: 45000000m,
            StatutoryRatePercentage: 12.5m,
            FiscalYearEndUtc: DateTime.UtcNow.AddMonths(1)
        );

        // Germany: 30% statutory rate -> passes simplified ETR test (> 15%)
        var de = new JurisdictionTaxProfile(
            JurisdictionCode: "DE",
            JurisdictionName: "Germany",
            ConsolidatedRevenueEur: 180000000m,
            GlobeNetIncomeEur: 35000000m,
            AdjustedCoveredTaxesEur: 10500000m, // 30.0% ETR
            EligiblePayrollCostsEur: 15000000m,
            EligibleTangibleAssetsEur: 20000000m,
            StatutoryRatePercentage: 30.0m,
            FiscalYearEndUtc: DateTime.UtcNow.AddMonths(1)
        );

        _profiles[bm.JurisdictionCode] = bm;
        _profiles[ie.JurisdictionCode] = ie;
        _profiles[de.JurisdictionCode] = de;
    }

    public JurisdictionTaxProfile AssessJurisdiction(AssessJurisdictionTaxRequest request)
    {
        var profile = new JurisdictionTaxProfile(
            JurisdictionCode: request.JurisdictionCode.ToUpperInvariant(),
            JurisdictionName: request.JurisdictionName,
            ConsolidatedRevenueEur: request.ConsolidatedRevenueEur,
            GlobeNetIncomeEur: request.GlobeNetIncomeEur,
            AdjustedCoveredTaxesEur: request.AdjustedCoveredTaxesEur,
            EligiblePayrollCostsEur: request.EligiblePayrollCostsEur,
            EligibleTangibleAssetsEur: request.EligibleTangibleAssetsEur,
            StatutoryRatePercentage: request.StatutoryRatePercentage,
            FiscalYearEndUtc: DateTime.UtcNow.AddYears(1)
        );

        _profiles[profile.JurisdictionCode] = profile;
        return profile;
    }

    public IReadOnlyList<JurisdictionTaxProfile> GetJurisdictions()
    {
        return _profiles.Values.OrderBy(j => j.JurisdictionName).ToList();
    }

    public SafeHarborEvaluation EvaluateSafeHarbor(string jurisdictionCode)
    {
        var code = jurisdictionCode.ToUpperInvariant();
        if (!_profiles.TryGetValue(code, out var profile))
        {
            throw new KeyNotFoundException($"Jurisdiction {code} not registered.");
        }

        // De Minimis: Revenue < €10M AND Profit < €1M
        bool deMinimis = profile.ConsolidatedRevenueEur < 10000000m && profile.GlobeNetIncomeEur < 1000000m;

        // Simplified ETR: Covered Taxes / Net Income >= 15.0%
        decimal etr = profile.GlobeNetIncomeEur > 0
            ? Math.Round(profile.AdjustedCoveredTaxesEur / profile.GlobeNetIncomeEur * 100m, 2)
            : 0m;
        bool simplifiedEtr = etr >= 15.0m;

        // Routine profits test: SBIE carveout (approx 5% payroll + 5% tangible assets)
        decimal sbie = (profile.EligiblePayrollCostsEur * 0.05m) + (profile.EligibleTangibleAssetsEur * 0.05m);
        bool routineProfits = profile.GlobeNetIncomeEur <= sbie;

        SafeHarborStatus status = SafeHarborStatus.IneligibleFailsAllTests;
        string rationale = "Fails all Transitional CbCR Safe Harbor tests; Top-Up Tax calculation required.";

        if (simplifiedEtr)
        {
            status = SafeHarborStatus.EligibleSimplifiedEtr;
            rationale = $"Passes Simplified ETR test ({etr}% >= 15.0%); Top-Up Tax deemed zero.";
        }
        else if (deMinimis)
        {
            status = SafeHarborStatus.EligibleDeMinimis;
            rationale = "Passes De Minimis test (Revenue < €10M & Profit < €1M); Top-Up Tax deemed zero.";
        }
        else if (routineProfits)
        {
            status = SafeHarborStatus.EligibleRoutineProfits;
            rationale = "Passes Routine Profits test (Income <= Substance-Based carve-out); Top-Up Tax deemed zero.";
        }

        return new SafeHarborEvaluation(
            JurisdictionCode: code,
            PassesDeMinimisTest: deMinimis,
            PassesSimplifiedEtrTest: simplifiedEtr,
            PassesRoutineProfitsTest: routineProfits,
            OverallStatus: status,
            SafeHarborRationale: rationale,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }

    public TopUpTaxCalculation CalculateTopUpTax(string jurisdictionCode)
    {
        var code = jurisdictionCode.ToUpperInvariant();
        if (!_profiles.TryGetValue(code, out var profile))
        {
            throw new KeyNotFoundException($"Jurisdiction {code} not registered.");
        }

        decimal etr = profile.GlobeNetIncomeEur > 0
            ? Math.Round(profile.AdjustedCoveredTaxesEur / profile.GlobeNetIncomeEur * 100m, 2)
            : 0m;

        decimal topUpPct = Math.Max(0m, 15.0m - etr);

        // Substance-Based Income Exclusion (SBIE): 9.6% payroll and 7.6% tangible assets (transitional OECD rates)
        decimal sbie = (profile.EligiblePayrollCostsEur * 0.096m) + (profile.EligibleTangibleAssetsEur * 0.076m);
        decimal excessProfit = Math.Max(0m, profile.GlobeNetIncomeEur - sbie);

        decimal topUpEur = Math.Round(excessProfit * (topUpPct / 100m), 2);

        // QDMTT absorbs domestic liability if enacted, remainder allocated to IIR
        decimal qdmtt = topUpEur > 0 && profile.StatutoryRatePercentage > 0 ? topUpEur * 0.8m : 0m;
        decimal iir = topUpEur - qdmtt;

        var calculation = new TopUpTaxCalculation(
            CalculationId: $"TOPUP-{code}-{TaxSeed()}",
            JurisdictionCode: code,
            EffectiveTaxRatePercentage: etr,
            MinimumRatePercentage: 15.0m,
            TopUpTaxPercentage: topUpPct,
            SubstanceBasedCarveoutEur: sbie,
            ExcessProfitEur: excessProfit,
            JurisdictionalTopUpTaxEur: topUpEur,
            AllocatedQdmttLiabilityEur: qdmtt,
            AllocatedIirLiabilityEur: iir,
            CalculatedAtUtc: DateTime.UtcNow
        );

        _calculations[code] = calculation;
        return calculation;
    }

    public GlobeTaxLedgerSummary GetLedgerSummary()
    {
        int total = _profiles.Count;
        int safeHarbor = 0;
        int subject = 0;
        decimal totalTopUp = 0m;
        decimal totalCovered = _profiles.Values.Sum(p => p.AdjustedCoveredTaxesEur);

        foreach (var p in _profiles.Values)
        {
            var sh = EvaluateSafeHarbor(p.JurisdictionCode);
            if (sh.OverallStatus != SafeHarborStatus.IneligibleFailsAllTests)
            {
                safeHarbor++;
            }
            else
            {
                subject++;
                var calc = CalculateTopUpTax(p.JurisdictionCode);
                totalTopUp += calc.JurisdictionalTopUpTaxEur;
            }
        }

        return new GlobeTaxLedgerSummary(
            TotalJurisdictionsEvaluated: total,
            SafeHarborExemptJurisdictions: safeHarbor,
            JurisdictionsSubjectToTopUp: subject,
            TotalGlobalTopUpTaxLiabilityEur: totalTopUp,
            TotalCoveredTaxesPaidEur: totalCovered,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    private static int _seedCounter = 6000;
    private static int TaxSeed() => Interlocked.Increment(ref _seedCounter);
}
