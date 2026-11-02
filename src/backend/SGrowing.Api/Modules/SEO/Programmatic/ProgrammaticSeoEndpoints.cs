namespace SGrowing.Api.Modules.SEO.Programmatic;

public static class ProgrammaticMeshEndpoints
{
    public static IServiceCollection AddProgrammaticMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<IProgrammaticSeoEngine, ProgrammaticSeoEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapProgrammaticMeshEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/seo/programmatic")
            .WithTags("Programmatic SEO & Backlink Authority Mesh");

        group.MapGet("/pages", (IProgrammaticSeoEngine engine) =>
        {
            var pages = engine.ListPages();
            return Results.Ok(pages);
        })
        .WithName("ListProgrammaticPages")
        .WithSummary("List all dynamically generated industry and intent landing pages");

        group.MapPost("/generate", (IProgrammaticSeoEngine engine, BatchGeneratePagesRequest request) =>
        {
            var created = engine.GeneratePages(request);
            return Results.Ok(created);
        })
        .WithName("BatchGenerateProgrammaticPages")
        .WithSummary("Generate hundreds of high-quality landing pages at scale with structured schema and anti-thin safeguards");

        group.MapGet("/backlinks", (IProgrammaticSeoEngine engine) =>
        {
            var backlinks = engine.ListBacklinkOpportunities();
            return Results.Ok(backlinks);
        })
        .WithName("ListBacklinkOpportunities")
        .WithSummary("Audit high-authority competitor backlink gaps and pitch pipelines");

        group.MapPost("/indexnow", (IProgrammaticSeoEngine engine, List<string> urls) =>
        {
            var result = engine.SubmitToIndexNow(urls);
            return Results.Ok(result);
        })
        .WithName("SubmitIndexNowBatch")
        .WithSummary("Instantly broadcast newly generated URLs to search engines via the IndexNow protocol");

        return app;
    }
}
