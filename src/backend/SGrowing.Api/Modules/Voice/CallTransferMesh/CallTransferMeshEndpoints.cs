using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.CallTransferMesh;

public static class CallTransferMeshEndpoints
{
    public static IServiceCollection AddCallTransferMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<ICallTransferMeshEngine, CallTransferMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCallTransferMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/call-transfer")
            .WithTags("Voice - Contact Center IVR & Call Transfer Mesh");

        group.MapGet("/active-calls", (ICallTransferMeshEngine engine) =>
        {
            var list = engine.GetActiveCalls();
            return Results.Ok(list);
        })
        .WithName("GetActiveCallSessions")
        .WithSummary("Retrieves active voice sessions, sentiment, and current transfer states");

        group.MapPost("/warm-transfer", (InitiateWarmTransferRequest request, ICallTransferMeshEngine engine) =>
        {
            try
            {
                var updated = engine.InitiateWarmTransfer(request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("InitiateWarmCallTransfer")
        .WithSummary("Pre-warms human agent headset with call summary and bridges 3-way conference");

        group.MapPost("/complete-transfer", (CompleteTransferRequest request, ICallTransferMeshEngine engine) =>
        {
            try
            {
                var updated = engine.CompleteTransfer(request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("CompleteCallTransfer")
        .WithSummary("Completes handover of live call session to human specialist with zero audio drops");

        return endpoints;
    }
}
