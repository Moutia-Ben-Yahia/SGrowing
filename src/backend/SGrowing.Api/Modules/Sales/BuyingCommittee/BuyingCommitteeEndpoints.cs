namespace SGrowing.Api.Modules.Sales.BuyingCommittee;

public static class BuyingCommitteeGraphEndpoints
{
    public static IServiceCollection AddBuyingCommitteeGraphServices(this IServiceCollection services)
    {
        services.AddSingleton<BuyingCommitteeGraphEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBuyingCommitteeGraphEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/buying-committee")
            .WithTags("Sales - Buying Committee Influence Graph");

        group.MapGet("/overview", (BuyingCommitteeGraphEngine engine, string? accountId) =>
        {
            var overview = engine.GetOverview(accountId ?? "ACC-NORTHSTAR");
            return Results.Ok(overview);
        });

        group.MapPost("/stakeholder", (BuyingCommitteeGraphEngine engine, AddStakeholderRequest request) =>
        {
            var stakeholder = engine.AddStakeholder(request);
            return Results.Created($"/api/v1/sales/buying-committee/stakeholder/{stakeholder.StakeholderId}", stakeholder);
        });

        group.MapPost("/intervention", (BuyingCommitteeGraphEngine engine, TriggerInterventionRequest request) =>
        {
            var intervention = engine.TriggerIntervention(request);
            return Results.Created($"/api/v1/sales/buying-committee/intervention/{intervention.InterventionId}", intervention);
        });

        return app;
    }
}
