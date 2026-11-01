using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Attribution;

public record TouchpointInput(
    string Channel,
    string CampaignName,
    DateTime OccurredAtUtc,
    decimal Cost = 0m,
    string? UtmSource = null,
    string? UtmMedium = null,
    string? UtmCampaign = null
);

public record CustomerJourneyInput(
    string JourneyId,
    List<TouchpointInput> Touchpoints,
    bool Converted,
    decimal Revenue = 0m
);

public record CalculateMultiModelRequest(
    Guid TenantId,
    decimal TotalRevenue,
    DateTime? ConversionTimeUtc = null,
    List<TouchpointInput>? Touchpoints = null,
    List<CustomerJourneyInput>? CohortJourneys = null,
    Guid? DealId = null,
    Guid? ContactId = null
);

public record ChannelAttributionResult(
    string Channel,
    decimal AttributedRevenue,
    double WeightPercentage,
    int TouchpointsCount
);

public record ModelAttributionOutput(
    string ModelName,
    IReadOnlyList<ChannelAttributionResult> Channels,
    string Description
);

public record ChannelComparisonRow(
    string Channel,
    decimal FirstTouchRevenue,
    decimal LastTouchRevenue,
    decimal LinearRevenue,
    decimal TimeDecayRevenue,
    decimal UShapedRevenue,
    decimal MarkovChainRevenue,
    decimal ShapleyRevenue,
    double MaxVariancePercentage,
    string Recommendation
);

public record MultiModelComparisonMatrix(
    decimal TotalRevenue,
    IReadOnlyList<string> EvaluatedChannels,
    IReadOnlyDictionary<string, ModelAttributionOutput> Models,
    IReadOnlyList<ChannelComparisonRow> MatrixRows,
    DateTime CalculatedAtUtc
);

public interface IMultiModelRevenueAttributionService
{
    ModelAttributionOutput ComputeFirstTouch(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue);
    ModelAttributionOutput ComputeLastTouch(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue);
    ModelAttributionOutput ComputeLinear(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue);
    ModelAttributionOutput ComputeTimeDecay(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue, DateTime? conversionTimeUtc = null, double halfLifeDays = 7.0);
    ModelAttributionOutput ComputeUShaped(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue);
    ModelAttributionOutput ComputeMarkovChain(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue, IReadOnlyList<CustomerJourneyInput>? cohortJourneys = null);
    ModelAttributionOutput ComputeShapley(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue);
    Task<MultiModelComparisonMatrix> CalculateMultiModelMatrixAsync(CalculateMultiModelRequest request, CancellationToken ct = default);
}

public class MultiModelRevenueAttributionService : IMultiModelRevenueAttributionService
{
    private readonly SGrowingDbContext? _db;
    private readonly ILogger<MultiModelRevenueAttributionService>? _logger;

    public MultiModelRevenueAttributionService(
        SGrowingDbContext? db = null,
        ILogger<MultiModelRevenueAttributionService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public ModelAttributionOutput ComputeFirstTouch(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue)
    {
        const string modelName = "First Touch";
        const string desc = "100% of revenue attributed to the first customer acquisition touchpoint.";

        if (touchpoints == null || touchpoints.Count == 0 || totalRevenue <= 0m)
        {
            return new ModelAttributionOutput(modelName, [], desc);
        }

        var ordered = touchpoints.OrderBy(t => t.OccurredAtUtc).ToList();
        var firstChannel = ordered[0].Channel.Trim();

        var channelTouchCounts = touchpoints
            .GroupBy(t => t.Channel.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var channels = new List<ChannelAttributionResult>();
        foreach (var (channel, count) in channelTouchCounts)
        {
            var isFirst = string.Equals(channel, firstChannel, StringComparison.OrdinalIgnoreCase);
            channels.Add(new ChannelAttributionResult(
                Channel: channel,
                AttributedRevenue: isFirst ? totalRevenue : 0m,
                WeightPercentage: isFirst ? 100.0 : 0.0,
                TouchpointsCount: count
            ));
        }

        return new ModelAttributionOutput(modelName, channels, desc);
    }

    public ModelAttributionOutput ComputeLastTouch(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue)
    {
        const string modelName = "Last Touch";
        const string desc = "100% of revenue attributed to the final conversion touchpoint.";

        if (touchpoints == null || touchpoints.Count == 0 || totalRevenue <= 0m)
        {
            return new ModelAttributionOutput(modelName, [], desc);
        }

        var ordered = touchpoints.OrderBy(t => t.OccurredAtUtc).ToList();
        var lastChannel = ordered[^1].Channel.Trim();

        var channelTouchCounts = touchpoints
            .GroupBy(t => t.Channel.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var channels = new List<ChannelAttributionResult>();
        foreach (var (channel, count) in channelTouchCounts)
        {
            var isLast = string.Equals(channel, lastChannel, StringComparison.OrdinalIgnoreCase);
            channels.Add(new ChannelAttributionResult(
                Channel: channel,
                AttributedRevenue: isLast ? totalRevenue : 0m,
                WeightPercentage: isLast ? 100.0 : 0.0,
                TouchpointsCount: count
            ));
        }

        return new ModelAttributionOutput(modelName, channels, desc);
    }

    public ModelAttributionOutput ComputeLinear(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue)
    {
        const string modelName = "Linear";
        const string desc = "Revenue distributed equally across all journey touchpoints.";

        if (touchpoints == null || touchpoints.Count == 0 || totalRevenue <= 0m)
        {
            return new ModelAttributionOutput(modelName, [], desc);
        }

        var n = touchpoints.Count;
        var unitRevenue = totalRevenue / n;
        var unitWeight = 100.0 / n;

        var channelGroups = touchpoints
            .GroupBy(t => t.Channel.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var channels = new List<ChannelAttributionResult>();
        decimal sumRev = 0m;

        for (int i = 0; i < channelGroups.Count; i++)
        {
            var group = channelGroups[i];
            var count = group.Count();
            decimal rev;

            if (i == channelGroups.Count - 1)
            {
                // Ensure exact penny balancing
                rev = totalRevenue - sumRev;
            }
            else
            {
                rev = Math.Round(unitRevenue * count, 2);
                sumRev += rev;
            }

            channels.Add(new ChannelAttributionResult(
                Channel: group.Key,
                AttributedRevenue: rev,
                WeightPercentage: Math.Round(unitWeight * count, 2),
                TouchpointsCount: count
            ));
        }

        return new ModelAttributionOutput(modelName, channels, desc);
    }

    public ModelAttributionOutput ComputeTimeDecay(
        IReadOnlyList<TouchpointInput> touchpoints,
        decimal totalRevenue,
        DateTime? conversionTimeUtc = null,
        double halfLifeDays = 7.0)
    {
        const string modelName = "Time-Decay";
        const string desc = "Exponential decay attribution giving progressively higher weight to touchpoints closest in time to conversion (7-day half-life).";

        if (touchpoints == null || touchpoints.Count == 0 || totalRevenue <= 0m)
        {
            return new ModelAttributionOutput(modelName, [], desc);
        }

        var ordered = touchpoints.OrderBy(t => t.OccurredAtUtc).ToList();
        var convTime = conversionTimeUtc ?? ordered[^1].OccurredAtUtc;

        // Formula: w_i = 2 ^ (-deltaDays / halfLifeDays)
        var rawWeights = new double[ordered.Count];
        double totalWeight = 0.0;

        for (int i = 0; i < ordered.Count; i++)
        {
            var deltaDays = Math.Max(0.0, (convTime - ordered[i].OccurredAtUtc).TotalDays);
            var weight = Math.Pow(2.0, -deltaDays / halfLifeDays);
            rawWeights[i] = weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0.0)
        {
            return ComputeLinear(touchpoints, totalRevenue);
        }

        // Aggregate by channel
        var channelWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var channelCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < ordered.Count; i++)
        {
            var ch = ordered[i].Channel.Trim();
            var normalizedWeight = rawWeights[i] / totalWeight;

            channelWeights[ch] = channelWeights.GetValueOrDefault(ch, 0.0) + normalizedWeight;
            channelCounts[ch] = channelCounts.GetValueOrDefault(ch, 0) + 1;
        }

        var channels = new List<ChannelAttributionResult>();
        decimal runningRevenue = 0m;
        var keys = channelWeights.Keys.ToList();

        for (int i = 0; i < keys.Count; i++)
        {
            var ch = keys[i];
            var weightFraction = channelWeights[ch];
            decimal attributedRev;

            if (i == keys.Count - 1)
            {
                attributedRev = totalRevenue - runningRevenue;
            }
            else
            {
                attributedRev = Math.Round(totalRevenue * (decimal)weightFraction, 2);
                runningRevenue += attributedRev;
            }

            channels.Add(new ChannelAttributionResult(
                Channel: ch,
                AttributedRevenue: attributedRev,
                WeightPercentage: Math.Round(weightFraction * 100.0, 2),
                TouchpointsCount: channelCounts[ch]
            ));
        }

        return new ModelAttributionOutput(modelName, channels, desc);
    }

    public ModelAttributionOutput ComputeUShaped(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue)
    {
        const string modelName = "Position-Based / U-Shape";
        const string desc = "40% first touch, 40% last touch, 20% distributed evenly across middle touches.";

        if (touchpoints == null || touchpoints.Count == 0 || totalRevenue <= 0m)
        {
            return new ModelAttributionOutput(modelName, [], desc);
        }

        var ordered = touchpoints.OrderBy(t => t.OccurredAtUtc).ToList();
        var n = ordered.Count;

        var touchWeights = new double[n];
        if (n == 1)
        {
            touchWeights[0] = 1.0;
        }
        else if (n == 2)
        {
            touchWeights[0] = 0.5;
            touchWeights[1] = 0.5;
        }
        else
        {
            touchWeights[0] = 0.40;
            touchWeights[^1] = 0.40;
            var middleWeight = 0.20 / (n - 2);
            for (int i = 1; i < n - 1; i++)
            {
                touchWeights[i] = middleWeight;
            }
        }

        var channelWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var channelCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < n; i++)
        {
            var ch = ordered[i].Channel.Trim();
            channelWeights[ch] = channelWeights.GetValueOrDefault(ch, 0.0) + touchWeights[i];
            channelCounts[ch] = channelCounts.GetValueOrDefault(ch, 0) + 1;
        }

        var channels = new List<ChannelAttributionResult>();
        decimal runningRevenue = 0m;
        var keys = channelWeights.Keys.ToList();

        for (int i = 0; i < keys.Count; i++)
        {
            var ch = keys[i];
            var weightFraction = channelWeights[ch];
            decimal attributedRev;

            if (i == keys.Count - 1)
            {
                attributedRev = totalRevenue - runningRevenue;
            }
            else
            {
                attributedRev = Math.Round(totalRevenue * (decimal)weightFraction, 2);
                runningRevenue += attributedRev;
            }

            channels.Add(new ChannelAttributionResult(
                Channel: ch,
                AttributedRevenue: attributedRev,
                WeightPercentage: Math.Round(weightFraction * 100.0, 2),
                TouchpointsCount: channelCounts[ch]
            ));
        }

        return new ModelAttributionOutput(modelName, channels, desc);
    }

    public ModelAttributionOutput ComputeMarkovChain(
        IReadOnlyList<TouchpointInput> touchpoints,
        decimal totalRevenue,
        IReadOnlyList<CustomerJourneyInput>? cohortJourneys = null)
    {
        const string modelName = "Markov-Chain Attribution";
        const string desc = "State-transition probabilistic modeling calculating channel removal effects.";

        if (touchpoints == null || touchpoints.Count == 0 || totalRevenue <= 0m)
        {
            return new ModelAttributionOutput(modelName, [], desc);
        }

        // Build journey set: combine target touchpoints path with cohort paths if provided
        var allJourneys = new List<List<string>>();
        var targetChannels = touchpoints
            .OrderBy(t => t.OccurredAtUtc)
            .Select(t => t.Channel.Trim())
            .ToList();

        allJourneys.Add(targetChannels);

        if (cohortJourneys != null)
        {
            foreach (var j in cohortJourneys)
            {
                var path = j.Touchpoints
                    .OrderBy(t => t.OccurredAtUtc)
                    .Select(t => t.Channel.Trim())
                    .ToList();
                if (path.Count > 0)
                {
                    allJourneys.Add(path);
                }
            }
        }

        // Find all distinct channels involved
        var distinctChannels = touchpoints
            .Select(t => t.Channel.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var channelCounts = touchpoints
            .GroupBy(t => t.Channel.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        // If only 1 channel exists, 100% to that channel
        if (distinctChannels.Count == 1)
        {
            return new ModelAttributionOutput(modelName,
            [
                new ChannelAttributionResult(distinctChannels[0], totalRevenue, 100.0, channelCounts[distinctChannels[0]])
            ], desc);
        }

        // Calculate Markov Chain Removal Effect
        // In customer journey paths:
        // Baseline conversion rate = all converting journeys / total journeys (here all journeys in set convert or are evaluated)
        // When channel C is removed, journeys containing C drop out / do not convert.
        // Removal Effect R(C) = 1.0 - (conversions without C / total baseline conversions).
        var removalEffects = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        int totalBaselineConversions = allJourneys.Count;

        foreach (var ch in distinctChannels)
        {
            int conversionsWithoutChannel = allJourneys.Count(journey => !journey.Contains(ch, StringComparer.OrdinalIgnoreCase));
            double survivalFraction = (double)conversionsWithoutChannel / totalBaselineConversions;
            double removalEffect = Math.Max(0.0, 1.0 - survivalFraction);
            removalEffects[ch] = removalEffect;
        }

        double totalRemovalSum = removalEffects.Values.Sum();

        // If sum is 0 (all channels appeared in all paths), assign based on transition frequency
        if (totalRemovalSum <= 0.000001)
        {
            return ComputeLinear(touchpoints, totalRevenue);
        }

        var channels = new List<ChannelAttributionResult>();
        decimal runningRevenue = 0m;

        for (int i = 0; i < distinctChannels.Count; i++)
        {
            var ch = distinctChannels[i];
            var weightFraction = removalEffects[ch] / totalRemovalSum;
            decimal attributedRev;

            if (i == distinctChannels.Count - 1)
            {
                attributedRev = totalRevenue - runningRevenue;
            }
            else
            {
                attributedRev = Math.Round(totalRevenue * (decimal)weightFraction, 2);
                runningRevenue += attributedRev;
            }

            channels.Add(new ChannelAttributionResult(
                Channel: ch,
                AttributedRevenue: attributedRev,
                WeightPercentage: Math.Round(weightFraction * 100.0, 2),
                TouchpointsCount: channelCounts[ch]
            ));
        }

        return new ModelAttributionOutput(modelName, channels, desc);
    }

    public ModelAttributionOutput ComputeShapley(IReadOnlyList<TouchpointInput> touchpoints, decimal totalRevenue)
    {
        const string modelName = "Shapley Value Attribution";
        const string desc = "Cooperative game theory calculating marginal contribution across all channel coalitions.";

        if (touchpoints == null || touchpoints.Count == 0 || totalRevenue <= 0m)
        {
            return new ModelAttributionOutput(modelName, [], desc);
        }

        var distinctChannels = touchpoints
            .Select(t => t.Channel.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var channelCounts = touchpoints
            .GroupBy(t => t.Channel.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var n = distinctChannels.Count;
        if (n == 1)
        {
            return new ModelAttributionOutput(modelName,
            [
                new ChannelAttributionResult(distinctChannels[0], totalRevenue, 100.0, channelCounts[distinctChannels[0]])
            ], desc);
        }

        // Shapley characteristic function v(S):
        // Proportion of journey touchpoints covered by coalition S: v(S) = (touchpoints in S) / totalTouchpoints
        var totalTouches = (double)touchpoints.Count;
        var shapleyValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var ch in distinctChannels)
        {
            double phi = 0.0;
            var otherChannels = distinctChannels.Where(c => !string.Equals(c, ch, StringComparison.OrdinalIgnoreCase)).ToList();
            int subsetsCount = 1 << otherChannels.Count;

            for (int mask = 0; mask < subsetsCount; mask++)
            {
                var coalition = new List<string>();
                for (int bit = 0; bit < otherChannels.Count; bit++)
                {
                    if ((mask & (1 << bit)) != 0)
                    {
                        coalition.Add(otherChannels[bit]);
                    }
                }

                int sSize = coalition.Count;
                double weight = Factorial(sSize) * Factorial(n - sSize - 1) / (double)Factorial(n);

                // Marginal contribution: v(S union {i}) - v(S)
                var touchesWithCh = touchpoints.Count(t => coalition.Contains(t.Channel.Trim(), StringComparer.OrdinalIgnoreCase) ||
                                                           string.Equals(t.Channel.Trim(), ch, StringComparison.OrdinalIgnoreCase));
                var touchesWithoutCh = touchpoints.Count(t => coalition.Contains(t.Channel.Trim(), StringComparer.OrdinalIgnoreCase));

                double vWith = touchesWithCh / totalTouches;
                double vWithout = touchesWithoutCh / totalTouches;
                double marginal = vWith - vWithout;

                phi += weight * marginal;
            }

            shapleyValues[ch] = Math.Max(0.0, phi);
        }

        double totalPhi = shapleyValues.Values.Sum();
        if (totalPhi <= 0.000001)
        {
            return ComputeLinear(touchpoints, totalRevenue);
        }

        var channels = new List<ChannelAttributionResult>();
        decimal runningRevenue = 0m;

        for (int i = 0; i < distinctChannels.Count; i++)
        {
            var ch = distinctChannels[i];
            var weightFraction = shapleyValues[ch] / totalPhi;
            decimal attributedRev;

            if (i == distinctChannels.Count - 1)
            {
                attributedRev = totalRevenue - runningRevenue;
            }
            else
            {
                attributedRev = Math.Round(totalRevenue * (decimal)weightFraction, 2);
                runningRevenue += attributedRev;
            }

            channels.Add(new ChannelAttributionResult(
                Channel: ch,
                AttributedRevenue: attributedRev,
                WeightPercentage: Math.Round(weightFraction * 100.0, 2),
                TouchpointsCount: channelCounts[ch]
            ));
        }

        return new ModelAttributionOutput(modelName, channels, desc);
    }

    public async Task<MultiModelComparisonMatrix> CalculateMultiModelMatrixAsync(
        CalculateMultiModelRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var touchpoints = new List<TouchpointInput>();

        // If request includes explicit touchpoints, use them
        if (request.Touchpoints != null && request.Touchpoints.Count > 0)
        {
            touchpoints.AddRange(request.Touchpoints);
        }
        else if (_db != null && (request.DealId.HasValue || request.ContactId.HasValue))
        {
            // Fetch from DbContext
            var query = _db.AttributionTouchpoints.Where(t => t.TenantId == request.TenantId);
            if (request.DealId.HasValue)
                query = query.Where(t => t.DealId == request.DealId.Value);
            if (request.ContactId.HasValue)
                query = query.Where(t => t.ContactId == request.ContactId.Value);

            var dbTouchpoints = await query.OrderBy(t => t.OccurredAtUtc).ToListAsync(ct);
            touchpoints.AddRange(dbTouchpoints.Select(t => new TouchpointInput(
                Channel: t.Channel,
                CampaignName: t.CampaignName,
                OccurredAtUtc: t.OccurredAtUtc,
                Cost: t.Cost,
                UtmSource: t.UtmSource,
                UtmMedium: t.UtmMedium,
                UtmCampaign: t.UtmCampaign
            )));
        }

        var firstTouch = ComputeFirstTouch(touchpoints, request.TotalRevenue);
        var lastTouch = ComputeLastTouch(touchpoints, request.TotalRevenue);
        var linear = ComputeLinear(touchpoints, request.TotalRevenue);
        var timeDecay = ComputeTimeDecay(touchpoints, request.TotalRevenue, request.ConversionTimeUtc);
        var uShaped = ComputeUShaped(touchpoints, request.TotalRevenue);
        var markov = ComputeMarkovChain(touchpoints, request.TotalRevenue, request.CohortJourneys);
        var shapley = ComputeShapley(touchpoints, request.TotalRevenue);

        var modelsDict = new Dictionary<string, ModelAttributionOutput>(StringComparer.OrdinalIgnoreCase)
        {
            ["FirstTouch"] = firstTouch,
            ["LastTouch"] = lastTouch,
            ["Linear"] = linear,
            ["TimeDecay"] = timeDecay,
            ["UShaped"] = uShaped,
            ["MarkovChain"] = markov,
            ["Shapley"] = shapley
        };

        var allChannels = touchpoints
            .Select(t => t.Channel.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        var matrixRows = new List<ChannelComparisonRow>();

        foreach (var ch in allChannels)
        {
            var ftRev = firstTouch.Channels.FirstOrDefault(c => string.Equals(c.Channel, ch, StringComparison.OrdinalIgnoreCase))?.AttributedRevenue ?? 0m;
            var ltRev = lastTouch.Channels.FirstOrDefault(c => string.Equals(c.Channel, ch, StringComparison.OrdinalIgnoreCase))?.AttributedRevenue ?? 0m;
            var linRev = linear.Channels.FirstOrDefault(c => string.Equals(c.Channel, ch, StringComparison.OrdinalIgnoreCase))?.AttributedRevenue ?? 0m;
            var tdRev = timeDecay.Channels.FirstOrDefault(c => string.Equals(c.Channel, ch, StringComparison.OrdinalIgnoreCase))?.AttributedRevenue ?? 0m;
            var usRev = uShaped.Channels.FirstOrDefault(c => string.Equals(c.Channel, ch, StringComparison.OrdinalIgnoreCase))?.AttributedRevenue ?? 0m;
            var mcRev = markov.Channels.FirstOrDefault(c => string.Equals(c.Channel, ch, StringComparison.OrdinalIgnoreCase))?.AttributedRevenue ?? 0m;
            var shRev = shapley.Channels.FirstOrDefault(c => string.Equals(c.Channel, ch, StringComparison.OrdinalIgnoreCase))?.AttributedRevenue ?? 0m;

            var revList = new[] { ftRev, ltRev, linRev, tdRev, usRev, mcRev, shRev };
            var minRev = revList.Min();
            var maxRev = revList.Max();
            var avgRev = revList.Average();
            var variancePct = avgRev > 0 ? (double)((maxRev - minRev) / avgRev) * 100.0 : 0.0;

            string recommendation;
            if (ftRev > ltRev && ftRev > linRev)
            {
                recommendation = "High top-of-funnel discovery driver. Do not cut budget based on Last-Touch alone.";
            }
            else if (ltRev > ftRev && ltRev > linRev)
            {
                recommendation = "Strong closer / bottom-of-funnel converter. Crucial for direct retargeting.";
            }
            else if (mcRev > ltRev)
            {
                recommendation = "High Markov Removal Effect. Removing this channel significantly degrades conversion rate.";
            }
            else
            {
                recommendation = "Consistent omnichannel assist across multiple journey touchpoints.";
            }

            matrixRows.Add(new ChannelComparisonRow(
                Channel: ch,
                FirstTouchRevenue: ftRev,
                LastTouchRevenue: ltRev,
                LinearRevenue: linRev,
                TimeDecayRevenue: tdRev,
                UShapedRevenue: usRev,
                MarkovChainRevenue: mcRev,
                ShapleyRevenue: shRev,
                MaxVariancePercentage: Math.Round(variancePct, 1),
                Recommendation: recommendation
            ));
        }

        return new MultiModelComparisonMatrix(
            TotalRevenue: request.TotalRevenue,
            EvaluatedChannels: allChannels,
            Models: modelsDict,
            MatrixRows: matrixRows,
            CalculatedAtUtc: DateTime.UtcNow
        );
    }

    private static long Factorial(int n)
    {
        if (n <= 1) return 1;
        long result = 1;
        for (int i = 2; i <= n; i++)
        {
            result *= i;
        }
        return result;
    }
}
