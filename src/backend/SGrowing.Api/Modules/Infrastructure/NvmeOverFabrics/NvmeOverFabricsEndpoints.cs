using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Infrastructure.NvmeOverFabrics;

public static class NvmeOverFabricsEndpoints
{
    public static IServiceCollection AddNvmeOverFabricsServices(this IServiceCollection services)
    {
        services.AddSingleton<INvmeOverFabricsEngine, NvmeOverFabricsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNvmeOverFabricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/nvme-over-fabrics")
            .WithTags("NVMe over Fabrics (NVMe-oF) Mesh & Multipath ANA Arbiter");

        group.MapGet("/subsystems", ([FromServices] INvmeOverFabricsEngine engine) =>
        {
            var subsystems = engine.GetSubsystems();
            return Results.Ok(subsystems);
        })
        .WithName("GetNvmeSubsystems")
        .WithSummary("Retrieves all registered NVMe-oF target subsystems and namespaces");

        group.MapPost("/failover", (string nqn, string cause, [FromServices] INvmeOverFabricsEngine engine) =>
        {
            var ev = engine.TriggerAnaFailover(nqn, cause);
            return Results.Ok(ev);
        })
        .WithName("TriggerNvmeAnaFailover")
        .WithSummary("Executes zero-data-loss sub-10ms ANA multipath controller failover");

        return app;
    }
}
