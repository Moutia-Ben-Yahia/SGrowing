namespace SGrowing.Api.Modules.Sales.CustomerAdvisoryBoard;

public static class CustomerAdvisoryBoardEndpoints
{
    public static IServiceCollection AddCustomerAdvisoryBoardServices(this IServiceCollection services)
    {
        services.AddSingleton<CustomerAdvisoryBoardEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCustomerAdvisoryBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/cab-matrix")
            .WithTags("Sales - Customer Advisory Board & Executive Alignment");

        group.MapGet("/overview", (CustomerAdvisoryBoardEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/align-sponsor", (CustomerAdvisoryBoardEngine engine, AlignSponsorRequest request) =>
        {
            var pairing = engine.AlignSponsor(request);
            return Results.Created($"/api/v1/sales/cab-matrix/pairings/{pairing.PairingId}", pairing);
        });

        group.MapPost("/vote-roadmap", (CustomerAdvisoryBoardEngine engine, VoteRoadmapRequest request) =>
        {
            var vote = engine.VoteRoadmap(request);
            return Results.Created($"/api/v1/sales/cab-matrix/votes/{vote.VoteId}", vote);
        });

        return app;
    }
}
