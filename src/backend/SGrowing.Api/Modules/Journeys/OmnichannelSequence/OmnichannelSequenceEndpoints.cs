using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Journeys.OmnichannelSequence;

public static class OmnichannelSequenceEndpoints
{
    public static IServiceCollection AddOmnichannelSequenceServices(this IServiceCollection services)
    {
        services.AddSingleton<IOmnichannelSequenceEngine, OmnichannelSequenceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapOmnichannelSequenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/journeys/omnichannel-sequence")
            .WithTags("Omnichannel Sequence Orchestrator");

        group.MapPost("/dispatch-step", (ExecuteCadenceStepRequest request, IOmnichannelSequenceEngine engine) =>
        {
            var result = engine.ExecuteStep(request);
            return Results.Ok(result);
        }).WithName("DispatchOmnichannelStep");

        group.MapGet("/cadences/{tenantId}/{cadenceId}", (string tenantId, string cadenceId, IOmnichannelSequenceEngine engine) =>
        {
            var cadence = engine.GetCadence(tenantId, cadenceId);
            return cadence is not null
                ? Results.Ok(cadence)
                : Results.NotFound(new { message = $"Cadence '{cadenceId}' not found for tenant '{tenantId}'." });
        }).WithName("GetOmnichannelCadence");

        group.MapPost("/cadences/{tenantId}", (string tenantId, OmnichannelCadenceDefinition cadence, IOmnichannelSequenceEngine engine) =>
        {
            engine.RegisterCadence(tenantId, cadence);
            return Results.Created($"/api/v1/journeys/omnichannel-sequence/cadences/{tenantId}/{cadence.CadenceId}", cadence);
        }).WithName("RegisterOmnichannelCadence");

        return endpoints;
    }
}
