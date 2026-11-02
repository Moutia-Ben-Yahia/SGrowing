using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Deliverability.WarmupSwarm;

public static class WarmupSwarmEndpoints
{
    public static IServiceCollection AddWarmupSwarmServices(this IServiceCollection services)
    {
        services.AddSingleton<IWarmupSwarmEngine, WarmupSwarmEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapWarmupSwarmEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/deliverability/warmup")
            .WithTags("Domain D2 & D3: P2P Mailbox Warmup Swarm & Deliverability Radar");

        // 1. Enroll Account
        group.MapPost("/enroll", (
            [FromBody] EnrollWarmupAccountRequest request,
            IWarmupSwarmEngine engine) =>
        {
            var account = engine.EnrollAccount(request);
            return Results.Created($"/api/v1/deliverability/warmup/{account.TenantId}/{account.AccountId}", account);
        })
        .WithName("EnrollWarmupAccount")
        .WithSummary("Enrolls a new sending mailbox into the peer-to-peer warmup swarm.");

        // 2. List Accounts
        group.MapGet("/{tenantId}", (
            string tenantId,
            IWarmupSwarmEngine engine) =>
        {
            var accounts = engine.ListAccounts(tenantId);
            return Results.Ok(accounts);
        })
        .WithName("ListWarmupAccounts")
        .WithSummary("Lists all enrolled mailbox accounts and their deliverability health.");

        // 3. Simulate P2P Exchange
        group.MapPost("/simulate-exchange", (
            [FromBody] SimulateP2PExchangeRequest request,
            IWarmupSwarmEngine engine) =>
        {
            try
            {
                var summary = engine.SimulateP2PExchange(request);
                return Results.Ok(summary);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("SimulateP2PWarmupExchange")
        .WithSummary("Dispatches a batch of synthetic warmup messages across peer accounts.");

        // 4. Audit DNS Records
        group.MapPost("/audit-dns", (
            [FromBody] AuditDnsRequest request,
            IWarmupSwarmEngine engine) =>
        {
            try
            {
                var audited = engine.AuditDns(request);
                return Results.Ok(audited);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("AuditMailboxDnsHealth")
        .WithSummary("Performs SPF, DKIM, DMARC, and BIMI diagnostic checks for domain health.");

        return endpoints;
    }
}
