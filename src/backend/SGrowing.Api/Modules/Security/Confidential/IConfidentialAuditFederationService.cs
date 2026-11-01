namespace SGrowing.Api.Modules.Security.Confidential;

/// <summary>
/// Service managing non-repudiable cryptographic attestations for confidential computations
/// ensuring zero plaintext data exfiltration across consortium participants.
/// </summary>
public interface IConfidentialAuditFederationService
{
    /// <summary>
    /// Issues a cryptographic attestation token certifying a confidential computation.
    /// </summary>
    ConfidentialAttestationToken IssueAttestation(IssueAttestationRequest request, string consortiumSecretKey);

    /// <summary>
    /// Verifies the cryptographic integrity and signature of an attestation token.
    /// </summary>
    bool VerifyAttestation(ConfidentialAttestationToken token, string consortiumSecretKey);

    /// <summary>
    /// Retrieves an issued attestation by its unique identifier.
    /// </summary>
    ConfidentialAttestationToken? GetAttestation(string attestationId);

    /// <summary>
    /// Retrieves recent attestations up to the specified limit.
    /// </summary>
    IReadOnlyList<ConfidentialAttestationToken> GetRecentAttestations(int limit = 50);
}
