namespace SGrowing.Api.Modules.Security.VerifiableIdentity;

/// <summary>
/// Service responsible for issuing dynamic biometric challenges, validating biometric liveness,
/// verifying GPS geofencing, enforcing cash float limits, and authorizing parcel custody unlock.
/// </summary>
public interface IBiometricCustodyUnlockService
{
    /// <summary>
    /// Generates a single-use dynamic biometric liveness challenge with a 3-minute TTL.
    /// </summary>
    BiometricChallenge GenerateLivenessChallenge(string courierDid);

    /// <summary>
    /// Verifies biometric liveness, challenge freshness, geodesic destination distance (under 50m),
    /// and issues a cryptographically signed handover certificate.
    /// </summary>
    CustodyUnlockResult AuthorizeCustodyUnlock(CustodyUnlockRequest request, string authoritySecretKey);

    /// <summary>
    /// Retrieves a previously signed custody handover certificate by certificate ID.
    /// </summary>
    CustodyHandoverCertificate? GetHandoverCertificate(string certificateId);
}
