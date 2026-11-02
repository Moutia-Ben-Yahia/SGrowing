using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.IoT.PredictiveTelemetry;

public static class PredictiveTelemetryEndpoints
{
    public static IServiceCollection AddPredictiveTelemetryServices(this IServiceCollection services)
    {
        services.AddSingleton<TimeSeriesAnomalyDetectorEngine>();
        services.AddSingleton<MachineryRemainingUsefulLifeEstimator>();
        services.AddSingleton<AutonomousMaintenanceSchedulerService>();
        return services;
    }

    public static IEndpointRouteBuilder MapPredictiveTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/iot/predictive-telemetry")
            .WithTags("Industrial IoT Time-Series Anomaly Forecaster & Predictive Machinery Telemetry");

        group.MapPost("/samples/evaluate", (
            MachineTelemetrySample sample,
            TimeSeriesAnomalyDetectorEngine engine) =>
        {
            var result = engine.AnalyzeSample(sample);
            return Results.Ok(result);
        });

        group.MapPost("/rul/estimate", (
            EstimateRulApiRequest request,
            MachineryRemainingUsefulLifeEstimator estimator) =>
        {
            var estimate = estimator.EstimateRul(
                request.MachineId,
                request.AccumulatedOperatingHours,
                request.MeanVibrationRms,
                request.MeanBearingTempCelsius);

            return Results.Ok(estimate);
        });

        group.MapPost("/maintenance/schedule", (
            ScheduleMaintenanceApiRequest request,
            AutonomousMaintenanceSchedulerService scheduler) =>
        {
            var order = scheduler.EvaluateAndSchedule(
                request.Anomaly,
                request.Rul,
                request.PreferredSparePartSku ?? "SKU-BEARING-6205");

            return Results.Ok(order);
        });

        group.MapGet("/work-orders", (AutonomousMaintenanceSchedulerService scheduler) =>
        {
            return Results.Ok(scheduler.GetActiveWorkOrders());
        });

        return app;
    }
}

public sealed record EstimateRulApiRequest(
    string MachineId,
    double AccumulatedOperatingHours,
    double MeanVibrationRms,
    double MeanBearingTempCelsius
);

public sealed record ScheduleMaintenanceApiRequest(
    AnomalyDetectionResult Anomaly,
    MachineryRulEstimate Rul,
    string? PreferredSparePartSku = null
);
