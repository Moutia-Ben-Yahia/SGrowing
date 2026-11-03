namespace SGrowing.Api.Modules.Finance.IntercompanyNetting;

public static class IntercompanyNettingEndpoints
{
    public static IServiceCollection AddIntercompanyNettingServices(this IServiceCollection services)
    {
        services.AddSingleton<IIntercompanyNettingEngine, IntercompanyNettingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapIntercompanyNettingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/intercompany-netting")
            .WithTags("Finance - Intercompany Trade & Multilateral Netting");

        group.MapGet("/overview", (IIntercompanyNettingEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/add-invoice", (AddIntercompanyInvoiceRequest req, IIntercompanyNettingEngine engine) =>
        {
            var inv = engine.AddInvoice(req.DebtorSubId, req.DebtorName, req.CreditorSubId, req.CreditorName, req.AmountUsd, req.Currency, req.Category);
            return Results.Ok(inv);
        });

        group.MapPost("/execute-netting", (ExecuteNettingRequest req, IIntercompanyNettingEngine engine) =>
        {
            var receipt = engine.ExecuteMultilateralNettingCycle(req.CycleNumber);
            return Results.Ok(receipt);
        });

        return endpoints;
    }
}

public sealed record AddIntercompanyInvoiceRequest(string DebtorSubId, string DebtorName, string CreditorSubId, string CreditorName, double AmountUsd, string Currency, string Category);
public sealed record ExecuteNettingRequest(int CycleNumber);
