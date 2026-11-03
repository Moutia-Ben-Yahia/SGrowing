using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.TaxNexus;

public static class TaxNexusEndpoints
{
    public static IServiceCollection AddTaxNexusServices(this IServiceCollection services)
    {
        services.AddSingleton<ITaxNexusEngine, TaxNexusEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTaxNexusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/tax-nexus")
            .WithTags("Finance - Global Tax Nexus Tracking & VAT/GST Remittance");

        group.MapGet("/jurisdictions", (ITaxNexusEngine engine) =>
        {
            var jurisdictions = engine.GetJurisdictions();
            return Results.Ok(jurisdictions);
        })
        .WithName("GetTaxNexusJurisdictions")
        .WithSummary("Retrieves all statutory tax jurisdictions with economic nexus threshold proximity and liabilities");

        group.MapPost("/registration", (UpdateNexusRegistrationRequest request, ITaxNexusEngine engine) =>
        {
            try
            {
                var result = engine.UpdateRegistration(request);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Jurisdiction {request.JurisdictionCode} not found." });
            }
        })
        .WithName("UpdateTaxNexusRegistration")
        .WithSummary("Updates statutory registration and remittance state for target tax jurisdiction");

        return endpoints;
    }
}
