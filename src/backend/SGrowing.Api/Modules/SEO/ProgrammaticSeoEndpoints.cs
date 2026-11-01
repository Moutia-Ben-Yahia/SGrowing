using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.SEO;

public record GenerateSchemaJsonLdRequest(
    string SchemaType,
    LocalBusinessSchemaRequest? LocalBusiness = null,
    FaqSchemaRequest? Faq = null,
    ProductSchemaRequest? Product = null,
    BreadcrumbSchemaRequest? Breadcrumbs = null
);

public static class ProgrammaticSeoEndpoints
{
    public static IEndpointRouteBuilder MapProgrammaticSeoEndpoints(this IEndpointRouteBuilder app)
    {
        var pseoGroup = app.MapGroup("/api/v1/seo/pseo")
            .WithTags("Programmatic SEO Factory");

        pseoGroup.MapPost("/generate-batch", (
            [FromBody] PSeoBatchRequest request,
            IProgrammaticSeoGeneratorService service) =>
        {
            var result = service.GenerateBatch(request);
            return Results.Ok(result);
        });

        pseoGroup.MapPost("/schema-jsonld", (
            [FromBody] GenerateSchemaJsonLdRequest request,
            ISchemaOrgJsonLdGeneratorService service) =>
        {
            var normalizedType = request.SchemaType.Trim();
            string jsonLd;

            if (normalizedType.Equals("LocalBusiness", StringComparison.OrdinalIgnoreCase) ||
                normalizedType.EndsWith("Business", StringComparison.OrdinalIgnoreCase) ||
                normalizedType.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
            {
                if (request.LocalBusiness is null)
                {
                    return Results.BadRequest(new { error = "LocalBusiness payload is required for LocalBusiness schema." });
                }
                jsonLd = service.GenerateLocalBusinessSchema(request.LocalBusiness);
            }
            else if (normalizedType.StartsWith("FAQ", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Faq is null)
                {
                    return Results.BadRequest(new { error = "Faq payload is required for FAQPage schema." });
                }
                jsonLd = service.GenerateFaqSchema(request.Faq);
            }
            else if (normalizedType.Equals("Product", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Product is null)
                {
                    return Results.BadRequest(new { error = "Product payload is required for Product schema." });
                }
                jsonLd = service.GenerateProductSchema(request.Product);
            }
            else if (normalizedType.StartsWith("Breadcrumb", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Breadcrumbs is null)
                {
                    return Results.BadRequest(new { error = "Breadcrumbs payload is required for BreadcrumbList schema." });
                }
                jsonLd = service.GenerateBreadcrumbSchema(request.Breadcrumbs);
            }
            else
            {
                return Results.BadRequest(new { error = $"Unsupported schema type: '{request.SchemaType}'. Expected LocalBusiness, FAQ, Product, or BreadcrumbList." });
            }

            var validation = service.ValidateJsonLd(jsonLd, normalizedType);
            return Results.Ok(new
            {
                schemaType = normalizedType,
                jsonLd,
                validation
            });
        });

        var serpGroup = app.MapGroup("/api/v1/seo/serp")
            .WithTags("SERP Rank Tracking & Volatility");

        serpGroup.MapPost("/track-keyword", async (
            [FromBody] TrackKeywordRankCommand command,
            ISerpPositionTrackerService service,
            CancellationToken ct) =>
        {
            var result = await service.TrackKeywordAsync(command, ct);
            return Results.Ok(result);
        });

        serpGroup.MapGet("/history/{keywordId:guid}", async (
            Guid keywordId,
            [FromQuery] Guid tenantId,
            [FromQuery] int? days,
            ISerpPositionTrackerService service,
            CancellationToken ct) =>
        {
            var history = await service.GetKeywordHistoryAsync(tenantId, keywordId, days ?? 30, ct);
            return Results.Ok(history);
        });

        serpGroup.MapGet("/volatility-report", async (
            [FromQuery] Guid tenantId,
            [FromQuery] int? windowDays,
            ISerpPositionTrackerService service,
            CancellationToken ct) =>
        {
            var report = await service.GetVolatilityReportAsync(tenantId, windowDays ?? 14, ct);
            return Results.Ok(report);
        });

        return app;
    }
}
