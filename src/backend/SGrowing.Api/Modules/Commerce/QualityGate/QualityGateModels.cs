namespace SGrowing.Api.Modules.Commerce.QualityGate;

/// <summary>
/// Classified types of physical packaging defects detected by autonomous multimodal vision models.
/// </summary>
public enum PackagingDefectType
{
    CrushedBoxCorner,
    TornCarton,
    PunctureHole,
    BrokenSecurityTape,
    LiquidSpillStain,
    IllegibleBarcode
}

/// <summary>
/// Severity grade of a detected packaging anomaly.
/// </summary>
public enum DefectSeverity
{
    Minor,
    Moderate,
    SevereCritical
}

/// <summary>
/// Automated quality gate routing disposition for incoming warehouse consignments.
/// </summary>
public enum InspectionDisposition
{
    AcceptedDirectToStock,
    QuarantinePendingReview,
    RejectedReturnToVendor
}

/// <summary>
/// Verification status resulting from optical diffraction and microtext matrix analysis.
/// </summary>
public enum HologramVerificationStatus
{
    AuthenticVerified,
    CounterfeitSuspect,
    TamperedDefaced,
    Indeterminate
}

/// <summary>
/// Normalized 2D bounding box coordinates on the packaging inspection image plane.
/// </summary>
public sealed record BoundingBoxCoordinates(double X, double Y, double Width, double Height);

/// <summary>
/// Specific packaging anomaly detected by the multimodal vision inspection pipeline.
/// </summary>
public sealed record PackagingAnomalyFinding(
    PackagingDefectType DefectType,
    DefectSeverity Severity,
    BoundingBoxCoordinates BoundingBox,
    double ConfidenceScore,
    string DefectDescription);

/// <summary>
/// Consolidated packaging inspection result for a warehouse consignment waybill.
/// </summary>
public sealed record PackagingInspectionResult(
    string InspectionId,
    string ConsignmentWaybill,
    int TotalDefectsFound,
    double IntegrityScore,
    InspectionDisposition Disposition,
    PackagingAnomalyFinding[] Findings,
    DateTime InspectedAtUtc);

/// <summary>
/// Optical verification finding for product security holograms and microtext matrices.
/// </summary>
public sealed record HologramVerificationFinding(
    string SerialMatrixCode,
    double DiffractionScore,
    bool MicrotextHashMatch,
    HologramVerificationStatus Status,
    string[] SecurityFeaturesDetected);

/// <summary>
/// Cryptographically signed Goods Receipt Note (GRN) discrepancy debit memo issued to suppliers.
/// </summary>
public sealed record GrnDiscrepancyDebitMemo(
    string MemoId,
    string PurchaseOrderId,
    string SupplierId,
    int DefectiveItemsCount,
    decimal TotalDebitAmountDzd,
    string QuarantineBinLocation,
    DateTime GeneratedAtUtc,
    string CryptographicAuditSignature);

/// <summary>
/// Request payload for packaging visual inspection.
/// </summary>
public sealed record InspectPackagingRequest(
    string ConsignmentWaybill,
    string? ImageBase64 = null,
    byte[]? ImageBytes = null);

/// <summary>
/// Request payload for security hologram and microtext verification.
/// </summary>
public sealed record VerifyHologramRequest(
    string SerialMatrixCode,
    string ExpectedMicrotext,
    string? HologramScanBase64 = null,
    byte[]? ScanBytes = null);

/// <summary>
/// Request payload to generate a GRN discrepancy debit memo for defective consignments.
/// </summary>
public sealed record GenerateDiscrepancyMemoRequest(
    string PurchaseOrderId,
    string SupplierId,
    string ConsignmentWaybill,
    decimal UnitPriceDzd);

/// <summary>
/// Aggregated warehouse quality gate metrics response.
/// </summary>
public sealed record QualityGateSummaryResponse(
    int TotalInspections,
    int TotalAccepted,
    int TotalQuarantined,
    int TotalRejected,
    decimal TotalDebitMemosIssuedDzd,
    double AverageIntegrityScore);

/// <summary>
/// Mathematical scoring constants and threshold definitions for packaging integrity.
/// </summary>
public static class QualityGateConstants
{
    public const double MinorSeverityWeight = 5.0;
    public const double ModerateSeverityWeight = 15.0;
    public const double SevereCriticalSeverityWeight = 35.0;

    public const double AcceptedScoreThreshold = 85.0;
    public const double QuarantineScoreThreshold = 60.0;

    public static double GetSeverityWeight(DefectSeverity severity) => severity switch
    {
        DefectSeverity.Minor => MinorSeverityWeight,
        DefectSeverity.Moderate => ModerateSeverityWeight,
        DefectSeverity.SevereCritical => SevereCriticalSeverityWeight,
        _ => MinorSeverityWeight
    };
}
