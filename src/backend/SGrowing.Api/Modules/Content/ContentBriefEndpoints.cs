using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Content;

public class ContentBriefEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/content/briefs")
            .WithTags("Content Marketing & Brief Generator");

        group.MapPost("/generate", ([FromBody] ContentBriefRequest request, IContentBriefGeneratorService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.PrimaryKeyword))
            {
                return Results.BadRequest(new { error = "PrimaryKeyword is required." });
            }

            var brief = service.GenerateBrief(request);
            return Results.Ok(brief);
        })
        .WithName("GenerateContentBrief")
        .WithSummary("Generates an SEO and GEO-optimized content brief with keyword clusters, outline, and benchmarks.");

        group.MapPost("/grade-draft", ([FromBody] DraftGradeRequest request, IContentBriefGeneratorService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.PrimaryKeyword) || string.IsNullOrWhiteSpace(request.DraftBody))
            {
                return Results.BadRequest(new { error = "PrimaryKeyword and DraftBody are required." });
            }

            var report = service.GradeDraft(request);
            return Results.Ok(report);
        })
        .WithName("GradeContentDraft")
        .WithSummary("Grades an editorial draft against SEO keyword density, outline coverage, Flesch reading ease, and search intent.");
    }
}
