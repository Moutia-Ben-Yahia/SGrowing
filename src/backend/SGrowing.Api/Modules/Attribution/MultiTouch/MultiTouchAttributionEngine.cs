using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Attribution.MultiTouch;

public interface IMultiTouchAttributionEngine
{
    CustomerTouchpoint RecordTouchpoint(CustomerTouchpoint touchpoint);
    List<CustomerTouchpoint> GetTouchpoints(string tenantId, string customerId);
    AttributionResult CalculateAttribution(AttributionCalculationRequest request);
    PredictiveLtvResult PredictCustomerLtv(PredictLtvRequest request);
}

public class MultiTouchAttributionEngine : IMultiTouchAttributionEngine
{
    private readonly ConcurrentDictionary<string, List<CustomerTouchpoint>> _touchpoints = new();
    private readonly object _lock = new();

    public CustomerTouchpoint RecordTouchpoint(CustomerTouchpoint touchpoint)
    {
        var validated = touchpoint.Id == Guid.Empty
            ? touchpoint with { Id = Guid.NewGuid() }
            : touchpoint;

        var key = BuildKey(validated.TenantId, validated.CustomerId);

        lock (_lock)
        {
            if (!_touchpoints.TryGetValue(key, out var list))
            {
                list = new List<CustomerTouchpoint>();
                _touchpoints[key] = list;
            }

            list.Add(validated);
        }

        return validated;
    }

    public List<CustomerTouchpoint> GetTouchpoints(string tenantId, string customerId)
    {
        var key = BuildKey(tenantId, customerId);

        lock (_lock)
        {
            if (_touchpoints.TryGetValue(key, out var list))
            {
                return list.OrderBy(t => t.TimestampUtc).ToList();
            }
        }

        return new List<CustomerTouchpoint>();
    }

    public AttributionResult CalculateAttribution(AttributionCalculationRequest request)
    {
        var touchpoints = GetTouchpoints(request.TenantId, request.CustomerId);
        if (touchpoints.Count == 0 || request.DealRevenue <= 0m)
        {
            return new AttributionResult(
                request.CustomerId,
                request.Model,
                request.DealRevenue,
                new List<AttributedChannelShare>(),
                DateTime.UtcNow
            );
        }

        var weights = ComputeTouchpointWeights(touchpoints, request.Model);

        // Aggregate by channel
        var channelGroups = touchpoints
            .Select((t, idx) => (Touchpoint: t, Weight: weights[idx]))
            .GroupBy(x => x.Touchpoint.Channel)
            .ToList();

        var channelShares = new List<AttributedChannelShare>();

        foreach (var group in channelGroups)
        {
            var channel = group.Key;
            var channelWeight = group.Sum(x => x.Weight);
            if (channelWeight <= 0.0)
            {
                continue;
            }
            var channelCost = group.Sum(x => x.Touchpoint.TouchCost);
            var attributedRev = Math.Round(request.DealRevenue * (decimal)channelWeight, 2, MidpointRounding.AwayFromZero);
            var safeCost = Math.Max(0.01m, channelCost);
            var roas = Math.Round(attributedRev / safeCost, 2, MidpointRounding.AwayFromZero);

            channelShares.Add(new AttributedChannelShare(
                Channel: channel,
                WeightPercent: Math.Round(channelWeight * 100.0, 4),
                AttributedRevenue: attributedRev,
                TotalCost: channelCost,
                Roas: roas
            ));
        }

        channelShares = channelShares
            .OrderByDescending(s => s.AttributedRevenue)
            .ThenBy(s => s.Channel)
            .ToList();

        return new AttributionResult(
            request.CustomerId,
            request.Model,
            request.DealRevenue,
            channelShares,
            DateTime.UtcNow
        );
    }

    private static double[] ComputeTouchpointWeights(List<CustomerTouchpoint> ordered, AttributionModelType model)
    {
        var n = ordered.Count;
        var weights = new double[n];

        switch (model)
        {
            case AttributionModelType.FirstTouch:
                weights[0] = 1.0;
                break;

            case AttributionModelType.LastTouch:
                weights[n - 1] = 1.0;
                break;

            case AttributionModelType.Linear:
                var linearWeight = 1.0 / n;
                for (var i = 0; i < n; i++)
                {
                    weights[i] = linearWeight;
                }
                break;

            case AttributionModelType.TimeDecay:
                // 7-day half-life exponential formula: weight = 2 ^ (- (t_close - t_touch).TotalDays / 7.0), then normalize sum to 1.0
                var dealCloseTouch = ordered.LastOrDefault(t => t.Stage == TouchpointStage.DealClose);
                var tClose = dealCloseTouch?.TimestampUtc ?? ordered[^1].TimestampUtc;
                double totalWeight = 0.0;

                for (var i = 0; i < n; i++)
                {
                    var deltaDays = Math.Max(0.0, (tClose - ordered[i].TimestampUtc).TotalDays);
                    var w = Math.Pow(2.0, -deltaDays / 7.0);
                    weights[i] = w;
                    totalWeight += w;
                }

                if (totalWeight > 0.0)
                {
                    for (var i = 0; i < n; i++)
                    {
                        weights[i] /= totalWeight;
                    }
                }
                else
                {
                    for (var i = 0; i < n; i++)
                    {
                        weights[i] = 1.0 / n;
                    }
                }
                break;

            case AttributionModelType.WShaped:
                if (n < 3)
                {
                    // If fewer touchpoints: evenly distributed
                    for (var i = 0; i < n; i++)
                    {
                        weights[i] = 1.0 / n;
                    }
                }
                else
                {
                    // 40% FirstDiscovery, 40% LeadCreation, 40% OpportunityCreation (or first, middle, last),
                    // remaining 20% evenly across intermediate touches, normalized to 100%.
                    var firstIdx = 0;
                    var midIdx = n / 2;
                    var lastIdx = n - 1;

                    var fdIdx = ordered.FindIndex(t => t.Stage == TouchpointStage.FirstDiscovery);
                    var lcIdx = ordered.FindIndex(t => t.Stage == TouchpointStage.LeadCreation);
                    var ocIdx = ordered.FindIndex(t => t.Stage == TouchpointStage.OpportunityCreation);

                    if (fdIdx >= 0 && lcIdx >= 0 && ocIdx >= 0 && fdIdx < lcIdx && lcIdx < ocIdx)
                    {
                        firstIdx = fdIdx;
                        midIdx = lcIdx;
                        lastIdx = ocIdx;
                    }

                    var rawWeights = new double[n];
                    rawWeights[firstIdx] = 0.40;
                    rawWeights[midIdx] = 0.40;
                    rawWeights[lastIdx] = 0.40;

                    var otherCount = n - 3;
                    if (otherCount > 0)
                    {
                        var intermediateWeight = 0.20 / otherCount;
                        for (var i = 0; i < n; i++)
                        {
                            if (i != firstIdx && i != midIdx && i != lastIdx)
                            {
                                rawWeights[i] = intermediateWeight;
                            }
                        }
                    }

                    var totalRaw = rawWeights.Sum();
                    for (var i = 0; i < n; i++)
                    {
                        weights[i] = rawWeights[i] / totalRaw;
                    }
                }
                break;
        }

        return weights;
    }

    public PredictiveLtvResult PredictCustomerLtv(PredictLtvRequest request)
    {
        var tenureDays = Math.Max(1.0, (DateTime.UtcNow - request.FirstSeenUtc).TotalDays);
        var tenureYears = Math.Max(tenureDays / 365.25, 0.25);

        var aov = request.CompletedTransactions > 0
            ? request.HistoricalRevenue / request.CompletedTransactions
            : request.HistoricalRevenue;

        if (aov < 0m) aov = 0m;

        var annualFrequency = request.CompletedTransactions / tenureYears;
        var annualRunRate = aov * (decimal)annualFrequency;

        double churnProb;
        if (request.CompletedTransactions <= 0)
        {
            churnProb = 0.85;
        }
        else
        {
            churnProb = Math.Clamp(0.50 * Math.Pow(0.82, request.CompletedTransactions - 1), 0.05, 0.80);
        }

        var retentionRate = 1.0 - churnProb;
        var churnProbabilityPercent = Math.Round(churnProb * 100.0, 2);

        var retentionRiskTier = churnProbabilityPercent switch
        {
            < 30.0 => "Low",
            <= 60.0 => "Medium",
            _ => "High"
        };

        var forward12Month = annualRunRate * (decimal)retentionRate;
        var forward36Month = forward12Month +
                             (annualRunRate * (decimal)Math.Pow(retentionRate, 2)) +
                             (annualRunRate * (decimal)Math.Pow(retentionRate, 3));

        var predicted12M = Math.Round(request.HistoricalRevenue + forward12Month, 2, MidpointRounding.AwayFromZero);
        var predicted36M = Math.Round(request.HistoricalRevenue + forward36Month, 2, MidpointRounding.AwayFromZero);

        return new PredictiveLtvResult(
            CustomerId: request.CustomerId,
            Predicted12MonthLtv: predicted12M,
            Predicted36MonthLtv: predicted36M,
            ChurnProbabilityPercent: churnProbabilityPercent,
            RetentionRiskTier: retentionRiskTier
        );
    }

    private static string BuildKey(string tenantId, string customerId) => $"{tenantId}:{customerId}";
}
