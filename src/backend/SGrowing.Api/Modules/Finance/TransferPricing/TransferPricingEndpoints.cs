using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.TransferPricing;

public static class TransferPricingEndpoints
{
    public static IServiceCollection AddTransferPricingServices(this IServiceCollection services)
    {
        services.AddSingleton<ITransferPricingEngine, TransferPricingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTransferPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/transfer-pricing")
            .WithTags("Finance - Transfer Pricing & Intercompany Ledger");

        group.MapGet("/overview", (ITransferPricingEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetTransferPricingOverview")
        .WithSummary("Lists multi-entity legal structure, BEPS compliance margins, and intercompany settlement balances");

        group.MapPost("/calculate-markup", (CalculateMarkupCommand command, ITransferPricingEngine engine) =>
        {
            var result = engine.CalculateMarkup(command);
            return Results.Ok(result);
        })
        .WithName("CalculateTransferPricingMarkup")
        .WithSummary("Computes OECD arm's length markup rates based on transaction type and direct costs");

        group.MapPost("/generate-invoices", (GenerateInvoicesCommand command, ITransferPricingEngine engine) =>
        {
            var result = engine.GenerateInvoices(command);
            return Results.Ok(result);
        })
        .WithName("GenerateIntercompanyInvoices")
        .WithSummary("Generates bilateral intercompany tax-compliant invoices with automatic FX netting");

        return app;
    }
}
