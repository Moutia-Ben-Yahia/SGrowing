using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.EbpfSecurityMesh;

public static class EbpfSecurityMeshEndpoints
{
    public static IServiceCollection AddEbpfSecurityMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<EbpfSecurityMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEbpfSecurityMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/ebpf-security-mesh")
            .WithTags("Infrastructure - Zero-Trust eBPF Deep Packet Inspection & Kernel Firewall");

        group.MapGet("/overview", (EbpfSecurityMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetEbpfSecurityMeshOverview")
        .WithSummary("Retrieve active eBPF probe telemetry, kernel hook stats, and lateral violation drops");

        group.MapPost("/block-socket", (BlockSuspiciousSocketRequest request, EbpfSecurityMeshEngine engine) =>
        {
            var alert = engine.BlockSuspiciousSocket(request);
            return Results.Ok(alert);
        })
        .WithName("BlockSuspiciousSocket")
        .WithSummary("Programs eBPF map in Linux kernel to instantly drop unauthorized lateral packet traversal");

        group.MapPost("/deploy-probe", (DeployProbeRequest request, EbpfSecurityMeshEngine engine) =>
        {
            var probe = engine.DeployProbe(request);
            return Results.Ok(probe);
        })
        .WithName("DeployEbpfProbe")
        .WithSummary("Compiles and attaches eBPF bytecode probe to kernel socket tracepoints");

        return endpoints;
    }
}
