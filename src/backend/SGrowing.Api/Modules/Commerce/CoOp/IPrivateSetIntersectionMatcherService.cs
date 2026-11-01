namespace SGrowing.Api.Modules.Commerce.CoOp;

/// <summary>
/// Privacy-preserving Private Set Intersection (PSI) service using Diffie-Hellman commutative exponentiation.
/// Enables collaborative merchants to discover customer overlap without revealing raw customer identifiers (emails/phones).
/// Strictly compliant with GDPR and Algerian Personal Data Protection Law 18-07.
/// </summary>
public interface IPrivateSetIntersectionMatcherService
{
    /// <summary>
    /// Total active PSI sessions.
    /// </summary>
    int TotalSessions { get; }

    /// <summary>
    /// Generates a cryptographically secure random 256-bit private exponent in hex format.
    /// </summary>
    string GeneratePrivateKey();

    /// <summary>
    /// Hashes an identifier (SHA-256) and blinds it using private exponent: H(x)^a mod p.
    /// Never stores or leaks the raw identifier.
    /// </summary>
    PsiBlindedToken BlindIdentifier(string rawIdentifier, string privateKeyHex);

    /// <summary>
    /// Re-blinds an already blinded token using a second private exponent: (H(x)^a)^b mod p = H(x)^ab mod p.
    /// </summary>
    PsiBlindedToken ReblindToken(PsiBlindedToken token, string privateKeyHex);

    /// <summary>
    /// Initiates a multi-step PSI session for an initiator merchant.
    /// Immediately blinds all identifiers and discards raw PII.
    /// </summary>
    PsiSession InitiateSession(
        string initiatorMerchantId,
        string partnerMerchantId,
        IReadOnlyList<string> initiatorRawIdentifiers);

    /// <summary>
    /// Executes the partner merchant exchange: blinds partner identifiers H(y)^b and double-blinds initiator's tokens H(x)^ab.
    /// </summary>
    PsiSession ExchangePartnerTokens(
        string sessionId,
        string partnerMerchantId,
        IReadOnlyList<string> partnerRawIdentifiers);

    /// <summary>
    /// Finalizes the PSI protocol: double-blinds partner tokens H(y)^ba and matches intersections H(x)^ab == H(y)^ba.
    /// </summary>
    PsiMatchResult ComputeIntersection(string sessionId);

    /// <summary>
    /// Retrieves a PSI session by its ID.
    /// </summary>
    PsiSession? GetSession(string sessionId);

    /// <summary>
    /// Evaluates mutual partnership potential between two merchants using customer overlap metrics.
    /// </summary>
    MerchantPartnershipSummary EvaluatePartnershipPotential(
        string merchantId,
        string partnerMerchantId,
        int discoveredBundleCount = 0,
        decimal potentialGrossRevenueDzd = 0m);
}
