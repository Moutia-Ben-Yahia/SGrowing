using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.NvCenterNuclearSpinRegister;

public static class NvCenterNuclearSpinEndpoints
{
    public static IServiceCollection AddNvCenterNuclearSpinRegisterServices(this IServiceCollection services)
    {
        services.AddScoped<INvCenterNuclearSpinRegisterEngine, NvCenterNuclearSpinRegisterEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNvCenterNuclearSpinEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/nv-center-nuclear-spin-register")
            .WithTags("Quantum NV Center Nuclear Spin Register");

        group.MapPost("/analyze", (NvCenterNuclearSpinTelemetry telemetry, INvCenterNuclearSpinRegisterEngine engine) =>
        {
            var result = engine.AnalyzeNuclearRegister(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeNvCenterNuclearRegister")
        .WithSummary("Analyzes diamond nitrogen-vacancy (NV) center electron-nuclear spin hyperpolarization at ESLAC and multi-qubit memory register coherence.");

        return endpoints;
    }
}
