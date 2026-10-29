using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations.PresenceAndCsat;

public class CsatSurveyService : ICsatSurveyService
{
    private readonly SGrowingDbContext _db;

    public CsatSurveyService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<CsatSurveyResult> RecordCsatResponseAsync(
        Guid tenantId,
        Guid conversationId,
        Guid customerContactId,
        string customerName,
        int score,
        string channel = "WhatsApp",
        string feedbackText = "")
    {
        var result = CsatSurveyResult.Create(
            tenantId,
            conversationId,
            customerContactId,
            customerName,
            score,
            channel,
            feedbackText);

        _db.CsatSurveyResults.Add(result);
        await _db.SaveChangesAsync();

        SGrowingTelemetry.CsatSurveysRecordedCounter.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId.ToString()),
            new KeyValuePair<string, object?>("channel", channel));

        return result;
    }

    public async Task<CsatSurveyResult?> GetConversationCsatAsync(Guid tenantId, Guid conversationId)
    {
        return await _db.CsatSurveyResults
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ConversationId == conversationId);
    }

    public async Task<CsatMetricsSummaryDto> GetTenantCsatMetricsAsync(Guid tenantId)
    {
        var records = await _db.CsatSurveyResults
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .ToListAsync();

        if (records.Count == 0)
        {
            return new CsatMetricsSummaryDto(tenantId, 0, 0.0, 0.0, 0.0, new Dictionary<string, int>());
        }

        var total = records.Count;
        var avgScore = Math.Round(records.Average(r => r.Score), 2);
        var satisfiedCount = records.Count(r => r.Score >= 4);
        var satisfactionPct = Math.Round(((double)satisfiedCount / total) * 100.0, 1);

        // NPS mapping: 5 = Promoter, 4 = Passive, 1-3 = Detractor
        var promoters = records.Count(r => r.Score == 5);
        var detractors = records.Count(r => r.Score <= 3);
        var nps = Math.Round((((double)(promoters - detractors)) / total) * 100.0, 1);

        var channelBreakdown = records
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Channel) ? "Unknown" : r.Channel)
            .ToDictionary(g => g.Key, g => g.Count());

        return new CsatMetricsSummaryDto(
            tenantId,
            total,
            avgScore,
            satisfactionPct,
            nps,
            channelBreakdown);
    }
}
