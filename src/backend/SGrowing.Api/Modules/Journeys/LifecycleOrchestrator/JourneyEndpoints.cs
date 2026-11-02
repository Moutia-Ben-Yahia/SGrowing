namespace SGrowing.Api.Modules.Journeys.LifecycleOrchestrator;

public static class JourneyEndpoints
{
    public static IServiceCollection AddLifecycleJourneyServices(this IServiceCollection services)
    {
        services.AddSingleton<IJourneyEngine, JourneyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapLifecycleJourneyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/journeys")
            .WithTags("Visual Customer Lifecycle Journey Orchestrator & Fatigue Gate");

        group.MapGet("/lifecycle", (IJourneyEngine engine) =>
        {
            var journeys = engine.ListJourneys();
            return Results.Ok(journeys);
        })
        .WithName("ListLifecycleJourneys")
        .WithSummary("List all multi-stage customer lifecycle journeys with wait conditions and conversion drop-offs");

        group.MapGet("/lifecycle/{id}", (IJourneyEngine engine, string id) =>
        {
            var journey = engine.GetJourney(id);
            return journey is not null ? Results.Ok(journey) : Results.NotFound();
        })
        .WithName("GetLifecycleJourneyById")
        .WithSummary("Get a specific lifecycle journey canvas and stage nodes");

        group.MapPost("/enroll", (IJourneyEngine engine, EnrollContactRequest request) =>
        {
            try
            {
                var enrollment = engine.EnrollContact(request);
                return Results.Created($"/api/v1/journeys/enrollments/{enrollment.EnrollmentId}", enrollment);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("EnrollContactInJourney")
        .WithSummary("Enroll a target contact into a multi-stage lifecycle journey");

        group.MapPost("/evaluate-fatigue", (IJourneyEngine engine, EvaluateFatigueRequest request) =>
        {
            var result = engine.EvaluateFatigue(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateOutreachFatigue")
        .WithSummary("Evaluate contact eligibility against weekly frequency caps and minimum hours cooldown spacing");

        group.MapGet("/lifecycle/{id}/enrollments", (IJourneyEngine engine, string id) =>
        {
            var enrollments = engine.GetEnrollments(id);
            return Results.Ok(enrollments);
        })
        .WithName("ListJourneyEnrollments")
        .WithSummary("List active contacts enrolled in a customer lifecycle journey");

        return app;
    }
}
