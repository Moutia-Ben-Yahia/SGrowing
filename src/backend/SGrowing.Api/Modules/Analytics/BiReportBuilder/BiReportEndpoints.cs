namespace SGrowing.Api.Modules.Analytics.BiReportBuilder;

public static class BiReportEndpoints
{
    public static IServiceCollection AddBiReportBuilderServices(this IServiceCollection services)
    {
        services.AddSingleton<IBiReportEngine, BiReportEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBiReportBuilderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/analytics/bi")
            .WithTags("Drag-and-Drop Enterprise BI Report Builder & Monte Carlo ARR Forecaster");

        group.MapGet("/reports", (IBiReportEngine engine) =>
        {
            var reports = engine.ListSavedReports();
            return Results.Ok(reports);
        })
        .WithName("ListSavedBiReports")
        .WithSummary("List all saved custom BI reports, dimensions, and chart definitions");

        group.MapPost("/execute", (IBiReportEngine engine, RunReportRequest request) =>
        {
            var result = engine.ExecuteReport(request);
            return Results.Ok(result);
        })
        .WithName("ExecuteAdHocBiReport")
        .WithSummary("Execute ad-hoc aggregation across Contacts, Opportunities, Ads CAPI, or Invoices");

        group.MapPost("/forecast-monte-carlo", (IBiReportEngine engine, MonteCarloSimRequest request) =>
        {
            var forecast = engine.RunMonteCarloForecast(request);
            return Results.Ok(forecast);
        })
        .WithName("RunMonteCarloArrForecast")
        .WithSummary("Execute 1,000 stochastic simulation iterations to project P10, P50, and P90 ARR cones");

        return app;
    }
}
