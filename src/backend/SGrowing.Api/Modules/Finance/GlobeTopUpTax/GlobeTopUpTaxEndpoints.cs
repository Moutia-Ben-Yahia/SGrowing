using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Finance.GlobeTopUpTax;

public static class GlobeTopUpTaxEndpoints
{
    public static IServiceCollection AddGlobeTopUpTaxServices(this IServiceCollection services)
    {
        services.AddSingleton<IGlobeTopUpTaxEngine, GlobeTopUpTaxEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapGlobeTopUpTaxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/globe-tax")
            .WithTags("OECD Pillar Two GloBE Top-Up Tax Ledger");

        group.MapPost("/assess-jurisdiction", (
            [FromBody] AssessJurisdictionTaxRequest request,
            [FromServices] IGlobeTopUpTaxEngine engine) =>
        {
            var profile = engine.AssessJurisdiction(request);
            return Results.Created($"/api/v1/finance/globe-tax/jurisdictions/{profile.JurisdictionCode}", profile);
        })
        .WithName("AssessGlobeJurisdiction")
        .WithSummary("Registers or updates jurisdictional GloBE income, covered taxes, and payroll data");

        group.MapGet("/jurisdictions", (
            [FromServices] IGlobeTopUpTaxEngine engine) =>
        {
            var jurisdictions = engine.GetJurisdictions();
            return Results.Ok(jurisdictions);
        })
        .WithName("GetGlobeJurisdictions")
        .WithSummary("Lists all sovereign tax jurisdictions evaluated under Pillar Two rules");

        group.MapGet("/safe-harbor/{jurisdictionCode}", (
            string jurisdictionCode,
            [FromServices] IGlobeTopUpTaxEngine engine) =>
        {
            try
            {
                var evaluation = engine.EvaluateSafeHarbor(jurisdictionCode);
                return Results.Ok(evaluation);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("EvaluateGlobeSafeHarbor")
        .WithSummary("Assesses Transitional CbCR Safe Harbor tests (De Minimis, Simplified ETR, Routine Profits)");

        group.MapPost("/calculate-top-up/{jurisdictionCode}", (
            string jurisdictionCode,
            [FromServices] IGlobeTopUpTaxEngine engine) =>
        {
            try
            {
                var calc = engine.CalculateTopUpTax(jurisdictionCode);
                return Results.Ok(calc);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("CalculateGlobeTopUpTax")
        .WithSummary("Calculates top-up tax rate, SBIE carveout, and QDMTT/IIR split for a jurisdiction");

        group.MapGet("/summary", (
            [FromServices] IGlobeTopUpTaxEngine engine) =>
        {
            var summary = engine.GetLedgerSummary();
            return Results.Ok(summary);
        })
        .WithName("GetGlobeTaxLedgerSummary")
        .WithSummary("Retrieves global consolidated Top-Up Tax liabilities and safe-harbor coverage status");

        return app;
    }
}
