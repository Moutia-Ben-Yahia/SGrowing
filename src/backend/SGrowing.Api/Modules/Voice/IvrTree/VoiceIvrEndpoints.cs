using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.IvrTree;

public static class VoiceIvrTreeEndpoints
{
    public static IServiceCollection AddVoiceIvrTreeServices(this IServiceCollection services)
    {
        services.AddSingleton<IVoiceIvrEngine, VoiceIvrEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVoiceIvrTreeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/ivr-tree")
            .WithTags("Voice IVR & Broadcast");

        group.MapGet("/trees", (IVoiceIvrEngine engine) =>
        {
            var trees = engine.ListTrees();
            return Results.Ok(trees);
        });

        group.MapPost("/simulate", (SimulateIvrCallRequest request, IVoiceIvrEngine engine) =>
        {
            var result = engine.SimulateCall(request);
            return Results.Ok(result);
        });

        group.MapPost("/broadcast", (VoiceBroadcastCampaign campaign, IVoiceIvrEngine engine) =>
        {
            var created = engine.CreateBroadcast(campaign);
            return Results.Ok(created);
        });

        group.MapGet("/broadcast/{campaignId}", (string campaignId, IVoiceIvrEngine engine) =>
        {
            var campaign = engine.GetBroadcast(campaignId);
            return campaign is not null ? Results.Ok(campaign) : Results.NotFound();
        });

        return app;
    }
}
