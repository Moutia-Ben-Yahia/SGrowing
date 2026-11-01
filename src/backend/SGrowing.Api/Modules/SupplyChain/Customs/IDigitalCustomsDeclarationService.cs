namespace SGrowing.Api.Modules.SupplyChain.Customs;

/// <summary>
/// Autonomous digital customs declaration, cryptographic signing, and border verification service.
/// Generates Single Administrative Documents (DAU / SAD), EUR.1 Movement Certificates, and ATA Carnets,
/// cryptographically signs declarations via HMAC-SHA256, generates tamper-evident QR payloads,
/// and validates transit clearance at customs border checkpoints.
/// </summary>
public interface IDigitalCustomsDeclarationService
{
    /// <summary>
    /// Creates, calculates landed duties for, and cryptographically signs a new digital customs declaration.
    /// </summary>
    DigitalDeclaration CreateDeclaration(CreateDeclarationRequest request);

    /// <summary>
    /// Retrieves a declaration by its unique identifier.
    /// </summary>
    DigitalDeclaration? GetDeclaration(string declarationId);

    /// <summary>
    /// Verifies the cryptographic HMAC signature of a registered declaration.
    /// </summary>
    DeclarationVerificationResult VerifyDeclaration(string declarationId, string signature, string? secretKey = null);

    /// <summary>
    /// Revokes an existing customs declaration, invalidating it for border transit.
    /// </summary>
    bool RevokeDeclaration(string declarationId, string reason);

    /// <summary>
    /// Verifies a scanned border QR payload string against a customs authority secret key,
    /// verifying cryptographic integrity, expiration, and revocation status.
    /// </summary>
    BorderVerificationResult VerifyDeclarationQr(string qrPayload, string secretKey);
}
