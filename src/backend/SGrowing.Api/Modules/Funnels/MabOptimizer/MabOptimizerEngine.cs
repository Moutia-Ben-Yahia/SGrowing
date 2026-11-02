using System.Collections.Concurrent;
using System.Text.Json;

namespace SGrowing.Api.Modules.Funnels.MabOptimizer;

public interface IMabOptimizerEngine
{
    FunnelPage CreateFunnel(CreateFunnelRequest request);
    FunnelPage? GetFunnel(string tenantId, string funnelId);
    FunnelPage UpdateBlocks(UpdateBlocksRequest request);
    VariantAllocation SelectVariant(string tenantId, string funnelId);
    FunnelPage RecordImpression(RecordImpressionRequest request);
    FunnelPage RecordConversion(RecordConversionRequest request);
    MabOptimizationReport GetOptimizationReport(string tenantId, string funnelId);
    string GenerateSeoJsonLd(FunnelPage page);
}

public class MabOptimizerEngine : IMabOptimizerEngine
{
    private readonly ConcurrentDictionary<string, FunnelPage> _funnels = new();
    private readonly Random _random;

    public MabOptimizerEngine(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    private static string BuildKey(string tenantId, string funnelId) => $"{tenantId}::{funnelId}";

    public FunnelPage CreateFunnel(CreateFunnelRequest request)
    {
        var key = BuildKey(request.TenantId, request.FunnelId);
        var variantNames = request.InitialVariantNames is { Count: > 0 }
            ? request.InitialVariantNames
            : new List<string> { "Variant Control", "Variant B (Aggressive CTA)" };

        var initialVariants = variantNames.Select((name, i) => new VariantAllocation(
            VariantId: $"var_{i + 1}",
            Name: name,
            TrafficWeight: Math.Round(1.0 / variantNames.Count, 4),
            Impressions: 0,
            Conversions: 0,
            ConversionRate: 0.0,
            Score: 0.5
        )).ToList();

        var page = new FunnelPage(
            TenantId: request.TenantId,
            FunnelId: request.FunnelId,
            Path: request.Path,
            Title: request.Title,
            SeoMetaDescription: request.SeoMetaDescription,
            Blocks: request.InitialBlocks,
            Variants: initialVariants,
            UpdatedAtUtc: DateTime.UtcNow
        );

        _funnels[key] = page;
        return page;
    }

    public FunnelPage? GetFunnel(string tenantId, string funnelId)
    {
        _funnels.TryGetValue(BuildKey(tenantId, funnelId), out var page);
        return page;
    }

    public FunnelPage UpdateBlocks(UpdateBlocksRequest request)
    {
        var key = BuildKey(request.TenantId, request.FunnelId);
        if (!_funnels.TryGetValue(key, out var existing))
        {
            throw new KeyNotFoundException($"Funnel '{request.FunnelId}' was not found for tenant '{request.TenantId}'.");
        }

        var updated = existing with
        {
            Blocks = request.Blocks.OrderBy(b => b.Order).ToList(),
            UpdatedAtUtc = DateTime.UtcNow
        };

        _funnels[key] = updated;
        return updated;
    }

    public VariantAllocation SelectVariant(string tenantId, string funnelId)
    {
        var key = BuildKey(tenantId, funnelId);
        if (!_funnels.TryGetValue(key, out var page) || page.Variants.Count == 0)
        {
            throw new KeyNotFoundException($"Funnel '{funnelId}' not found or has no variants.");
        }

        // Thompson Sampling over Beta(alpha, beta) distribution
        VariantAllocation? bestVariant = null;
        var highestSample = double.MinValue;

        foreach (var variant in page.Variants)
        {
            var alpha = 1.0 + variant.Conversions;
            var beta = 1.0 + Math.Max(0, variant.Impressions - variant.Conversions);

            var sample = SampleBeta(alpha, beta);
            if (sample > highestSample)
            {
                highestSample = sample;
                bestVariant = variant;
            }
        }

        return bestVariant ?? page.Variants[0];
    }

    public FunnelPage RecordImpression(RecordImpressionRequest request)
    {
        var key = BuildKey(request.TenantId, request.FunnelId);
        if (!_funnels.TryGetValue(key, out var page))
        {
            throw new KeyNotFoundException($"Funnel '{request.FunnelId}' not found.");
        }

        var updatedVariants = page.Variants.Select(v =>
        {
            if (v.VariantId == request.VariantId)
            {
                var newImp = v.Impressions + 1;
                var cr = newImp > 0 ? (double)v.Conversions / newImp : 0.0;
                return v with
                {
                    Impressions = newImp,
                    ConversionRate = Math.Round(cr, 4),
                    Score = CalculateConfidenceScore(v.Conversions, newImp)
                };
            }
            return v;
        }).ToList();

        RecalculateTrafficWeights(updatedVariants);

        var updated = page with
        {
            Variants = updatedVariants,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _funnels[key] = updated;
        return updated;
    }

    public FunnelPage RecordConversion(RecordConversionRequest request)
    {
        var key = BuildKey(request.TenantId, request.FunnelId);
        if (!_funnels.TryGetValue(key, out var page))
        {
            throw new KeyNotFoundException($"Funnel '{request.FunnelId}' not found.");
        }

        var updatedVariants = page.Variants.Select(v =>
        {
            if (v.VariantId == request.VariantId)
            {
                var newConv = v.Conversions + 1;
                var cr = v.Impressions > 0 ? (double)newConv / v.Impressions : 1.0;
                return v with
                {
                    Conversions = newConv,
                    ConversionRate = Math.Round(cr, 4),
                    Score = CalculateConfidenceScore(newConv, v.Impressions)
                };
            }
            return v;
        }).ToList();

        RecalculateTrafficWeights(updatedVariants);

        var updated = page with
        {
            Variants = updatedVariants,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _funnels[key] = updated;
        return updated;
    }

    public MabOptimizationReport GetOptimizationReport(string tenantId, string funnelId)
    {
        var page = GetFunnel(tenantId, funnelId)
            ?? throw new KeyNotFoundException($"Funnel '{funnelId}' not found.");

        var sorted = page.Variants.OrderByDescending(v => v.ConversionRate).ToList();
        var leader = sorted.FirstOrDefault() ?? new VariantAllocation("none", "None", 0, 0, 0, 0, 0);
        var baseline = sorted.LastOrDefault() ?? leader;

        var uplift = baseline.ConversionRate > 0.0
            ? Math.Round(((leader.ConversionRate - baseline.ConversionRate) / baseline.ConversionRate) * 100.0, 2)
            : 0.0;

        var confidence = CalculateBayesianConfidence(leader, baseline);

        return new MabOptimizationReport(
            FunnelId: funnelId,
            LeadingVariantId: leader.VariantId,
            ConfidenceLevel: confidence,
            UpliftPercent: uplift,
            Variants: page.Variants,
            GeneratedJsonLd: GenerateSeoJsonLd(page)
        );
    }

    public string GenerateSeoJsonLd(FunnelPage page)
    {
        var schema = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebPage",
            ["name"] = page.Title,
            ["description"] = page.SeoMetaDescription,
            ["url"] = $"https://app.sgrowing.io{page.Path}",
            ["dateModified"] = page.UpdatedAtUtc.ToString("o"),
            ["mainEntity"] = new Dictionary<string, object>
            {
                ["@type"] = "Action",
                ["name"] = "FunnelConversion",
                ["target"] = $"https://app.sgrowing.io{page.Path}"
            }
        };

        return JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
    }

    private double SampleBeta(double alpha, double beta)
    {
        // Sample Gamma(alpha, 1) and Gamma(beta, 1) using Marsaglia and Tsang method or approximation
        var x = SampleGamma(alpha);
        var y = SampleGamma(beta);
        var sum = x + y;
        return sum > 0 ? x / sum : 0.5;
    }

    private double SampleGamma(double shape)
    {
        if (shape < 1.0)
        {
            return SampleGamma(shape + 1.0) * Math.Pow(_random.NextDouble(), 1.0 / shape);
        }

        var d = shape - 1.0 / 3.0;
        var c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double z;
            double v;
            do
            {
                z = NextGaussian();
                v = 1.0 + c * z;
            } while (v <= 0.0);

            v = v * v * v;
            var u = _random.NextDouble();

            if (u < 1.0 - 0.0331 * z * z * z * z)
            {
                return d * v;
            }

            if (Math.Log(u) < 0.5 * z * z + d * (1.0 - v + Math.Log(v)))
            {
                return d * v;
            }
        }
    }

    private double NextGaussian()
    {
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    private static double CalculateConfidenceScore(int conversions, int impressions)
    {
        if (impressions <= 0) return 0.5;
        var cr = (double)conversions / impressions;
        var se = Math.Sqrt((cr * (1.0 - cr)) / impressions);
        return Math.Round(Math.Max(0.0, Math.Min(1.0, cr - 1.96 * se)), 4);
    }

    private static double CalculateBayesianConfidence(VariantAllocation leader, VariantAllocation baseline)
    {
        if (leader.Impressions < 5 || baseline.Impressions < 5) return 50.0;
        var diff = leader.ConversionRate - baseline.ConversionRate;
        if (diff <= 0) return 50.0;

        var varLeader = (leader.ConversionRate * (1 - leader.ConversionRate)) / leader.Impressions;
        var varBase = (baseline.ConversionRate * (1 - baseline.ConversionRate)) / baseline.Impressions;
        var se = Math.Sqrt(varLeader + varBase);
        if (se <= 0.0001) return 99.0;

        var z = diff / se;
        var p = 0.5 * (1.0 + Erf(z / Math.Sqrt(2.0)));
        return Math.Round(Math.Min(99.9, p * 100.0), 1);
    }

    private static double Erf(double x)
    {
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        var sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }

    private static void RecalculateTrafficWeights(List<VariantAllocation> variants)
    {
        var totalScore = variants.Sum(v => Math.Max(0.05, v.Score));
        if (totalScore <= 0) totalScore = 1.0;

        for (var i = 0; i < variants.Count; i++)
        {
            var rawWeight = Math.Max(0.05, variants[i].Score) / totalScore;
            variants[i] = variants[i] with { TrafficWeight = Math.Round(rawWeight, 4) };
        }
    }
}
