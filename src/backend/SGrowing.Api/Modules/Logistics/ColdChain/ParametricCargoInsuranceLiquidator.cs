using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Logistics.ColdChain;

/// <summary>
/// Autonomous zero-touch parametric cargo insurance claim liquidation service.
/// Evaluates decentralized oracle telemetry, discharges buyer COD collection obligations,
/// and issues cryptographically signed liquidation certificates.
/// </summary>
public sealed class ParametricCargoInsuranceLiquidator(IColdChainTelemetryOracleEngine oracleEngine) : IParametricCargoInsuranceLiquidator
{
    private const string DefaultSecretKey = "sgrowing-parametric-insurance-notary-key-2026";
    private readonly IColdChainTelemetryOracleEngine _oracleEngine = oracleEngine ?? throw new ArgumentNullException(nameof(oracleEngine));
    private readonly Lock _syncLock = new();
    private readonly Dictionary<string, ParametricInsuranceClaim> _claims = [];

    /// <inheritdoc />
    public ParametricInsuranceClaim ProcessTelemetryForClaims(
        string consignmentId,
        CargoThermalProfile profile,
        decimal policyPayoutDzd,
        string recipientId,
        string? secretKey = null,
        string? policyNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consignmentId);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientId);

        if (policyPayoutDzd <= 0m)
        {
            throw new ArgumentException("Policy payout must be greater than zero.", nameof(policyPayoutDzd));
        }

        var keyToUse = string.IsNullOrWhiteSpace(secretKey) ? DefaultSecretKey : secretKey;
        var policy = string.IsNullOrWhiteSpace(policyNumber) ? $"POL-PARAMETRIC-{consignmentId}" : policyNumber;
        var claimId = $"CLM-{Guid.NewGuid():N}";

        var mktResult = _oracleEngine.CalculateMkt(consignmentId, profile);
        var maxShock = _oracleEngine.GetMaxRecordedShock(consignmentId);
        var hasLightBreach = _oracleEngine.HasLightExposureBreach(consignmentId);

        string breachType;
        double breachedValue;
        double thresholdValue;
        bool isBreached;
        string reason;

        // 1. Extreme Shock Impact breach (> 5.0 Gs)
        if (maxShock >= 5.0)
        {
            isBreached = true;
            breachType = nameof(BreachType.ShockImpact);
            breachedValue = maxShock;
            thresholdValue = 5.0;
            reason = $"High-impact drop or collision detected: {maxShock:F2} Gs exceeded parametric shock trigger threshold (5.0 Gs).";
        }
        // 2. Severe Thermal Excursion breach
        else if (mktResult.ExcursionDurationMinutes > profile.MaxExcursionMinutes ||
                 mktResult.ComplianceStatus == ThermalComplianceStatus.SevereExcursionBreached ||
                 mktResult.ComplianceStatus == ThermalComplianceStatus.Spoiled)
        {
            isBreached = true;
            breachType = nameof(BreachType.ThermalExcursion);
            breachedValue = mktResult.ExcursionDurationMinutes;
            thresholdValue = profile.MaxExcursionMinutes;
            reason = $"Thermal excursion duration of {mktResult.ExcursionDurationMinutes:F1} minutes exceeded safe tolerance ({profile.MaxExcursionMinutes:F1} min). MKT: {mktResult.MktCelsius:F2}°C.";
        }
        else
        {
            isBreached = false;
            breachType = nameof(BreachType.ThermalExcursion);
            breachedValue = mktResult.ExcursionDurationMinutes;
            thresholdValue = profile.MaxExcursionMinutes;
            reason = $"Cargo thermal envelope and shock integrity maintained within contractual thresholds. Claim rejected. Light breach: {hasLightBreach}.";
        }

        ParametricInsuranceClaim claim;
        if (isBreached)
        {
            var liquidatedAt = DateTime.UtcNow;
            var certificate = GenerateLiquidationCertificate(claimId, consignmentId, policy, policyPayoutDzd, recipientId, liquidatedAt, keyToUse);

            claim = new ParametricInsuranceClaim(
                ClaimId: claimId,
                ConsignmentId: consignmentId,
                PolicyNumber: policy,
                BreachType: breachType,
                BreachedValue: Math.Round(breachedValue, 2),
                ThresholdValue: Math.Round(thresholdValue, 2),
                LiquidationAmountDzd: policyPayoutDzd,
                PayoutRecipientId: recipientId,
                Status: InsuranceClaimStatus.AutoApprovedLiquidated,
                LiquidatedAtUtc: liquidatedAt,
                BuyerCodObligationVoided: true,
                LiquidationCertificate: certificate,
                Reason: reason);
        }
        else
        {
            claim = new ParametricInsuranceClaim(
                ClaimId: claimId,
                ConsignmentId: consignmentId,
                PolicyNumber: policy,
                BreachType: breachType,
                BreachedValue: Math.Round(breachedValue, 2),
                ThresholdValue: Math.Round(thresholdValue, 2),
                LiquidationAmountDzd: 0m,
                PayoutRecipientId: recipientId,
                Status: InsuranceClaimStatus.Rejected,
                LiquidatedAtUtc: null,
                BuyerCodObligationVoided: false,
                LiquidationCertificate: null,
                Reason: reason);
        }

        lock (_syncLock)
        {
            _claims[claim.ClaimId] = claim;
        }

        return claim;
    }

    /// <inheritdoc />
    public ParametricInsuranceClaim? GetClaim(string claimId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimId);

        lock (_syncLock)
        {
            return _claims.GetValueOrDefault(claimId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ParametricInsuranceClaim> ListLiquidatedClaims()
    {
        lock (_syncLock)
        {
            return _claims.Values
                .Where(c => c.Status == InsuranceClaimStatus.AutoApprovedLiquidated)
                .OrderByDescending(c => c.LiquidatedAtUtc)
                .ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ParametricInsuranceClaim> GetClaimsForConsignment(string consignmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consignmentId);

        lock (_syncLock)
        {
            return _claims.Values
                .Where(c => c.ConsignmentId == consignmentId)
                .OrderByDescending(c => c.LiquidatedAtUtc ?? DateTime.MinValue)
                .ToList();
        }
    }

    /// <inheritdoc />
    public bool VerifyLiquidationCertificate(ParametricInsuranceClaim claim, string? secretKey = null)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (claim.Status != InsuranceClaimStatus.AutoApprovedLiquidated ||
            claim.LiquidatedAtUtc is null ||
            string.IsNullOrWhiteSpace(claim.LiquidationCertificate))
        {
            return false;
        }

        var keyToUse = string.IsNullOrWhiteSpace(secretKey) ? DefaultSecretKey : secretKey;
        var expectedCertificate = GenerateLiquidationCertificate(
            claim.ClaimId,
            claim.ConsignmentId,
            claim.PolicyNumber,
            claim.LiquidationAmountDzd,
            claim.PayoutRecipientId,
            claim.LiquidatedAtUtc.Value,
            keyToUse);

        var expectedBytes = Encoding.UTF8.GetBytes(expectedCertificate);
        var actualBytes = Encoding.UTF8.GetBytes(claim.LiquidationCertificate);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_syncLock)
        {
            _claims.Clear();
        }
    }

    private static string GenerateLiquidationCertificate(
        string claimId,
        string consignmentId,
        string policyNumber,
        decimal payoutDzd,
        string recipientId,
        DateTime liquidatedAtUtc,
        string secretKey)
    {
        var payload = $"CERT|CLAIM:{claimId}|CONSIGNMENT:{consignmentId}|POLICY:{policyNumber}|PAYOUT:{payoutDzd:F2}|RECIPIENT:{recipientId}|STATUS:AutoApprovedLiquidated|TIME:{liquidatedAtUtc:O}";
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var signature = Convert.ToHexString(hash).ToLowerInvariant();

        return $"CERT-PARAMETRIC-{claimId}:{signature}";
    }
}
