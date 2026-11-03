using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.DbQueryRouter;

public static class DbQueryRouterEndpoints
{
    public static IServiceCollection AddDbQueryRouterServices(this IServiceCollection services)
    {
        services.AddSingleton<IDbQueryRouterEngine, DbQueryRouterEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDbQueryRouterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/db-query-router")
            .WithTags("Infrastructure - Cross-Region DB Replica Query Router");

        group.MapGet("/overview", (IDbQueryRouterEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetDbQueryRouterOverview")
        .WithSummary("Multi-region replica health matrix, replication lag ms, and read/write routing ratios");

        group.MapPost("/probe-lag", (ProbeLagCommand command, IDbQueryRouterEngine engine) =>
        {
            var result = engine.ProbeLag(command);
            return Results.Ok(result);
        })
        .WithName("ProbeDbReplicaLag")
        .WithSummary("Ingests replication lag heartbeats from edge database nodes and triggers circuit breaker");

        group.MapPost("/route-query", (RouteQueryCommand command, IDbQueryRouterEngine engine) =>
        {
            var result = engine.RouteQuery(command);
            return Results.Ok(result);
        })
        .WithName("RouteDatabaseQuery")
        .WithSummary("Evaluates SQL query AST, determines read vs write intent, and selects optimal target replica");

        return app;
    }
}
