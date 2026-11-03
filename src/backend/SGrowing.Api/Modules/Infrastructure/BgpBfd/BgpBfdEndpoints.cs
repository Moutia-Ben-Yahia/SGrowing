namespace SGrowing.Api.Modules.Infrastructure.BgpBfd;

public static class BgpBfdEndpoints
{
    public static IServiceCollection AddBgpBfdServices(this IServiceCollection services)
    {
        services.AddSingleton<BgpBfdMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBgpBfdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/bgp-bfd")
            .WithTags("Infrastructure - Multi-Cloud BGP Anycast BFD Fast-Failover");

        group.MapGet("/overview", (BgpBfdMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/simulate-failover", (BgpBfdMeshEngine engine, SimulateFailoverRequest request) =>
        {
            var routeEvent = engine.SimulateFailover(request);
            return Results.Ok(routeEvent);
        });

        group.MapPost("/restore-route", (BgpBfdMeshEngine engine, RestoreRouteRequest request) =>
        {
            var routeEvent = engine.RestoreRoute(request);
            return Results.Ok(routeEvent);
        });

        return app;
    }
}
