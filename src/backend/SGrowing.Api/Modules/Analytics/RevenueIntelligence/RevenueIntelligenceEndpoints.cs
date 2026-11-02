namespace SGrowing.Api.Modules.Analytics.RevenueIntelligence;

public static class RevenueIntelligenceEndpoints
{
    public static IServiceCollection AddRevenueIntelligenceServices(this IServiceCollection services)
    {
        services.AddSingleton<IRevenueIntelligenceEngine, RevenueIntelligenceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRevenueIntelligenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/analytics/revenue")
            .WithTags("Executive Revenue Intelligence");

        group.MapGet("/snapshot", (IRevenueIntelligenceEngine engine, string? tenantId) =>
        {
            var targetTenant = tenantId ?? "tenant_default";
            var snapshot = engine.ComputeSnapshot(targetTenant);
            return Results.Ok(snapshot);
        })
        .WithName("GetRevenueSnapshot")
        .WithSummary("Retrieve high-level ARR, MRR, NRR, GRR, and full cohort intelligence");

        group.MapGet("/waterfall", (IRevenueIntelligenceEngine engine, string? tenantId) =>
        {
            var targetTenant = tenantId ?? "tenant_default";
            var waterfall = engine.GetWaterfall(targetTenant);
            return Results.Ok(waterfall);
        })
        .WithName("GetMrrWaterfall")
        .WithSummary("Retrieve decomposed MRR waterfall (New, Expansion, Contraction, Churn, Quick Ratio)");

        group.MapGet("/cohorts", (IRevenueIntelligenceEngine engine, string? tenantId) =>
        {
            var targetTenant = tenantId ?? "tenant_default";
            var cohorts = engine.GetCohortMatrix(targetTenant);
            return Results.Ok(cohorts);
        })
        .WithName("GetCohortRetentionMatrix")
        .WithSummary("Retrieve N x M retention and revenue expansion cohort analysis table");

        return app;
    }
}
