using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Documents.Idp;

#region Enums

/// <summary>
/// Supported document taxonomy classifications for intelligent document processing.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IdpDocumentType
{
    SupplierInvoice = 0,
    PurchaseOrder = 1,
    GoodsReceiptNote = 2,
    CustomsWaybill = 3,
    PaperPodReceipt = 4
}

/// <summary>
/// Status outcome of 3-way reconciliation across Purchase Order, GRN, and Supplier Invoice.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThreeWayMatchStatus
{
    FullMatch = 0,
    PriceVarianceDispute = 1,
    QuantityVarianceDispute = 2,
    TaxDiscrepancy = 3,
    UnmatchedPo = 4,
    DuplicateInvoiceBlocked = 5
}

/// <summary>
/// Physical delivery slip recipient signature and corporate stamp verification status.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PodStampStatus
{
    Verified = 0,
    MissingSignature = 1,
    MissingStamp = 2,
    SuspectedForgery = 3
}

#endregion

#region Domain Records

/// <summary>
/// Geometric coordinate bounding box and OCR textual token metadata.
/// </summary>
public record OcrBoundingBox(
    double X,
    double Y,
    double Width,
    double Height,
    string Text,
    double Confidence,
    string? BlockType = null,
    bool IsHandwritten = false,
    bool IsStamp = false)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + (Width / 2.0);
    public double CenterY => Y + (Height / 2.0);
}

/// <summary>
/// Structured invoice line item parsed from OCR bounding boxes and tabular grids.
/// </summary>
public record ExtractedLineItem(
    int LineNumber,
    string Sku,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    decimal TaxRate,
    OcrBoundingBox? Box = null);

/// <summary>
/// Complete structured invoice payload extracted by intelligent document processing.
/// </summary>
public record ExtractedInvoiceData(
    string DocumentId,
    string VendorName,
    string VendorTaxId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    string Currency,
    IReadOnlyList<ExtractedLineItem> LineItems,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalAmount,
    string BankRib,
    double ConfidenceScore,
    string Sha256Hash);

/// <summary>
/// Result of 3-way matching across Purchase Order, Goods Receipt Note, and Supplier Invoice.
/// </summary>
public record ThreeWayMatchResult(
    ThreeWayMatchStatus MatchStatus,
    string PoId,
    string GrnId,
    string InvoiceId,
    decimal TotalPoAmount,
    decimal TotalGrnAmount,
    decimal TotalInvoiceAmount,
    decimal PriceVariancePercent,
    decimal QuantityVariance,
    IReadOnlyList<string> DiscrepancyNotes,
    bool AutoApproved,
    DateTime ReconciledAtUtc);

/// <summary>
/// Result of physical delivery receipt (POD) handwriting and corporate stamp inspection.
/// </summary>
public record PaperPodVerificationResult(
    string DocumentId,
    bool RecipientSignatureDetected,
    OcrBoundingBox? SignatureBoundingBox,
    bool OfficialStampDetected,
    OcrBoundingBox? StampBoundingBox,
    double OverallConfidence,
    DateTime VerifiedAtUtc,
    PodStampStatus StampStatus = PodStampStatus.Verified,
    string? Notes = null);

#endregion

#region Reconciliation Input Contracts

/// <summary>
/// Line item defined on a Purchase Order.
/// </summary>
public record PoLineItem(
    string Sku,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    decimal TaxRate = 0m);

/// <summary>
/// Purchase Order against which delivery and invoicing are evaluated.
/// </summary>
public record PurchaseOrderRecord(
    string PoId,
    string VendorName,
    string VendorTaxId,
    string Currency,
    IReadOnlyList<PoLineItem> LineItems,
    decimal TotalAmount,
    string? BankRib = null,
    decimal ExpectedTaxAmount = 0m);

/// <summary>
/// Line item received and inspected at warehouse intake.
/// </summary>
public record GrnLineItem(
    string Sku,
    decimal AcceptedQuantity,
    decimal RejectedQuantity = 0m,
    string? RejectionReason = null);

/// <summary>
/// Warehouse Goods Receipt Note (GRN) logging actual physical cargo accepted.
/// </summary>
public record GoodsReceiptNoteRecord(
    string GrnId,
    string PoId,
    DateTime ReceivedDateUtc,
    IReadOnlyList<GrnLineItem> ReceivedItems,
    string? ReceivedBy = null);

/// <summary>
/// Configurable reconciliation tolerance parameters.
/// </summary>
public record ToleranceOptions(
    decimal MaxPriceVariancePercent = 0.5m,
    decimal QuantityTolerance = 0m,
    decimal TaxTolerance = 0.01m);

#endregion

#region API Request & Response Contracts

/// <summary>
/// Request to parse raw document bytes or OCR coordinate tokens into structured invoice data.
/// </summary>
public record ParseDocumentRequest(
    string DocumentId,
    IdpDocumentType DocumentType,
    byte[]? RawData = null,
    string? RawText = null,
    IReadOnlyList<OcrBoundingBox>? Tokens = null,
    string? MimeType = null);

/// <summary>
/// Response containing structured invoice data or parsing failure details.
/// </summary>
public record ParseDocumentResponse(
    bool Success,
    ExtractedInvoiceData? ExtractedData,
    string? ErrorMessage = null);

/// <summary>
/// Request to perform 3-way reconciliation across PO, GRN, and Extracted Invoice.
/// </summary>
public record ThreeWayMatchRequest(
    PurchaseOrderRecord PurchaseOrder,
    GoodsReceiptNoteRecord GoodsReceiptNote,
    ExtractedInvoiceData Invoice,
    ToleranceOptions? Tolerance = null);

/// <summary>
/// Response carrying the detailed 3-way match result and discrepancy notes.
/// </summary>
public record ThreeWayMatchResponse(
    bool Success,
    ThreeWayMatchResult Result,
    string? Message = null);

/// <summary>
/// Request to verify physical paper Proof of Delivery slip for signatures and official stamps.
/// </summary>
public record VerifyPaperPodRequest(
    string DocumentId,
    byte[]? RawData = null,
    IReadOnlyList<OcrBoundingBox>? Tokens = null,
    double MinSignatureConfidence = 0.85,
    double MinStampConfidence = 0.80,
    bool HasForgeryMarkers = false);

/// <summary>
/// Response containing physical delivery verification status.
/// </summary>
public record VerifyPaperPodResponse(
    bool Success,
    PaperPodVerificationResult Result,
    string? Message = null);

/// <summary>
/// Health and capability status report for the IDP Engine.
/// </summary>
public record IdpHealthResponse(
    string Status,
    string Version,
    IReadOnlyList<string> Capabilities,
    DateTime TimestampUtc);

#endregion
