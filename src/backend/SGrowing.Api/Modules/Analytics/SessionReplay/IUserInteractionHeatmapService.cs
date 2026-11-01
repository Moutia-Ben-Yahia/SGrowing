namespace SGrowing.Api.Modules.Analytics.SessionReplay;

/// <summary>
/// High-frequency telemetry ingestion, frustration detection (rage clicks, dead clicks, scroll thrashing),
/// and spatial 2D density heatmap reconstruction.
/// </summary>
public interface IUserInteractionHeatmapService
{
    /// <summary>
    /// Ingests a batch of high-frequency pointer, touch, click, or scroll interaction events.
    /// </summary>
    int IngestInteractions(string sessionId, string pageUrl, IEnumerable<InteractionEvent> events);

    /// <summary>
    /// Reconstructs a 2D spatial density grid for visual heatmap rendering on a specific page.
    /// </summary>
    HeatmapResult GetHeatmap(string pageUrl, HeatmapType type, int gridWidth = 50, int gridHeight = 50);

    /// <summary>
    /// Analyzes recorded telemetry for a session and identifies micro-frustrations.
    /// </summary>
    IReadOnlyList<FrustrationEvent> DetectFrustrations(string sessionId);

    /// <summary>
    /// Retrieves all raw interaction telemetry captured for a session.
    /// </summary>
    IReadOnlyList<InteractionEvent> GetSessionInteractions(string sessionId);

    /// <summary>
    /// Clears interactions for a completed or archived session.
    /// </summary>
    bool ClearSession(string sessionId);
}
