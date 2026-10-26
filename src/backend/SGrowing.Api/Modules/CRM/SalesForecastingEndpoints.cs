using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.CRM;

public record CreateForecastPeriodRequest(
    Guid TenantId,
    string Name,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    decimal TargetQuotaAmount
);

public record CreateCommissionPlanRequest(
    Guid TenantId,
    string PlanName,
    decimal BaseRatePercent = 10.0m,
    decimal QuotaThresholdPercent = 100.0m,
    decimal AcceleratedRatePercent = 15.0m
);

public record CalculateCommissionRequest(
    Guid TenantId,
    Guid PeriodId,
    Guid PlanId,
    Guid RepUserId,
    string RepName
);

public static class SalesForecastingEndpoints
{
    public static IEndpointRouteBuilder MapSalesForecastingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/sales")
            .WithTags("Sales Forecasting, Quota Attainment & Commissions (PRD 4.13)");

        group.MapPost("/forecast-periods", async ([FromBody] CreateForecastPeriodRequest req, ISalesForecastingService service) =>
        {
            var period = await service.CreateForecastPeriodAsync(req.TenantId, req.Name, req.StartDateUtc, req.EndDateUtc, req.TargetQuotaAmount);
            return Results.Ok(period);
        });

        group.MapGet("/forecast-periods", async ([FromQuery] Guid tenantId, ISalesForecastingService service) =>
        {
            var periods = await service.GetForecastPeriodsAsync(tenantId);
            return Results.Ok(periods);
        });

        group.MapGet("/forecasts/{periodId:guid}/summary", async (Guid periodId, [FromQuery] Guid tenantId, ISalesForecastingService service) =>
        {
            var summary = await service.GetForecastSummaryAsync(tenantId, periodId);
            return Results.Ok(summary);
        });

        group.MapPost("/commissions/plans", async ([FromBody] CreateCommissionPlanRequest req, ISalesForecastingService service) =>
        {
            var plan = await service.CreateCommissionPlanAsync(req.TenantId, req.PlanName, req.BaseRatePercent, req.QuotaThresholdPercent, req.AcceleratedRatePercent);
            return Results.Ok(plan);
        });

        group.MapPost("/commissions/calculate", async ([FromBody] CalculateCommissionRequest req, ISalesForecastingService service) =>
        {
            var statement = await service.CalculateRepCommissionsAsync(req.TenantId, req.PeriodId, req.PlanId, req.RepUserId, req.RepName);
            return Results.Ok(statement);
        });

        group.MapPost("/commissions/{id:guid}/approve", async (Guid id, [FromQuery] Guid tenantId, [FromBody] string? notes, ISalesForecastingService service) =>
        {
            var statement = await service.ApproveCommissionStatementAsync(tenantId, id, notes ?? "Approved");
            return Results.Ok(statement);
        });

        return routes;
    }
}
