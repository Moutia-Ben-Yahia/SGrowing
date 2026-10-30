using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Commerce;

#region Domain Models & DTOs

public sealed class PricingArm
{
    public required string ArmId { get; init; }
    public decimal Price { get; init; }
    public double Alpha { get; set; }
    public double Beta { get; set; }
    public long Impressions { get; set; }
    public long Conversions { get; set; }
    public decimal TotalRevenue { get; set; }

    public double ConversionRate =>
        Impressions > 0 
            ? (double)Conversions / Impressions 
            : (Alpha + Beta > 0 ? Alpha / (Alpha + Beta) : 0.0);

    public decimal ExpectedValue =>
        Price * (decimal)(Alpha + Beta > 0 ? Alpha / (Alpha + Beta) : 0.0);

    public PricingArm Clone() => new()
    {
        ArmId = ArmId,
        Price = Price,
        Alpha = Alpha,
        Beta = Beta,
        Impressions = Impressions,
        Conversions = Conversions,
        TotalRevenue = TotalRevenue
    };
}

public sealed class PricingExperiment
{
    public required string Sku { get; init; }
    public required string ProductName { get; init; }
    public required string Category { get; init; }
    public decimal BaseCostCogs { get; init; }
    public decimal ShippingAllocated { get; init; }
    public decimal MinimumMarginHurdle { get; init; }
    public decimal StandardPrice { get; init; }
    public int CurrentStock { get; set; }
    public decimal DailyRunRate { get; set; }
    public decimal MaxMarkdownPct { get; init; }
    public decimal MarkdownStep { get; init; }
    public List<PricingArm> Arms { get; init; } = [];
    public DateTime CreatedAtUtc { get; init; }
    public DateTime LastUpdatedUtc { get; set; }

    public decimal HardMarginFloor =>
        Math.Round((BaseCostCogs * (1.0m + MinimumMarginHurdle)) + ShippingAllocated, 2);

    public decimal DaysOfSupply =>
        DailyRunRate > 0 
            ? Math.Round((decimal)CurrentStock / DailyRunRate, 2)
            : (CurrentStock > 0 ? 9999m : 0m);

    public PricingExperiment Clone() => new()
    {
        Sku = Sku,
        ProductName = ProductName,
        Category = Category,
        BaseCostCogs = BaseCostCogs,
        ShippingAllocated = ShippingAllocated,
        MinimumMarginHurdle = MinimumMarginHurdle,
        StandardPrice = StandardPrice,
        CurrentStock = CurrentStock,
        DailyRunRate = DailyRunRate,
        MaxMarkdownPct = MaxMarkdownPct,
        MarkdownStep = MarkdownStep,
        Arms = Arms.Select(a => a.Clone()).ToList(),
        CreatedAtUtc = CreatedAtUtc,
        LastUpdatedUtc = LastUpdatedUtc
    };
}

public record ArmSelectionResult(
    string Sku,
    string SelectedArmId,
    decimal ProposedPrice,
    decimal FinalPrice,
    bool FloorClampApplied,
    decimal FloorPrice,
    double SampledTheta,
    decimal SampledExpectedRevenue,
    Dictionary<string, double> AllSampledThetas,
    DateTime TimestampUtc);

public record PriceElasticityReport(
    string Sku,
    decimal BaselinePrice,
    decimal TargetPrice,
    decimal BaselineQuantity,
    decimal TargetQuantity,
    double ElasticityCoefficient,
    string ElasticityClassification,
    string RecommendedStrategy,
    string Rationale,
    DateTime CalculatedAtUtc);

public record MarginFloorCheckResult(
    string Sku,
    decimal CandidatePrice,
    decimal FloorPrice,
    decimal BaseCostCogs,
    decimal MinimumMarginHurdle,
    decimal ShippingAllocated,
    decimal EffectiveMarginPercent,
    bool FloorClampApplied,
    decimal EffectivePrice,
    string GuardrailStatus);

public record InventoryLiquidationSchedule(
    string Sku,
    string ProductName,
    int CurrentStock,
    decimal DailyRunRate,
    decimal DaysOfSupply,
    decimal StandardPrice,
    decimal HardMarginFloor,
    decimal RecommendedMarkdownPct,
    decimal RawDiscountedPrice,
    decimal FinalPrice,
    bool LiquidationTriggered,
    bool MarginFloorClamped,
    string LiquidationTier,
    string StatusMessage,
    DateTime EvaluatedAtUtc);

#endregion

#region Interface

public interface IDynamicPricingBanditService
{
    Task<IReadOnlyList<PricingExperiment>> GetExperimentsAsync(CancellationToken ct = default);
    Task<PricingExperiment?> GetExperimentBySkuAsync(string sku, CancellationToken ct = default);
    Task<ArmSelectionResult> SelectArmAsync(string sku, CancellationToken ct = default);
    Task<PricingExperiment> RecordImpressionAsync(string sku, string armId, CancellationToken ct = default);
    Task<PricingExperiment> RecordConversionAsync(string sku, string armId, decimal? revenue = null, CancellationToken ct = default);
    Task<PriceElasticityReport> CalculateElasticityAsync(string sku, decimal price0, decimal price1, decimal quantity0, decimal quantity1, CancellationToken ct = default);
    Task<MarginFloorCheckResult> EvaluatePriceGuardrailAsync(string sku, decimal candidatePrice, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryLiquidationSchedule>> GetLiquidationScheduleAsync(CancellationToken ct = default);
    Task<InventoryLiquidationSchedule> GetLiquidationScheduleForSkuAsync(string sku, CancellationToken ct = default);
    Task<PricingExperiment> ResetSkuPriorAsync(string sku, double? alphaPrior = null, double? betaPrior = null, CancellationToken ct = default);
    Task<PricingExperiment> UpdateInventoryTelemetryAsync(string sku, int currentStock, decimal dailyRunRate, CancellationToken ct = default);
}

#endregion

#region Mathematical Beta & Gamma Distribution Sampler

public static class BetaDistributionSampler
{
    [ThreadStatic]
    private static Random? _threadRandom;
    private static Random Rng => _threadRandom ??= new Random(Guid.NewGuid().GetHashCode());

    /// <summary>
    /// Samples theta ~ Beta(alpha, beta) using Gamma variate transformation:
    /// X ~ Gamma(alpha, 1), Y ~ Gamma(beta, 1) => X / (X + Y) ~ Beta(alpha, beta).
    /// </summary>
    public static double Sample(double alpha, double beta, Random? customRng = null)
    {
        var rng = customRng ?? Rng;
        if (alpha <= 0.0 || beta <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha and Beta must be strictly positive (> 0).");

        double x = SampleGamma(alpha, 1.0, rng);
        double y = SampleGamma(beta, 1.0, rng);

        if (x + y <= 0.0) return 0.5;

        double sample = x / (x + y);
        return Math.Clamp(sample, 0.0, 1.0);
    }

    /// <summary>
    /// Samples Gamma(shape, scale) using Marsaglia and Tsang's method (2000) for shape >= 1,
    /// and Stuart's theorem reduction for shape < 1.
    /// </summary>
    public static double SampleGamma(double shape, double scale, Random rng)
    {
        if (shape < 1.0)
        {
            // Stuart's Theorem: Gamma(a) = Gamma(a+1) * U^(1/a)
            double u = NextNonZeroDouble(rng);
            return SampleGamma(shape + 1.0, scale, rng) * Math.Pow(u, 1.0 / shape);
        }

        double d = shape - (1.0 / 3.0);
        double c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double z = NextGaussian(rng);
            double v = 1.0 + (c * z);
            if (v <= 0.0) continue;

            v = v * v * v;
            double u = NextNonZeroDouble(rng);

            // Fast squeeze test
            if (u < 1.0 - (0.0331 * z * z * z * z))
                return d * v * scale;

            if (Math.Log(u) < (0.5 * z * z) + (d * (1.0 - v + Math.Log(v))))
                return d * v * scale;
        }
    }

    private static double NextGaussian(Random rng)
    {
        // Standard Box-Muller transformation
        double u1 = NextNonZeroDouble(rng);
        double u2 = NextNonZeroDouble(rng);
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static double NextNonZeroDouble(Random rng)
    {
        double val = rng.NextDouble();
        while (val <= 0.0) val = rng.NextDouble();
        return val;
    }
}

#endregion

#region Service Implementation

public class DynamicPricingBanditService : IDynamicPricingBanditService
{
    private static DynamicPricingBanditService? _instance;
    private static readonly object StaticLock = new();

    public static DynamicPricingBanditService Instance
    {
        get
        {
            if (_instance is null)
            {
                lock (StaticLock)
                {
                    _instance ??= new DynamicPricingBanditService();
                }
            }
            return _instance;
        }
    }

    private readonly ConcurrentDictionary<string, (PricingExperiment Experiment, object SkuLock)> _experiments = new(StringComparer.OrdinalIgnoreCase);

    public DynamicPricingBanditService()
    {
        SeedDefaultExperiments();
    }

    private void SeedDefaultExperiments()
    {
        var now = DateTime.UtcNow;

        // 1. Enterprise SaaS Copilot Addon
        var saas = new PricingExperiment
        {
            Sku = "SKU-SAAS-ENTERPRISE-ADDON",
            ProductName = "Enterprise AI Copilot SaaS Addon",
            Category = "Software / Cloud SaaS",
            BaseCostCogs = 15.00m,
            ShippingAllocated = 0.00m,
            MinimumMarginHurdle = 0.60m, // 60% hurdle -> floor: 15 * 1.60 = $24.00
            StandardPrice = 49.00m,
            CurrentStock = 1200,
            DailyRunRate = 25.0m, // DOS = 48 days (Healthy <= 60)
            MaxMarkdownPct = 0.30m,
            MarkdownStep = 0.10m,
            CreatedAtUtc = now.AddDays(-60),
            LastUpdatedUtc = now,
            Arms =
            [
                new PricingArm { ArmId = "ARM-29", Price = 29.00m, Alpha = 15.0, Beta = 85.0, Impressions = 100, Conversions = 15, TotalRevenue = 435.00m },
                new PricingArm { ArmId = "ARM-39", Price = 39.00m, Alpha = 12.0, Beta = 88.0, Impressions = 100, Conversions = 12, TotalRevenue = 468.00m },
                new PricingArm { ArmId = "ARM-49", Price = 49.00m, Alpha = 10.0, Beta = 90.0, Impressions = 100, Conversions = 10, TotalRevenue = 490.00m },
                new PricingArm { ArmId = "ARM-69", Price = 69.00m, Alpha = 5.0, Beta = 95.0, Impressions = 100, Conversions = 5, TotalRevenue = 345.00m }
            ]
        };

        // 2. Wireless Earbuds Pro
        var earbuds = new PricingExperiment
        {
            Sku = "SKU-AUDIO-EARBUDS-PRO",
            ProductName = "AcousticPro True Wireless Earbuds",
            Category = "Consumer Electronics",
            BaseCostCogs = 32.00m,
            ShippingAllocated = 4.50m,
            MinimumMarginHurdle = 0.25m, // Floor: 32 * 1.25 + 4.50 = $44.50
            StandardPrice = 79.99m,
            CurrentStock = 850,
            DailyRunRate = 8.5m, // DOS = 100 days (SlowMoving_60Days -> 60-120 days)
            MaxMarkdownPct = 0.35m,
            MarkdownStep = 0.12m,
            CreatedAtUtc = now.AddDays(-90),
            LastUpdatedUtc = now,
            Arms =
            [
                new PricingArm { ArmId = "ARM-59", Price = 59.99m, Alpha = 22.0, Beta = 78.0, Impressions = 120, Conversions = 22, TotalRevenue = 1319.78m },
                new PricingArm { ArmId = "ARM-69", Price = 69.99m, Alpha = 17.0, Beta = 83.0, Impressions = 120, Conversions = 17, TotalRevenue = 1189.83m },
                new PricingArm { ArmId = "ARM-79", Price = 79.99m, Alpha = 11.0, Beta = 89.0, Impressions = 120, Conversions = 11, TotalRevenue = 879.89m },
                new PricingArm { ArmId = "ARM-89", Price = 89.99m, Alpha = 6.0, Beta = 94.0, Impressions = 120, Conversions = 6, TotalRevenue = 539.94m }
            ]
        };

        // 3. Ergonomic Motorized Standing Desk
        var desk = new PricingExperiment
        {
            Sku = "SKU-DESK-ERGO-MOTORIZED",
            ProductName = "ApexLift Dual-Motor Ergonomic Standing Desk",
            Category = "Office Furniture",
            BaseCostCogs = 195.00m,
            ShippingAllocated = 35.00m,
            MinimumMarginHurdle = 0.20m, // Floor: 195 * 1.20 + 35 = $269.00
            StandardPrice = 449.00m,
            CurrentStock = 420,
            DailyRunRate = 2.1m, // DOS = 200 days (Critical_180DaysPlus > 180)
            MaxMarkdownPct = 0.45m,
            MarkdownStep = 0.15m,
            CreatedAtUtc = now.AddDays(-120),
            LastUpdatedUtc = now,
            Arms =
            [
                new PricingArm { ArmId = "ARM-349", Price = 349.00m, Alpha = 8.0, Beta = 92.0, Impressions = 80, Conversions = 8, TotalRevenue = 2792.00m },
                new PricingArm { ArmId = "ARM-399", Price = 399.00m, Alpha = 6.0, Beta = 94.0, Impressions = 80, Conversions = 6, TotalRevenue = 2394.00m },
                new PricingArm { ArmId = "ARM-449", Price = 449.00m, Alpha = 4.0, Beta = 96.0, Impressions = 80, Conversions = 4, TotalRevenue = 1796.00m },
                new PricingArm { ArmId = "ARM-499", Price = 499.00m, Alpha = 2.0, Beta = 98.0, Impressions = 80, Conversions = 2, TotalRevenue = 998.00m }
            ]
        };

        // 4. Premium Cloud Storage 2TB
        var storage = new PricingExperiment
        {
            Sku = "SKU-CLOUD-STORAGE-PREMIUM",
            ProductName = "HyperVault 2TB Encrypted Cloud Storage",
            Category = "Cloud Infrastructure",
            BaseCostCogs = 4.20m,
            ShippingAllocated = 0.00m,
            MinimumMarginHurdle = 0.50m, // Floor: 4.20 * 1.50 = $6.30
            StandardPrice = 12.99m,
            CurrentStock = 5000,
            DailyRunRate = 125.0m, // DOS = 40 days (Healthy)
            MaxMarkdownPct = 0.40m,
            MarkdownStep = 0.10m,
            CreatedAtUtc = now.AddDays(-30),
            LastUpdatedUtc = now,
            Arms =
            [
                new PricingArm { ArmId = "ARM-7", Price = 7.99m, Alpha = 24.0, Beta = 76.0, Impressions = 150, Conversions = 24, TotalRevenue = 191.76m },
                new PricingArm { ArmId = "ARM-9", Price = 9.99m, Alpha = 19.0, Beta = 81.0, Impressions = 150, Conversions = 19, TotalRevenue = 189.81m },
                new PricingArm { ArmId = "ARM-12", Price = 12.99m, Alpha = 14.0, Beta = 86.0, Impressions = 150, Conversions = 14, TotalRevenue = 181.86m },
                new PricingArm { ArmId = "ARM-14", Price = 14.99m, Alpha = 8.0, Beta = 92.0, Impressions = 150, Conversions = 8, TotalRevenue = 119.92m }
            ]
        };

        _experiments[saas.Sku] = (saas, new object());
        _experiments[earbuds.Sku] = (earbuds, new object());
        _experiments[desk.Sku] = (desk, new object());
        _experiments[storage.Sku] = (storage, new object());
    }

    public Task<IReadOnlyList<PricingExperiment>> GetExperimentsAsync(CancellationToken ct = default)
    {
        var list = _experiments.Values
            .Select(v =>
            {
                lock (v.SkuLock)
                {
                    return v.Experiment.Clone();
                }
            })
            .OrderBy(e => e.Category)
            .ThenBy(e => e.ProductName)
            .ToList();

        return Task.FromResult<IReadOnlyList<PricingExperiment>>(list);
    }

    public Task<PricingExperiment?> GetExperimentBySkuAsync(string sku, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(sku, out var entry))
            return Task.FromResult<PricingExperiment?>(null);

        lock (entry.SkuLock)
        {
            return Task.FromResult<PricingExperiment?>(entry.Experiment.Clone());
        }
    }

    public Task<ArmSelectionResult> SelectArmAsync(string sku, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(sku, out var entry))
            throw new KeyNotFoundException($"SKU '{sku}' was not found in dynamic pricing bandit experiments.");

        lock (entry.SkuLock)
        {
            var exp = entry.Experiment;
            if (exp.Arms.Count == 0)
                throw new InvalidOperationException($"SKU '{sku}' has no configured pricing arms.");

            var sampledThetas = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            PricingArm? bestArm = null;
            decimal highestExpectedRevenue = -1m;
            double bestArmSampledTheta = 0.0;

            foreach (var arm in exp.Arms)
            {
                // Ensure strictly positive alpha & beta priors
                double alpha = Math.Max(0.001, arm.Alpha);
                double beta = Math.Max(0.001, arm.Beta);

                double theta = BetaDistributionSampler.Sample(alpha, beta);
                sampledThetas[arm.ArmId] = Math.Round(theta, 4);

                decimal expectedRev = arm.Price * (decimal)theta;
                if (bestArm is null || expectedRev > highestExpectedRevenue)
                {
                    bestArm = arm;
                    highestExpectedRevenue = expectedRev;
                    bestArmSampledTheta = theta;
                }
            }

            bestArm ??= exp.Arms[0];

            // Evaluate Hard Margin Floor Guardrail
            decimal floorPrice = exp.HardMarginFloor;
            bool floorClampApplied = bestArm.Price < floorPrice;
            decimal finalPrice = floorClampApplied ? floorPrice : bestArm.Price;

            var result = new ArmSelectionResult(
                Sku: exp.Sku,
                SelectedArmId: bestArm.ArmId,
                ProposedPrice: bestArm.Price,
                FinalPrice: finalPrice,
                FloorClampApplied: floorClampApplied,
                FloorPrice: floorPrice,
                SampledTheta: Math.Round(bestArmSampledTheta, 4),
                SampledExpectedRevenue: Math.Round(highestExpectedRevenue, 2),
                AllSampledThetas: sampledThetas,
                TimestampUtc: DateTime.UtcNow);

            return Task.FromResult(result);
        }
    }

    public Task<PricingExperiment> RecordImpressionAsync(string sku, string armId, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(sku, out var entry))
            throw new KeyNotFoundException($"SKU '{sku}' not found.");

        lock (entry.SkuLock)
        {
            var arm = entry.Experiment.Arms.FirstOrDefault(a => string.Equals(a.ArmId, armId, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Arm '{armId}' not found for SKU '{sku}'.");

            arm.Impressions += 1;
            // Record impression as an un-converted observation (Beta += 1) until conversion arrives
            arm.Beta += 1.0;
            entry.Experiment.LastUpdatedUtc = DateTime.UtcNow;

            return Task.FromResult(entry.Experiment.Clone());
        }
    }

    public Task<PricingExperiment> RecordConversionAsync(string sku, string armId, decimal? revenue = null, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(sku, out var entry))
            throw new KeyNotFoundException($"SKU '{sku}' not found.");

        lock (entry.SkuLock)
        {
            var arm = entry.Experiment.Arms.FirstOrDefault(a => string.Equals(a.ArmId, armId, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Arm '{armId}' not found for SKU '{sku}'.");

            arm.Conversions += 1;
            arm.Alpha += 1.0;
            // Balance beta since this impression converted rather than failed (never drop below 1.0)
            arm.Beta = Math.Max(1.0, arm.Beta - 1.0);

            decimal rev = revenue ?? arm.Price;
            arm.TotalRevenue += rev;
            entry.Experiment.LastUpdatedUtc = DateTime.UtcNow;

            return Task.FromResult(entry.Experiment.Clone());
        }
    }

    public Task<PriceElasticityReport> CalculateElasticityAsync(
        string sku,
        decimal price0,
        decimal price1,
        decimal quantity0,
        decimal quantity1,
        CancellationToken ct = default)
    {
        decimal priceDelta = price1 - price0;
        decimal priceAvg = (price1 + price0) / 2.0m;
        decimal quantityDelta = quantity1 - quantity0;
        decimal quantityAvg = (quantity1 + quantity0) / 2.0m;

        double elasticity;
        string classification;
        string strategy;
        string rationale;

        // Guard against zero division or zero price/quantity changes
        if (priceDelta == 0m || priceAvg == 0m || quantityAvg == 0m)
        {
            elasticity = 0.0;
            classification = "Inelastic";
            strategy = "HoldPrice";
            rationale = "Price or quantity midpoints are zero, or price change is zero. Arc elasticity is undefined and defaulted to 0.0.";
        }
        else
        {
            decimal pctQty = quantityDelta / quantityAvg;
            decimal pctPrice = priceDelta / priceAvg;

            decimal rawArcElasticity = pctQty / pctPrice;
            elasticity = Math.Round((double)rawArcElasticity, 3);
            double absE = Math.Abs(elasticity);

            if (absE < 0.8)
            {
                classification = "Inelastic";
                strategy = "IncreasePrice";
                rationale = $"Demand is inelastic (|ε| = {absE:F2} < 0.8). Buyers exhibit low sensitivity to price movements. A tactical price increase will expand gross margin without significant volume degradation.";
            }
            else if (absE <= 1.2)
            {
                classification = "Unitary";
                strategy = "HoldPrice";
                rationale = $"Demand is unitary (|ε| = {absE:F2} between 0.8 and 1.2). Quantity demand shifts symmetrically with price. Maintain current price to preserve optimal total revenue equilibrium.";
            }
            else
            {
                classification = "Elastic";
                strategy = "DiscountToDriveVolume";
                rationale = $"Demand is highly elastic (|ε| = {absE:F2} > 1.2). Sales volume responds aggressively to pricing shifts. Promotional discounting or volume tiering will maximize top-line revenue.";
            }
        }

        var report = new PriceElasticityReport(
            Sku: sku,
            BaselinePrice: price0,
            TargetPrice: price1,
            BaselineQuantity: quantity0,
            TargetQuantity: quantity1,
            ElasticityCoefficient: elasticity,
            ElasticityClassification: classification,
            RecommendedStrategy: strategy,
            Rationale: rationale,
            CalculatedAtUtc: DateTime.UtcNow);

        return Task.FromResult(report);
    }

    public Task<MarginFloorCheckResult> EvaluatePriceGuardrailAsync(string sku, decimal candidatePrice, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(sku, out var entry))
            throw new KeyNotFoundException($"SKU '{sku}' not found.");

        lock (entry.SkuLock)
        {
            var exp = entry.Experiment;
            decimal floorPrice = exp.HardMarginFloor;
            bool clamped = candidatePrice < floorPrice;
            decimal effectivePrice = clamped ? floorPrice : candidatePrice;

            decimal totalUnitCost = exp.BaseCostCogs + exp.ShippingAllocated;
            decimal effectiveMarginPct = effectivePrice > 0m
                ? Math.Round(((effectivePrice - totalUnitCost) / effectivePrice) * 100m, 2)
                : 0m;

            string status = clamped
                ? $"Violates margin hurdle. Candidate price ${candidatePrice:F2} was clamped to hard floor ${floorPrice:F2} (hurdle {exp.MinimumMarginHurdle:P0})."
                : $"Compliant. Candidate price ${candidatePrice:F2} satisfies margin hurdle (Floor: ${floorPrice:F2}, Margin: {effectiveMarginPct:F1}%).";

            var result = new MarginFloorCheckResult(
                Sku: exp.Sku,
                CandidatePrice: candidatePrice,
                FloorPrice: floorPrice,
                BaseCostCogs: exp.BaseCostCogs,
                MinimumMarginHurdle: exp.MinimumMarginHurdle,
                ShippingAllocated: exp.ShippingAllocated,
                EffectiveMarginPercent: effectiveMarginPct,
                FloorClampApplied: clamped,
                EffectivePrice: effectivePrice,
                GuardrailStatus: status);

            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<InventoryLiquidationSchedule>> GetLiquidationScheduleAsync(CancellationToken ct = default)
    {
        var list = _experiments.Values
            .Select(v =>
            {
                lock (v.SkuLock)
                {
                    return ComputeLiquidationScheduleInternal(v.Experiment);
                }
            })
            .OrderByDescending(s => s.DaysOfSupply)
            .ToList();

        return Task.FromResult<IReadOnlyList<InventoryLiquidationSchedule>>(list);
    }

    public Task<InventoryLiquidationSchedule> GetLiquidationScheduleForSkuAsync(string sku, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(sku, out var entry))
            throw new KeyNotFoundException($"SKU '{sku}' not found.");

        lock (entry.SkuLock)
        {
            return Task.FromResult(ComputeLiquidationScheduleInternal(entry.Experiment));
        }
    }

    public Task<PricingExperiment> ResetSkuPriorAsync(string sku, double? alphaPrior = null, double? betaPrior = null, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(sku, out var entry))
            throw new KeyNotFoundException($"SKU '{sku}' not found.");

        lock (entry.SkuLock)
        {
            double a = alphaPrior ?? 1.0;
            double b = betaPrior ?? 1.0;

            foreach (var arm in entry.Experiment.Arms)
            {
                arm.Alpha = a;
                arm.Beta = b;
                arm.Impressions = 0;
                arm.Conversions = 0;
                arm.TotalRevenue = 0m;
            }

            entry.Experiment.LastUpdatedUtc = DateTime.UtcNow;
            return Task.FromResult(entry.Experiment.Clone());
        }
    }

    public Task<PricingExperiment> UpdateInventoryTelemetryAsync(string sku, int currentStock, decimal dailyRunRate, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(sku, out var entry))
            throw new KeyNotFoundException($"SKU '{sku}' not found.");

        lock (entry.SkuLock)
        {
            entry.Experiment.CurrentStock = Math.Max(0, currentStock);
            entry.Experiment.DailyRunRate = Math.Max(0m, dailyRunRate);
            entry.Experiment.LastUpdatedUtc = DateTime.UtcNow;

            return Task.FromResult(entry.Experiment.Clone());
        }
    }

    private static InventoryLiquidationSchedule ComputeLiquidationScheduleInternal(PricingExperiment exp)
    {
        decimal dos = exp.DaysOfSupply;
        decimal floor = exp.HardMarginFloor;
        decimal standardPrice = exp.StandardPrice;

        bool triggered = dos > 60m;
        decimal rawMarkdownPct = 0m;
        decimal rawDiscountedPrice = standardPrice;
        decimal finalPrice = standardPrice;
        bool marginFloorClamped = false;
        string tier;
        string status;

        if (dos <= 60m)
        {
            tier = "Healthy";
            status = $"Inventory turnover is healthy ({dos:F1} Days of Supply <= 60). No liquidation markdown required.";
        }
        else
        {
            // Overage calculation: MarkdownPct = min(MaxMarkdownPct, ((DOS - 60) / 60) * MarkdownStep)
            decimal overageTiers = (dos - 60m) / 60m;
            rawMarkdownPct = Math.Min(exp.MaxMarkdownPct, overageTiers * exp.MarkdownStep);
            rawDiscountedPrice = Math.Round(standardPrice * (1.0m - rawMarkdownPct), 2);

            if (dos <= 120m)
            {
                tier = "SlowMoving_60Days";
            }
            else if (dos <= 180m)
            {
                tier = "AtRisk_120Days";
            }
            else
            {
                tier = "Critical_180DaysPlus";
            }

            if (rawDiscountedPrice < floor)
            {
                finalPrice = floor;
                marginFloorClamped = true;
                decimal appliedPct = standardPrice > 0m
                    ? Math.Round((standardPrice - finalPrice) / standardPrice, 4)
                    : 0m;

                status = $"Liquidation triggered for {tier} ({dos:F1} DOS). Raw markdown of {rawMarkdownPct:P0} ($ {rawDiscountedPrice:F2}) was clamped to hard floor ${floor:F2} (effective discount {appliedPct:P1}) to protect unit profitability.";
                rawMarkdownPct = appliedPct;
            }
            else
            {
                finalPrice = rawDiscountedPrice;
                marginFloorClamped = false;
                status = $"Liquidation active for {tier} ({dos:F1} DOS). Markdown of {rawMarkdownPct:P1} applied. Unit margin remains profitable at ${finalPrice:F2}.";
            }
        }

        return new InventoryLiquidationSchedule(
            Sku: exp.Sku,
            ProductName: exp.ProductName,
            CurrentStock: exp.CurrentStock,
            DailyRunRate: exp.DailyRunRate,
            DaysOfSupply: dos,
            StandardPrice: standardPrice,
            HardMarginFloor: floor,
            RecommendedMarkdownPct: Math.Round(rawMarkdownPct, 4),
            RawDiscountedPrice: rawDiscountedPrice,
            FinalPrice: finalPrice,
            LiquidationTriggered: triggered,
            MarginFloorClamped: marginFloorClamped,
            LiquidationTier: tier,
            StatusMessage: status,
            EvaluatedAtUtc: DateTime.UtcNow);
    }
}

#endregion
