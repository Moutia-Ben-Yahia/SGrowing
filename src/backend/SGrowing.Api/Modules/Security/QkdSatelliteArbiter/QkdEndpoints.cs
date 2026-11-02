using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.QkdSatelliteArbiter;

public static class QkdEndpoints
{
    public static IServiceCollection AddQkdSatelliteArbiterServices(this IServiceCollection services)
    {
        services.AddSingleton<IQkdSatelliteArbiterEngine, QkdSatelliteArbiterEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapQkdSatelliteArbiterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/security/qkd")
            .WithTags("Security - Quantum Key Distribution (QKD) Arbiter");

        group.MapPost("/distill-keys", (
            QkdSessionRequest request,
            IQkdSatelliteArbiterEngine engine) =>
        {
            var result = engine.DistillSecretKeys(request);
            return Results.Ok(result);
        })
        .WithName("DistillQkdSecretKeys")
        .WithSummary("Processes quantum pulse transmissions, performs BB84 sifting, evaluates QBER threshold, and distills unconditional secret keys")
        .Produces<QkdSessionResult>(StatusCodes.Status200OK);

        return endpoints;
    }
}
