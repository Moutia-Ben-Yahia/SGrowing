using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Manufacturing.OpticalQualityControl;

public static class OpticalQcEndpoints
{
    public static IServiceCollection AddOpticalQualityControlServices(this IServiceCollection services)
    {
        services.AddSingleton<VisualDefectAnomalyClassifierEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapOpticalQualityControlEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/manufacturing/optical-qc")
            .WithTags("Autonomous Multi-Modal Optical Quality Control & Defect Detection Computer Vision Pipeline");

        group.MapPost("/inspect", (
            InspectItemRequest request,
            VisualDefectAnomalyClassifierEngine engine) =>
        {
            var result = engine.InspectAndClassifyItem(request);
            return Results.Ok(result);
        });

        return app;
    }
}
