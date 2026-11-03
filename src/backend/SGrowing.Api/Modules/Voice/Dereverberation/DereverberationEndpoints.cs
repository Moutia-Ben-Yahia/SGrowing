namespace SGrowing.Api.Modules.Voice.Dereverberation;

public static class DereverberationEndpoints
{
    public static IServiceCollection AddDereverberationServices(this IServiceCollection services)
    {
        services.AddSingleton<IDereverberationEngine, DereverberationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDereverberationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/dereverberation")
            .WithTags("Voice - Acoustic Room Profiling & Dereverberation");

        group.MapGet("/overview", (IDereverberationEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/profile-room", (ProfileRoomRequest req, IDereverberationEngine engine) =>
        {
            var profile = engine.ProfileRoomAcoustics(req.CallSessionId, req.RoomType, req.RawRt60Sec);
            return Results.Ok(profile);
        });

        group.MapPost("/clean-audio", (CleanAudioRequest req, IDereverberationEngine engine) =>
        {
            var flt = engine.CleanAudioStream(req.CallSessionId, req.InputSnrDb);
            return Results.Ok(flt);
        });

        return endpoints;
    }
}

public sealed record ProfileRoomRequest(string CallSessionId, RoomAcousticType RoomType, double RawRt60Sec);
public sealed record CleanAudioRequest(string CallSessionId, double InputSnrDb);
