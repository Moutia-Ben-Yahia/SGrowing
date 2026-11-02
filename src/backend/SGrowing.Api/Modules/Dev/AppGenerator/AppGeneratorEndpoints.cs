using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Dev.AppGenerator;

public static class AppGeneratorEndpoints
{
    public static IServiceCollection AddAppGeneratorServices(this IServiceCollection services)
    {
        services.AddSingleton<IAppGeneratorEngine, AppGeneratorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAppGeneratorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dev/apps")
            .WithTags("Autonomous Micro-SaaS App Generator");

        group.MapPost("/", (CreateAppRequest request, IAppGeneratorEngine engine) =>
        {
            var created = engine.CreateApp(request);
            return Results.Ok(created);
        });

        group.MapGet("/", (IAppGeneratorEngine engine) =>
        {
            var apps = engine.ListApps();
            return Results.Ok(apps);
        });

        group.MapGet("/{appId}", (string appId, IAppGeneratorEngine engine) =>
        {
            var appItem = engine.GetApp(appId);
            return appItem is not null ? Results.Ok(appItem) : Results.NotFound();
        });

        group.MapPost("/{appId}/records", (string appId, InsertRecordRequest request, IAppGeneratorEngine engine) =>
        {
            var req = request with { AppId = appId };
            var record = engine.InsertRecord(req);
            return Results.Ok(record);
        });

        group.MapGet("/{appId}/records", (string appId, IAppGeneratorEngine engine) =>
        {
            var records = engine.ListRecords(appId);
            return Results.Ok(records);
        });

        group.MapGet("/snapshot/overview", (IAppGeneratorEngine engine) =>
        {
            var snapshot = engine.GetSnapshot();
            return Results.Ok(snapshot);
        });

        return app;
    }
}
