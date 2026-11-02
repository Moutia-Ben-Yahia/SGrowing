using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Tenancy.CustomDomains;

public static class CustomDomainEndpoints
{
    public static IServiceCollection AddCustomDomainServices(this IServiceCollection services)
    {
        services.AddSingleton<CustomDomainEngine>();
        return services;
    }

    public static RouteGroupBuilder MapCustomDomainEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenancy/domains")
            .WithTags("TenancyCustomDomains");

        group.MapGet("/", async Task<Ok<IReadOnlyList<TenantCustomDomain>>> (
            [FromQuery] string? tenantId,
            [FromServices] CustomDomainEngine engine,
            CancellationToken ct) =>
        {
            var domains = await engine.ListDomainsAsync(tenantId, ct);
            return TypedResults.Ok(domains);
        })
        .WithName("ListTenantCustomDomains")
        .WithSummary("List registered custom domains for a tenant");

        group.MapPost("/", async Task<Results<Ok<TenantCustomDomain>, BadRequest<string>>> (
            [FromBody] CustomDomainRegistrationRequest request,
            [FromServices] CustomDomainEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var domain = await engine.RegisterCustomDomainAsync(request, ct);
                return TypedResults.Ok(domain);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        })
        .WithName("RegisterCustomDomain")
        .WithSummary("Register a new custom domain and generate DNS verification tokens");

        group.MapPost("/{id}/verify", async Task<Results<Ok<DnsVerificationResult>, NotFound<string>>> (
            [FromRoute] string id,
            [FromServices] CustomDomainEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var result = await engine.VerifyDnsRecordsAsync(id, ct);
                return TypedResults.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return TypedResults.NotFound($"Custom domain '{id}' not found.");
            }
        })
        .WithName("VerifyCustomDomainDns")
        .WithSummary("Validate CNAME and TXT DNS records for custom domain ownership");

        group.MapPost("/{id}/ssl/renew", async Task<Results<Ok<SslRenewalResult>, NotFound<string>>> (
            [FromRoute] string id,
            [FromServices] CustomDomainEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var result = await engine.IssueOrRenewSslAsync(id, ct);
                return TypedResults.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return TypedResults.NotFound($"Custom domain '{id}' not found.");
            }
        })
        .WithName("RenewCustomDomainSsl")
        .WithSummary("Trigger automated ACME SSL certificate issuance or renewal");

        group.MapGet("/{hostOrId}/branding", async Task<Ok<TenantBrandingPayload>> (
            [FromRoute] string hostOrId,
            [FromServices] CustomDomainEngine engine,
            CancellationToken ct) =>
        {
            var branding = await engine.ResolveTenantBrandAsync(hostOrId, ct);
            return TypedResults.Ok(branding);
        })
        .WithName("ResolveTenantBranding")
        .WithSummary("Resolve white-label branding and CSS theme variables for custom domain");

        return group;
    }
}
