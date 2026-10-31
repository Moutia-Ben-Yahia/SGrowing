namespace SGrowing.Api.Modules.Security.SyntheticData;

public static class SyntheticDataEndpoints
{
    public static IEndpointRouteBuilder MapSyntheticDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/security/synthetic-data")
            .WithTags("Enterprise Synthetic Data & Masking");

        group.MapPost("/generate", async (
            GenerateSyntheticDatasetRequest request,
            ISyntheticDataGeneratorService service,
            CancellationToken ct) =>
        {
            var result = await service.GenerateSyntheticDatasetAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("GenerateSyntheticDataset")
        .WithSummary("Generates synthetic realistic test data with differential privacy and format preservation for CI/CD sandboxes.");

        group.MapPost("/inspect-schema", async (
            List<string> columnNames,
            ISyntheticDataGeneratorService service,
            CancellationToken ct) =>
        {
            var rules = await service.InspectAndRecommendRulesAsync(columnNames, ct);
            return Results.Ok(rules);
        })
        .WithName("InspectSchemaColumns")
        .WithSummary("Auto-classifies column names into DirectIdentifier, QuasiIdentifier, and SensitiveAttribute rules.");

        return app;
    }
}
