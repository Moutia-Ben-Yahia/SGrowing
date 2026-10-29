using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.SEO;

public record KeywordRankOverviewDto(
    string Keyword,
    string TargetUrl,
    int CurrentPosition,
    int Delta7Days,
    int BestPosition,
    string DeviceType,
    string CountryCode,
    List<string> SerpFeatures,
    DateTime LastUpdatedUtc
);

public interface ISerpRankTrackerService
{
    Task<SerpRankHistoryRecord> RecordDailyRankAsync(
        Guid tenantId,
        string keyword,
        string targetUrl,
        int position,
        string deviceType = "Desktop",
        string countryCode = "TN",
        List<string>? serpFeatures = null,
        Guid? keywordId = null);

    Task<List<SerpRankHistoryRecord>> GetKeywordRankHistoryAsync(
        Guid tenantId,
        string keyword,
        int days = 30);

    Task<List<KeywordRankOverviewDto>> GetDailyRankOverviewAsync(
        Guid tenantId);
}
