namespace SGrowing.Api.Modules.Sustainability.MineralCarbonation;

public static class MineralCarbonationEndpoints
{
    public static IServiceCollection AddMineralCarbonationServices(this IServiceCollection services)
    {
        services.AddSingleton<MineralCarbonationReactorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMineralCarbonationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sustainability/mineral-carbonation")
            .WithTags("Mineral Carbonation Reactor");

        group.MapPost("/process-batch", (MineralCarbonationBatchRequest request, MineralCarbonationReactorEngine engine) =>
        {
            var result = engine.ProcessMineralCarbonation(request);
            return Results.Ok(result);
        })
        .WithName("ProcessMineralCarbonationBatch")
        .WithSummary("Models ex-situ silicate mineral carbonation kinetics, CO2 sequestration tonnage, exothermic heat recovery, and carbon removal credits");

        return app;
    }
}
