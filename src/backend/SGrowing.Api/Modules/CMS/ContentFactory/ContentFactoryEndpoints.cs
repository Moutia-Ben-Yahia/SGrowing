namespace SGrowing.Api.Modules.CMS.ContentFactory;

public static class ContentFactoryEndpoints
{
    public static IServiceCollection AddContentFactoryServices(this IServiceCollection services)
    {
        services.AddSingleton<IContentFactoryEngine, ContentFactoryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapContentFactoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/cms")
            .WithTags("AI Multilingual Headless CMS & Content Factory");

        group.MapGet("/articles", (IContentFactoryEngine engine) =>
        {
            var articles = engine.ListArticles();
            return Results.Ok(articles);
        })
        .WithName("ListCmsArticles")
        .WithSummary("Retrieve all dynamic headless CMS articles with modular content blocks");

        group.MapGet("/articles/{id}", (IContentFactoryEngine engine, string id) =>
        {
            var article = engine.GetArticle(id);
            return article is not null ? Results.Ok(article) : Results.NotFound();
        })
        .WithName("GetCmsArticleById")
        .WithSummary("Get a specific CMS article by ID");

        group.MapPost("/articles", (IContentFactoryEngine engine, CreateArticleRequest request) =>
        {
            var article = engine.CreateOrUpdateArticle(request);
            return Results.Created($"/api/v1/cms/articles/{article.ArticleId}", article);
        })
        .WithName("CreateCmsArticle")
        .WithSummary("Create or publish a new modular content article with AI brand voice audit");

        group.MapPost("/audit-voice", (IContentFactoryEngine engine, EvaluateVoiceRequest request) =>
        {
            var evaluation = engine.EvaluateBrandVoice(request);
            return Results.Ok(evaluation);
        })
        .WithName("AuditBrandVoice")
        .WithSummary("Audit copy against enterprise brand voice policies and readability thresholds");

        group.MapPost("/localize", (IContentFactoryEngine engine, LocalizeRequest request) =>
        {
            var job = engine.TriggerLocalization(request.ArticleId, request.TargetLocale);
            return Results.Ok(job);
        })
        .WithName("LocalizeArticle")
        .WithSummary("Trigger automated AI localization and layout adaptation for a target locale");

        group.MapPost("/dispatch-edge", (IContentFactoryEngine engine, DispatchWebhookRequest request) =>
        {
            var result = engine.DispatchEdgeWebhook(request.ArticleId, request.TargetUrl);
            return Results.Ok(result);
        })
        .WithName("DispatchEdgeWebhook")
        .WithSummary("Dispatch instant edge publication webhook to Next.js or Astro SSG deployments");

        return app;
    }
}

public record LocalizeRequest(string ArticleId, string TargetLocale);
public record DispatchWebhookRequest(string ArticleId, string TargetUrl);
