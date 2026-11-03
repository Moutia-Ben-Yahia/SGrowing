namespace SGrowing.Api.Modules.Finance.TaxExemption;

public static class TaxExemptionEndpoints
{
    public static IServiceCollection AddTaxExemptionServices(this IServiceCollection services)
    {
        services.AddSingleton<TaxExemptionLedgerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTaxExemptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/tax-exemption")
            .WithTags("Finance - Sovereign Indirect Tax Exemption & Reverse Charge VAT");

        group.MapGet("/overview", (TaxExemptionLedgerEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/validate", (TaxExemptionLedgerEngine engine, ValidateCertificateRequest request) =>
        {
            var cert = engine.ValidateCertificate(request);
            return Results.Created($"/api/v1/finance/tax-exemption/certificates/{cert.CertificateId}", cert);
        });

        group.MapPost("/clearance/issue", (TaxExemptionLedgerEngine engine, IssueVatClearanceRequest request) =>
        {
            var clearance = engine.IssueClearance(request);
            return Results.Created($"/api/v1/finance/tax-exemption/clearances/{clearance.InvoiceClearanceId}", clearance);
        });

        return app;
    }
}
