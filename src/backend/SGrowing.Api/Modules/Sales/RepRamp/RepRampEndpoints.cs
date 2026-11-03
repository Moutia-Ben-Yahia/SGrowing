using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Sales.RepRamp;

public static class RepRampEndpoints
{
    public static IServiceCollection AddRepRampServices(this IServiceCollection services)
    {
        services.AddSingleton<IRepRampEngine, RepRampEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRepRampEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/rep-ramp")
            .WithTags("Sales Rep Ramp Acceleration");

        group.MapPost("/profiles", (
            [FromBody] CreateRepProfileRequest request,
            [FromServices] IRepRampEngine engine) =>
        {
            var profile = engine.CreateOrUpdateRepProfile(request);
            return Results.Created($"/api/v1/sales/rep-ramp/profiles/{profile.RepId}", profile);
        })
        .WithName("CreateRepRampProfile")
        .WithSummary("Creates or initializes a sales rep ramp acceleration profile");

        group.MapGet("/profiles", (
            [FromServices] IRepRampEngine engine) =>
        {
            var profiles = engine.GetProfiles();
            return Results.Ok(profiles);
        })
        .WithName("GetRepRampProfiles")
        .WithSummary("Retrieves all active sales rep onboarding profiles");

        group.MapPost("/shadow-guidance", (
            [FromBody] ShadowCallGuidanceRequest request,
            [FromServices] IRepRampEngine engine) =>
        {
            var guidance = engine.GenerateShadowGuidance(request);
            return Results.Ok(guidance);
        })
        .WithName("GenerateShadowCallGuidance")
        .WithSummary("Generates real-time AI whisper prompts and micro-coaching during live shadow calls");

        group.MapGet("/trajectories/{repId}", (
            string repId,
            [FromServices] IRepRampEngine engine) =>
        {
            var trajectory = engine.GetRampTrajectory(repId);
            return Results.Ok(trajectory);
        })
        .WithName("GetRepRampTrajectory")
        .WithSummary("Calculates ramp velocity trajectory, days to full quota, and targeted micro-curriculum");

        return app;
    }
}
