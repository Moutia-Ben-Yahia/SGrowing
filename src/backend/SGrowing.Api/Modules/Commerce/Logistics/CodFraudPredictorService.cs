using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Commerce.Logistics;

public enum CodDeliveryVerificationStatus
{
    PendingEvaluation = 0,
    AutoApproved = 1,
    ModerateRiskPendingSms = 2,
    HighRiskPendingCall = 3,
    VerifiedByCustomer = 4,
    FailedVerification = 5,
    RejectedFraud = 6
}

public enum FraudRiskTier
{
    Low = 1,
    Moderate = 2,
    High = 3
}

public record CodRiskFactorBreakdown(
    string FactorCode,
    string Description,
    int ScorePoints,
    string Severity);

public record CodFraudEvaluationRequest(
    Guid TenantId,
    Guid OrderId,
    string PhoneNumber,
    string DeliveryAddress,
    string? CustomerName = null,
    string? DestinationCity = null,
    decimal OrderAmount = 0m,
    int TotalPastOrders = 0,
    int PastRefusalsCount = 0,
    int PastUncollectedCount = 0,
    int SuccessfulDeliveriesCount = 0);

public record CodFraudRiskAssessment(
    Guid TenantId,
    Guid OrderId,
    int RiskScore,
    FraudRiskTier Tier,
    CodDeliveryVerificationStatus Status,
    string? VerificationCode,
    DateTime? VerificationCodeExpiresAtUtc,
    IReadOnlyList<CodRiskFactorBreakdown> Factors,
    string Recommendation,
    DateTime EvaluatedAtUtc);

public class CodVerificationRecord
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string VerificationCode { get; set; } = string.Empty;
    public CodDeliveryVerificationStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public int VerificationAttempts { get; set; }
    public string? VerifiedChannel { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
}

public record CodVerificationResult(
    bool Success,
    Guid OrderId,
    CodDeliveryVerificationStatus Status,
    string Message,
    string? Channel = null,
    DateTime? VerifiedAtUtc = null)
{
    public static CodVerificationResult Succeeded(Guid orderId, string channel, string message) =>
        new(true, orderId, CodDeliveryVerificationStatus.VerifiedByCustomer, message, channel, DateTime.UtcNow);

    public static CodVerificationResult Failed(Guid orderId, CodDeliveryVerificationStatus status, string message) =>
        new(false, orderId, status, message);
}

public interface ICodFraudPredictorService
{
    Task<CodFraudRiskAssessment> EvaluateRiskAsync(Guid tenantId, CodFraudEvaluationRequest request, CancellationToken ct = default);
    Task<CodVerificationResult> VerifyOrderAsync(Guid tenantId, Guid orderId, string verificationCode, string channel, CancellationToken ct = default);
    Task<CodVerificationRecord?> GetVerificationStatusAsync(Guid tenantId, Guid orderId, CancellationToken ct = default);
}

public class CodFraudPredictorService : ICodFraudPredictorService
{
    private static readonly Regex RepeatedDigitsRegex = new(@"(\d)\1{5,}", RegexOptions.Compiled);
    private static readonly Regex GibberishRegex = new(@"[a-zA-Z]{18,}|(asdf|qwer|test|fake|null|none|12345|xyz)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RepeatedCharPattern = new(@"(.)\1{4,}", RegexOptions.Compiled);

    private readonly ConcurrentDictionary<(Guid TenantId, Guid OrderId), CodVerificationRecord> _verifications = new();

    public Task<CodFraudRiskAssessment> EvaluateRiskAsync(
        Guid tenantId,
        CodFraudEvaluationRequest request,
        CancellationToken ct = default)
    {
        var factors = new List<CodRiskFactorBreakdown>();
        int score = 0;

        // 1. Phone number refusal rate & validity check
        var rawPhone = request.PhoneNumber ?? string.Empty;
        var cleanPhone = Regex.Replace(rawPhone, @"[^\d+]", "");

        if (string.IsNullOrWhiteSpace(cleanPhone) || cleanPhone.Length < 8)
        {
            factors.Add(new CodRiskFactorBreakdown("INVALID_PHONE_FORMAT", "Phone number is too short or missing digits.", 35, "High"));
            score += 35;
        }
        else if (RepeatedDigitsRegex.IsMatch(cleanPhone))
        {
            factors.Add(new CodRiskFactorBreakdown("SUSPICIOUS_PHONE_REPETITION", "Phone number contains 6+ repeated identical digits (probable burner/dummy).", 35, "High"));
            score += 35;
        }

        if (request.TotalPastOrders > 0)
        {
            decimal refusalRate = (decimal)request.PastRefusalsCount / request.TotalPastOrders;
            if (refusalRate >= 0.50m)
            {
                int refusalPoints = 40;
                factors.Add(new CodRiskFactorBreakdown("HIGH_PHONE_REFUSAL_RATE", $"Customer historical doorstep refusal rate is {refusalRate:P0} (>= 50%).", refusalPoints, "Critical"));
                score += refusalPoints;
            }
            else if (refusalRate >= 0.25m)
            {
                int refusalPoints = 25;
                factors.Add(new CodRiskFactorBreakdown("MODERATE_PHONE_REFUSAL_RATE", $"Customer historical doorstep refusal rate is {refusalRate:P0} (>= 25%).", refusalPoints, "High"));
                score += refusalPoints;
            }
        }

        // 2. Previous uncollected COD orders
        if (request.PastUncollectedCount > 0)
        {
            int uncollectedPoints = Math.Min(45, request.PastUncollectedCount * 15);
            factors.Add(new CodRiskFactorBreakdown("PREVIOUS_UNCOLLECTED_ORDERS", $"Customer has {request.PastUncollectedCount} previously uncollected/returned COD deliveries.", uncollectedPoints, uncollectedPoints >= 30 ? "Critical" : "High"));
            score += uncollectedPoints;
        }

        // 3. Delivery address entropy & anomaly analysis
        var address = request.DeliveryAddress ?? string.Empty;
        var trimmedAddress = address.Trim();

        if (trimmedAddress.Length < 10)
        {
            factors.Add(new CodRiskFactorBreakdown("VAGUE_DELIVERY_ADDRESS", "Street address is fewer than 10 characters, insufficient for courier delivery.", 25, "Medium"));
            score += 25;
        }
        else
        {
            double entropy = CalculateShannonEntropy(trimmedAddress);
            if (entropy < 2.5)
            {
                factors.Add(new CodRiskFactorBreakdown("LOW_ADDRESS_ENTROPY", $"Address string has unnaturally low information entropy ({entropy:F2}), suggesting repetitive keyboard mash.", 30, "High"));
                score += 30;
            }
            else if (GibberishRegex.IsMatch(trimmedAddress) || RepeatedCharPattern.IsMatch(trimmedAddress))
            {
                factors.Add(new CodRiskFactorBreakdown("GIBBERISH_ADDRESS_DETECTED", "Address text contains known keyboard mash tokens or excessive character repetition.", 35, "Critical"));
                score += 35;
            }
        }

        if (string.IsNullOrWhiteSpace(request.DestinationCity))
        {
            factors.Add(new CodRiskFactorBreakdown("MISSING_DESTINATION_CITY", "Destination city or municipality is missing or empty.", 20, "Medium"));
            score += 20;
        }

        // 4. Order value threshold exposure
        if (request.OrderAmount >= 500m)
        {
            factors.Add(new CodRiskFactorBreakdown("HIGH_ORDER_VALUE_THRESHOLD", $"COD order total ({request.OrderAmount:C0}) exceeds high-exposure threshold ($500).", 25, "Medium"));
            score += 25;
        }
        else if (request.OrderAmount >= 250m)
        {
            factors.Add(new CodRiskFactorBreakdown("MODERATE_ORDER_VALUE_THRESHOLD", $"COD order total ({request.OrderAmount:C0}) exceeds moderate exposure threshold ($250).", 15, "Low"));
            score += 15;
        }

        // 5. Trusted customer bonus
        if (request.SuccessfulDeliveriesCount >= 2 && request.PastRefusalsCount == 0 && request.PastUncollectedCount == 0)
        {
            factors.Add(new CodRiskFactorBreakdown("TRUSTED_BUYER_BONUS", $"Customer has {request.SuccessfulDeliveriesCount} successfully delivered orders with 0 refusals.", -20, "Low"));
            score -= 20;
        }

        int finalScore = Math.Clamp(score, 0, 100);

        FraudRiskTier tier;
        CodDeliveryVerificationStatus status;
        string? verificationCode = null;
        DateTime? expiresAtUtc = null;
        string recommendation;

        if (finalScore < 30)
        {
            tier = FraudRiskTier.Low;
            status = CodDeliveryVerificationStatus.AutoApproved;
            recommendation = "Low fraud probability. Auto-approved for courier dispatch without manual intervention.";
        }
        else if (finalScore <= 65)
        {
            tier = FraudRiskTier.Moderate;
            status = CodDeliveryVerificationStatus.ModerateRiskPendingSms;
            recommendation = "Moderate risk profile. Hold for automated 1-click SMS/WhatsApp confirmation before fulfillment.";
        }
        else
        {
            tier = FraudRiskTier.High;
            status = CodDeliveryVerificationStatus.HighRiskPendingCall;
            recommendation = "High fraud risk. Flagged with HighRiskPendingCall. Automated verification token generated for WhatsApp 1-click or IVR voice confirmation.";

            // Generate automated 6-digit OTP verification token
            verificationCode = GenerateVerificationToken();
            expiresAtUtc = DateTime.UtcNow.AddHours(24);

            var verificationRecord = new CodVerificationRecord
            {
                TenantId = tenantId,
                OrderId = request.OrderId,
                VerificationCode = verificationCode,
                Status = status,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = expiresAtUtc.Value,
                VerificationAttempts = 0
            };

            _verifications[(tenantId, request.OrderId)] = verificationRecord;
        }

        var assessment = new CodFraudRiskAssessment(
            tenantId,
            request.OrderId,
            finalScore,
            tier,
            status,
            verificationCode,
            expiresAtUtc,
            factors,
            recommendation,
            DateTime.UtcNow);

        return Task.FromResult(assessment);
    }

    public Task<CodVerificationResult> VerifyOrderAsync(
        Guid tenantId,
        Guid orderId,
        string verificationCode,
        string channel,
        CancellationToken ct = default)
    {
        if (!_verifications.TryGetValue((tenantId, orderId), out var record))
        {
            return Task.FromResult(CodVerificationResult.Failed(
                orderId,
                CodDeliveryVerificationStatus.FailedVerification,
                "No active COD verification token found for this order."));
        }

        if (DateTime.UtcNow > record.ExpiresAtUtc)
        {
            record.Status = CodDeliveryVerificationStatus.FailedVerification;
            return Task.FromResult(CodVerificationResult.Failed(
                orderId,
                CodDeliveryVerificationStatus.FailedVerification,
                "COD verification token has expired."));
        }

        record.VerificationAttempts++;

        var cleanInput = verificationCode?.Trim() ?? string.Empty;
        if (string.Equals(cleanInput, record.VerificationCode, StringComparison.OrdinalIgnoreCase))
        {
            record.Status = CodDeliveryVerificationStatus.VerifiedByCustomer;
            record.VerifiedChannel = channel;
            record.VerifiedAtUtc = DateTime.UtcNow;

            return Task.FromResult(CodVerificationResult.Succeeded(
                orderId,
                channel,
                $"Order verified successfully via {channel}. Risk hold lifted."));
        }

        if (record.VerificationAttempts >= 3)
        {
            record.Status = CodDeliveryVerificationStatus.RejectedFraud;
            return Task.FromResult(CodVerificationResult.Failed(
                orderId,
                CodDeliveryVerificationStatus.RejectedFraud,
                "Maximum verification attempts exceeded. Order flagged as RejectedFraud."));
        }

        return Task.FromResult(CodVerificationResult.Failed(
            orderId,
            CodDeliveryVerificationStatus.HighRiskPendingCall,
            $"Invalid verification code. {3 - record.VerificationAttempts} attempts remaining."));
    }

    public Task<CodVerificationRecord?> GetVerificationStatusAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken ct = default)
    {
        _verifications.TryGetValue((tenantId, orderId), out var record);
        return Task.FromResult(record);
    }

    private static double CalculateShannonEntropy(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0.0;
        var clean = text.Trim().ToLowerInvariant();
        var counts = new Dictionary<char, int>();
        foreach (var c in clean)
        {
            counts[c] = counts.GetValueOrDefault(c) + 1;
        }

        double entropy = 0.0;
        double len = clean.Length;
        foreach (var count in counts.Values)
        {
            double p = count / len;
            entropy -= p * Math.Log2(p);
        }
        return entropy;
    }

    private static string GenerateVerificationToken()
    {
        // 6-digit numeric token
        int num = RandomNumberGenerator.GetInt32(100000, 1000000);
        return num.ToString("D6");
    }
}
