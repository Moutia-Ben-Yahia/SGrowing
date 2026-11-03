namespace SGrowing.Api.Modules.Advertising.VehicleAudioDsp;

public static class VehicleAudioDspEndpoints
{
    public static IServiceCollection AddVehicleAudioDspServices(this IServiceCollection services)
    {
        services.AddSingleton<IVehicleAudioDspEngine, VehicleAudioDspEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVehicleAudioDspEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/vehicle-audio-dsp")
            .WithTags("Advertising - Connected Vehicle In-Cabin Audio DSP");

        group.MapGet("/overview", (IVehicleAudioDspEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/bid-slot", (BidSlotRequest req, IVehicleAudioDspEngine engine) =>
        {
            var bid = engine.BidInCabinAudioSlot(req.VehicleMakeModel, req.Context, req.SpeedKmh, req.Latitude, req.Longitude);
            return Results.Ok(bid);
        });

        group.MapPost("/log-playout", (LogPlayoutRequest req, IVehicleAudioDspEngine engine) =>
        {
            var att = engine.LogPlayoutAttestation(req.BidId, req.VehicleHardwareId, req.DurationSeconds, req.VolumeDb);
            return Results.Ok(att);
        });

        return endpoints;
    }
}

public sealed record BidSlotRequest(string VehicleMakeModel, CommuteContextType Context, double SpeedKmh, double Latitude, double Longitude);
public sealed record LogPlayoutRequest(string BidId, string VehicleHardwareId, int DurationSeconds, double VolumeDb);
