using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.MpcThresholdSigner;

public static class MpcEndpoints
{
    public static IServiceCollection AddMpcThresholdSignerServices(this IServiceCollection services)
    {
        services.AddSingleton<ThresholdEcdsaMpcEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMpcThresholdSignerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/mpc-threshold")
            .WithTags("Autonomous Self-Sovereign Decentralized Key Management (DKMS) & Multi-Party Computation (MPC) Threshold Signer");

        group.MapPost("/generate-cluster", (
            GenerateClusterRequest request,
            ThresholdEcdsaMpcEngine engine) =>
        {
            var cluster = engine.GenerateThresholdCluster(request.ThresholdT, request.TotalN);
            return Results.Ok(cluster);
        });

        group.MapPost("/sign", (
            SignThresholdMessageRequest request,
            ThresholdEcdsaMpcEngine engine) =>
        {
            var signature = engine.SignMessage(
                request.MasterPublicKeyHex,
                request.MessageText,
                request.SignerShares
            );

            return Results.Ok(signature);
        });

        return app;
    }
}
