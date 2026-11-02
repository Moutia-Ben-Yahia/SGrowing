using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.AI.SpikingRetinaGazeArbiter;

public static class SpikingRetinaEndpoints
{
    public static IServiceCollection AddSpikingRetinaGazeArbiterServices(this IServiceCollection services)
    {
        services.AddScoped<ISpikingRetinaGazeArbiterEngine, SpikingRetinaGazeArbiterEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSpikingRetinaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/spiking-retina")
            .WithTags("Neuromorphic Spiking Silicon Retina Saccadic Gaze");

        group.MapPost("/evaluate", (
            [FromBody] SpikingRetinaEvaluationRequest request,
            [FromServices] ISpikingRetinaGazeArbiterEngine engine) =>
        {
            var decision = engine.EvaluateSpikeStream(request);
            return Results.Ok(decision);
        })
        .WithName("EvaluateRetinaSpikeStream")
        .WithSummary("Processes asynchronous event-based neuromorphic spike streams, calculates optical flow, and directs robotic saccades.");

        group.MapGet("/telemetry/{sensorId}", (string sensorId) =>
        {
            var nominal = new NeuromorphicSensorState(sensorId);
            return Results.Ok(new
            {
                SensorId = sensorId,
                Resolution = $"{nominal.ResolutionWidth}x{nominal.ResolutionHeight}",
                BackgroundRate = nominal.BackgroundSpikeRateEventsPerSecond,
                TemporalContrastThreshold = nominal.TemporalContrastThresholdLog,
                Status = "AER_BUS_READY"
            });
        })
        .WithName("GetSpikingRetinaTelemetry")
        .WithSummary("Returns pixel resolution, temporal contrast threshold, and AER bus status for a spiking silicon retina sensor.");

        return app;
    }
}
