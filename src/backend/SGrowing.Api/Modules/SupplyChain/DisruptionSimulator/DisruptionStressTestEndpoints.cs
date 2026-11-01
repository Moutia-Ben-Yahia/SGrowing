using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.SupplyChain.DisruptionSimulator;

public static class DisruptionStressTestEndpoints
{
    public static IServiceCollection AddDisruptionSimulationServices(this IServiceCollection services)
    {
        services.AddSingleton<MonteCarloDisruptionSimulator>();
        services.AddSingleton<ChokePointVulnerabilityIndex>();
        services.AddSingleton<StrategicStockBufferOptimizer>();
        return services;
    }

    public static IEndpointRouteBuilder MapDisruptionSimulationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/supply-chain/disruption-simulator")
            .WithTags("Supply Chain Disruption Simulator & Monte Carlo Stress Testing");

        group.MapPost("/simulate", (
            RunSimulationRequest request,
            MonteCarloDisruptionSimulator simulator) =>
        {
            var result = simulator.RunSimulation(request);
            return Results.Ok(result);
        })
        .WithName("RunMonteCarloDisruptionSimulation")
        .WithSummary("Runs stochastic multi-thousand run Monte Carlo simulation to evaluate fulfillment resilience and VaR.");

        group.MapPost("/vulnerabilities", (
            IReadOnlyList<SupplyChainNode> nodes,
            IReadOnlyList<SupplyChainLink> links,
            ChokePointVulnerabilityIndex evaluator) =>
        {
            var scores = evaluator.EvaluateNetworkVulnerabilities(nodes, links);
            return Results.Ok(scores);
        })
        .WithName("EvaluateChokePointVulnerabilities")
        .WithSummary("Evaluates betweenness centrality and sole-source choke points across the supply chain network.");

        group.MapPost("/safety-stock", (
            CalculateSafetyStockRequest request,
            StrategicStockBufferOptimizer optimizer) =>
        {
            var result = optimizer.CalculateSafetyStock(request);
            return Results.Ok(result);
        })
        .WithName("CalculateStrategicSafetyStock")
        .WithSummary("Optimizes safety stock buffers and reorder points considering stochastic demand and lead time variance.");

        return app;
    }
}
