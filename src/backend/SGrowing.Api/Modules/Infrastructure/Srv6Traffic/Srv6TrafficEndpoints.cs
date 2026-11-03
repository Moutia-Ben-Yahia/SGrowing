namespace SGrowing.Api.Modules.Infrastructure.Srv6Traffic;

public static class Srv6TrafficEndpoints
{
    public static IServiceCollection AddSrv6TrafficServices(this IServiceCollection services)
    {
        services.AddSingleton<ISrv6TrafficEngine, Srv6TrafficEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSrv6TrafficEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/srv6-traffic")
            .WithTags("Infrastructure - Multi-Cloud SRv6 Traffic Engineering");

        group.MapGet("/overview", (ISrv6TrafficEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/create-policy", (CreateSrv6PolicyRequest req, ISrv6TrafficEngine engine) =>
        {
            var policy = engine.CreatePolicy(req.Name, req.IngressHeadend, req.EgressTailend, req.Profile, req.SegmentListSids);
            return Results.Ok(policy);
        });

        group.MapPost("/record-telemetry", (RecordTelemetryRequest req, ISrv6TrafficEngine engine) =>
        {
            var tel = engine.RecordTelemetry(req.PolicyId, req.PacketsPerSecond, req.ThroughputGbps, req.PacketLossRatio);
            return Results.Ok(tel);
        });

        return endpoints;
    }
}

public sealed record CreateSrv6PolicyRequest(string Name, string IngressHeadend, string EgressTailend, string Profile, List<string> SegmentListSids);
public sealed record RecordTelemetryRequest(string PolicyId, double PacketsPerSecond, double ThroughputGbps, double PacketLossRatio);
