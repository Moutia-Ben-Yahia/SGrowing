namespace SGrowing.Api.Modules.Finance.TaxRdCredit;

public static class TaxRdCreditEndpoints
{
    public static IServiceCollection AddTaxRdCreditServices(this IServiceCollection services)
    {
        services.AddSingleton<TaxRdCreditLedgerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTaxRdCreditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/tax-rd-credit")
            .WithTags("Finance - Sovereign Corporate R&D Tax Credit Ledger (Form 6765)");

        group.MapGet("/overview", (TaxRdCreditLedgerEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/qualify-project", (TaxRdCreditLedgerEngine engine, QualifyProjectRequest request) =>
        {
            var project = engine.QualifyProject(request);
            return Results.Created($"/api/v1/finance/tax-rd-credit/projects/{project.ProjectId}", project);
        });

        group.MapPost("/compute-credit", (TaxRdCreditLedgerEngine engine, ComputeCreditRequest request) =>
        {
            var claim = engine.ComputeCredit(request);
            return Results.Created($"/api/v1/finance/tax-rd-credit/claims/{claim.ClaimId}", claim);
        });

        return app;
    }
}
