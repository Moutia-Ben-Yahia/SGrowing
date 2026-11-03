namespace SGrowing.Api.Modules.Voice.NeuralPlc;

public static class NeuralPlcEndpoints
{
    public static IServiceCollection AddNeuralPlcServices(this IServiceCollection services)
    {
        services.AddSingleton<INeuralPlcEngine, NeuralPlcEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNeuralPlcEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/neural-plc")
            .WithTags("Voice - Deep Neural Packet Loss Concealment (PLC)");

        group.MapGet("/overview", (INeuralPlcEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/reconstruct-frame", (ReconstructFrameRequest req, INeuralPlcEngine engine) =>
        {
            var recon = engine.ReconstructLostFrame(req.CallSessionId, req.SequenceNumber, req.Codec, req.ConsecutiveLost);
            return Results.Ok(recon);
        });

        group.MapPost("/tune-buffer", (TuneBufferRequest req, INeuralPlcEngine engine) =>
        {
            var metric = engine.TuneJitterBuffer(req.CallSessionId, req.PacketLossPct, req.JitterVarianceMs);
            return Results.Ok(metric);
        });

        return endpoints;
    }
}

public sealed record ReconstructFrameRequest(string CallSessionId, int SequenceNumber, PlcCodecType Codec, int ConsecutiveLost);
public sealed record TuneBufferRequest(string CallSessionId, double PacketLossPct, double JitterVarianceMs);
