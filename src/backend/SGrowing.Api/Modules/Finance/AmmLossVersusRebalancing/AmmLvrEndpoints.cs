namespace SGrowing.Api.Modules.Finance.AmmLossVersusRebalancing;

public static class AmmLvrEndpoints
{
    public static IServiceCollection AddAmmLossVersusRebalancingServices(this IServiceCollection services)
    {
        services.AddScoped<AmmLossVersusRebalancingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAmmLvrEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/amm-lvr")
            .WithTags("AMM Loss-Versus-Rebalancing (LVR) Arbiter");

        group.MapPost("/evaluate", (AmmLvrTelemetry telemetry, AmmLossVersusRebalancingEngine engine) =>
        {
            var result = engine.EvaluatePoolLvr(telemetry);
            return Results.Ok(result);
        })
        .WithName("EvaluateAmmLvr")
        .WithSummary("Evaluates Automated Market Maker Loss-Versus-Rebalancing (LVR) continuous arbitrage drain, net LP PnL, and dynamic fee optimization.");

        return endpoints;
    }
}
