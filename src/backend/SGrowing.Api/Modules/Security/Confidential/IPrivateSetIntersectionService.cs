namespace SGrowing.Api.Modules.Security.Confidential;

/// <summary>
/// Service providing blind Private Set Intersection (PSI) for multi-party fraud detection
/// and VIP identification across merchant boundaries without exposing non-matching records.
/// </summary>
public interface IPrivateSetIntersectionService
{
    /// <summary>
    /// Computes the intersection of two blinded candidate sets (A ∩ B) without revealing non-matching elements.
    /// </summary>
    PsiIntersectionResult ComputeIntersection(
        IEnumerable<PsiCandidateItem> setA,
        IEnumerable<PsiCandidateItem> setB,
        string consortiumSessionId,
        IReadOnlyList<string>? participantIds = null);

    /// <summary>
    /// Computes the multi-party intersection across 3 or more consortium participant candidate sets.
    /// </summary>
    PsiIntersectionResult ComputeMultiPartyIntersection(
        IReadOnlyDictionary<string, IEnumerable<PsiCandidateItem>> participantSets,
        string consortiumSessionId);

    /// <summary>
    /// Blinds a raw identifier (e.g. email, phone, account number) using salted HMAC-SHA256
    /// with an ephemeral consortium session secret.
    /// </summary>
    PsiCandidateItem BlindItem(string rawIdentifier, string partitionKey, string consortiumSessionSecret);

    /// <summary>
    /// Batch blinds raw items using salted HMAC-SHA256 with an ephemeral consortium session secret.
    /// </summary>
    IReadOnlyList<PsiCandidateItem> BlindItems(
        IEnumerable<(string Identifier, string PartitionKey)> items,
        string consortiumSessionSecret);
}
