namespace SGrowing.Api.Modules.Security.VerifiableIdentity;

/// <summary>
/// Engine for generating W3C Decentralized Identifiers (DIDs), issuing cryptographically
/// signed Verifiable Credentials, verifying tamper-proof claims, and managing revocation.
/// </summary>
public interface IVerifiableCredentialEngine
{
    /// <summary>
    /// Generates a new W3C-compliant 'did:key' identifier and associated DID Document.
    /// </summary>
    DidDocument CreateDid(string keyType = "Ed25519");

    /// <summary>
    /// Resolves a previously generated DID document from the internal registry if available.
    /// </summary>
    DidDocument? ResolveDid(string did);

    /// <summary>
    /// Issues a cryptographically signed W3C Verifiable Credential over canonical claims.
    /// </summary>
    VerifiableCredential IssueCredential(
        string issuerDid,
        string subjectDid,
        string credentialType,
        IDictionary<string, object> claims,
        string secretKey,
        TimeSpan? validityPeriod = null);

    /// <summary>
    /// Verifies the cryptographic signature, expiration, and revocation status of a credential.
    /// </summary>
    CredentialVerificationResult VerifyCredential(
        VerifiableCredential credential,
        string secretKey);

    /// <summary>
    /// Revokes a credential by ID with a timestamped reason.
    /// </summary>
    RevocationRecord RevokeCredential(string credentialId, string reason);

    /// <summary>
    /// Checks whether a credential has been revoked.
    /// </summary>
    bool IsCredentialRevoked(string credentialId);

    /// <summary>
    /// Retrieves the revocation record for a credential if it was revoked.
    /// </summary>
    RevocationRecord? GetRevocationRecord(string credentialId);
}
