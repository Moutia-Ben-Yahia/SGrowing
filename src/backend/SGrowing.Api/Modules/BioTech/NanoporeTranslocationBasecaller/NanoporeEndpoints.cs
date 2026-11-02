using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.BioTech.NanoporeTranslocationBasecaller;

public static class NanoporeEndpoints
{
    public static IServiceCollection AddNanoporeTranslocationServices(this IServiceCollection services)
    {
        services.AddScoped<INanoporeTranslocationEngine, NanoporeTranslocationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNanoporeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/nanopore")
            .WithTags("Nanopore Single-Molecule DNA/RNA Basecaller");

        group.MapPost("/evaluate", (
            [FromBody] NanoporeEvaluationRequest request,
            [FromServices] INanoporeTranslocationEngine engine) =>
        {
            var decision = engine.EvaluateTranslocation(request);
            return Results.Ok(decision);
        })
        .WithName("EvaluateNanoporeTranslocation")
        .WithSummary("Processes ionic current blockade signals, identifies poly-A tails, detects stall events, and controls voltage bias.");

        group.MapGet("/channel/{channelId}", (string channelId) =>
        {
            var nominal = new NanoporeChannelTelemetry(channelId);
            return Results.Ok(new
            {
                ChannelId = channelId,
                OpenPoreCurrentPa = nominal.OpenPoreCurrentPicoAmperes,
                AppliedVoltageMv = nominal.AppliedVoltageBiasMilliVolts,
                ResistanceGigaOhms = nominal.MembraneResistanceGigaOhms,
                Status = "LIPID_BILAYER_SEALED"
            });
        })
        .WithName("GetNanoporeChannelStatus")
        .WithSummary("Returns membrane resistance, voltage bias, and open pore baseline current for a nanopore sequencing channel.");

        return app;
    }
}
