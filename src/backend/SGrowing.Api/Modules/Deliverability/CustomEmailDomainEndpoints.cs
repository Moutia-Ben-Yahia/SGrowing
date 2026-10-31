using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Deliverability;

public record RegisterCustomDomainRequest(
    string Domain,
    Guid? TenantId = null,
    string? ReturnPathPrefix = "mail",
    string? DmarcPolicy = "quarantine",
    string? CustomDmarcReportEmail = null
);

public static class CustomEmailDomainEndpoints
{
    public static IEndpointRouteBuilder MapCustomEmailDomainEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/deliverability/custom-domains")
            .WithTags("Whitelabel Custom Email Domains & Automated DKIM/CNAME Provisioner");

        // 1. GET /api/v1/deliverability/custom-domains: List all custom sending domains
        group.MapGet("/", async (
            [FromQuery] Guid? tenantId,
            ICustomEmailDomainService service) =>
        {
            var domains = await service.ListDomainsAsync(tenantId);
            return Results.Ok(domains);
        })
        .WithName("ListCustomDomains")
        .WithSummary("List all tenant custom email sending domains with verification statuses.");

        // 2. GET /api/v1/deliverability/custom-domains/metrics: Get domain health & capacity metrics
        group.MapGet("/metrics", async (
            [FromQuery] Guid? tenantId,
            ICustomEmailDomainService service) =>
        {
            var metrics = await service.GetOverviewMetricsAsync(tenantId);
            return Results.Ok(metrics);
        })
        .WithName("GetCustomDomainMetrics")
        .WithSummary("Get aggregate metrics across all custom sending domains.");

        // 3. GET /api/v1/deliverability/custom-domains/{id:guid}: Get domain details with DKIM key & DNS records
        group.MapGet("/{id:guid}", async (
            Guid id,
            ICustomEmailDomainService service) =>
        {
            var domain = await service.GetDomainAsync(id);
            return domain is not null
                ? Results.Ok(domain)
                : Results.NotFound(new { error = $"Custom sending domain with ID '{id}' was not found." });
        })
        .WithName("GetCustomDomainById")
        .WithSummary("Retrieve detailed configuration, 2048-bit DKIM key, and DNS records for a domain.");

        // 4. POST /api/v1/deliverability/custom-domains/register: Register a new custom domain
        group.MapPost("/register", async (
            [FromBody] RegisterCustomDomainRequest request,
            ICustomEmailDomainService service) =>
        {
            try
            {
                var effectiveTenantId = request.TenantId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
                var dto = new RegisterCustomDomainDto(
                    Domain: request.Domain,
                    TenantId: effectiveTenantId,
                    ReturnPathPrefix: request.ReturnPathPrefix,
                    DmarcPolicy: request.DmarcPolicy,
                    CustomDmarcReportEmail: request.CustomDmarcReportEmail
                );

                var domain = await service.RegisterDomainAsync(effectiveTenantId, dto);
                var detail = await service.GetDomainAsync(domain.Id);
                return Results.Created($"/api/v1/deliverability/custom-domains/{domain.Id}", detail);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .WithName("RegisterCustomDomain")
        .WithSummary("Register a new whitelabel custom sending domain and generate dedicated 2048-bit RSA DKIM keys.");

        // 5. POST /api/v1/deliverability/custom-domains/verify/{id:guid}: Trigger DNS verification sweep
        group.MapPost("/verify/{id:guid}", async (
            Guid id,
            [FromQuery] bool? passAll,
            ICustomEmailDomainService service) =>
        {
            try
            {
                var result = await service.VerifyDomainDnsAsync(id, passAll);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("VerifyCustomDomainDns")
        .WithSummary("Trigger an automated DNS verification sweep against nameservers for DKIM, SPF, CNAME, and MX.");

        // 6. GET /api/v1/deliverability/custom-domains/{id:guid}/export/{registrar}: Export registrar configuration
        group.MapGet("/{id:guid}/export/{registrar}", (
            Guid id,
            string registrar,
            ICustomEmailDomainService service) =>
        {
            try
            {
                var export = service.ExportRegistrarConfig(id, registrar);
                return Results.Ok(export);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ExportRegistrarDnsConfig")
        .WithSummary("Export pre-formatted DNS configuration for Cloudflare, AWS Route 53, GoDaddy, or Namecheap.");

        // 7. DELETE /api/v1/deliverability/custom-domains/{id:guid}: Delete sending domain
        group.MapDelete("/{id:guid}", async (
            Guid id,
            ICustomEmailDomainService service) =>
        {
            var removed = await service.DeleteDomainAsync(id);
            return removed
                ? Results.Ok(new { message = $"Domain '{id}' removed successfully." })
                : Results.NotFound(new { error = $"Domain with ID '{id}' was not found." });
        })
        .WithName("DeleteCustomDomain")
        .WithSummary("De-provision and delete a custom sending domain.");

        return app;
    }
}
