using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.MicrochannelFischerTropschSaf;

public static class FischerTropschSafEndpoints
{
    public static IServiceCollection AddFischerTropschSafServices(this IServiceCollection services)
    {
        services.AddScoped<IFischerTropschSafEngine, FischerTropschSafEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapFischerTropschSafEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/microchannel-fischer-tropsch-saf")
            .WithTags("Energy Microchannel Fischer-Tropsch Sustainable Aviation Fuel (SAF)");

        group.MapPost("/analyze", (FischerTropschSafTelemetry telemetry, IFischerTropschSafEngine engine) =>
        {
            var evaluation = engine.AnalyzeReactor(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeFischerTropschSaf")
        .WithSummary("Analyzes microchannel Fischer-Tropsch reactors, ASF paraffin chain growth kinetics, jet fuel cuts, and cobalt catalyst deactivation");

        return endpoints;
    }
}
