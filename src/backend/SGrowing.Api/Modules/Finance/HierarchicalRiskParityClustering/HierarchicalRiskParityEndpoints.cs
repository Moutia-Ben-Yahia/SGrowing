using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.HierarchicalRiskParityClustering;

public static class HierarchicalRiskParityEndpoints
{
    public static IServiceCollection AddHierarchicalRiskParityClusteringServices(this IServiceCollection services)
    {
        services.AddScoped<IHierarchicalRiskParityClusteringEngine, HierarchicalRiskParityClusteringEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHierarchicalRiskParityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/hierarchical-risk-parity-clustering")
            .WithTags("Finance Hierarchical Risk Parity Clustering");

        group.MapPost("/analyze", (HierarchicalRiskParityTelemetry telemetry, IHierarchicalRiskParityClusteringEngine engine) =>
        {
            var result = engine.AnalyzePortfolioRiskParity(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeHierarchicalRiskParity")
        .WithSummary("Analyzes machine learning tree clustering hierarchical risk parity (HRP) asset allocation, quasi-diagonalization, and out-of-sample diversification.");

        return endpoints;
    }
}
