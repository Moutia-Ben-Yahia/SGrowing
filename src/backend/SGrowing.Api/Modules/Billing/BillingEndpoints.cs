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

        group.MapPost("/quota-check", async ([FromBody] CheckQuotaDto req, ISaasBillingService service) =>
        {
            var res = await service.VerifyQuotaAsync(req.TenantId, req.Metric, req.RequestedQuantity);
            return Results.Ok(res);
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

        group.MapPost("/webhooks/stripe", async ([FromBody] StripeWebhookDto req, ISaasBillingService service) =>
        {
            var invoice = await service.ProcessStripePaymentSuccessWebhookAsync(req.TenantId, req.StripeInvoiceId, req.Amount, req.Currency ?? "USD");
            return Results.Ok(invoice);
        });

        group.MapGet("/invoices", async ([FromQuery] Guid tenantId, ISaasBillingService service) =>
        {
            var invoices = await service.GetInvoicesAsync(tenantId);
            return Results.Ok(invoices);
        });

        return endpoints;
    }
}
