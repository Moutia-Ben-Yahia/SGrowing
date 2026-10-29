using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.SEO;

public class SerpRankTrackerService : ISerpRankTrackerService
{
    private readonly SGrowingDbContext _db;

    public SerpRankTrackerService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<SerpRankHistoryRecord> RecordDailyRankAsync(
        Guid tenantId,
        string keyword,
        string targetUrl,
        int position,
        string deviceType = "Desktop",
        string countryCode = "TN",
        List<string>? serpFeatures = null,
        Guid? keywordId = null)
    {
        var cleanKw = keyword.Trim().ToLowerInvariant();
        var prevRecord = await _db.SerpRankHistoryRecords
            .Where(r => r.TenantId == tenantId && r.Keyword.ToLower() == cleanKw && r.DeviceType == deviceType)
            .OrderByDescending(r => r.RecordedDateUtc)
            .FirstOrDefaultAsync();

        var prevPos = prevRecord?.Position ?? 0;

        var record = SerpRankHistoryRecord.Create(
            tenantId,
            keyword,
            targetUrl,
            position,
            prevPos,
            deviceType,
            countryCode,
            serpFeatures,
            keywordId);

        _db.SerpRankHistoryRecords.Add(record);
        await _db.SaveChangesAsync();

        return record;
    }

    public async Task<List<SerpRankHistoryRecord>> GetKeywordRankHistoryAsync(
        Guid tenantId,
        string keyword,
        int days = 30)
    {
        var cleanKw = keyword.Trim().ToLowerInvariant();
        var since = DateTime.UtcNow.AddDays(-days);

        return await _db.SerpRankHistoryRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Keyword.ToLower() == cleanKw && r.RecordedDateUtc >= since)
            .OrderBy(r => r.RecordedDateUtc)
            .ToListAsync();
    }

    public async Task<List<KeywordRankOverviewDto>> GetDailyRankOverviewAsync(Guid tenantId)
    {
        var allRecords = await _db.SerpRankHistoryRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.RecordedDateUtc)
            .ToListAsync();

        var grouped = allRecords
            .GroupBy(r => (r.Keyword.ToLowerInvariant(), r.DeviceType.ToLowerInvariant()));

        var result = new List<KeywordRankOverviewDto>();
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        foreach (var g in grouped)
        {
            var ordered = g.OrderByDescending(r => r.RecordedDateUtc).ToList();
            var latest = ordered.First();
            var sevenDaysRecord = ordered.FirstOrDefault(r => r.RecordedDateUtc <= sevenDaysAgo);

            int delta7d = 0;
            if (sevenDaysRecord != null)
            {
                delta7d = sevenDaysRecord.Position - latest.Position; // e.g. was 10, now 6 -> +4 positions
            }
            else if (latest.PreviousPosition > 0)
            {
                delta7d = latest.PreviousPosition - latest.Position;
            }

            var bestPos = ordered.Min(r => r.Position);
            var features = JsonSerializer.Deserialize<List<string>>(latest.SerpFeaturesJson) ?? new List<string>();

            result.Add(new KeywordRankOverviewDto(
                latest.Keyword,
                latest.TargetUrl,
                latest.Position,
                delta7d,
                bestPos,
                latest.DeviceType,
                latest.CountryCode,
                features,
                latest.RecordedDateUtc));
        }

        return result.OrderBy(r => r.CurrentPosition).ToList();
    }
}
