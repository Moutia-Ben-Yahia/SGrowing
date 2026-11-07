using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGrowing.Client.Models;

namespace SGrowing.Client;

/// <summary>
/// Extension methods for setting up SGrowing Growth OS SDK client in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures the <see cref="ISGrowingClient"/> typed HTTP client into the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Delegate to configure <see cref="SGrowingClientOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSGrowingClient(
        this IServiceCollection services,
        Action<SGrowingClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        services.AddHttpClient<ISGrowingClient, SGrowingClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SGrowingClientOptions>>().Value;

            httpClient.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            httpClient.Timeout = options.Timeout;

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {options.ApiKey}");
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", options.ApiKey);
            }

            if (options.TenantId.HasValue)
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Tenant-Id", options.TenantId.Value.ToString());
            }

            foreach (var (headerKey, headerValue) in options.CustomHeaders)
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(headerKey, headerValue);
            }
        });

        return services;
    }
}
