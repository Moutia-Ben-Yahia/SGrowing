using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public record CompetitorRadarSummaryDto(
    int TotalCompetitorAdsMonitored,
    int HighThreatCompetitorsCount,
    int SkusWithPriceDeficit,
    List<CompetitorPriceTracker> PriceAlerts,
    List<CompetitorAdRecord> ActiveCompetitorCampaigns
);

public interface ICompetitorIntelligenceService
{
    Task<CompetitorAdRecord> IngestCompetitorAdAsync(
        Guid tenantId,
        string competitorName,
        string platform,
        string hookSnippet,
        string offerDetails,
        decimal estimatedDailySpend,
        string threatLevel = "Medium");
    Task<CompetitorPriceTracker> TrackPriceDisparityAsync(
        Guid tenantId,
        string sku,
        string competitorName,
        string url,
        decimal competitorPrice,
        decimal ourPrice);
    Task<CompetitorRadarSummaryDto> GetRadarSummaryAsync(Guid tenantId);
}

public class CompetitorIntelligenceService : ICompetitorIntelligenceService
{
    private readonly SGrowingDbContext _db;

    public CompetitorIntelligenceService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<CompetitorAdRecord> IngestCompetitorAdAsync(
        Guid tenantId,
        string competitorName,
        string platform,
        string hookSnippet,
        string offerDetails,
        decimal estimatedDailySpend,
        string threatLevel = "Medium")
    {
        var record = CompetitorAdRecord.Create(
            tenantId,
            competitorName,
            platform,
            hookSnippet,
            offerDetails,
            estimatedDailySpend,
            threatLevel
        );

        _db.CompetitorAdRecords.Add(record);
        await _db.SaveChangesAsync();
        return record;
    }

    public async Task<CompetitorPriceTracker> TrackPriceDisparityAsync(
        Guid tenantId,
        string sku,
        string competitorName,
        string url,
        decimal competitorPrice,
        decimal ourPrice)
    {
        var existing = await _db.CompetitorPriceTrackers
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.TargetSku == sku.Trim() && p.CompetitorName == competitorName.Trim());

        if (existing != null)
        {
            existing.CompetitorPrice = competitorPrice;
            existing.OurPrice = ourPrice;
            existing.PriceDifferencePercentage = ourPrice > 0 ? Math.Round(((competitorPrice - ourPrice) / ourPrice) * 100m, 1) : 0;
            existing.LastCheckedAtUtc = DateTime.UtcNow;

            if (competitorPrice < ourPrice * 0.85m)
                existing.RecommendedCounterAction = "BundleIncentive";
            else if (competitorPrice < ourPrice)
                existing.RecommendedCounterAction = "FlashDiscount";
            else
                existing.RecommendedCounterAction = "MaintainPrice";

            await _db.SaveChangesAsync();
            return existing;
        }

        var tracker = CompetitorPriceTracker.Create(tenantId, sku, competitorName, url, competitorPrice, ourPrice);
        _db.CompetitorPriceTrackers.Add(tracker);
        await _db.SaveChangesAsync();
        return tracker;
    }

    public async Task<CompetitorRadarSummaryDto> GetRadarSummaryAsync(Guid tenantId)
    {
        var ads = await _db.CompetitorAdRecords
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .OrderByDescending(a => a.EstimatedDailySpend)
            .Take(20)
            .ToListAsync();

        var priceTrackers = await _db.CompetitorPriceTrackers
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.PriceDifferencePercentage)
            .Take(20)
            .ToListAsync();

        int highThreats = ads.Count(a => a.ThreatLevel == "High");
        int priceDeficits = priceTrackers.Count(p => p.CompetitorPrice < p.OurPrice);

        return new CompetitorRadarSummaryDto(
            ads.Count,
            highThreats,
            priceDeficits,
            priceTrackers,
            ads
        );
    }
}
