using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Analytics.RevenueSimulation;

public interface IRevenueSimulationService
{
    IReadOnlyList<OpportunitySimulationInput> GetOpportunities(Guid tenantId);
    OpportunitySimulationInput AddOpportunity(Guid tenantId, AddOpportunityInputRequest request);
    MonteCarloRunResult RunSimulation(Guid tenantId, RunMonteCarloSimulationRequest? request = null);
    IReadOnlyList<DealSlippageRiskAlert> GetSlippageAlerts(Guid tenantId);
    RevenueSimulationOverviewReport GetOverviewReport(Guid tenantId, double? quotaTarget = null);
}

public class RevenueSimulationService : IRevenueSimulationService
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ConcurrentDictionary<Guid, List<OpportunitySimulationInput>> _tenantStore = new();
    private readonly object _lock = new();

    public RevenueSimulationService()
    {
        SeedDefaultTenant();
    }

    private void SeedDefaultTenant()
    {
        var baseDate = DateTime.UtcNow;
        var defaultDeals = new List<OpportunitySimulationInput>
        {
            new(
                Id: "opp-001",
                DealName: "Acme Corp Global ERP Modernization",
                AccountName: "Acme Holdings",
                Amount: 420000,
                Stage: PipelineStage.ContractNegotiation,
                Category: ForecastCategory.Commit,
                WinProbability: 0.85,
                TargetCloseDate: baseDate.AddDays(20),
                DealAgeDays: 45,
                SlippageRiskScore: 0.15,
                HistoricalStageConversionRate: 0.80,
                MinEstimatedValue: 380000,
                MostLikelyEstimatedValue: 420000,
                MaxEstimatedValue: 450000,
                Distribution: SimulationDistributionModel.Triangular
            ),
            new(
                Id: "opp-002",
                DealName: "FinTech Alpha Cloud Migration",
                AccountName: "Alpha Financial Tech",
                Amount: 310000,
                Stage: PipelineStage.ProposalPresented,
                Category: ForecastCategory.BestCase,
                WinProbability: 0.60,
                TargetCloseDate: baseDate.AddDays(35),
                DealAgeDays: 75,
                SlippageRiskScore: 0.58,
                HistoricalStageConversionRate: 0.45,
                MinEstimatedValue: 280000,
                MostLikelyEstimatedValue: 310000,
                MaxEstimatedValue: 350000,
                Distribution: SimulationDistributionModel.Triangular
            ),
            new(
                Id: "opp-003",
                DealName: "HealthPlus Omnichannel Engagement Hub",
                AccountName: "HealthPlus Systems",
                Amount: 260000,
                Stage: PipelineStage.ProposalPresented,
                Category: ForecastCategory.BestCase,
                WinProbability: 0.50,
                TargetCloseDate: baseDate.AddDays(40),
                DealAgeDays: 35,
                SlippageRiskScore: 0.28,
                HistoricalStageConversionRate: 0.55,
                MinEstimatedValue: 220000,
                MostLikelyEstimatedValue: 260000,
                MaxEstimatedValue: 300000,
                Distribution: SimulationDistributionModel.Normal
            ),
            new(
                Id: "opp-004",
                DealName: "Global Logistics IoT Track & Trace",
                AccountName: "TransLogistics Group",
                Amount: 550000,
                Stage: PipelineStage.ContractNegotiation,
                Category: ForecastCategory.Commit,
                WinProbability: 0.90,
                TargetCloseDate: baseDate.AddDays(15),
                DealAgeDays: 90,
                SlippageRiskScore: 0.62,
                HistoricalStageConversionRate: 0.85,
                MinEstimatedValue: 500000,
                MostLikelyEstimatedValue: 550000,
                MaxEstimatedValue: 600000,
                Distribution: SimulationDistributionModel.Triangular
            ),
            new(
                Id: "opp-005",
                DealName: "Nordic Retail AI Demand Forecasting",
                AccountName: "Nordic Retailers Corp",
                Amount: 180000,
                Stage: PipelineStage.Qualification,
                Category: ForecastCategory.Pipeline,
                WinProbability: 0.35,
                TargetCloseDate: baseDate.AddDays(60),
                DealAgeDays: 20,
                SlippageRiskScore: 0.20,
                HistoricalStageConversionRate: 0.40,
                MinEstimatedValue: 150000,
                MostLikelyEstimatedValue: 180000,
                MaxEstimatedValue: 210000,
                Distribution: SimulationDistributionModel.Triangular
            ),
            new(
                Id: "opp-006",
                DealName: "Sovereign Cloud Data Clean Room",
                AccountName: "GovTech Security Bureau",
                Amount: 480000,
                Stage: PipelineStage.ProposalPresented,
                Category: ForecastCategory.BestCase,
                WinProbability: 0.55,
                TargetCloseDate: baseDate.AddDays(45),
                DealAgeDays: 110,
                SlippageRiskScore: 0.74,
                HistoricalStageConversionRate: 0.30,
                MinEstimatedValue: 400000,
                MostLikelyEstimatedValue: 480000,
                MaxEstimatedValue: 520000,
                Distribution: SimulationDistributionModel.LogNormal
            ),
            new(
                Id: "opp-007",
                DealName: "Telecom 5G Billing Infrastructure",
                AccountName: "TeleConnect Global",
                Amount: 375000,
                Stage: PipelineStage.ContractNegotiation,
                Category: ForecastCategory.Commit,
                WinProbability: 0.88,
                TargetCloseDate: baseDate.AddDays(25),
                DealAgeDays: 50,
                SlippageRiskScore: 0.12,
                HistoricalStageConversionRate: 0.78,
                MinEstimatedValue: 350000,
                MostLikelyEstimatedValue: 375000,
                MaxEstimatedValue: 410000,
                Distribution: SimulationDistributionModel.Triangular
            ),
            new(
                Id: "opp-008",
                DealName: "Nexus Payments Real-time Settlement",
                AccountName: "Nexus Payments",
                Amount: 225000,
                Stage: PipelineStage.Discovery,
                Category: ForecastCategory.Pipeline,
                WinProbability: 0.25,
                TargetCloseDate: baseDate.AddDays(80),
                DealAgeDays: 15,
                SlippageRiskScore: 0.18,
                HistoricalStageConversionRate: 0.35,
                MinEstimatedValue: 190000,
                MostLikelyEstimatedValue: 225000,
                MaxEstimatedValue: 250000,
                Distribution: SimulationDistributionModel.Normal
            ),
            new(
                Id: "opp-009",
                DealName: "CyberShield Threat Hunting Expansion",
                AccountName: "CyberShield Labs",
                Amount: 140000,
                Stage: PipelineStage.ClosedWon,
                Category: ForecastCategory.Closed,
                WinProbability: 1.0,
                TargetCloseDate: baseDate.AddDays(-5),
                DealAgeDays: 30,
                SlippageRiskScore: 0.0,
                HistoricalStageConversionRate: 1.0,
                MinEstimatedValue: 140000,
                MostLikelyEstimatedValue: 140000,
                MaxEstimatedValue: 140000,
                Distribution: SimulationDistributionModel.Triangular
            ),
            new(
                Id: "opp-010",
                DealName: "AeroSpace Defense CAD Integration",
                AccountName: "AeroSystems Ltd",
                Amount: 620000,
                Stage: PipelineStage.Discovery,
                Category: ForecastCategory.Pipeline,
                WinProbability: 0.20,
                TargetCloseDate: baseDate.AddDays(90),
                DealAgeDays: 85,
                SlippageRiskScore: 0.82,
                HistoricalStageConversionRate: 0.22,
                MinEstimatedValue: 500000,
                MostLikelyEstimatedValue: 620000,
                MaxEstimatedValue: 750000,
                Distribution: SimulationDistributionModel.Triangular
            )
        };

        _tenantStore[DefaultTenantId] = defaultDeals;
    }

    private List<OpportunitySimulationInput> GetTenantList(Guid tenantId)
    {
        return _tenantStore.GetOrAdd(tenantId, id =>
        {
            if (id == DefaultTenantId)
            {
                SeedDefaultTenant();
                return _tenantStore[DefaultTenantId];
            }
            return new List<OpportunitySimulationInput>();
        });
    }

    public IReadOnlyList<OpportunitySimulationInput> GetOpportunities(Guid tenantId)
    {
        lock (_lock)
        {
            return GetTenantList(tenantId).ToList();
        }
    }

    public OpportunitySimulationInput AddOpportunity(Guid tenantId, AddOpportunityInputRequest request)
    {
        lock (_lock)
        {
            var list = GetTenantList(tenantId);
            var id = $"opp-{Guid.NewGuid():N}"[..11];
            var minVal = request.MinEstimatedValue > 0 ? request.MinEstimatedValue : request.Amount * 0.85;
            var likelyVal = request.MostLikelyEstimatedValue > 0 ? request.MostLikelyEstimatedValue : request.Amount;
            var maxVal = request.MaxEstimatedValue > 0 ? request.MaxEstimatedValue : request.Amount * 1.15;

            var slippageScore = request.DealAgeDays > 60 ? 0.65 : (request.DealAgeDays > 30 ? 0.35 : 0.15);
            var convRate = request.Stage switch
            {
                PipelineStage.Discovery => 0.25,
                PipelineStage.Qualification => 0.40,
                PipelineStage.ProposalPresented => 0.60,
                PipelineStage.ContractNegotiation => 0.85,
                PipelineStage.ClosedWon => 1.0,
                PipelineStage.ClosedLost => 0.0,
                _ => 0.5
            };

            var targetClose = request.TargetCloseDate ?? DateTime.UtcNow.AddDays(30);

            var item = new OpportunitySimulationInput(
                Id: id,
                DealName: request.DealName,
                AccountName: request.AccountName,
                Amount: request.Amount,
                Stage: request.Stage,
                Category: request.Category,
                WinProbability: request.WinProbability,
                TargetCloseDate: targetClose,
                DealAgeDays: request.DealAgeDays,
                SlippageRiskScore: slippageScore,
                HistoricalStageConversionRate: convRate,
                MinEstimatedValue: minVal,
                MostLikelyEstimatedValue: likelyVal,
                MaxEstimatedValue: maxVal,
                Distribution: request.Distribution
            );

            list.Add(item);
            return item;
        }
    }

    public MonteCarloRunResult RunSimulation(Guid tenantId, RunMonteCarloSimulationRequest? request = null)
    {
        var req = request ?? new RunMonteCarloSimulationRequest();
        var iterations = Math.Clamp(req.Iterations, 100, 50000);
        var quotaTarget = req.QuotaTarget > 0 ? req.QuotaTarget : 1500000;
        var volatility = Math.Clamp(req.MarketVolatilityIndex, 0.5, 2.0);

        List<OpportunitySimulationInput> deals;
        if (req.CustomOpportunities != null && req.CustomOpportunities.Count > 0)
        {
            deals = req.CustomOpportunities;
        }
        else
        {
            lock (_lock)
            {
                deals = GetTenantList(tenantId).ToList();
            }
        }

        if (deals.Count == 0)
        {
            return new MonteCarloRunResult(
                Iterations: iterations,
                P10: 0,
                P50: 0,
                P90: 0,
                Mean: 0,
                StandardDeviation: 0,
                TargetQuota: quotaTarget,
                QuotaAttainmentProbability: 0,
                ExpectedValue: 0,
                PipelineTotalValue: 0,
                CommitTotalValue: 0,
                BestCaseTotalValue: 0,
                PercentileDistribution: Array.Empty<double>(),
                HistogramBins: Array.Empty<SimulationHistogramBin>(),
                SimulatedAtUtc: DateTime.UtcNow
            );
        }

        var outcomes = new double[iterations];
        var rng = Random.Shared;

        for (int i = 0; i < iterations; i++)
        {
            double simRevenue = 0.0;
            foreach (var deal in deals)
            {
                if (deal.Stage == PipelineStage.ClosedLost)
                {
                    continue;
                }

                if (deal.Stage == PipelineStage.ClosedWon)
                {
                    simRevenue += SampleOpportunityValue(deal, req.DefaultDistribution, volatility, rng);
                    continue;
                }

                // Calculate slippage-adjusted win probability
                var effectiveWinProb = deal.WinProbability * Math.Clamp(1.0 - (deal.SlippageRiskScore * 0.25), 0.05, 0.98);
                if (rng.NextDouble() <= effectiveWinProb)
                {
                    simRevenue += SampleOpportunityValue(deal, req.DefaultDistribution, volatility, rng);
                }
            }
            outcomes[i] = simRevenue;
        }

        Array.Sort(outcomes);

        var p10Index = Math.Clamp((int)(iterations * 0.10), 0, iterations - 1);
        var p50Index = Math.Clamp((int)(iterations * 0.50), 0, iterations - 1);
        var p90Index = Math.Clamp((int)(iterations * 0.90), 0, iterations - 1);

        var p10 = Math.Round(outcomes[p10Index], 2);
        var p50 = Math.Round(outcomes[p50Index], 2);
        var p90 = Math.Round(outcomes[p90Index], 2);

        var mean = Math.Round(outcomes.Average(), 2);
        var sumSquares = outcomes.Sum(val => Math.Pow(val - mean, 2));
        var stdDev = Math.Round(Math.Sqrt(sumSquares / iterations), 2);

        var quotaHits = outcomes.Count(x => x >= quotaTarget);
        var quotaProb = Math.Round(((double)quotaHits / iterations) * 100.0, 2);

        var expectedValue = Math.Round(deals.Where(d => d.Stage != PipelineStage.ClosedLost)
            .Sum(d => d.Stage == PipelineStage.ClosedWon ? d.Amount : d.Amount * d.WinProbability), 2);

        var pipelineTotal = Math.Round(deals.Sum(d => d.Amount), 2);
        var commitTotal = Math.Round(deals.Where(d => d.Category == ForecastCategory.Commit || d.Category == ForecastCategory.Closed).Sum(d => d.Amount), 2);
        var bestCaseTotal = Math.Round(deals.Where(d => d.Category == ForecastCategory.BestCase || d.Category == ForecastCategory.Commit || d.Category == ForecastCategory.Closed).Sum(d => d.Amount), 2);

        // Percentiles: 10, 20, 30, 40, 50, 60, 70, 80, 90, 99
        var percentilePoints = new[] { 0.10, 0.20, 0.30, 0.40, 0.50, 0.60, 0.70, 0.80, 0.90, 0.99 };
        var percentileDist = percentilePoints
            .Select(pct => Math.Round(outcomes[Math.Clamp((int)(iterations * pct), 0, iterations - 1)], 2))
            .ToList();

        // 10 Histogram bins
        var minVal = outcomes[0];
        var maxVal = outcomes[^1];
        var span = maxVal - minVal;
        var binCounts = new int[10];

        if (span <= 0.001)
        {
            binCounts[0] = iterations;
        }
        else
        {
            foreach (var x in outcomes)
            {
                var binIdx = (int)((x - minVal) / span * 10.0);
                if (binIdx >= 10) binIdx = 9;
                if (binIdx < 0) binIdx = 0;
                binCounts[binIdx]++;
            }
        }

        var bins = new List<SimulationHistogramBin>();
        var binStep = span > 0 ? span / 10.0 : 1.0;

        for (int b = 0; b < 10; b++)
        {
            var rangeMin = Math.Round(minVal + (b * binStep), 2);
            var rangeMax = Math.Round(b == 9 ? maxVal : minVal + ((b + 1) * binStep), 2);
            var count = binCounts[b];
            var densityPct = Math.Round(((double)count / iterations) * 100.0, 2);

            bins.Add(new SimulationHistogramBin(rangeMin, rangeMax, count, densityPct));
        }

        return new MonteCarloRunResult(
            Iterations: iterations,
            P10: p10,
            P50: p50,
            P90: p90,
            Mean: mean,
            StandardDeviation: stdDev,
            TargetQuota: quotaTarget,
            QuotaAttainmentProbability: quotaProb,
            ExpectedValue: expectedValue,
            PipelineTotalValue: pipelineTotal,
            CommitTotalValue: commitTotal,
            BestCaseTotalValue: bestCaseTotal,
            PercentileDistribution: percentileDist,
            HistogramBins: bins,
            SimulatedAtUtc: DateTime.UtcNow
        );
    }

    private static double SampleOpportunityValue(OpportunitySimulationInput deal, SimulationDistributionModel defaultModel, double volatility, Random rng)
    {
        var model = deal.Distribution != SimulationDistributionModel.Triangular ? deal.Distribution : defaultModel;
        var min = deal.MinEstimatedValue > 0 ? deal.MinEstimatedValue : deal.Amount * 0.85;
        var likely = deal.MostLikelyEstimatedValue > 0 ? deal.MostLikelyEstimatedValue : deal.Amount;
        var max = deal.MaxEstimatedValue > 0 ? deal.MaxEstimatedValue : deal.Amount * 1.15;

        if (max < min) (min, max) = (max, min);
        if (likely < min) likely = min;
        if (likely > max) likely = max;

        double sample = model switch
        {
            SimulationDistributionModel.Normal => SampleNormal(likely, Math.Max(1.0, (max - min) / 6.0), rng),
            SimulationDistributionModel.LogNormal => SampleLogNormal(likely, rng),
            _ => SampleTriangular(min, likely, max, rng)
        };

        return Math.Max(0, sample * volatility);
    }

    private static double SampleTriangular(double min, double likely, double max, Random rng)
    {
        if (Math.Abs(max - min) < 0.001) return likely;
        double fc = (likely - min) / (max - min);
        double u = rng.NextDouble();
        return u < fc
            ? min + Math.Sqrt(u * (max - min) * (likely - min))
            : max - Math.Sqrt((1.0 - u) * (max - min) * (max - likely));
    }

    private static double SampleNormal(double mean, double stdDev, Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return Math.Max(0, mean + (randStdNormal * stdDev));
    }

    private static double SampleLogNormal(double medianVal, Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        const double sigma = 0.20;
        double mu = Math.Log(Math.Max(1.0, medianVal)) - (sigma * sigma / 2.0);
        return Math.Max(0, Math.Exp(mu + (sigma * randStdNormal)));
    }

    public IReadOnlyList<DealSlippageRiskAlert> GetSlippageAlerts(Guid tenantId)
    {
        IReadOnlyList<OpportunitySimulationInput> deals;
        lock (_lock)
        {
            deals = GetTenantList(tenantId).ToList();
        }

        var alerts = new List<DealSlippageRiskAlert>();

        foreach (var deal in deals)
        {
            if (deal.Stage is PipelineStage.ClosedWon or PipelineStage.ClosedLost)
            {
                continue;
            }

            var daysStalled = Math.Max(0, deal.DealAgeDays - 30);
            var isSlipping = deal.SlippageRiskScore >= 0.40 || deal.DealAgeDays >= 60 || deal.HistoricalStageConversionRate <= 0.35;

            if (isSlipping)
            {
                var riskLevel = deal.SlippageRiskScore >= 0.70 ? "Critical"
                    : (deal.SlippageRiskScore >= 0.50 ? "High"
                    : (deal.SlippageRiskScore >= 0.30 ? "Medium" : "Low"));

                var reason = deal.DealAgeDays > 75
                    ? $"Deal in {deal.Stage} for {deal.DealAgeDays} days (exceeds stage benchmark by {deal.DealAgeDays - 45} days)."
                    : (deal.HistoricalStageConversionRate <= 0.35
                        ? $"Historical stage conversion of {deal.HistoricalStageConversionRate * 100:0}% is below viable threshold."
                        : $"Calculated slippage velocity score ({deal.SlippageRiskScore:0.00}) signals stalled stakeholder momentum.");

                var intervention = deal.Stage switch
                {
                    PipelineStage.Discovery => "Conduct ICP qualification reset and re-engage business executive sponsor.",
                    PipelineStage.Qualification => "Trigger automated technical requirements survey & confirm allocated budget.",
                    PipelineStage.ProposalPresented => "Organize Mutual Action Plan (MAP) sync with VP-level economic buyer.",
                    PipelineStage.ContractNegotiation => "Initiate redline legal acceleration desk and offer limited-time closing concession.",
                    _ => "Review opportunity velocity in weekly pipeline triage."
                };

                alerts.Add(new DealSlippageRiskAlert(
                    OpportunityId: deal.Id,
                    DealName: deal.DealName,
                    AccountName: deal.AccountName,
                    Amount: deal.Amount,
                    Stage: deal.Stage,
                    SlippageProbability: Math.Round(deal.SlippageRiskScore, 2),
                    RiskLevel: riskLevel,
                    RiskReason: reason,
                    RecommendedIntervention: intervention,
                    DaysStalled: daysStalled
                ));
            }
        }

        return alerts.OrderByDescending(a => a.SlippageProbability).ToList();
    }

    public RevenueSimulationOverviewReport GetOverviewReport(Guid tenantId, double? quotaTarget = null)
    {
        var target = quotaTarget ?? 1500000;
        var opportunities = GetOpportunities(tenantId);
        var slippageAlerts = GetSlippageAlerts(tenantId);

        var baselineRun = RunSimulation(tenantId, new RunMonteCarloSimulationRequest(
            Iterations: 5000,
            QuotaTarget: target,
            DefaultDistribution: SimulationDistributionModel.Triangular
        ));

        var totalPipeline = opportunities.Sum(o => o.Amount);
        var weightedPipeline = opportunities
            .Where(o => o.Stage != PipelineStage.ClosedLost)
            .Sum(o => o.Stage == PipelineStage.ClosedWon ? o.Amount : o.Amount * o.WinProbability);

        var stageBreakdown = Enum.GetValues<PipelineStage>()
            .ToDictionary(stage => stage, stage => Math.Round(opportunities.Where(o => o.Stage == stage).Sum(o => o.Amount), 2));

        var categoryBreakdown = Enum.GetValues<ForecastCategory>()
            .ToDictionary(cat => cat, cat => Math.Round(opportunities.Where(o => o.Category == cat).Sum(o => o.Amount), 2));

        var topDeals = opportunities
            .OrderByDescending(o => o.Amount)
            .Take(8)
            .ToList();

        return new RevenueSimulationOverviewReport(
            TenantId: tenantId.ToString(),
            GeneratedAtUtc: DateTime.UtcNow,
            TotalPipelineValue: Math.Round(totalPipeline, 2),
            WeightedPipelineValue: Math.Round(weightedPipeline, 2),
            TotalOpportunitiesCount: opportunities.Count,
            BaselineSimulation: baselineRun,
            TopOpportunities: topDeals,
            CriticalSlippageAlerts: slippageAlerts,
            StageBreakdown: stageBreakdown,
            CategoryBreakdown: categoryBreakdown
        );
    }
}
