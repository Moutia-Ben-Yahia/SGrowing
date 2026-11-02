namespace SGrowing.Api.Modules.Finance.CbdcCrossBorderBridge;

public static class CbdcBridgeEndpoints
{
    public static IServiceCollection AddCbdcCrossBorderServices(this IServiceCollection services)
    {
        services.AddSingleton<CbdcCrossBorderSettlementEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCbdcCrossBorderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/cbdc-cross-border")
            .WithTags("Cross-Border CBDC Bridge");

        group.MapPost("/settle-batch", (CbdcSettlementRequest request, CbdcCrossBorderSettlementEngine engine) =>
        {
            var result = engine.SettleCrossBorderBatch(request);
            return Results.Ok(result);
        })
        .WithName("SettleCrossBorderCbdcBatch")
        .WithSummary("Executes multi-currency Payment-versus-Payment (PvP) atomic settlements across sovereign Central Bank Digital Currency corridors");

        return app;
    }
}
