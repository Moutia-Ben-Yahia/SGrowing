using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public record RecordClickRequest(string CouponCode);
public record RecordConversionRequest(string CouponCode, decimal OrderValue);

public static class SocialCommentEndpoints
{
    private static readonly Guid DefaultDemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapSocialCommentToDmEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/social/comment-to-dm")
            .WithTags("Social Media Comment-to-DM Inbound Lead Conversion");

        // 1. GET /rules - List all rules for tenant
        group.MapGet("/rules", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromServices] ISocialCommentToDmService service) =>
        {
            var effectiveTenant = ResolveTenantId(headerTenantId, tenantId);
            var rules = await service.GetRulesAsync(effectiveTenant);
            return Results.Ok(rules);
        });

        // 2. GET /rules/{id} - Get a specific rule
        group.MapGet("/rules/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromServices] ISocialCommentToDmService service) =>
        {
            var effectiveTenant = ResolveTenantId(headerTenantId, tenantId);
            var rule = await service.GetRuleByIdAsync(effectiveTenant, id);
            return rule is not null ? Results.Ok(rule) : Results.NotFound(new { message = "Rule not found." });
        });

        // 3. POST /rules - Create or update rule
        group.MapPost("/rules", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromBody] CreateOrUpdateRuleDto dto,
            [FromServices] ISocialCommentToDmService service) =>
        {
            var effectiveTenant = ResolveTenantId(headerTenantId, tenantId, dto.TenantId);

            SocialPlatform? platform = null;
            if (!string.IsNullOrWhiteSpace(dto.Platform) && !string.Equals(dto.Platform, "All", StringComparison.OrdinalIgnoreCase))
            {
                platform = SocialCommentToDmService.ResolvePlatform(dto.Platform);
            }

            var rule = new SocialCommentRule
            {
                Id = dto.Id.HasValue && dto.Id.Value != Guid.Empty ? dto.Id.Value : Guid.NewGuid(),
                TenantId = effectiveTenant,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? "Inbound Keyword Rule" : dto.Name.Trim(),
                Platform = platform,
                MatchType = dto.MatchType,
                Keywords = dto.Keywords ?? new List<string>(),
                RegexPattern = dto.RegexPattern,
                PublicReplyTemplates = dto.PublicReplyTemplates != null && dto.PublicReplyTemplates.Count > 0
                    ? dto.PublicReplyTemplates
                    : SocialCommentToDmService.GetDefaultPublicReplies(),
                PrivateDmTemplate = string.IsNullOrWhiteSpace(dto.PrivateDmTemplate)
                    ? "Hey @{{username}}! 👋 Here is your exclusive discount code: {{discount_code}}\n\nDirect link: {{checkout_url}}"
                    : dto.PrivateDmTemplate,
                DiscountPrefix = string.IsNullOrWhiteSpace(dto.DiscountPrefix) ? "VIP" : dto.DiscountPrefix.Trim().ToUpperInvariant(),
                DiscountPercent = dto.DiscountPercent ?? 20,
                BaseCheckoutUrl = string.IsNullOrWhiteSpace(dto.BaseCheckoutUrl) ? "https://sgrowing.com/checkout" : dto.BaseCheckoutUrl.Trim(),
                UtmSource = string.IsNullOrWhiteSpace(dto.UtmSource) ? "social" : dto.UtmSource.Trim(),
                UtmMedium = string.IsNullOrWhiteSpace(dto.UtmMedium) ? "comment_to_dm" : dto.UtmMedium.Trim(),
                UtmCampaign = string.IsNullOrWhiteSpace(dto.UtmCampaign) ? "inbound_lead_h14" : dto.UtmCampaign.Trim(),
                MinJitterSeconds = dto.MinJitterSeconds ?? 5,
                MaxJitterSeconds = dto.MaxJitterSeconds ?? 30,
                MaxDmsPerHour = dto.MaxDmsPerHour ?? 60,
                MaxDmsPerMinute = dto.MaxDmsPerMinute ?? 10,
                IsEnabled = dto.IsEnabled ?? true,
                Priority = dto.Priority ?? 1
            };

            var saved = await service.CreateOrUpdateRuleAsync(rule);
            return Results.Ok(saved);
        });

        // 4. DELETE /rules/{id} - Delete a rule
        group.MapDelete("/rules/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromServices] ISocialCommentToDmService service) =>
        {
            var effectiveTenant = ResolveTenantId(headerTenantId, tenantId);
            var deleted = await service.DeleteRuleAsync(effectiveTenant, id);
            return deleted ? Results.Ok(new { success = true }) : Results.NotFound(new { message = "Rule not found." });
        });

        // 5. POST /webhook - Simulate or ingest incoming social comment event
        group.MapPost("/webhook", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromBody] IncomingSocialCommentDto incoming,
            [FromServices] ISocialCommentToDmService service) =>
        {
            var effectiveTenant = ResolveTenantId(headerTenantId, tenantId, incoming.TenantId);
            var normalizedIncoming = incoming with { TenantId = effectiveTenant };

            var result = await service.ProcessCommentAsync(normalizedIncoming);
            return Results.Ok(result);
        });

        // 6. GET /logs - Recent comment processing logs
        group.MapGet("/logs", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromQuery] int? limit,
            [FromServices] ISocialCommentToDmService service) =>
        {
            var effectiveTenant = ResolveTenantId(headerTenantId, tenantId);
            var logs = await service.GetLogsAsync(effectiveTenant, limit ?? 50);
            return Results.Ok(logs);
        });

        // 7. GET /analytics - Funnel metrics
        group.MapGet("/analytics", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromServices] ISocialCommentToDmService service) =>
        {
            var effectiveTenant = ResolveTenantId(headerTenantId, tenantId);
            var metrics = await service.GetFunnelMetricsAsync(effectiveTenant);
            return Results.Ok(metrics);
        });

        // 8. POST /click - Simulate link click from DM
        group.MapPost("/click", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromBody] RecordClickRequest request,
            [FromServices] ISocialCommentToDmService service) =>
        {
            var effectiveTenant = ResolveTenantId(headerTenantId, tenantId);
            var success = await service.RecordClickAsync(effectiveTenant, request.CouponCode);
            return Results.Ok(new { success });
        });

        // 9. POST /conversion - Simulate checkout order conversion from DM
        group.MapPost("/conversion", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromBody] RecordConversionRequest request,
            [FromServices] ISocialCommentToDmService service) =>
        {
            var effectiveTenant = ResolveTenantId(headerTenantId, tenantId);
            var success = await service.RecordConversionAsync(effectiveTenant, request.CouponCode, request.OrderValue);
            return Results.Ok(new { success });
        });

        return app;
    }

    private static Guid ResolveTenantId(Guid? headerTenantId, Guid? queryTenantId, Guid? bodyTenantId = null)
    {
        if (bodyTenantId.HasValue && bodyTenantId.Value != Guid.Empty) return bodyTenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        if (queryTenantId.HasValue && queryTenantId.Value != Guid.Empty) return queryTenantId.Value;
        return DefaultDemoTenantId;
    }
}
