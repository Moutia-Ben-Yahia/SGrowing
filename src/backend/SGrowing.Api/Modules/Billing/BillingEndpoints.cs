using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Billing;

public record UpgradePlanDto(Guid TenantId, PlanTier TargetTier);
public record CheckQuotaDto(Guid TenantId, UsageMetricType Metric, int RequestedQuantity = 1);
public record RecordUsageDto(Guid TenantId, UsageMetricType Metric, long Quantity, string? Metadata = null);
public record CreateCheckoutDto(Guid TenantId, PlanTier TargetTier, string SuccessUrl);
public record StripeWebhookDto(Guid TenantId, string StripeInvoiceId, decimal Amount, string? Currency = "USD");

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/billing").WithTags("SaaS Subscription Billing & Plan Quotas");
        var v1Group = endpoints.MapGroup("/api/v1/billing").WithTags("SaaS Subscription Billing v1");

        // 1. Subscription & Plans
        group.MapGet("/subscription", async ([FromQuery] Guid tenantId, ISaasBillingService service) =>
        {
            var sub = await service.GetSubscriptionDetailsAsync(tenantId);
            return Results.Ok(sub);
        });

        group.MapPost("/upgrade", async ([FromBody] UpgradePlanDto req, ISaasBillingService service) =>
        {
            var sub = await service.UpgradePlanAsync(req.TenantId, req.TargetTier);
            return Results.Ok(sub);
        });

        // 2. Plan Quotas (Legacy & Tiered Enforcement)
        group.MapPost("/quota-check", async ([FromBody] CheckQuotaDto req, ISaasBillingService service) =>
        {
            var res = await service.VerifyQuotaAsync(req.TenantId, req.Metric, req.RequestedQuantity);
            return Results.Ok(res);
        });

        group.MapPost("/quota/enforce", async ([FromBody] CheckQuotaDto req, IPlanQuotaEnforcementService service) =>
        {
            var res = await service.CheckQuotaAsync(req.TenantId, req.Metric, req.RequestedQuantity);
            if (res.IsHardBlocked)
            {
                return Results.Json(res, statusCode: StatusCodes.Status403Forbidden);
            }
            return Results.Ok(res);
        });

        group.MapGet("/quota/status", async ([FromQuery] Guid tenantId, IPlanQuotaEnforcementService service) =>
        {
            var status = await service.GetTenantQuotaStatusAsync(tenantId);
            return Results.Ok(status);
        });

        group.MapPost("/usage", async ([FromBody] RecordUsageDto req, ISaasBillingService service) =>
        {
            var record = await service.RecordUsageAsync(req.TenantId, req.Metric, req.Quantity, req.Metadata);
            return Results.Ok(record);
        });

        group.MapPost("/checkout-session", async ([FromBody] CreateCheckoutDto req, ISaasBillingService service) =>
        {
            var url = await service.CreateCheckoutSessionUrlAsync(req.TenantId, req.TargetTier, req.SuccessUrl);
            return Results.Ok(new { checkoutUrl = url });
        });

        group.MapGet("/invoices", async ([FromQuery] Guid tenantId, ISaasBillingService service) =>
        {
            var invoices = await service.GetInvoicesAsync(tenantId);
            return Results.Ok(invoices);
        });

        // 3. Subscription Billing Webhooks Integration (Stripe, LemonSqueezy, Chargily)
        var registerWebhookRoute = (IEndpointRouteBuilder r) =>
        {
            r.MapPost("/webhooks", async (
                [FromBody] BillingWebhookPayloadDto payload,
                [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
                [FromQuery] Guid? queryTenantId,
                ISaasBillingService service) =>
            {
                var effectiveTenantId = payload.TenantId != Guid.Empty
                    ? payload.TenantId
                    : (headerTenantId ?? queryTenantId ?? Guid.Empty);

                if (effectiveTenantId == Guid.Empty)
                {
                    return Results.BadRequest(new { error = "TenantId is required in body, X-Tenant-Id header, or query parameter." });
                }

                var effectivePayload = payload with { TenantId = effectiveTenantId };
                var result = await service.ProcessWebhookEventAsync(effectivePayload);
                return Results.Ok(result);
            });

            r.MapPost("/webhooks/stripe", async (
                [FromBody] BillingWebhookPayloadDto payload,
                [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
                [FromQuery] Guid? queryTenantId,
                ISaasBillingService service) =>
            {
                var effectiveTenantId = payload.TenantId != Guid.Empty
                    ? payload.TenantId
                    : (headerTenantId ?? queryTenantId ?? Guid.Empty);

                if (effectiveTenantId == Guid.Empty)
                {
                    return Results.BadRequest(new { error = "TenantId is required in body, X-Tenant-Id header, or query parameter." });
                }

                var stripePayload = payload with { Provider = "Stripe", TenantId = effectiveTenantId };
                var result = await service.ProcessWebhookEventAsync(stripePayload);
                return Results.Ok(result);
            });

            r.MapPost("/webhooks/lemonsqueezy", async (
                [FromBody] BillingWebhookPayloadDto payload,
                [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
                [FromQuery] Guid? queryTenantId,
                ISaasBillingService service) =>
            {
                var effectiveTenantId = payload.TenantId != Guid.Empty
                    ? payload.TenantId
                    : (headerTenantId ?? queryTenantId ?? Guid.Empty);

                if (effectiveTenantId == Guid.Empty)
                {
                    return Results.BadRequest(new { error = "TenantId is required in body, X-Tenant-Id header, or query parameter." });
                }

                var lsPayload = payload with { Provider = "LemonSqueezy", TenantId = effectiveTenantId };
                var result = await service.ProcessWebhookEventAsync(lsPayload);
                return Results.Ok(result);
            });

            r.MapPost("/webhooks/chargily", async (
                [FromBody] BillingWebhookPayloadDto payload,
                [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
                [FromQuery] Guid? queryTenantId,
                ISaasBillingService service) =>
            {
                var effectiveTenantId = payload.TenantId != Guid.Empty
                    ? payload.TenantId
                    : (headerTenantId ?? queryTenantId ?? Guid.Empty);

                if (effectiveTenantId == Guid.Empty)
                {
                    return Results.BadRequest(new { error = "TenantId is required in body, X-Tenant-Id header, or query parameter." });
                }

                var chPayload = payload with { Provider = "Chargily", TenantId = effectiveTenantId };
                var result = await service.ProcessWebhookEventAsync(chPayload);
                return Results.Ok(result);
            });
        };

        registerWebhookRoute(group);
        registerWebhookRoute(v1Group);

        return endpoints;
    }
}
