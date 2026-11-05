using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Common.Caching;

public static class CachingServiceExtensions
{
    public static IServiceCollection AddGrowthOsCaching(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ITenantSettingsCache, HybridTenantSettingsCache>();
        return services;
    }
}
