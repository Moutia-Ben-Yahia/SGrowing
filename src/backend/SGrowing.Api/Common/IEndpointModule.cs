using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Common;

/// <summary>
/// Defines a contract for modular Minimal API endpoint discovery and mapping.
/// </summary>
public interface IEndpointModule
{
    /// <summary>
    /// Configures HTTP route endpoints within the application routing pipeline.
    /// </summary>
    /// <param name="app">The endpoint route builder to register routes on.</param>
    void MapEndpoints(IEndpointRouteBuilder app);
}
