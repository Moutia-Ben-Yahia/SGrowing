using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Tenancy;

public record RegisterDomainRequest(
    Guid? TenantId,
    string Hostname,
    bool IsPrimary = false
);

public record VerifyDnsRequest(
    bool SimulateValidDns = true
);

public record RevokeSslRequest(
    string? Reason
);

public record ResolveHostRequest(
    string Host
);

public record GenerateDkimRequest(
    Guid? TenantId,
    string CustomMailDomain
);

public static class WhitelabelBrandingEndpoints
{
    public static IEndpointRouteBuilder MapWhitelabelBrandingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenancy/whitelabel-branding")
            .WithTags("Multi-Tenant Whitelabel Branding & Custom Domain SSL Engine");

        // 1. GET /branding: get current tenant branding & domain config
        group.MapGet("/branding", async (
            [FromQuery] Guid? tenantId,
            IWhitelabelBrandingService brandingService,
            HttpContext httpContext) =>
        {
            var resolvedTenantId = await DetermineTenantIdAsync(tenantId, httpContext, brandingService);
            var config = await brandingService.GetTenantConfigAsync(resolvedTenantId);
            return Results.Ok(config);
        })
        .WithName("GetTenantBranding")
        .WithSummary("Retrieve tenant whitelabel branding, theme tokens, assets, email settings, and custom domains");

        // 2. PUT /branding: update branding tokens and assets
        group.MapPut("/branding", async (
            [FromQuery] Guid? tenantId,
            [FromBody] UpdateBrandingRequest request,
            IWhitelabelBrandingService brandingService,
            HttpContext httpContext) =>
        {
            var resolvedTenantId = await DetermineTenantIdAsync(tenantId, httpContext, brandingService);
            var updated = await brandingService.UpdateBrandingAsync(resolvedTenantId, request);
            return Results.Ok(updated);
        })
        .WithName("UpdateTenantBranding")
        .WithSummary("Update tenant whitelabel theme tokens, assets, agency profile, and email whitelabeling");

        // 3. POST /domains: register custom domain
        group.MapPost("/domains", async (
            [FromBody] RegisterDomainRequest request,
            IWhitelabelBrandingService brandingService,
            HttpContext httpContext) =>
        {
            if (string.IsNullOrWhiteSpace(request.Hostname))
            {
                return Results.BadRequest(new { error = "Hostname is required." });
            }

            var resolvedTenantId = request.TenantId ?? await DetermineTenantIdAsync(null, httpContext, brandingService);
            var domain = await brandingService.RegisterCustomDomainAsync(resolvedTenantId, request.Hostname, request.IsPrimary);
            return Results.Created($"/api/v1/tenancy/whitelabel-branding/domains/{domain.Hostname}/ssl-status", domain);
        })
        .WithName("RegisterCustomDomain")
        .WithSummary("Register a new custom domain for tenant and generate cryptographic DNS challenge TXT token");

        // 4. POST /domains/{domain}/verify-dns: trigger DNS verification check
        group.MapPost("/domains/{domain}/verify-dns", async (
            string domain,
            [FromQuery] Guid? tenantId,
            [FromQuery] bool? simulateValidDns,
            [FromBody] VerifyDnsRequest? bodyRequest,
            IWhitelabelBrandingService brandingService,
            HttpContext httpContext) =>
        {
            var resolvedTenantId = await DetermineTenantIdAsync(tenantId, httpContext, brandingService);
            var shouldSimulateValid = bodyRequest?.SimulateValidDns ?? simulateValidDns ?? true;

            try
            {
                var verifiedDomain = await brandingService.VerifyDomainDnsAsync(resolvedTenantId, domain, shouldSimulateValid);
                return Results.Ok(verifiedDomain);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("VerifyDomainDns")
        .WithSummary("Perform DNS challenge verification and auto-provision TLS 1.3 certificate state");

        // 5. GET /domains/{domain}/ssl-status: check SSL cert issuance status
        group.MapGet("/domains/{domain}/ssl-status", async (
            string domain,
            [FromQuery] Guid? tenantId,
            IWhitelabelBrandingService brandingService,
            HttpContext httpContext) =>
        {
            var resolvedTenantId = await DetermineTenantIdAsync(tenantId, httpContext, brandingService);
            var status = await brandingService.GetDomainSslStatusAsync(resolvedTenantId, domain);
            if (status == null)
            {
                return Results.NotFound(new { error = $"Domain '{domain}' not found for tenant '{resolvedTenantId}'." });
            }

            return Results.Ok(status);
        })
        .WithName("GetDomainSslStatus")
        .WithSummary("Get custom domain SSL certificate provisioning status, cipher suite, and expiry");

        // 6. POST /domains/{domain}/renew-ssl: simulate certificate renewal
        group.MapPost("/domains/{domain}/renew-ssl", async (
            string domain,
            [FromQuery] Guid? tenantId,
            IWhitelabelBrandingService brandingService,
            HttpContext httpContext) =>
        {
            var resolvedTenantId = await DetermineTenantIdAsync(tenantId, httpContext, brandingService);
            try
            {
                var renewed = await brandingService.RenewSslCertificateAsync(resolvedTenantId, domain);
                return Results.Ok(renewed);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("RenewDomainSsl")
        .WithSummary("Force renewal of TLS certificate for active custom domain");

        // 7. POST /domains/{domain}/revoke-ssl: simulate certificate revocation
        group.MapPost("/domains/{domain}/revoke-ssl", async (
            string domain,
            [FromQuery] Guid? tenantId,
            [FromBody] RevokeSslRequest? request,
            IWhitelabelBrandingService brandingService,
            HttpContext httpContext) =>
        {
            var resolvedTenantId = await DetermineTenantIdAsync(tenantId, httpContext, brandingService);
            try
            {
                var revoked = await brandingService.RevokeSslCertificateAsync(resolvedTenantId, domain, request?.Reason ?? "Revoked by user");
                return Results.Ok(revoked);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("RevokeDomainSsl")
        .WithSummary("Revoke TLS certificate for custom domain");

        // 8. GET /theme.css: returns text/css stylesheet dynamically generated for the resolving host
        group.MapGet("/theme.css", async (
            [FromQuery] Guid? tenantId,
            IWhitelabelBrandingService brandingService,
            HttpContext httpContext) =>
        {
            string css;
            if (tenantId.HasValue)
            {
                css = await brandingService.GenerateDynamicCssAsync(tenantId.Value);
            }
            else
            {
                var host = httpContext.Request.Host.Value;
                css = await brandingService.GenerateDynamicCssForHostAsync(string.IsNullOrWhiteSpace(host) ? "localhost" : host);
            }

            return Results.Text(css, "text/css", Encoding.UTF8);
        })
        .WithName("GetDynamicThemeCss")
        .WithSummary("Compiles tenant theme tokens into CSS custom properties stylesheet (:root { ... })");

        // 9. POST /resolve-host: test host header resolution
        group.MapPost("/resolve-host", async (
            [FromBody] ResolveHostRequest request,
            IWhitelabelBrandingService brandingService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Host))
            {
                return Results.BadRequest(new { error = "Host header or domain string is required." });
            }

            var result = await brandingService.ResolveHostAsync(request.Host);
            return Results.Ok(result);
        })
        .WithName("ResolveIncomingHost")
        .WithSummary("Test host header resolution against custom domains and tenant subdomains with fallback");

        // 10. GET /tenants: list all seeded and registered tenants
        group.MapGet("/tenants", async (IWhitelabelBrandingService brandingService) =>
        {
            var list = await brandingService.GetAllTenantsAsync();
            return Results.Ok(list);
        })
        .WithName("GetAllWhitelabelTenants")
        .WithSummary("List all seeded tenant whitelabel brand profiles for studio switching");

        // 11. POST /email/generate-dkim: generate DKIM and SPF configuration
        group.MapPost("/email/generate-dkim", async (
            [FromBody] GenerateDkimRequest request,
            IWhitelabelBrandingService brandingService,
            HttpContext httpContext) =>
        {
            var resolvedTenantId = request.TenantId ?? await DetermineTenantIdAsync(null, httpContext, brandingService);
            var config = await brandingService.GenerateEmailWhitelabelDkimAsync(resolvedTenantId, request.CustomMailDomain);
            return Results.Ok(config);
        })
        .WithName("GenerateEmailDkim")
        .WithSummary("Generate cryptographic 2048-bit DKIM selector & SPF record format for mail domain");

        // 12. POST /reset-defaults: reset tenant branding to defaults
        group.MapPost("/reset-defaults", async (
            [FromQuery] Guid? tenantId,
            IWhitelabelBrandingService brandingService,
            HttpContext httpContext) =>
        {
            var resolvedTenantId = await DetermineTenantIdAsync(tenantId, httpContext, brandingService);
            var reset = await brandingService.ResetToDefaultsAsync(resolvedTenantId);
            return Results.Ok(reset);
        })
        .WithName("ResetTenantBranding")
        .WithSummary("Reset tenant branding tokens and profile to default values");

        return app;
    }

    private static async Task<Guid> DetermineTenantIdAsync(
        Guid? tenantIdQuery,
        HttpContext httpContext,
        IWhitelabelBrandingService brandingService)
    {
        if (tenantIdQuery.HasValue && tenantIdQuery.Value != Guid.Empty)
        {
            return tenantIdQuery.Value;
        }

        if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) &&
            Guid.TryParse(tenantHeader.ToString(), out var headerTenantId))
        {
            return headerTenantId;
        }

        var host = httpContext.Request.Host.Host;
        var resolved = await brandingService.ResolveHostAsync(host);
        return resolved.ResolvedTenantId;
    }
}
