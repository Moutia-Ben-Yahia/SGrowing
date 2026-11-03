using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.MicroSegmentation;

public static class MicroSegmentationEndpoints
{
    public static IServiceCollection AddMicroSegmentationServices(this IServiceCollection services)
    {
        services.AddSingleton<IMicroSegmentationEngine, MicroSegmentationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMicroSegmentationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/security/micro-segmentation")
            .WithTags("Security - Zero-Trust Micro-Segmentation & mTLS");

        group.MapGet("/topology", (IMicroSegmentationEngine engine) =>
        {
            var topology = engine.GetTopology();
            return Results.Ok(topology);
        })
        .WithName("GetMicroSegmentationTopology")
        .WithSummary("Retrieves SPIFFE service identities, mTLS certificate status, and L7 policy mesh");

        group.MapPost("/rotate-cert", (RotateCertRequest request, IMicroSegmentationEngine engine) =>
        {
            try
            {
                var renewed = engine.RotateCertificate(request);
                return Results.Ok(renewed);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("RotateServiceMtlsCertificate")
        .WithSummary("Rotates ephemeral x509 SVID certificate for zero-trust service identity");

        group.MapPost("/evaluate-traffic", (EvaluateTrafficRequest request, IMicroSegmentationEngine engine) =>
        {
            var audit = engine.EvaluateTraffic(request);
            return Results.Ok(audit);
        })
        .WithName("EvaluateMeshTrafficPolicy")
        .WithSummary("Evaluates ingress/egress call against L7 policy rules to block lateral movement attacks");

        return endpoints;
    }
}
