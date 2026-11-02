namespace SGrowing.Api.Modules.Manufacturing.CleanroomAirflowPredictor;

public static class CleanroomAirflowEndpoints
{
    public static IServiceCollection AddCleanroomAirflowServices(this IServiceCollection services)
    {
        services.AddSingleton<CleanroomAirflowPredictorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCleanroomAirflowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/manufacturing/cleanroom-airflow")
            .WithTags("Cleanroom Airflow Predictor");

        group.MapPost("/evaluate-facility", (CleanroomAnalysisRequest request, CleanroomAirflowPredictorEngine engine) =>
        {
            var result = engine.EvaluateCleanroomFacility(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateCleanroomFacility")
        .WithSummary("Models ISO 14644 semiconductor cleanroom laminar airflow, particulate contamination, and FFU energy modulation");

        return app;
    }
}
