namespace SGrowing.Api.Modules.Finance.CapitalAllocation;

public static class CapitalAllocationEndpoints
{
    public static IServiceCollection AddCapitalAllocationServices(this IServiceCollection services)
    {
        services.AddSingleton<CapitalAllocationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCapitalAllocationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/capital-allocation")
            .WithTags("Finance - Sovereign Capital Allocation & Treasury Optimizer Ledger");

        group.MapGet("/overview", (CapitalAllocationEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/allocate-capital", (CapitalAllocationEngine engine, AllocateCapitalRequest request) =>
        {
            var alloc = engine.AllocateCapital(request);
            return Results.Created($"/api/v1/finance/capital-allocation/allocations/{alloc.AllocationId}", alloc);
        });

        group.MapPost("/optimize-runway", (CapitalAllocationEngine engine, OptimizeRunwayRequest request) =>
        {
            var opt = engine.OptimizeRunway(request);
            return Results.Created($"/api/v1/finance/capital-allocation/optimizations/{opt.OptimizationId}", opt);
        });

        return app;
    }
}
