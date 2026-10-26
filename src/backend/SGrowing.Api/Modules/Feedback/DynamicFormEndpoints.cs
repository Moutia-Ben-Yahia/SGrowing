using Microsoft.AspNetCore.Mvc;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Feedback;

public record CreateFormDefinitionRequest(
    Guid TenantId,
    string Title,
    string Slug,
    string Description,
    List<DynamicFormField> Fields,
    string SubmitAction = "ThankYou",
    string ActionPayload = "Thank you!"
);

public record SubmitAnswersRequest(
    Guid TenantId,
    Guid FormId,
    Dictionary<string, string> Answers,
    Guid? RespondentContactId = null,
    int CompletedInSeconds = 30
);

public record CreateDocumentRequest(
    Guid TenantId,
    string Title,
    string Content,
    string SignaturePlacementJson = "[]"
);

public record SealDocumentRequest(
    Guid TenantId,
    Guid DocumentId,
    string SignerEmail,
    string SignatureCoordinates
);

public static class DynamicFormEndpoints
{
    public static IEndpointRouteBuilder MapDynamicFormEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/forms")
            .WithTags("Dynamic Forms & Survey Engine");

        // 1. Form Definitions
        group.MapPost("/", async ([FromBody] CreateFormDefinitionRequest req, IDynamicFormService service) =>
        {
            var form = await service.CreateFormDefinitionAsync(
                req.TenantId, req.Title, req.Slug, req.Description, req.Fields, req.SubmitAction, req.ActionPayload);
            return Results.Ok(form);
        });

        group.MapGet("/", async ([FromQuery] Guid tenantId, IDynamicFormService service) =>
        {
            var forms = await service.GetFormsAsync(tenantId);
            return Results.Ok(forms);
        });

        group.MapGet("/{slug}/public", async (string slug, [FromQuery] Guid tenantId, IDynamicFormService service) =>
        {
            var form = await service.GetFormBySlugAsync(tenantId, slug);
            return form != null ? Results.Ok(form) : Results.NotFound(new { error = "Form not found or unpublished." });
        });

        // 2. Submissions
        group.MapPost("/{slug}/submit", async (string slug, [FromBody] SubmitAnswersRequest req, HttpContext ctx, IDynamicFormService service) =>
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var submission = await service.SubmitFormAnswersAsync(req.TenantId, req.FormId, req.Answers, req.RespondentContactId, ip, req.CompletedInSeconds);
            return Results.Ok(submission);
        });

        group.MapGet("/{id}/submissions", async (Guid id, [FromQuery] Guid tenantId, IDynamicFormService service) =>
        {
            var submissions = await service.GetSubmissionsAsync(tenantId, id);
            return Results.Ok(submissions);
        });

        // 3. Cryptographic PDF Seal Registry
        var docGroup = routes.MapGroup("/api/documents")
            .WithTags("Cryptographic PDF Signatures");

        docGroup.MapPost("/create", async ([FromBody] CreateDocumentRequest req, IDynamicFormService service) =>
        {
            var doc = await service.CreateDocumentRecordAsync(req.TenantId, req.Title, req.Content, req.SignaturePlacementJson);
            return Results.Ok(doc);
        });

        docGroup.MapPost("/seal", async ([FromBody] SealDocumentRequest req, IDynamicFormService service) =>
        {
            var sealedDoc = await service.SealDocumentCryptographicallyAsync(req.TenantId, req.DocumentId, req.SignerEmail, req.SignatureCoordinates);
            return Results.Ok(sealedDoc);
        });

        docGroup.MapGet("/{id}/verify-audit-chain", async (Guid id, [FromQuery] Guid tenantId, IDynamicFormService service) =>
        {
            var (isTamperFree, summary) = await service.VerifyDocumentAuditChainAsync(tenantId, id);
            return Results.Ok(new { isTamperFree, summary });
        });

        return routes;
    }
}
