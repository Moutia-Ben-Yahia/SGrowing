using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Healthcare.BciNeuralKinematics;

public static class BciNeuralEndpoints
{
    public static IServiceCollection AddBciNeuralKinematicsServices(this IServiceCollection services)
    {
        services.AddSingleton<IBciNeuralKinematicsEngine, BciNeuralKinematicsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBciNeuralKinematicsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/healthcare/bci")
            .WithTags("Healthcare - Brain-Computer Interface Kinematics");

        group.MapPost("/decode-kinematics", (
            BciKinematicsDecodeRequest request,
            IBciNeuralKinematicsEngine engine) =>
        {
            var result = engine.DecodeKinematics(request);
            return Results.Ok(result);
        })
        .WithName("DecodeBciNeuralKinematics")
        .WithSummary("Decodes motor cortex intracortical population spikes into 3D end-effector kinematics via Kalman filtering")
        .Produces<BciKinematicsDecodeResult>(StatusCodes.Status200OK);

        return endpoints;
    }
}
