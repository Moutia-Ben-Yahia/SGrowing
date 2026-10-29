using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Deliverability;

public static class WarmupRescueEndpoints
{
    public static IEndpointRouteBuilder MapWarmupRescueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/deliverability/warmup")
            .WithTags("P2P Mailbox Warmup & Autonomous Spam Rescue Daemon");

        // 1. POST /mailboxes (enroll mailbox)
        group.MapPost("/mailboxes", async (
            [FromBody] EnrollMailboxRequest request,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IP2PWarmupRescueService service) =>
        {
            var tenantId = request.TenantId ?? headerTenantId;
            var enrolled = await service.EnrollMailboxAsync(request with { TenantId = tenantId });
            return Results.Created($"/api/v1/deliverability/warmup/mailboxes/{enrolled.Id}", enrolled);
        })
        .WithName("EnrollWarmupMailbox")
        .WithSummary("Enroll a mailbox into the autonomous P2P warmup peer network with automated ramp schedule.");

        // 2. GET /mailboxes (list enrolled mailboxes & daily ramp status)
        group.MapGet("/mailboxes", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IP2PWarmupRescueService service) =>
        {
            var mailboxes = await service.GetEnrolledMailboxesAsync(headerTenantId);
            return Results.Ok(mailboxes);
        })
        .WithName("ListEnrolledWarmupMailboxes")
        .WithSummary("List all enrolled mailboxes across Google Workspace, Microsoft 365, and Generic SMTP with ramp progress.");

        // 3. POST /run-exchange (trigger peer conversational warmup exchange between paired mailboxes)
        group.MapPost("/run-exchange", async (
            [FromBody] TriggerExchangeRequest? request,
            IP2PWarmupRescueService service) =>
        {
            var exchange = await service.TriggerWarmupExchangeAsync(request);
            return Results.Ok(exchange);
        })
        .WithName("TriggerP2PWarmupExchange")
        .WithSummary("Trigger synthetic multi-turn corporate dialogue exchange between paired peers with X-SGrowing-Warmup-Token.");

        // 4. POST /spam-rescue/scan (execute spam rescue sweep)
        group.MapPost("/spam-rescue/scan", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IP2PWarmupRescueService service) =>
        {
            var scanResult = await service.ExecuteSpamRescueScanAsync(headerTenantId);
            return Results.Ok(scanResult);
        })
        .WithName("ExecuteSpamRescueSweep")
        .WithSummary("Run autonomous spam rescue daemon sweep: moves spam to inbox, marks not spam, stars, and sends positive replies.");

        // 5. GET /dns-audit/{domain} (run live SPF, DKIM, DMARC, BIMI, and DNSBL audit)
        group.MapGet("/dns-audit/{domain}", async (
            string domain,
            [FromQuery] bool simulateListings,
            IP2PWarmupRescueService service) =>
        {
            var audit = await service.AuditDomainDnsAndBlacklistsAsync(domain, simulateListings);
            return Results.Ok(audit);
        })
        .WithName("AuditDomainDeliverabilityDns")
        .WithSummary("Deep diagnostic audit of SPF (lookup limit <10), DKIM (2048-bit), DMARC policy, BIMI (VMC), and 25+ DNSBL blacklists.");

        // 6. GET /metrics (aggregate deliverability health index, total rescues, volume ramp)
        group.MapGet("/metrics", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IP2PWarmupRescueService service) =>
        {
            var metrics = await service.GetDeliverabilityMetricsAsync(headerTenantId);
            return Results.Ok(metrics);
        })
        .WithName("GetDeliverabilityWarmupMetrics")
        .WithSummary("Aggregate deliverability health index, active ramp volume, total rescues, and ESP ML classifier status.");

        // 7. GET /rescue-logs (recent rescue activity stream)
        group.MapGet("/rescue-logs", (
            [FromQuery] int? limit,
            IP2PWarmupRescueService service) =>
        {
            var logs = service.GetRecentRescueLogs(limit ?? 50);
            return Results.Ok(logs);
        })
        .WithName("GetSpamRescueLogs")
        .WithSummary("Retrieve live spam rescue terminal audit trail.");

        return app;
    }
}
