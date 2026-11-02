namespace SGrowing.Api.Modules.Finance.MicroPaymentMesh;

public static class MicroPaymentMeshEndpoints
{
    public static IServiceCollection AddMicroPaymentMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<MicroPaymentChannelMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMicroPaymentMeshEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/micropayment-mesh")
            .WithTags("MicroPayment Mesh");

        group.MapPost("/route-payment", (RoutePaymentRequest request, MicroPaymentChannelMeshEngine engine) =>
        {
            var result = engine.RouteMicroPayment(request);
            return Results.Ok(result);
        })
        .WithName("RouteMicroPayment")
        .WithSummary("Finds fee-optimal off-chain state channel routes for high-throughput machine-to-machine micro-payments");

        return app;
    }
}
