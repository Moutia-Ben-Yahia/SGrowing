using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Retention.CustomerHealth;

/// <summary>
/// Customer Success Health & Proactive Retention Radar endpoints.
/// Auto-discovered via <see cref="IEndpointModule"/>.
/// </summary>
public class CustomerHealthEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/retention/health")
            .WithTags("Retention - Customer Success Health & Proactive Retention Radar");

        // GET /api/v1/retention/health/radar
        group.MapGet("/radar", (
            [FromQuery] Guid? tenantId,
            [FromQuery] HealthTier? tier,
            [FromQuery] string? search,
            [FromServices] ICustomerHealthService service) =>
        {
            var tid = tenantId ?? CustomerHealthService.DefaultTenantId;
            var accounts = service.GetAccounts(tid, tier, search);
            return Results.Ok(accounts);
        })
        .WithName("GetCustomerHealthRadarAccounts")
        .WithSummary("Retrieves all customer accounts on retention radar with tier and search filtering.");

        // GET /api/v1/retention/health/radar/overview
        group.MapGet("/radar/overview", (
            [FromQuery] Guid? tenantId,
            [FromServices] ICustomerHealthService service) =>
        {
            var tid = tenantId ?? CustomerHealthService.DefaultTenantId;
            var overview = service.GetOverview(tid);
            return Results.Ok(overview);
        })
        .WithName("GetCustomerHealthRadarOverview")
        .WithSummary("Retrieves aggregate retention radar KPIs, tier distributions, and ARR at churn risk.");

        // GET /api/v1/retention/health/accounts/{accountId}
        group.MapGet("/accounts/{accountId:guid}", (
            [FromRoute] Guid accountId,
            [FromQuery] Guid? tenantId,
            [FromServices] ICustomerHealthService service) =>
        {
            var tid = tenantId ?? CustomerHealthService.DefaultTenantId;
            var account = service.GetAccountById(tid, accountId);
            return account is not null
                ? Results.Ok(account)
                : Results.NotFound(new { error = $"Account '{accountId}' was not found for tenant '{tid}'." });
        })
        .WithName("GetCustomerAccountHealthById")
        .WithSummary("Retrieves full health metrics, triggers, and active playbooks for a specific account.");

        // POST /api/v1/retention/health/accounts
        group.MapPost("/accounts", (
            [FromBody] CreateCustomerAccountRequest request,
            [FromServices] ICustomerHealthService service) =>
        {
            try
            {
                var targetRequest = request.TenantId == Guid.Empty
                    ? request with { TenantId = CustomerHealthService.DefaultTenantId }
                    : request;

                var account = service.CreateAccount(targetRequest);
                return Results.Created($"/api/v1/retention/health/accounts/{account.AccountId}", account);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateCustomerAccountHealthRecord")
        .WithSummary("Registers a new customer account onto the retention radar and evaluates initial health composite.");

        // POST /api/v1/retention/health/accounts/{accountId}/recalculate
        group.MapPost("/accounts/{accountId:guid}/recalculate", (
            [FromRoute] Guid accountId,
            [FromBody] UpdateAccountMetricsRequest? updatedMetrics,
            [FromQuery] Guid? tenantId,
            [FromServices] ICustomerHealthService service) =>
        {
            var tid = tenantId ?? CustomerHealthService.DefaultTenantId;
            var updated = service.RecalculateAccountHealth(tid, accountId, updatedMetrics);
            return updated is not null
                ? Results.Ok(updated)
                : Results.NotFound(new { error = $"Account '{accountId}' was not found for tenant '{tid}'." });
        })
        .WithName("RecalculateCustomerAccountHealth")
        .WithSummary("Recalculates account composite health score, triggers early-warning churn detectors, and updates CSM playbooks.");

        // POST /api/v1/retention/health/radar/recalculate-all
        group.MapPost("/radar/recalculate-all", (
            [FromQuery] Guid? tenantId,
            [FromServices] ICustomerHealthService service) =>
        {
            var tid = tenantId ?? CustomerHealthService.DefaultTenantId;
            var results = service.RecalculateAll(tid);
            return Results.Ok(results);
        })
        .WithName("RecalculateAllCustomerHealthAccounts")
        .WithSummary("Executes a tenant-wide recalculation across all registered customer accounts.");

        // POST /api/v1/retention/health/accounts/{accountId}/playbooks
        group.MapPost("/accounts/{accountId:guid}/playbooks", (
            [FromRoute] Guid accountId,
            [FromBody] CreatePlaybookRequest request,
            [FromQuery] Guid? tenantId,
            [FromServices] ICustomerHealthService service) =>
        {
            var tid = tenantId ?? CustomerHealthService.DefaultTenantId;
            var playbook = service.AddPlaybook(tid, accountId, request);
            return playbook is not null
                ? Results.Created($"/api/v1/retention/health/playbooks/{playbook.PlaybookId}", playbook)
                : Results.NotFound(new { error = $"Account '{accountId}' was not found." });
        })
        .WithName("AddCustomerPlaybook")
        .WithSummary("Adds a dedicated or proactive CSM playbook to a customer account.");

        // PATCH /api/v1/retention/health/playbooks/{playbookId}/status
        group.MapPatch("/playbooks/{playbookId:guid}/status", (
            [FromRoute] Guid playbookId,
            [FromBody] UpdatePlaybookStatusRequest request,
            [FromQuery] Guid? tenantId,
            [FromServices] ICustomerHealthService service) =>
        {
            var tid = tenantId ?? CustomerHealthService.DefaultTenantId;
            var updated = service.UpdatePlaybookStatus(tid, playbookId, request.Status, request.Notes);
            return updated is not null
                ? Results.Ok(updated)
                : Results.NotFound(new { error = $"Playbook '{playbookId}' was not found." });
        })
        .WithName("UpdateCsmPlaybookStatus")
        .WithSummary("Updates the status of an active or suggested CSM playbook (e.g. Active, Completed, Dismissed).");

        // POST /api/v1/retention/health/radar/reset
        group.MapPost("/radar/reset", (
            [FromQuery] Guid? tenantId,
            [FromServices] ICustomerHealthService service) =>
        {
            var tid = tenantId ?? CustomerHealthService.DefaultTenantId;
            service.Reset(tid);
            return Results.Ok(new { message = "Customer retention health radar reset to seeded demo records successfully." });
        })
        .WithName("ResetCustomerHealthRadar")
        .WithSummary("Resets tenant accounts back to default enterprise demo dataset.");
    }
}
