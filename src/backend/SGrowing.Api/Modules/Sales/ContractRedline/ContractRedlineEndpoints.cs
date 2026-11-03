namespace SGrowing.Api.Modules.Sales.ContractRedline;

public static class ContractRedlineEndpoints
{
    public static IServiceCollection AddContractRedlineServices(this IServiceCollection services)
    {
        services.AddSingleton<ContractRedlineEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapContractRedlineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/contract-redline")
            .WithTags("Sales - B2B Contract Redline Auto-Negotiator");

        group.MapGet("/overview", (ContractRedlineEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/negotiate-clause", (ContractRedlineEngine engine, NegotiateClauseRequest request) =>
        {
            var record = engine.NegotiateClause(request);
            return Results.Created($"/api/v1/sales/contract-redline/clauses/{record.ClauseId}", record);
        });

        group.MapPost("/approve-deviation", (ContractRedlineEngine engine, ApproveDeviationRequest request) =>
        {
            var approval = engine.ApproveDeviation(request);
            return Results.Created($"/api/v1/sales/contract-redline/approvals/{approval.ApprovalId}", approval);
        });

        return app;
    }
}
