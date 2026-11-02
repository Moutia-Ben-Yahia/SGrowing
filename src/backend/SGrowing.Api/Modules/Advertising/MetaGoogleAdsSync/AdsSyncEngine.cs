using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Advertising.MetaGoogleAdsSync;

public interface IAdsSyncEngine
{
    CapiUploadResult UploadConversions(CapiUploadRequest request);
    AudienceHashSyncResult SyncAudienceHashes(AudienceHashSyncRequest request);
    RoasOptimizationResult OptimizeRoas(RoasOptimizationRequest request);
}

public class AdsSyncEngine : IAdsSyncEngine
{
    // Deduplication store: key = tenantId:platform:eventId, value = event timestamp
    private readonly ConcurrentDictionary<string, DateTime> _seenEvents = new();

    public CapiUploadResult UploadConversions(CapiUploadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.AdAccountId))
            throw new ArgumentException("AdAccountId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Platform))
            throw new ArgumentException("Platform is required.", nameof(request));

        int accepted = 0;
        int deduplicated = 0;
        var payloadHashes = new List<string>();
        var now = DateTime.UtcNow;

        foreach (var evt in request.Events)
        {
            var dedupKey = $"{request.TenantId}:{request.Platform}:{evt.EventId}";
            if (_seenEvents.TryGetValue(dedupKey, out var priorTime))
            {
                // If seen within 7 days, deduplicate
                if (now - priorTime < TimeSpan.FromDays(7))
                {
                    deduplicated++;
                    continue;
                }
            }

            _seenEvents[dedupKey] = now;
            accepted++;

            // Create normalized payload hash simulating server payload dispatch
            var normalizedData = $"{evt.EventId}:{evt.EventName}:{evt.EventTimeUtc:O}:{evt.ConversionValue}:{evt.Currency}";
            if (!string.IsNullOrWhiteSpace(evt.Email))
            {
                normalizedData += $":em_{ComputeSha256(evt.Email.Trim().ToLowerInvariant())}";
            }
            if (!string.IsNullOrWhiteSpace(evt.PhoneNumber))
            {
                normalizedData += $":ph_{ComputeSha256(NormalizePhone(evt.PhoneNumber))}";
            }

            payloadHashes.Add(ComputeSha256(normalizedData));
        }

        return new CapiUploadResult(
            Success: true,
            Platform: request.Platform,
            ProcessedEventsCount: request.Events.Count,
            AcceptedEventsCount: accepted,
            DeduplicatedEventsCount: deduplicated,
            GeneratedPayloadHashes: payloadHashes,
            StatusMessage: $"Successfully processed {request.Events.Count} conversion events ({accepted} accepted, {deduplicated} deduplicated).",
            DispatchedAtUtc: now
        );
    }

    public AudienceHashSyncResult SyncAudienceHashes(AudienceHashSyncRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.AudienceId))
            throw new ArgumentException("AudienceId is required.", nameof(request));

        var uniqueHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var email in request.RawEmails)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                var clean = email.Trim().ToLowerInvariant();
                uniqueHashes.Add(ComputeSha256(clean));
            }
        }

        foreach (var phone in request.RawPhoneNumbers)
        {
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var clean = NormalizePhone(phone);
                uniqueHashes.Add(ComputeSha256(clean));
            }
        }

        int totalInputRecords = request.RawEmails.Count + request.RawPhoneNumbers.Count;

        return new AudienceHashSyncResult(
            AudienceId: request.AudienceId,
            Platform: request.Platform,
            TotalRecords: totalInputRecords,
            ValidHashesGenerated: uniqueHashes.Count,
            Sha256Hashes: uniqueHashes.ToList(),
            SyncedAtUtc: DateTime.UtcNow
        );
    }

    public RoasOptimizationResult OptimizeRoas(RoasOptimizationRequest request)
    {
        if (request.Campaigns.Count == 0)
        {
            return new RoasOptimizationResult(
                TenantId: request.TenantId,
                CurrentTotalSpend: 0m,
                RecommendedTotalSpend: request.TotalBudget,
                BlendedCurrentRoas: 0m,
                ProjectedBlendedRoas: 0m,
                Recommendations: new List<CampaignBudgetRecommendation>(),
                EvaluatedAtUtc: DateTime.UtcNow
            );
        }

        decimal currentTotalSpend = request.Campaigns.Sum(c => c.Spend);
        decimal currentTotalRevenue = request.Campaigns.Sum(c => c.Revenue);
        decimal blendedCurrentRoas = currentTotalSpend > 0 ? Math.Round(currentTotalRevenue / currentTotalSpend, 2) : 0m;

        decimal budgetToAllocate = request.TotalBudget > 0 ? request.TotalBudget : currentTotalSpend;
        decimal targetRoas = request.TargetRoas > 0 ? request.TargetRoas : 3.0m;

        var recommendations = new List<CampaignBudgetRecommendation>();
        decimal budgetPoolFromTrims = 0m;
        var scaleCampaigns = new List<(CampaignPerformanceRecord Record, decimal CurrentRoas)>();

        // Phase 1: Assess each campaign and trim/pause underperformers
        foreach (var c in request.Campaigns)
        {
            decimal currentRoas = c.Spend > 0 ? Math.Round(c.Revenue / c.Spend, 2) : 0m;
            string action;
            decimal recommendedSpend;

            if (c.Spend > 0 && c.Conversions == 0 && c.Spend >= 50m)
            {
                action = "Pause";
                recommendedSpend = 0m;
                budgetPoolFromTrims += c.Spend;
            }
            else if (currentRoas >= targetRoas * 1.25m)
            {
                action = "Scale";
                scaleCampaigns.Add((c, currentRoas));
                recommendedSpend = c.Spend; // Will receive addition in Phase 2
            }
            else if (currentRoas < targetRoas * 0.75m && c.Spend > 0)
            {
                action = "Trim";
                decimal trimAmount = Math.Round(c.Spend * request.MaxShiftPercentage, 2);
                recommendedSpend = Math.Max(0m, c.Spend - trimAmount);
                budgetPoolFromTrims += (c.Spend - recommendedSpend);
            }
            else
            {
                action = "Maintain";
                recommendedSpend = c.Spend;
            }

            recommendations.Add(new CampaignBudgetRecommendation(
                CampaignId: c.CampaignId,
                CampaignName: c.CampaignName,
                Platform: c.Platform,
                CurrentSpend: c.Spend,
                RecommendedSpend: recommendedSpend,
                CurrentRoas: currentRoas,
                ProjectedRoas: currentRoas,
                AllocationAction: action
            ));
        }

        // Phase 2: Distribute trimmed pool and any delta budget to top scaling performers
        decimal additionalSpendPool = (budgetToAllocate - currentTotalSpend) + budgetPoolFromTrims;
        if (scaleCampaigns.Count > 0 && additionalSpendPool > 0)
        {
            decimal totalScaleWeight = scaleCampaigns.Sum(s => s.CurrentRoas);
            for (int i = 0; i < recommendations.Count; i++)
            {
                var r = recommendations[i];
                if (r.AllocationAction == "Scale")
                {
                    decimal weight = totalScaleWeight > 0 ? r.CurrentRoas / totalScaleWeight : (1m / scaleCampaigns.Count);
                    decimal addition = Math.Round(additionalSpendPool * weight, 2);
                    decimal newSpend = r.RecommendedSpend + addition;
                    // Marginal ROAS with diminishing returns factor (0.92)
                    decimal projectedRoas = Math.Round(r.CurrentRoas * 0.92m, 2);

                    recommendations[i] = r with
                    {
                        RecommendedSpend = newSpend,
                        ProjectedRoas = projectedRoas
                    };
                }
            }
        }

        decimal recommendedTotalSpend = recommendations.Sum(r => r.RecommendedSpend);
        decimal projectedTotalRevenue = recommendations.Sum(r => r.RecommendedSpend * r.ProjectedRoas);
        decimal projectedBlendedRoas = recommendedTotalSpend > 0 ? Math.Round(projectedTotalRevenue / recommendedTotalSpend, 2) : 0m;

        return new RoasOptimizationResult(
            TenantId: request.TenantId,
            CurrentTotalSpend: currentTotalSpend,
            RecommendedTotalSpend: recommendedTotalSpend,
            BlendedCurrentRoas: blendedCurrentRoas,
            ProjectedBlendedRoas: projectedBlendedRoas,
            Recommendations: recommendations,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }

    private static string NormalizePhone(string phone)
    {
        var clean = phone.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (!clean.StartsWith("+"))
        {
            clean = "+" + clean;
        }
        return clean;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
