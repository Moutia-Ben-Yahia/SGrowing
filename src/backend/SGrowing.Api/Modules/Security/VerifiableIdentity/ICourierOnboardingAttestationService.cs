namespace SGrowing.Api.Modules.Security.VerifiableIdentity;

/// <summary>
/// Result of an onboarded courier including their issued badge and verifiable credential.
/// </summary>
public record CourierOnboardingResult
{
    public required CourierBadge Badge { get; init; }
    public required VerifiableCredential Credential { get; init; }
    public required DidDocument CourierDidDocument { get; init; }

    public void Deconstruct(out CourierBadge badge, out VerifiableCredential credential)
    {
        badge = Badge;
        credential = Credential;
    }

    public void Deconstruct(out CourierBadge badge, out VerifiableCredential credential, out DidDocument didDocument)
    {
        badge = Badge;
        credential = Credential;
        didDocument = CourierDidDocument;
    }
}

/// <summary>
/// Service responsible for validating government IDs, generating criminal record & insurance
/// attestations, onboarding couriers with W3C DIDs, and issuing signed delivery badges.
/// </summary>
public interface ICourierOnboardingAttestationService
{
    /// <summary>
    /// Validates official credentials, clears background checks, generates a W3C DID,
    /// and issues a cryptographically signed CourierDeliveryBadgeCredential.
    /// </summary>
    CourierOnboardingResult OnboardCourier(CourierOnboardingRequest request, string issuerSecretKey);

    /// <summary>
    /// Retrieves a courier's badge by their decentralized identifier (DID).
    /// </summary>
    CourierBadge? GetCourierBadge(string courierDid);

    /// <summary>
    /// Updates the authorized cash float tier and maximum allowed Cash-on-Delivery amount for a courier.
    /// </summary>
    CourierBadge UpdateCashFloatLimit(string courierDid, CashFloatLimitTier newTier);
}
