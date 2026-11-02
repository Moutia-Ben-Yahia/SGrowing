using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Commerce.FraudFirewall;

public sealed record FraudRule(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("riskWeight")] int RiskWeight,
    [property: JsonPropertyName("ruleType")] string RuleType,
    [property: JsonPropertyName("isActive")] bool IsActive
);

public sealed record EvaluateTransactionRequest(
    [property: JsonPropertyName("transactionId")] string TransactionId,
    [property: JsonPropertyName("amountUsd")] decimal AmountUsd,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("customerEmail")] string CustomerEmail,
    [property: JsonPropertyName("ipAddress")] string IpAddress,
    [property: JsonPropertyName("cardCountry")] string CardCountry,
    [property: JsonPropertyName("billingCountry")] string BillingCountry,
    [property: JsonPropertyName("deviceFingerprint")] string DeviceFingerprint
);

public sealed record TransactionRiskEvaluation(
    [property: JsonPropertyName("transactionId")] string TransactionId,
    [property: JsonPropertyName("amountUsd")] decimal AmountUsd,
    [property: JsonPropertyName("customerEmail")] string CustomerEmail,
    [property: JsonPropertyName("riskScore")] int RiskScore,
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("triggeredRules")] IReadOnlyList<string> TriggeredRules,
    [property: JsonPropertyName("evaluatedAtUtc")] DateTime EvaluatedAtUtc
);

public sealed record FraudFirewallStats(
    [property: JsonPropertyName("totalEvaluated")] int TotalEvaluated,
    [property: JsonPropertyName("allowedCount")] int AllowedCount,
    [property: JsonPropertyName("challengedCount")] int ChallengedCount,
    [property: JsonPropertyName("blockedCount")] int BlockedCount,
    [property: JsonPropertyName("blockRatePercent")] double BlockRatePercent,
    [property: JsonPropertyName("averageRiskScore")] double AverageRiskScore
);
