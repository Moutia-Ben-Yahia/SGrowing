namespace SGrowing.Api.Modules.Sales.DealCopilot;

public static class DealCopilotEndpoints
{
    public static IServiceCollection AddDealCopilotServices(this IServiceCollection services)
    {
        services.AddSingleton<IDealCopilotEngine, DealCopilotEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDealCopilotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/deal-copilot")
            .WithTags("Autonomous RevOps Deal Copilot & Risk Radar");

        group.MapGet("/deals", (IDealCopilotEngine engine, string? tenantId) =>
        {
            var targetTenant = tenantId ?? "tenant_default";
            var deals = engine.GetPipelineDeals(targetTenant);
            return Results.Ok(deals);
        })
        .WithName("ListPipelineDealsWithRisk")
        .WithSummary("Retrieve all active sales pipeline deals with AI slippage and risk factor analysis");

        group.MapGet("/deals/{dealId}", (IDealCopilotEngine engine, string dealId) =>
        {
            var deal = engine.GetDeal(dealId);
            return deal is not null ? Results.Ok(deal) : Results.NotFound(new { error = $"Deal '{dealId}' not found" });
        })
        .WithName("GetPipelineDealDetails")
        .WithSummary("Retrieve detailed deal slippage telemetry and prescribed intervention actions");

        group.MapPost("/deals/intervene", (IDealCopilotEngine engine, InterventionActionRequest request) =>
        {
            try
            {
                var result = engine.ExecuteIntervention(request);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ExecuteDealIntervention")
        .WithSummary("Execute autonomous AI copilot intervention to recover slipping enterprise deals");

        group.MapGet("/health", (IDealCopilotEngine engine, string? tenantId) =>
        {
            var targetTenant = tenantId ?? "tenant_default";
            var score = engine.CalculateAggregatePipelineHealth(targetTenant);
            return Results.Ok(new { tenantId = targetTenant, aggregateHealthScore = score, status = "Monitored" });
        })
        .WithName("GetPipelineHealthScore")
        .WithSummary("Calculate aggregate probability-weighted pipeline health score");

        return app;
    }
}
