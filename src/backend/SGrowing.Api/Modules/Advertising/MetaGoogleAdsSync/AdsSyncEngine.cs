using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Advertising.MetaGoogleAdsSync;

public interface IAdsSyncEngine
{
    CapiUploadResult UploadConversions(CapiUploadRequest request);
    Task<CapiUploadResult> UploadConversionsAsync(CapiUploadRequest request, CancellationToken cancellationToken = default);
    AudienceHashSyncResult SyncAudienceHashes(AudienceHashSyncRequest request);
    RoasOptimizationResult OptimizeRoas(RoasOptimizationRequest request);
}

public class AdsSyncEngine : IAdsSyncEngine
{
    // Deduplication store: key = tenantId:platform:eventId, value = event timestamp
    private readonly ConcurrentDictionary<string, DateTime> _seenEvents = new();
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly HttpClient? _httpClient;
    private readonly IConfiguration? _configuration;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly SGrowingDbContext? _dbContext;
    private readonly ILogger<AdsSyncEngine>? _logger;

    public AdsSyncEngine() : this(null, null, null, null, null, null)
    {
    }

    public AdsSyncEngine(
        IHttpClientFactory? httpClientFactory = null,
        IConfiguration? configuration = null,
        IServiceScopeFactory? scopeFactory = null,
        SGrowingDbContext? dbContext = null,
        ILogger<AdsSyncEngine>? logger = null,
        HttpClient? httpClient = null)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _dbContext = dbContext;
        _logger = logger;
        _httpClient = httpClient;
    }

    public CapiUploadResult UploadConversions(CapiUploadRequest request)
    {
        return UploadConversionsAsync(request).GetAwaiter().GetResult();
    }

    public async Task<CapiUploadResult> UploadConversionsAsync(CapiUploadRequest request, CancellationToken cancellationToken = default)
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
        var acceptedEvents = new List<ConversionEvent>();
        var eventsToPersist = new List<(ConversionEvent Event, string PayloadHash, bool IsDeduplicated)>();
        var now = DateTime.UtcNow;

        foreach (var evt in request.Events)
        {
            var dedupKey = $"{request.TenantId}:{request.Platform}:{evt.EventId}";
            bool isDedup = false;

            if (_seenEvents.TryGetValue(dedupKey, out var priorTime))
            {
                // If seen within 7 days, deduplicate
                if (now - priorTime < TimeSpan.FromDays(7))
                {
                    deduplicated++;
                    isDedup = true;
                }
            }

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

            var hash = ComputeSha256(normalizedData);

            if (isDedup)
            {
                eventsToPersist.Add((evt, hash, true));
                continue;
            }

            _seenEvents[dedupKey] = now;
            accepted++;
            payloadHashes.Add(hash);
            acceptedEvents.Add(evt);
            eventsToPersist.Add((evt, hash, false));
        }

        // External CAPI HTTP Dispatch if credentials configured, otherwise fallback to simulated success
        bool dispatchSuccess = true;
        string statusDetail = string.Empty;

        if (acceptedEvents.Count > 0)
        {
            if (request.Platform.Equals("Meta", StringComparison.OrdinalIgnoreCase))
            {
                var pixelId = _configuration?["Advertising:Meta:PixelId"];
                if (string.IsNullOrWhiteSpace(pixelId) && !request.AdAccountId.StartsWith("act_", StringComparison.OrdinalIgnoreCase))
                {
                    pixelId = request.AdAccountId;
                }
                var accessToken = _configuration?["Advertising:Meta:AccessToken"];

                if (!string.IsNullOrWhiteSpace(pixelId) && !string.IsNullOrWhiteSpace(accessToken))
                {
                    dispatchSuccess = await DispatchToMetaCapiAsync(pixelId, accessToken, acceptedEvents, request.TestMode, cancellationToken);
                    statusDetail = dispatchSuccess ? " (dispatched to Meta CAPI v21.0)" : " (Meta CAPI dispatch failed, logged)";
                }
                else
                {
                    _logger?.LogInformation("Meta CAPI credentials not configured; using simulated fallback for {Count} events.", acceptedEvents.Count);
                }
            }
            else if (request.Platform.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                var customerId = _configuration?["Advertising:Google:CustomerId"] ?? request.AdAccountId;
                var developerToken = _configuration?["Advertising:Google:DeveloperToken"];

                if (!string.IsNullOrWhiteSpace(customerId) && !string.IsNullOrWhiteSpace(developerToken))
                {
                    dispatchSuccess = await DispatchToGoogleAdsAsync(customerId, developerToken, acceptedEvents, cancellationToken);
                    statusDetail = dispatchSuccess ? " (dispatched to Google Ads Conversion API v17)" : " (Google Ads dispatch failed, logged)";
                }
                else
                {
                    _logger?.LogInformation("Google Ads credentials not configured; using simulated fallback for {Count} events.", acceptedEvents.Count);
                }
            }
        }

        // Persist conversions into SGrowingDbContext.CapiConversions
        await PersistConversionsToDbAsync(request.TenantId, request.Platform, eventsToPersist, cancellationToken);

        return new CapiUploadResult(
            Success: dispatchSuccess,
            Platform: request.Platform,
            ProcessedEventsCount: request.Events.Count,
            AcceptedEventsCount: accepted,
            DeduplicatedEventsCount: deduplicated,
            GeneratedPayloadHashes: payloadHashes,
            StatusMessage: $"Successfully processed {request.Events.Count} conversion events ({accepted} accepted, {deduplicated} deduplicated){statusDetail}.",
            DispatchedAtUtc: now
        );
    }

    private async Task<bool> DispatchToMetaCapiAsync(
        string pixelId,
        string accessToken,
        List<ConversionEvent> events,
        bool testMode,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory?.CreateClient("MetaCapi") ?? _httpClient ?? new HttpClient();

        var eventsData = events.Select(evt =>
        {
            var userData = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(evt.Email))
            {
                userData["em"] = new[] { ComputeSha256(evt.Email.Trim().ToLowerInvariant()) };
            }
            if (!string.IsNullOrWhiteSpace(evt.PhoneNumber))
            {
                userData["ph"] = new[] { ComputeSha256(NormalizePhone(evt.PhoneNumber)) };
            }
            if (!string.IsNullOrWhiteSpace(evt.ClientIpAddress))
            {
                userData["client_ip_address"] = evt.ClientIpAddress.Trim();
            }
            if (!string.IsNullOrWhiteSpace(evt.UserAgent))
            {
                userData["client_user_agent"] = evt.UserAgent.Trim();
            }
            if (!string.IsNullOrWhiteSpace(evt.Fbp))
            {
                userData["fbp"] = evt.Fbp.Trim();
            }
            if (!string.IsNullOrWhiteSpace(evt.Fbc))
            {
                userData["fbc"] = evt.Fbc.Trim();
            }

            var customData = new Dictionary<string, object?>
            {
                ["value"] = evt.ConversionValue,
                ["currency"] = evt.Currency
            };
            if (!string.IsNullOrWhiteSpace(evt.OrderId))
            {
                customData["order_id"] = evt.OrderId;
            }

            return new Dictionary<string, object?>
            {
                ["event_name"] = evt.EventName,
                ["event_time"] = new DateTimeOffset(evt.EventTimeUtc).ToUnixTimeSeconds(),
                ["event_id"] = evt.EventId,
                ["action_source"] = "website",
                ["user_data"] = userData,
                ["custom_data"] = customData
            };
        }).ToList();

        var payload = new Dictionary<string, object?>
        {
            ["data"] = eventsData
        };

        if (testMode)
        {
            payload["test_event_code"] = "TEST_SGROWING";
        }

        var json = JsonSerializer.Serialize(payload);
        var url = $"https://graph.facebook.com/v21.0/{pixelId}/events";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogWarning("Meta CAPI responded with {StatusCode}: {Response}", response.StatusCode, body);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "HTTP exception during Meta CAPI dispatch to {Url}", url);
            return false;
        }
    }

    private async Task<bool> DispatchToGoogleAdsAsync(
        string customerId,
        string developerToken,
        List<ConversionEvent> events,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory?.CreateClient("GoogleAdsCapi") ?? _httpClient ?? new HttpClient();
        var cleanCustomerId = customerId.Replace("-", "").Trim();

        var conversions = events.Select(evt =>
        {
            var userIdentifiers = new List<Dictionary<string, object?>>();
            if (!string.IsNullOrWhiteSpace(evt.Email))
            {
                userIdentifiers.Add(new Dictionary<string, object?>
                {
                    ["hashed_email"] = ComputeSha256(evt.Email.Trim().ToLowerInvariant())
                });
            }
            if (!string.IsNullOrWhiteSpace(evt.PhoneNumber))
            {
                userIdentifiers.Add(new Dictionary<string, object?>
                {
                    ["hashed_phone_number"] = ComputeSha256(NormalizePhone(evt.PhoneNumber))
                });
            }

            var conv = new Dictionary<string, object?>
            {
                ["conversion_action"] = $"customers/{cleanCustomerId}/conversionActions/{evt.EventName}",
                ["conversion_date_time"] = evt.EventTimeUtc.ToString("yyyy-MM-dd HH:mm:ss+00:00"),
                ["conversion_value"] = (double)evt.ConversionValue,
                ["currency_code"] = evt.Currency,
                ["order_id"] = evt.OrderId ?? evt.EventId,
                ["user_identifiers"] = userIdentifiers
            };
            if (!string.IsNullOrWhiteSpace(evt.Gclid))
            {
                conv["gclid"] = evt.Gclid;
            }
            return conv;
        }).ToList();

        var payload = new Dictionary<string, object?>
        {
            ["conversions"] = conversions,
            ["partial_failure"] = true
        };

        var json = JsonSerializer.Serialize(payload);
        var url = $"https://googleads.googleapis.com/v17/customers/{cleanCustomerId}:uploadClickConversions";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("developer-token", developerToken);

            var loginCustomerId = _configuration?["Advertising:Google:LoginCustomerId"];
            if (!string.IsNullOrWhiteSpace(loginCustomerId))
            {
                request.Headers.Add("login-customer-id", loginCustomerId.Replace("-", "").Trim());
            }

            var authToken = _configuration?["Advertising:Google:AccessToken"] ?? _configuration?["Advertising:Google:OAuthToken"];
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogWarning("Google Ads Conversion API responded with {StatusCode}: {Response}", response.StatusCode, body);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "HTTP exception during Google Ads conversion dispatch to {Url}", url);
            return false;
        }
    }

    private async Task PersistConversionsToDbAsync(
        string tenantId,
        string platform,
        List<(ConversionEvent Event, string PayloadHash, bool IsDeduplicated)> events,
        CancellationToken cancellationToken)
    {
        SGrowingDbContext? db = _dbContext;
        AsyncServiceScope? createdScope = null;

        if (db == null && _scopeFactory != null)
        {
            var scope = _scopeFactory.CreateAsyncScope();
            createdScope = scope;
            db = scope.ServiceProvider.GetService<SGrowingDbContext>();
        }

        if (db == null)
        {
            return;
        }

        try
        {
            foreach (var (evt, payloadHash, isDedup) in events)
            {
                var existing = await db.CapiConversions.FirstOrDefaultAsync(
                    c => c.TenantId == tenantId && c.Platform == platform && c.EventId == evt.EventId,
                    cancellationToken);

                if (existing == null)
                {
                    await db.CapiConversions.AddAsync(new CapiConversionEntity
                    {
                        EventId = evt.EventId,
                        TenantId = tenantId,
                        Platform = platform,
                        EventName = evt.EventName,
                        ConversionValue = evt.ConversionValue,
                        Currency = evt.Currency ?? "USD",
                        PayloadHash = payloadHash,
                        DispatchedAtUtc = DateTime.UtcNow,
                        IsDeduplicated = isDedup
                    }, cancellationToken);
                }
                else
                {
                    existing.IsDeduplicated = isDedup;
                    existing.PayloadHash = payloadHash;
                    existing.DispatchedAtUtc = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to persist CAPI conversions to database for tenant {TenantId}", tenantId);
        }
        finally
        {
            if (createdScope.HasValue)
            {
                await createdScope.Value.DisposeAsync();
            }
        }
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
