using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Documents.Idp;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 24:
/// Intelligent Document Processing (IDP), Multi-Modal OCR &amp; 3-Way Invoice Reconciliation.
/// </summary>
public static class IdpEndpoints
{
    /// <summary>
    /// Registers all Phase 24 IDP, multi-modal OCR parser, 3-way reconciliation,
    /// and physical delivery slip verification services in the DI container.
    /// </summary>
    public static IServiceCollection AddIdpServices(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentIntelligenceParserService, DocumentIntelligenceParserService>();
        services.AddSingleton<IThreeWayReconciliationService, ThreeWayReconciliationService>();
        services.AddSingleton<IPhysicalPodVerificationService, PhysicalPodVerificationService>();

        return services;
    }

    /// <summary>
    /// Maps all Phase 24 IDP endpoints under the /api/v1/documents/idp route prefix.
    /// </summary>
    public static IEndpointRouteBuilder MapIdpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents/idp")
            .WithTags("Intelligent Document Processing & 3-Way Invoice Reconciliation");

        // 1. POST /api/v1/documents/idp/parse
        group.MapPost("/parse", async (
            [FromBody] ParseDocumentRequest request,
            [FromServices] IDocumentIntelligenceParserService parser,
            CancellationToken ct) =>
        {
            var response = await parser.ParseInvoiceDocumentAsync(request, ct);
            return response.Success ? Results.Ok(response) : Results.BadRequest(response);
        })
        .WithName("ParseDocumentOcr")
        .WithSummary("Ingests raw document bytes or OCR coordinate bounding boxes and extracts structured invoice data.");

        // 2. POST /api/v1/documents/idp/reconcile/3way
        group.MapPost("/reconcile/3way", async (
            [FromBody] ThreeWayMatchRequest request,
            [FromServices] IThreeWayReconciliationService reconciliationService,
            CancellationToken ct) =>
        {
            var response = await reconciliationService.ReconcileAsync(request, ct);
            return response.Success ? Results.Ok(response) : Results.UnprocessableEntity(response);
        })
        .WithName("ReconcileThreeWayInvoice")
        .WithSummary("Performs 3-way match across PO, GRN, and Supplier Invoice with automated tolerance enforcement.");

        // 3. POST /api/v1/documents/idp/verify/paper-pod
        group.MapPost("/verify/paper-pod", async (
            [FromBody] VerifyPaperPodRequest request,
            [FromServices] IPhysicalPodVerificationService podVerifier,
            CancellationToken ct) =>
        {
            var response = await podVerifier.VerifyPaperPodAsync(request, ct);
            return response.Success ? Results.Ok(response) : Results.BadRequest(response);
        })
        .WithName("VerifyPaperPodReceipt")
        .WithSummary("Verifies physical paper Proof of Delivery slip for handwritten recipient signature and official corporate stamp.");

        // 4. GET /api/v1/documents/idp/health
        group.MapGet("/health", () =>
        {
            var health = new IdpHealthResponse(
                Status: "Healthy",
                Version: "10.0.0-phase24",
                Capabilities:
                [
                    "MultiModalOcrIngestion",
                    "SpatialTokenClustering",
                    "NifRcTaxIdExtraction",
                    "TabularGridRecognition",
                    "ThreeWayInvoiceReconciliation",
                    "DuplicateInvoicePrevention",
                    "BankRibScreening",
                    "PhysicalPaperPodVerification"
                ],
                TimestampUtc: DateTime.UtcNow);

            return Results.Ok(health);
        })
        .WithName("GetIdpEngineHealth")
        .WithSummary("Retrieves the operational health and active capabilities of the IDP Engine.");

        return app;
    }
}
