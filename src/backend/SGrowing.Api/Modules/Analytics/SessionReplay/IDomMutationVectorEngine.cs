namespace SGrowing.Api.Modules.Analytics.SessionReplay;

/// <summary>
/// Interface for ingesting, validating, compressing, and querying DOM mutation vectors.
/// </summary>
public interface IDomMutationVectorEngine
{
    /// <summary>
    /// Ingests a chronological batch of DOM mutations, enforcing strict sequence monotonicity.
    /// </summary>
    IngestResult IngestMutations(string sessionId, DomMutationBatch batch, bool throwOnError = false);

    /// <summary>
    /// Retrieves all recorded DOM mutations for the given session in chronological sequence.
    /// </summary>
    IReadOnlyList<DomMutationOperation> GetSessionMutations(string sessionId);

    /// <summary>
    /// Calculates network throughput, compression ratio, and checks compliance with bandwidth target (&lt; 2.0 KB/s).
    /// </summary>
    ThroughputMetrics CalculateThroughputMetrics(string sessionId);

    /// <summary>
    /// Clears cached mutations for a completed or archived session.
    /// </summary>
    bool ClearSession(string sessionId);
}
