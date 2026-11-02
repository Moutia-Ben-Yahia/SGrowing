using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.CdoGaussianCopulaTrancheCorrelation;

public static class CdoCopulaTrancheEndpoints
{
    public static IServiceCollection AddCdoCopulaTrancheServices(this IServiceCollection services)
    {
        services.AddScoped<ICdoCopulaTrancheEngine, CdoCopulaTrancheEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCdoCopulaTrancheEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/cdo-copula-tranche-correlation")
            .WithTags("Finance CDO Gaussian Copula Tranche Correlation");

        group.MapPost("/analyze", (CdoCopulaTrancheTelemetry telemetry, ICdoCopulaTrancheEngine engine) =>
        {
            var evaluation = engine.AnalyzeTranche(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeCdoCopulaTranche")
        .WithSummary("Analyzes collateralized debt obligation (CDO) tranches, default correlation, and extreme tail risk contagion");

        return endpoints;
    }
}
