using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Data;

namespace SGrowing.Api.Modules.Finance;

public static class CashflowForecastingEndpoints
{
    public static IEndpointRouteBuilder MapCashflowForecastingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/cashflow-forecasting")
            .WithTags("Autonomous Cash Flow Forecasting & Working Capital Float Engine");

        // Helper to resolve or fallback ICashflowForecastingService seamlessly
        static ICashflowForecastingService ResolveService(HttpContext httpContext)
        {
            var svc = httpContext.RequestServices.GetService<ICashflowForecastingService>();
            if (svc != null) return svc;
            var db = httpContext.RequestServices.GetService<SGrowingDbContext>();
            return new CashflowForecastingService(db);
        }

        // 1. GET /summary
        group.MapGet("/summary", async (
            HttpContext context,
            [FromQuery] Guid? tenantId,
            [FromQuery] decimal? safetyThreshold) =>
        {
            var tid = tenantId ?? Guid.Empty;
            var service = ResolveService(context);
            var summary = await service.GetSummaryAsync(tid, safetyThreshold, context.RequestAborted);
            return Results.Ok(summary);
        })
        .WithName("GetCashflowSummary")
        .WithSummary("Get high-level float, cash position, CCC metrics, and deficit alerts");

        // 2. GET /projection
        group.MapGet("/projection", async (
            HttpContext context,
            [FromQuery] Guid? tenantId,
            [FromQuery] int? horizonDays,
            [FromQuery] decimal? safetyThreshold) =>
        {
            var tid = tenantId ?? Guid.Empty;
            var service = ResolveService(context);
            int days = horizonDays is 30 or 60 or 90 ? horizonDays.Value : 90;
            var points = await service.GenerateProjectionAsync(tid, days, safetyThreshold, context.RequestAborted);
            return Results.Ok(points);
        })
        .WithName("GetCashflowProjection")
        .WithSummary("Get 30/60/90-day daily cash trajectory simulation points");

        // 3. GET /courier-float
        group.MapGet("/courier-float", async (
            HttpContext context,
            [FromQuery] Guid? tenantId) =>
        {
            var tid = tenantId ?? Guid.Empty;
            var service = ResolveService(context);
            var breakdown = await service.GetCourierFloatBreakdownAsync(tid, context.RequestAborted);
            return Results.Ok(breakdown);
        })
        .WithName("GetCourierFloatBreakdown")
        .WithSummary("Get breakdown of in-transit float per courier provider with clearance delay curves");

        // 4. GET /receivables-payables
        group.MapGet("/receivables-payables", async (
            HttpContext context,
            [FromQuery] Guid? tenantId) =>
        {
            var tid = tenantId ?? Guid.Empty;
            var service = ResolveService(context);
            var aging = await service.GetReceivablesPayablesAgingAsync(tid, context.RequestAborted);
            return Results.Ok(aging);
        })
        .WithName("GetReceivablesPayablesAging")
        .WithSummary("Get accounts receivable and payable aging schedules with Net 30/60 due dates");

        // 5. POST /factoring/simulate
        group.MapPost("/factoring/simulate", async (
            HttpContext context,
            [FromBody] FactoringSimulationRequestDto request) =>
        {
            var service = ResolveService(context);
            var result = await service.SimulateFactoringAdvanceAsync(request, context.RequestAborted);
            return Results.Ok(result);
        })
        .WithName("SimulateFactoringAdvance")
        .WithSummary("Simulate drawing down instant capital against unremitted courier float or B2B receivables");

        // 6. POST /adjustment
        group.MapPost("/adjustment", async (
            HttpContext context,
            [FromBody] RecordAdjustmentRequestDto request) =>
        {
            var service = ResolveService(context);
            var adj = await service.RecordAdjustmentAsync(request, context.RequestAborted);
            return Results.Created($"/api/v1/finance/cashflow-forecasting/adjustments/{adj.Id}", adj);
        })
        .WithName("RecordCashflowAdjustment")
        .WithSummary("Record manual one-time cashflow adjustment (inflow/outflow)");

        // 7. GET /adjustments
        group.MapGet("/adjustments", async (
            HttpContext context,
            [FromQuery] Guid? tenantId) =>
        {
            var tid = tenantId ?? Guid.Empty;
            var service = ResolveService(context);
            var adjs = await service.GetAdjustmentsAsync(tid, context.RequestAborted);
            return Results.Ok(adjs);
        })
        .WithName("GetCashflowAdjustments")
        .WithSummary("Retrieve recorded manual cashflow adjustments");

        return app;
    }
}
