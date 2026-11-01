using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.VerifiableIdentity;

/// <summary>
/// Production-grade implementation of Biometric Custody & Handover authorization with
/// dynamic liveness challenge-response, 50-meter GPS geofencing, and signed handover certificates.
/// </summary>
public sealed class BiometricCustodyUnlockService(ICourierOnboardingAttestationService? courierService = null)
    : IBiometricCustodyUnlockService
{
    public const double MaxGeofenceDistanceMeters = 50.0;
    public const double MinBiometricLivenessScore = 0.85;

    private static readonly string[] ChallengeActions =
    [
        "BlinkAndSmile",
        "HeadTurnLeft",
        "NodTwice"
    ];

    private readonly Lock _lock = new();
    private readonly Dictionary<string, BiometricChallenge> _activeChallenges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CustodyHandoverCertificate> _certificates = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public BiometricChallenge GenerateLivenessChallenge(string courierDid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(courierDid);

        int actionIndex = RandomNumberGenerator.GetInt32(ChallengeActions.Length);
        string action = ChallengeActions[actionIndex];
        string challengeId = $"CHAL-{Guid.NewGuid():N}";
        string nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = now.AddMinutes(3); // 3-minute TTL

        var challenge = new BiometricChallenge
        {
            ChallengeId = challengeId,
            CourierDid = courierDid,
            Nonce = nonce,
            ChallengeAction = action,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        lock (_lock)
        {
            _activeChallenges[challengeId] = challenge;
        }

        return challenge;
    }

    /// <inheritdoc />
    public CustodyUnlockResult AuthorizeCustodyUnlock(CustodyUnlockRequest request, string authoritySecretKey)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(authoritySecretKey);

        if (string.IsNullOrWhiteSpace(request.CourierDid))
        {
            return Failure("Courier DID is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RecipientDid))
        {
            return Failure("Recipient DID is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ConsignmentWaybill))
        {
            return Failure("Consignment waybill number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FacialTemplateHash))
        {
            return Failure("Biometric facial template hash is missing.");
        }

        // 1. Challenge Validation & Consumption (single-use)
        BiometricChallenge? challenge;
        lock (_lock)
        {
            if (!_activeChallenges.TryGetValue(request.ChallengeId, out challenge))
            {
                return Failure("Biometric challenge not found or already consumed.");
            }

            // Challenge is single-use, remove immediately
            _activeChallenges.Remove(request.ChallengeId);
        }

        if (!challenge.CourierDid.Equals(request.CourierDid, StringComparison.Ordinal))
        {
            return Failure("Biometric challenge courier DID mismatch.");
        }

        if (challenge.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return Failure($"Biometric challenge has expired (expired at {challenge.ExpiresAt:O}, TTL is 3 minutes).");
        }

        // 2. Biometric Liveness Threshold Check (reject spoof / photo presentation attacks)
        if (request.BiometricLivenessScore < MinBiometricLivenessScore)
        {
            return Failure(
                $"Biometric liveness score {request.BiometricLivenessScore:F2} is below required threshold {MinBiometricLivenessScore:F2} (possible spoof / presentation attack).");
        }

        // 3. Geofence Distance Calculation (must be <= 50m of destination)
        double distanceMeters = CalculateHaversineDistanceMeters(
            request.Latitude, request.Longitude,
            request.DestinationLatitude, request.DestinationLongitude);

        if (distanceMeters > MaxGeofenceDistanceMeters)
        {
            return Failure(
                $"Geofence violation: Courier is {distanceMeters:F1} meters away from destination, exceeding maximum allowed {MaxGeofenceDistanceMeters:F0} meters.");
        }

        // 4. Cash Float Tier Limit Verification (if courier badge is registered)
        if (courierService is not null)
        {
            var badge = courierService.GetCourierBadge(request.CourierDid);
            if (badge is not null)
            {
                if (!badge.IsActive)
                {
                    return Failure($"Courier badge for '{request.CourierDid}' is suspended or inactive.");
                }

                if (request.CashCollectedDzd > badge.MaxCashFloatDzd)
                {
                    return Failure(
                        $"Cash collected {request.CashCollectedDzd:N2} DZD exceeds authorized cash float limit of {badge.MaxCashFloatDzd:N2} DZD for tier {badge.CashFloatTier}.");
                }
            }
        }

        // 5. Generate Immutable Cryptographically Signed Custody Handover Certificate
        string certificateId = $"CERT-{Guid.NewGuid():N}";
        DateTimeOffset handoverTime = DateTimeOffset.UtcNow;

        string canonicalPayload = BuildCertificatePayload(
            certificateId,
            request.CourierDid,
            request.RecipientDid,
            request.ConsignmentWaybill,
            request.CashCollectedDzd,
            request.Latitude,
            request.Longitude,
            handoverTime);

        byte[] sigBytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(authoritySecretKey),
            Encoding.UTF8.GetBytes(canonicalPayload));
        string signatureHex = Convert.ToHexStringLower(sigBytes);

        var certificate = new CustodyHandoverCertificate
        {
            CertificateId = certificateId,
            CourierDid = request.CourierDid,
            RecipientDid = request.RecipientDid,
            WaybillNumber = request.ConsignmentWaybill,
            CashAmountDzd = request.CashCollectedDzd,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            HandoverTimestampUtc = handoverTime,
            CryptographicSignature = signatureHex
        };

        lock (_lock)
        {
            _certificates[certificateId] = certificate;
        }

        string authorizationCode = $"AUTH-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}";

        return new CustodyUnlockResult
        {
            IsAuthorized = true,
            AuthorizationCode = authorizationCode,
            CustodyCertificateId = certificateId,
            Message = "Custody unlock authorized and signed handover certificate generated.",
            RejectionReason = null
        };
    }

    /// <inheritdoc />
    public CustodyHandoverCertificate? GetHandoverCertificate(string certificateId)
    {
        if (string.IsNullOrWhiteSpace(certificateId)) return null;

        lock (_lock)
        {
            return _certificates.GetValueOrDefault(certificateId);
        }
    }

    /// <summary>
    /// Computes great-circle distance between two GPS coordinates using the Haversine formula in meters.
    /// </summary>
    public static double CalculateHaversineDistanceMeters(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        const double earthRadiusMeters = 6_371_000.0;

        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double rLat1 = ToRadians(lat1);
        double rLat2 = ToRadians(lat2);

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(rLat1) * Math.Cos(rLat2) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        // Clamp to avoid rounding error pushing a out of [0, 1]
        a = Math.Clamp(a, 0.0, 1.0);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return earthRadiusMeters * c;

        static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }

    /// <summary>
    /// Verifies the cryptographic HMAC-SHA256 signature of a CustodyHandoverCertificate.
    /// </summary>
    public static bool VerifyCertificateSignature(CustodyHandoverCertificate cert, string authoritySecretKey)
    {
        ArgumentNullException.ThrowIfNull(cert);
        ArgumentException.ThrowIfNullOrWhiteSpace(authoritySecretKey);

        string canonicalPayload = BuildCertificatePayload(
            cert.CertificateId,
            cert.CourierDid,
            cert.RecipientDid,
            cert.WaybillNumber,
            cert.CashAmountDzd,
            cert.Latitude,
            cert.Longitude,
            cert.HandoverTimestampUtc);

        byte[] expectedSig = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(authoritySecretKey),
            Encoding.UTF8.GetBytes(canonicalPayload));

        byte[] actualSig;
        try
        {
            actualSig = Convert.FromHexString(cert.CryptographicSignature);
        }
        catch (FormatException)
        {
            return false;
        }

        return actualSig.Length == expectedSig.Length &&
               CryptographicOperations.FixedTimeEquals(actualSig, expectedSig);
    }

    private static string BuildCertificatePayload(
        string certificateId,
        string courierDid,
        string recipientDid,
        string waybillNumber,
        decimal cashAmountDzd,
        double latitude,
        double longitude,
        DateTimeOffset timestamp)
    {
        return $"{certificateId}|{courierDid}|{recipientDid}|{waybillNumber}|{cashAmountDzd.ToString("F2", CultureInfo.InvariantCulture)}|{latitude.ToString("F6", CultureInfo.InvariantCulture)}|{longitude.ToString("F6", CultureInfo.InvariantCulture)}|{timestamp:O}";
    }

    private static CustodyUnlockResult Failure(string reason) => new()
    {
        IsAuthorized = false,
        AuthorizationCode = null,
        CustodyCertificateId = null,
        Message = "Custody unlock authorization rejected.",
        RejectionReason = reason
    };
}
