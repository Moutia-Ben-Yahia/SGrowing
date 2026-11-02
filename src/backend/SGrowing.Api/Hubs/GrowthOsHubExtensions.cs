using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Hubs;

public static class GrowthOsHubExtensions
{
    public static IServiceCollection AddGrowthOsHubServices(this IServiceCollection services)
    {
        services.AddSingleton<IGrowthOsHubBroadcaster, GrowthOsHubBroadcaster>();
        return services;
    }

    public static IEndpointRouteBuilder MapGrowthOsHubEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<GrowthOsTelemetryHub>("/hubs/growth-telemetry");
        return endpoints;
    }
}
