using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.AntiFraud;

public class CodAntiFraudEngine(SGrowingDbContext db) : ICodAntiFraudEngine
{
    private static readonly Regex RepeatedDigitsRegex = new(@"(\d)\1{5,}", RegexOptions.Compiled);
    private static readonly Regex GibberishRegex = new(@"[a-zA-Z]{18,}|(asdf|qwer|test|fake|null|none)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<CodFraudEvaluationResult> EvaluateOrderRiskAsync(
        Guid tenantId,
        CodFraudEvaluationRequest req,
        CancellationToken ct = default)
    {
        var factors = new List<CodRiskFactor>();
        int score = 0;

        var cleanPhone = Regex.Replace(req.CustomerPhone ?? "", @"[^\d+]", "");

        // 1. Phone number sanity & pattern check
        if (string.IsNullOrWhiteSpace(cleanPhone) || cleanPhone.Length < 8)
        {
            factors.Add(new CodRiskFactor("INVALID_PHONE_LENGTH", "Phone number is too short or missing.", 40, "High"));
            score += 40;
        }
        else if (RepeatedDigitsRegex.IsMatch(cleanPhone))
        {
            factors.Add(new CodRiskFactor("REPEATED_PHONE_DIGITS", "Phone number contains 6+ repeated identical digits (likely dummy phone).", 35, "High"));
            score += 35;
        }

        // 2. Synthetic address & gibberish detection
        var address = req.DeliveryAddress ?? "";
        if (address.Trim().Length < 8)
        {
            factors.Add(new CodRiskFactor("VAGUE_STREET_ADDRESS", "Street address has fewer than 8 characters, insufficient for courier delivery.", 30, "Medium"));
            score += 30;
        }
        else if (GibberishRegex.IsMatch(address) || GibberishRegex.IsMatch(req.CustomerName ?? ""))
        {
            factors.Add(new CodRiskFactor("GIBBERISH_KEYSTROKES", "Detected random keyboard mash or test phrases in address/name.", 40, "Critical"));
            score += 40;
        }

        if (string.IsNullOrWhiteSpace(req.DeliveryCity))
        {
            factors.Add(new CodRiskFactor("MISSING_DESTINATION_CITY", "Delivery destination city or municipality is missing.", 20, "Medium"));
            score += 20;
        }

        // 3. High order value / bulk quantity anomaly for COD
        if (req.OrderTotal > 500m)
        {
            factors.Add(new CodRiskFactor("HIGH_COD_VALUE_EXPOSURE", $"Order total ({req.OrderTotal:C0}) exceeds safe doorstep cash limit ($500).", 25, "Medium"));
            score += 25;
        }

        if (req.ItemsCount >= 10)
        {
            factors.Add(new CodRiskFactor("BULK_ITEM_QUANTITY", "Bulk item count (10+ units) ordered via unconfirmed COD.", 20, "Low"));
            score += 20;
        }

        // 4. Historical customer refusal / return velocity check in DB
        if (!string.IsNullOrWhiteSpace(cleanPhone))
        {
            var pastOrders = await db.CodOrders
                .Where(o => o.TenantId == tenantId && o.PhoneNumber == cleanPhone)
                .ToListAsync(ct);

            if (pastOrders.Count > 0)
            {
                var returnedCount = pastOrders.Count(o => o.Status == CodOrderStatus.Returned);
                var deliveredCount = pastOrders.Count(o => o.Status == CodOrderStatus.Delivered);

                if (returnedCount >= 2 && deliveredCount == 0)
                {
                    factors.Add(new CodRiskFactor("SERIAL_REFUSAL_HISTORY", $"Customer has {returnedCount} previous returns/refusals and 0 accepted deliveries.", 50, "Critical"));
                    score += 50;
                }
                else if (returnedCount > deliveredCount)
                {
                    factors.Add(new CodRiskFactor("HIGH_RETURN_RATIO", "Customer has more rejected doorstep deliveries than accepted packages.", 30, "High"));
                    score += 30;
                }
                else if (deliveredCount >= 2 && returnedCount == 0)
                {
                    // Positive credit for trusted buyer
                    score = Math.Max(0, score - 20);
                }
            }
        }

        int finalScore = Math.Clamp(score, 0, 100);

        CodRiskTier tier;
        CodFraudAction action;
        string summary;

        if (finalScore >= 70)
        {
            tier = CodRiskTier.Fraudulent;
            action = CodFraudAction.AutoReject;
            summary = "High probability of fake or malicious COD order. Recommend auto-rejection to prevent return shipping fees.";
        }
        else if (finalScore >= 45)
        {
            tier = CodRiskTier.HighRisk;
            action = CodFraudAction.RequirePrepaymentDeposit;
            summary = "High return risk detected. Require a small advance micro-deposit (e.g. shipping fee) before fulfillment.";
        }
        else if (finalScore >= 25)
        {
            tier = CodRiskTier.Moderate;
            action = CodFraudAction.RequireSmsOtpConfirmation;
            summary = "Moderate risk. Trigger automated SMS OTP / WhatsApp verification before warehouse packing.";
        }
        else
        {
            tier = CodRiskTier.Safe;
            action = CodFraudAction.AutoApprove;
            summary = "Clean profile. Order meets all deliverability thresholds for immediate carrier dispatch.";
        }

        return new CodFraudEvaluationResult(
            RiskScore: finalScore,
            Tier: tier,
            RecommendedAction: action,
            TriggeredRules: factors,
            AssessmentSummary: summary,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }
}
