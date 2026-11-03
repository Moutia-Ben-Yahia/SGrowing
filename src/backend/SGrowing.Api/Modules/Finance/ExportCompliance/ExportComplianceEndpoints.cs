namespace SGrowing.Api.Modules.Finance.ExportCompliance;

public static class ExportComplianceEndpoints
{
    public static IServiceCollection AddExportComplianceServices(this IServiceCollection services)
    {
        services.AddSingleton<ExportComplianceLedgerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapExportComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/export-compliance")
            .WithTags("Finance - Sovereign Export Control & EAR/ITAR Compliance Ledger");

        group.MapGet("/overview", (ExportComplianceLedgerEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/screen-transaction", (ExportComplianceLedgerEngine engine, ScreenTransactionRequest request) =>
        {
            var record = engine.ScreenTransaction(request);
            return Results.Created($"/api/v1/finance/export-compliance/screenings/{record.ScreeningId}", record);
        });

        group.MapPost("/issue-clearance", (ExportComplianceLedgerEngine engine, IssueClearanceRequest request) =>
        {
            var token = engine.IssueClearance(request);
            return Results.Created($"/api/v1/finance/export-compliance/tokens/{token.TokenId}", token);
        });

        return app;
    }
}
