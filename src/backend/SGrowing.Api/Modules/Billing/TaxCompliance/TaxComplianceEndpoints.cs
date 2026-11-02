namespace SGrowing.Api.Modules.Billing.TaxCompliance;

public static class TaxComplianceEndpoints
{
    public static IServiceCollection AddTaxComplianceServices(this IServiceCollection services)
    {
        services.AddSingleton<ITaxComplianceEngine, TaxComplianceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTaxComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/billing/tax-compliance")
            .WithTags("Cross-Border FX & Tax Compliance Engine");

        group.MapPost("/calculate", (ITaxComplianceEngine engine, TaxCalculationRequest request) =>
        {
            var result = engine.CalculateTax(request);
            return Results.Ok(result);
        })
        .WithName("CalculateTransactionTax")
        .WithSummary("Compute real-time foreign exchange conversion, EU VAT reverse charge, and local tax rates");

        group.MapGet("/fx-rates", (ITaxComplianceEngine engine) =>
        {
            var rates = engine.GetLiveFxRates();
            return Results.Ok(rates);
        })
        .WithName("GetLiveFxRates")
        .WithSummary("Query live hedged settlement currency exchange rates with slippage protection");

        group.MapGet("/nexus", (ITaxComplianceEngine engine) =>
        {
            var nexusList = engine.ListNexusJurisdictions();
            return Results.Ok(nexusList);
        })
        .WithName("ListNexusJurisdictions")
        .WithSummary("Track state-by-state economic nexus thresholds and EU VAT OSS compliance status");

        group.MapGet("/validate-vat", (ITaxComplianceEngine engine, string vatNumber, string countryCode) =>
        {
            var isValid = engine.ValidateVatNumber(vatNumber, countryCode);
            return Results.Ok(new { vatNumber, countryCode, isValid });
        })
        .WithName("ValidateVatNumber")
        .WithSummary("Verify cross-border European VIES corporate VAT ID syntax and checksum");

        return app;
    }
}
