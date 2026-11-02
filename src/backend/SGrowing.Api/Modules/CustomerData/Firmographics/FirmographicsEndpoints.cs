using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.CustomerData.Firmographics;

public static class FirmographicsEndpoints
{
    public static IServiceCollection AddFirmographicsServices(this IServiceCollection services)
    {
        services.AddSingleton<IFirmographicsEngine, FirmographicsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapFirmographicsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/customer-data/firmographics")
            .WithTags("B2B Firmographic Reverse IP Deanonymization");

        group.MapPost("/deanonymize", (DeanonymizeIpRequest request, IFirmographicsEngine engine) =>
        {
            var result = engine.DeanonymizeIp(request);
            return Results.Ok(result);
        }).WithName("DeanonymizeVisitorIp");

        group.MapGet("/companies/{tenantId}/{domain}", (string tenantId, string domain, IFirmographicsEngine engine) =>
        {
            var company = engine.GetCompany(tenantId, domain);
            return company is not null ? Results.Ok(company) : Results.NotFound();
        }).WithName("GetFirmographicCompanyByDomain");

        group.MapPost("/rules/{tenantId}", (string tenantId, List<IcpScoringRule> rules, IFirmographicsEngine engine) =>
        {
            engine.RegisterTenantIcpRules(tenantId, rules);
            return Results.Ok(new { message = $"Registered {rules.Count} ICP scoring rules for tenant {tenantId}" });
        }).WithName("RegisterTenantIcpRules");

        return endpoints;
    }
}
