namespace SGrowing.Api.Modules.Commerce.QualityGate;

/// <summary>
/// Interface for autonomous multimodal computer vision packaging anomaly and defect detection.
/// </summary>
public interface IVisualPackagingAnomalyDetector
{
    /// <summary>
    /// Analyzes an incoming consignment packaging image frame for surface anomalies, defects, and structural integrity.
    /// </summary>
    PackagingInspectionResult AnalyzePackagingImage(string waybill, byte[] imageFrameBytes);

    /// <summary>
    /// Retrieves historical inspection results for a given consignment waybill.
    /// </summary>
    IReadOnlyList<PackagingInspectionResult> GetInspectionHistory(string waybill);

    /// <summary>
    /// Retrieves a specific packaging inspection result by its unique ID.
    /// </summary>
    PackagingInspectionResult? GetInspectionById(string inspectionId);

    /// <summary>
    /// Retrieves all recorded packaging inspections in the system.
    /// </summary>
    IReadOnlyList<PackagingInspectionResult> GetAllInspections();
}
