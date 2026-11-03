namespace SGrowing.Api.Modules.Infrastructure.RouteFlapDamping;

public static class RouteFlapDampingEndpoints
{
    public static IServiceCollection AddRouteFlapDampingServices(this IServiceCollection services)
    {
        services.AddSingleton<IRouteFlapDampingEngine, RouteFlapDampingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRouteFlapDampingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/route-flap-damping")
            .WithTags("Infrastructure - Multi-Cloud BGP Route Flap Damping");

        group.MapGet("/overview", (IRouteFlapDampingEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/record-flap", (RecordFlapRequest req, IRouteFlapDampingEngine engine) =>
        {
            var evt = engine.RecordFlapEvent(req.PrefixCidr, req.OriginAsn, req.PeerIp);
            return Results.Ok(evt);
        });

        group.MapGet("/prefix-state/{prefix}", (string prefix, IRouteFlapDampingEngine engine) =>
        {
            var decoded = System.Net.WebUtility.UrlDecode(prefix);
            var state = engine.CheckPrefixState(decoded);
            return Results.Ok(state);
        });

        group.MapPost("/evacuate-pop", (EvacuatePoPRequest req, IRouteFlapDampingEngine engine) =>
        {
            var evac = engine.EvacuatePoP(req.TargetPoPName, req.CloudProvider, req.Reason);
            return Results.Ok(evac);
        });

        return endpoints;
    }
}

public sealed record RecordFlapRequest(string PrefixCidr, string OriginAsn, string PeerIp);
public sealed record EvacuatePoPRequest(string TargetPoPName, string CloudProvider, string Reason);
