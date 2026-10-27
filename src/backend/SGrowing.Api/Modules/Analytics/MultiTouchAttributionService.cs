using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Analytics;

public class MultiTouchAttributionService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<MultiTouchAttributionService>? _logger;

    public MultiTouchAttributionService(SGrowingDbContext db, ILogger<MultiTouchAttributionService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<AttributionTouchpoint> RecordTouchpointAsync(
        Guid tenantId,
        Guid contactId,
        string channel,
        string campaignName,
        TouchpointStage stage,
        Guid? dealId = null,
        decimal cost = 0,
        string? utmSource = null,
        string? utmMedium = null,
        string? utmCampaign = null,
        CancellationToken cancellationToken = default)
    {
        var touchpoint = AttributionTouchpoint.Create(
            tenantId, contactId, channel, campaignName, stage, dealId, cost, utmSource, utmMedium, utmCampaign);

        await _db.AttributionTouchpoints.AddAsync(touchpoint, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Recorded touchpoint {Id} for contact {ContactId} via {Channel} ({CampaignName})",
            touchpoint.Id, contactId, channel, campaignName);

        return touchpoint;
    }

    public async Task<List<AttributedRevenueItem>> CalculateAttributionAsync(
        Guid tenantId,
        Guid dealId,
        AttributionModelType model = AttributionModelType.WShaped,
        CancellationToken cancellationToken = default)
    {
        var deal = await _db.Deals
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == dealId, cancellationToken);

        if (deal == null)
        {
            throw new KeyNotFoundException($"Deal {dealId} not found for tenant {tenantId}.");
        }

        var touchpoints = await _db.AttributionTouchpoints
            .Where(t => t.TenantId == tenantId && (t.DealId == dealId || t.ContactId == deal.ContactId))
            .OrderBy(t => t.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        if (touchpoints.Count == 0)
        {
            return new List<AttributedRevenueItem>();
        }

        decimal totalRevenue = deal.Amount;
        var results = new List<AttributedRevenueItem>();

        switch (model)
        {
            case AttributionModelType.FirstTouch:
            {
                var first = touchpoints.First();
                results.Add(new AttributedRevenueItem
                {
                    TouchpointId = first.Id,
                    Channel = first.Channel,
                    CampaignName = first.CampaignName,
                    Stage = first.Stage,
                    AttributedRevenue = totalRevenue,
                    WeightPercent = 100m
                });
                break;
            }

            case AttributionModelType.LastTouch:
            {
                var last = touchpoints.Last();
                results.Add(new AttributedRevenueItem
                {
                    TouchpointId = last.Id,
                    Channel = last.Channel,
                    CampaignName = last.CampaignName,
                    Stage = last.Stage,
                    AttributedRevenue = totalRevenue,
                    WeightPercent = 100m
                });
                break;
            }

            case AttributionModelType.Linear:
            {
                decimal weight = 100m / touchpoints.Count;
                decimal perItemRev = Math.Round(totalRevenue / touchpoints.Count, 2);

                foreach (var tp in touchpoints)
                {
                    results.Add(new AttributedRevenueItem
                    {
                        TouchpointId = tp.Id,
                        Channel = tp.Channel,
                        CampaignName = tp.CampaignName,
                        Stage = tp.Stage,
                        AttributedRevenue = perItemRev,
                        WeightPercent = Math.Round(weight, 2)
                    });
                }
                break;
            }

            case AttributionModelType.TimeDecay:
            {
                var latestTime = touchpoints.Last().OccurredAtUtc;
                const double halfLifeDays = 7.0;

                // Calculate exponential decay weight: 2^(-deltaDays / halfLifeDays)
                var rawWeights = touchpoints.Select(tp =>
                {
                    var deltaDays = Math.Max(0.0, (latestTime - tp.OccurredAtUtc).TotalDays);
                    return Math.Pow(2.0, -deltaDays / halfLifeDays);
                }).ToList();

                var totalWeight = rawWeights.Sum();
                if (totalWeight <= 0) totalWeight = 1.0;

                for (int i = 0; i < touchpoints.Count; i++)
                {
                    var tp = touchpoints[i];
                    decimal weightFraction = (decimal)(rawWeights[i] / totalWeight);
                    decimal weightPercent = Math.Round(weightFraction * 100m, 2);
                    decimal revenueShare = Math.Round(totalRevenue * weightFraction, 2);

                    results.Add(new AttributedRevenueItem
                    {
                        TouchpointId = tp.Id,
                        Channel = tp.Channel,
                        CampaignName = tp.CampaignName,
                        Stage = tp.Stage,
                        AttributedRevenue = revenueShare,
                        WeightPercent = weightPercent
                    });
                }
                break;
            }

            case AttributionModelType.WShaped:
            default:
            {
                if (touchpoints.Count <= 2)
                {
                    // If 1 or 2 touchpoints, split equally
                    decimal weight = 100m / touchpoints.Count;
                    decimal share = Math.Round(totalRevenue / touchpoints.Count, 2);
                    foreach (var tp in touchpoints)
                    {
                        results.Add(new AttributedRevenueItem
                        {
                            TouchpointId = tp.Id,
                            Channel = tp.Channel,
                            CampaignName = tp.CampaignName,
                            Stage = tp.Stage,
                            AttributedRevenue = share,
                            WeightPercent = weight
                        });
                    }
                }
                else
                {
                    // W-Shaped: 40% First Discovery, 40% Lead Created, 20% Opportunity Created
                    var first = touchpoints.First();
                    var last = touchpoints.Last();
                    var middleTouchpoints = touchpoints.Skip(1).Take(touchpoints.Count - 2).ToList();

                    results.Add(new AttributedRevenueItem
                    {
                        TouchpointId = first.Id,
                        Channel = first.Channel,
                        CampaignName = first.CampaignName,
                        Stage = first.Stage,
                        AttributedRevenue = Math.Round(totalRevenue * 0.40m, 2),
                        WeightPercent = 40m
                    });

                    results.Add(new AttributedRevenueItem
                    {
                        TouchpointId = last.Id,
                        Channel = last.Channel,
                        CampaignName = last.CampaignName,
                        Stage = last.Stage,
                        AttributedRevenue = Math.Round(totalRevenue * 0.40m, 2),
                        WeightPercent = 40m
                    });

                    decimal midShare = Math.Round((totalRevenue * 0.20m) / middleTouchpoints.Count, 2);
                    decimal midWeight = 20m / middleTouchpoints.Count;

                    foreach (var mid in middleTouchpoints)
                    {
                        results.Add(new AttributedRevenueItem
                        {
                            TouchpointId = mid.Id,
                            Channel = mid.Channel,
                            CampaignName = mid.CampaignName,
                            Stage = mid.Stage,
                            AttributedRevenue = midShare,
                            WeightPercent = Math.Round(midWeight, 2)
                        });
                    }
                }
                break;
            }
        }

        return results;
    }

    public async Task<ExecutiveKpiSummary> CalculateExecutiveKpisAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var wonDeals = await _db.Deals
            .Where(d => d.TenantId == tenantId && d.Stage == DealStage.Won)
            .ToListAsync(cancellationToken);

        var totalSpend = await _db.AttributionTouchpoints
            .Where(t => t.TenantId == tenantId)
            .SumAsync(t => t.Cost, cancellationToken);

        decimal totalRevenue = wonDeals.Sum(d => d.Amount);
        int wonCount = wonDeals.Count;

        decimal roas = totalSpend > 0 ? Math.Round(totalRevenue / totalSpend, 2) : 0m;
        decimal cac = wonCount > 0 ? Math.Round(totalSpend / wonCount, 2) : 0m;
        decimal aov = wonCount > 0 ? Math.Round(totalRevenue / wonCount, 2) : 0m;

        return new ExecutiveKpiSummary
        {
            TotalRevenue = totalRevenue,
            TotalAdSpend = totalSpend,
            ReturnOnAdSpend = roas,
            CustomerAcquisitionCost = cac,
            AverageDealSize = aov,
            ClosedWonDealsCount = wonCount
        };
    }
}
