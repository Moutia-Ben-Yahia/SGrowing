namespace SGrowing.Api.Modules.Finance.SovereignDst;

public static class SovereignDstEndpoints
{
    public static IServiceCollection AddSovereignDstServices(this IServiceCollection services)
    {
        services.AddSingleton<SovereignDstLedgerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSovereignDstEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/sovereign-dst")
            .WithTags("Finance - Sovereign Digital Services Tax & OECD Pillar 2 Ledger");

        group.MapGet("/overview", (SovereignDstLedgerEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/compute-dst", (SovereignDstLedgerEngine engine, ComputeDstRequest request) =>
        {
            var record = engine.ComputeDst(request);
            return Results.Created($"/api/v1/finance/sovereign-dst/records/{record.DstRecordId}", record);
        });

        group.MapPost("/compute-globe-tax", (SovereignDstLedgerEngine engine, ComputeGlobeTaxRequest request) =>
        {
            var assessment = engine.ComputeGlobeTax(request);
            return Results.Created($"/api/v1/finance/sovereign-dst/assessments/{assessment.AssessmentId}", assessment);
        });

        return app;
    }
}
