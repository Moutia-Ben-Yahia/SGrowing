namespace SGrowing.Api.Modules.Sales.DealAutopsy;

public static class DealAutopsyEndpoints
{
    public static IServiceCollection AddDealAutopsyServices(this IServiceCollection services)
    {
        services.AddSingleton<IDealAutopsyEngine, DealAutopsyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDealAutopsyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sales/deal-autopsy")
            .WithTags("Sales - Win-Rate Deal Autopsy & Loss Forensics");

        group.MapGet("/overview", (IDealAutopsyEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/analyze-lost-deal", (AnalyzeLostDealRequest req, IDealAutopsyEngine engine) =>
        {
            var record = engine.AnalyzeLostDeal(req.OpportunityId, req.AccountName, req.DealValue, req.SalesRepName, req.NotesOrTranscripts);
            return Results.Ok(record);
        });

        group.MapPost("/generate-playbook", (GeneratePlaybookRequest req, IDealAutopsyEngine engine) =>
        {
            var playbook = engine.GenerateRecoveryPlaybook(req.OpportunityId, req.Category);
            return Results.Ok(playbook);
        });

        return endpoints;
    }
}

public sealed record AnalyzeLostDealRequest(string OpportunityId, string AccountName, double DealValue, string SalesRepName, string NotesOrTranscripts);
public sealed record GeneratePlaybookRequest(string OpportunityId, LossReasonCategory Category);
