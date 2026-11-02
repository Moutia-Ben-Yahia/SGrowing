using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Logistics.DrayageScheduler;

public static class DrayageSchedulerEndpoints
{
    public static IServiceCollection AddDrayageSchedulerServices(this IServiceCollection services)
    {
        services.AddSingleton<IntermodalDrayageSchedulerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDrayageSchedulerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/drayage-scheduler")
            .WithTags("Autonomous Intermodal Container Terminal Drayage Scheduling & Demurrage Minimizer");

        group.MapPost("/schedule", (
            ScheduleDrayageRequest request,
            IntermodalDrayageSchedulerEngine engine) =>
        {
            var plan = engine.GenerateOptimalSchedule(request);
            return Results.Ok(plan);
        });

        return app;
    }
}
