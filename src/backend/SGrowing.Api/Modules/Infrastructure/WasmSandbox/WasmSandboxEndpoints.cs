namespace SGrowing.Api.Modules.Infrastructure.WasmSandbox;

public static class WasmSandboxEndpoints
{
    public static IServiceCollection AddWasmSandboxServices(this IServiceCollection services)
    {
        services.AddSingleton<WasmSandboxMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapWasmSandboxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/wasm-sandbox")
            .WithTags("Infrastructure - Multi-Cloud WebAssembly Sandbox Orchestrator");

        group.MapGet("/overview", (WasmSandboxMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/deploy-worker", (WasmSandboxMeshEngine engine, DeployWasmWorkerRequest request) =>
        {
            var worker = engine.DeployWorker(request);
            return Results.Created($"/api/v1/infrastructure/wasm-sandbox/workers/{worker.WorkerId}", worker);
        });

        group.MapPost("/terminate-worker", (WasmSandboxMeshEngine engine, TerminateWasmWorkerRequest request) =>
        {
            var worker = engine.TerminateWorker(request);
            return Results.Ok(worker);
        });

        return app;
    }
}
