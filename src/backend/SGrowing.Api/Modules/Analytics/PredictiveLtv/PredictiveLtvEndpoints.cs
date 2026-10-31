namespace SGrowing.Api.Modules.Analytics.PredictiveLtv;

public static class PredictiveLtvEndpoints
{
    public static IEndpointRouteBuilder MapPredictiveLtvEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics/pltv")
            .WithTags("Predictive Customer Lifetime Value (pLTV)");

        group.MapPost("/predict-single", async (
            Guid tenantId,
            CustomerTransactionHistory history,
            IPredictiveLtvService service,
            CancellationToken ct) =>
        {
            var result = await service.PredictCustomerLtvAsync(tenantId, history, ct: ct);
            return Results.Ok(result);
        })
        .WithName("PredictCustomerPltv")
        .WithSummary("Calculates probabilistic P(Alive), future transaction count, and predicted residual LTV for a customer.");

        group.MapPost("/predict-cohort", async (
            CalculatePltvRequest request,
            IPredictiveLtvService service,
            CancellationToken ct) =>
        {
            var summary = await service.PredictCohortBatchAsync(request, ct);
            return Results.Ok(summary);
        })
        .WithName("PredictCohortPltv")
        .WithSummary("Runs cohort-level BG/NBD and Gamma-Gamma predictive analysis with segment distribution.");

        group.MapGet("/at-risk-vips", async (
            Guid tenantId,
            IPredictiveLtvService service,
            CancellationToken ct) =>
        {
            var vips = await service.GetAtRiskVipInterventionsAsync(tenantId, ct);
            return Results.Ok(vips);
        })
        .WithName("GetAtRiskVipPltvInterventions")
        .WithSummary("Retrieves all high-value customers classified as AtRiskDefecting requiring win-back interventions.");

        return app;
    }
}
