using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.CRM;

public static class RetentionEndpoints
{
    private static readonly Guid FallbackTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static ICustomerRetentionDaemonService ResolveService(HttpContext context, ICustomerRetentionDaemonService? service)
    {
        return service 
            ?? (ICustomerRetentionDaemonService?)context.RequestServices.GetService(typeof(ICustomerRetentionDaemonService))
            ?? CustomerRetentionDaemonService.Default;
    }

    public static IEndpointRouteBuilder MapRetentionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/crm/customer-retention")
            .WithTags("Customer Retention & Churn Prediction Daemon (Horizon 28)");

        // 1. GET /overview-kpis
        group.MapGet("/overview-kpis", async (
            HttpContext context,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromServices] ICustomerRetentionDaemonService? service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var svc = ResolveService(context, service);
            var kpis = await svc.GetOverviewKpisAsync(effectiveTenantId, ct);
            return Results.Ok(kpis);
        })
        .WithName("GetRetentionOverviewKpis")
        .WithSummary("Retrieve overall churn rate, at-risk accounts, recovered revenue, and active winback playbooks");

        // 2. GET /rfm-cohorts
        group.MapGet("/rfm-cohorts", async (
            HttpContext context,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromServices] ICustomerRetentionDaemonService? service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var svc = ResolveService(context, service);
            var cohorts = await svc.GetRfmCohortsAsync(effectiveTenantId, ct);
            return Results.Ok(cohorts);
        })
        .WithName("GetRfmCohorts")
        .WithSummary("Retrieve RFM segment distributions, cohort customer counts, average LTV and churn risk");

        // 3. GET /at-risk-customers
        group.MapGet("/at-risk-customers", async (
            HttpContext context,
            [FromQuery] Guid? tenantId,
            [FromQuery] ChurnRiskTier? minTier,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromServices] ICustomerRetentionDaemonService? service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var svc = ResolveService(context, service);
            var customers = await svc.GetAtRiskCustomersAsync(effectiveTenantId, minTier, ct);
            return Results.Ok(customers);
        })
        .WithName("GetAtRiskCustomers")
        .WithSummary("List customers filtered by churn risk tier (AtRisk and Critical by default)");

        // 4. GET /customers
        group.MapGet("/customers", async (
            HttpContext context,
            [FromQuery] Guid? tenantId,
            [FromQuery] ChurnRiskTier? tier,
            [FromQuery] RfmSegment? segment,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromServices] ICustomerRetentionDaemonService? service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var svc = ResolveService(context, service);
            var customers = await svc.GetCustomerProfilesAsync(effectiveTenantId, tier, segment, ct);
            return Results.Ok(customers);
        })
        .WithName("GetCustomerProfiles")
        .WithSummary("List all evaluated customer RFM profiles with optional tier or segment filtering");

        // 5. GET /customers/{id:guid}
        group.MapGet("/customers/{id:guid}", async (
            Guid id,
            HttpContext context,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromServices] ICustomerRetentionDaemonService? service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var svc = ResolveService(context, service);
            var profile = await svc.GetCustomerProfileByIdAsync(effectiveTenantId, id, ct);
            return profile is not null ? Results.Ok(profile) : Results.NotFound(new { message = $"Customer profile '{id}' not found." });
        })
        .WithName("GetCustomerProfileById")
        .WithSummary("Get detailed RFM and churn metrics for a specific customer profile");

        // 6. POST /evaluate-customer
        group.MapPost("/evaluate-customer", async (
            [FromBody] EvaluateCustomerRequest request,
            HttpContext context,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromServices] ICustomerRetentionDaemonService? service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = request.TenantId ?? headerTenantId ?? FallbackTenantId;
            var svc = ResolveService(context, service);
            var evaluated = await svc.EvaluateCustomerAsync(effectiveTenantId, request, ct);
            return Results.Ok(evaluated);
        })
        .WithName("EvaluateCustomerRfmAndChurn")
        .WithSummary("Recalculate RFM scores and logistic churn probability for customer parameters or what-if simulation");

        // 7. POST /trigger-winback
        group.MapPost("/trigger-winback", async (
            [FromBody] TriggerWinbackRequest request,
            HttpContext context,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromServices] ICustomerRetentionDaemonService? service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = request.TenantId != Guid.Empty ? request.TenantId : (headerTenantId ?? FallbackTenantId);
            var svc = ResolveService(context, service);
            var campaign = await svc.TriggerWinbackPlaybookAsync(effectiveTenantId, request with { TenantId = effectiveTenantId }, ct);
            return Results.Ok(campaign);
        })
        .WithName("TriggerWinbackPlaybook")
        .WithSummary("Manually or automatically dispatch a retention playbook (VIP coupon, AE reachout, pulse survey)");

        // 8. POST /run-daemon-scan
        group.MapPost("/run-daemon-scan", async (
            HttpContext context,
            [FromQuery] Guid? tenantId,
            [FromQuery] double? threshold,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromServices] ICustomerRetentionDaemonService? service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var svc = ResolveService(context, service);
            var result = await svc.RunPreemptiveWinbackDaemonScanAsync(effectiveTenantId, threshold ?? 0.50, ct);
            return Results.Ok(result);
        })
        .WithName("RunPreemptiveWinbackDaemonScan")
        .WithSummary("Execute daemon batch scan for churn threshold breaches and auto-dispatch retention playbooks");

        // 9. GET /winback-campaigns
        group.MapGet("/winback-campaigns", async (
            HttpContext context,
            [FromQuery] Guid? tenantId,
            [FromQuery] WinbackStatus? status,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromServices] ICustomerRetentionDaemonService? service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var svc = ResolveService(context, service);
            var campaigns = await svc.GetWinbackCampaignsAsync(effectiveTenantId, status, ct);
            return Results.Ok(campaigns);
        })
        .WithName("GetWinbackCampaigns")
        .WithSummary("List historical and active winback campaigns with status and recovered revenue");

        // 10. POST /winback-campaigns/{id:guid}/record-conversion
        group.MapPost("/winback-campaigns/{id:guid}/record-conversion", async (
            Guid id,
            [FromBody] RecordConversionRequest request,
            HttpContext context,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromServices] ICustomerRetentionDaemonService? service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? FallbackTenantId;
            var svc = ResolveService(context, service);
            try
            {
                var converted = await svc.RecordWinbackConversionAsync(effectiveTenantId, id, request, ct);
                return Results.Ok(converted);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("RecordWinbackConversion")
        .WithSummary("Record successful purchase conversion resulting from an executed winback campaign");

        return group;
    }
}
