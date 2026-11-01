using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.QualityGate;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 43:
/// Autonomous Multimodal Vision-Language Inspection &amp; Automated Warehouse Quality Gate.
/// </summary>
public static class QualityGateEndpoints
{
    /// <summary>
    /// Registers warehouse quality gate and multimodal vision inspection services in DI.
    /// </summary>
    public static IServiceCollection AddQualityGateServices(this IServiceCollection services)
    {
        services.AddSingleton<IVisualPackagingAnomalyDetector, VisualPackagingAnomalyDetector>();
        services.AddSingleton<ICounterfeitHologramVerifierService, CounterfeitHologramVerifierService>();
        services.AddSingleton<IAutonomousGrnDiscrepancyRouter, AutonomousGrnDiscrepancyRouter>();

        return services;
    }

    /// <summary>
    /// Maps Quality Gate minimal API routes under /api/v1/commerce/quality-gate.
    /// </summary>
    public static IEndpointRouteBuilder MapQualityGateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/quality-gate")
            .WithTags("Autonomous Multimodal Vision-Language Inspection & Automated Warehouse Quality Gate");

        // 1. POST /api/v1/commerce/quality-gate/inspect/packaging
        group.MapPost("/inspect/packaging", (
            [FromBody] InspectPackagingRequest req,
            [FromServices] IVisualPackagingAnomalyDetector detector) =>
        {
            if (string.IsNullOrWhiteSpace(req.ConsignmentWaybill))
            {
                return Results.BadRequest(new { Success = false, Message = "ConsignmentWaybill is required." });
            }

            byte[]? imageBytes = req.ImageBytes;
            if (imageBytes == null && !string.IsNullOrWhiteSpace(req.ImageBase64))
            {
                try
                {
                    imageBytes = Convert.FromBase64String(req.ImageBase64);
                }
                catch
                {
                    return Results.BadRequest(new { Success = false, Message = "Invalid ImageBase64 format." });
                }
            }

            if (imageBytes == null || imageBytes.Length == 0)
            {
                return Results.BadRequest(new { Success = false, Message = "ImageBytes or valid ImageBase64 payload must be provided." });
            }

            var result = detector.AnalyzePackagingImage(req.ConsignmentWaybill, imageBytes);
            return Results.Ok(result);
        })
        .WithName("InspectPackagingAnomaly")
        .WithSummary("Analyzes physical consignment packaging image frame for anomalies, computes integrity score, and assigns warehouse disposition.");

        // 2. POST /api/v1/commerce/quality-gate/inspect/hologram
        group.MapPost("/inspect/hologram", (
            [FromBody] VerifyHologramRequest req,
            [FromServices] ICounterfeitHologramVerifierService verifier) =>
        {
            if (string.IsNullOrWhiteSpace(req.SerialMatrixCode))
            {
                return Results.BadRequest(new { Success = false, Message = "SerialMatrixCode is required." });
            }

            byte[]? scanBytes = req.ScanBytes;
            if (scanBytes == null && !string.IsNullOrWhiteSpace(req.HologramScanBase64))
            {
                try
                {
                    scanBytes = Convert.FromBase64String(req.HologramScanBase64);
                }
                catch
                {
                    return Results.BadRequest(new { Success = false, Message = "Invalid HologramScanBase64 format." });
                }
            }

            if (scanBytes == null || scanBytes.Length == 0)
            {
                return Results.BadRequest(new { Success = false, Message = "ScanBytes or valid HologramScanBase64 payload must be provided." });
            }

            var finding = verifier.VerifyHologram(
                req.SerialMatrixCode,
                scanBytes,
                req.ExpectedMicrotext ?? string.Empty);

            return Results.Ok(finding);
        })
        .WithName("VerifySecurityHologram")
        .WithSummary("Verifies optical diffraction and microtext matrix to detect counterfeit or cloned security holograms.");

        // 3. POST /api/v1/commerce/quality-gate/grn/discrepancies
        group.MapPost("/grn/discrepancies", (
            [FromBody] GenerateDiscrepancyMemoRequest req,
            [FromServices] IAutonomousGrnDiscrepancyRouter router,
            [FromServices] IVisualPackagingAnomalyDetector detector) =>
        {
            if (string.IsNullOrWhiteSpace(req.PurchaseOrderId))
            {
                return Results.BadRequest(new { Success = false, Message = "PurchaseOrderId is required." });
            }
            if (string.IsNullOrWhiteSpace(req.SupplierId))
            {
                return Results.BadRequest(new { Success = false, Message = "SupplierId is required." });
            }
            if (string.IsNullOrWhiteSpace(req.ConsignmentWaybill))
            {
                return Results.BadRequest(new { Success = false, Message = "ConsignmentWaybill is required." });
            }
            if (req.UnitPriceDzd < 0m)
            {
                return Results.BadRequest(new { Success = false, Message = "UnitPriceDzd cannot be negative." });
            }

            var history = detector.GetInspectionHistory(req.ConsignmentWaybill);
            var inspection = history.Count > 0 ? history[^1] : detector.GetInspectionById(req.ConsignmentWaybill);

            if (inspection == null)
            {
                inspection = new PackagingInspectionResult(
                    InspectionId: $"INSP-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
                    ConsignmentWaybill: req.ConsignmentWaybill,
                    TotalDefectsFound: 1,
                    IntegrityScore: 45.0,
                    Disposition: InspectionDisposition.RejectedReturnToVendor,
                    Findings: [
                        new PackagingAnomalyFinding(
                            PackagingDefectType.TornCarton,
                            DefectSeverity.Moderate,
                            new BoundingBoxCoordinates(0.10, 0.10, 0.50, 0.50),
                            0.90,
                            "Manual defect discrepancy logged for unindexed waybill")
                    ],
                    InspectedAtUtc: DateTime.UtcNow);
            }

            var memo = router.ProcessInspectionDiscrepancy(
                req.PurchaseOrderId,
                req.SupplierId,
                inspection,
                req.UnitPriceDzd);

            return Results.Ok(memo);
        })
        .WithName("GenerateGrnDiscrepancyDebitMemo")
        .WithSummary("Reconciles PO with packaging inspection findings and issues a cryptographically signed debit memo in DZD.");

        // 4. GET /api/v1/commerce/quality-gate/grn/discrepancies/{memoId}
        group.MapGet("/grn/discrepancies/{memoId}", (
            [FromRoute] string memoId,
            [FromServices] IAutonomousGrnDiscrepancyRouter router) =>
        {
            var memo = router.GetDebitMemo(memoId);
            return memo is not null
                ? Results.Ok(memo)
                : Results.NotFound(new { Message = $"Debit memo '{memoId}' was not found." });
        })
        .WithName("GetGrnDebitMemo")
        .WithSummary("Retrieves a generated GRN discrepancy debit memo by ID.");

        // 5. GET /api/v1/commerce/quality-gate/quarantine/items
        group.MapGet("/quarantine/items", (
            [FromServices] IAutonomousGrnDiscrepancyRouter router) =>
        {
            var memos = router.ListQuarantinedMemos();
            return Results.Ok(memos);
        })
        .WithName("ListQuarantinedItems")
        .WithSummary("Lists all active debit memos routing defective inventory into warehouse quarantine zones.");

        // 6. GET /api/v1/commerce/quality-gate/summary
        group.MapGet("/summary", (
            [FromServices] IVisualPackagingAnomalyDetector detector,
            [FromServices] IAutonomousGrnDiscrepancyRouter router) =>
        {
            var inspections = detector.GetAllInspections();
            var quarantinedMemos = router.ListQuarantinedMemos();

            int totalInspections = inspections.Count;
            int totalAccepted = inspections.Count(i => i.Disposition == InspectionDisposition.AcceptedDirectToStock);
            int totalQuarantined = inspections.Count(i => i.Disposition == InspectionDisposition.QuarantinePendingReview);
            int totalRejected = inspections.Count(i => i.Disposition == InspectionDisposition.RejectedReturnToVendor);

            decimal totalDebit = quarantinedMemos.Sum(m => m.TotalDebitAmountDzd);
            double avgScore = totalInspections > 0
                ? Math.Round(inspections.Average(i => i.IntegrityScore), 2)
                : 100.0;

            var summary = new QualityGateSummaryResponse(
                TotalInspections: totalInspections,
                TotalAccepted: totalAccepted,
                TotalQuarantined: totalQuarantined,
                TotalRejected: totalRejected,
                TotalDebitMemosIssuedDzd: totalDebit,
                AverageIntegrityScore: avgScore);

            return Results.Ok(summary);
        })
        .WithName("GetQualityGateSummary")
        .WithSummary("Retrieves high-level summary metrics of all warehouse inspections, dispositions, and discrepancy debit amounts.");

        return app;
    }
}
