namespace SGrowing.Api.Modules.Commerce.AntiFraud;

public enum CodRiskTier
{
    Safe,
    Moderate,
    HighRisk,
    Fraudulent
}

public enum CodFraudAction
{
    AutoApprove,
    RequireSmsOtpConfirmation,
    RequirePrepaymentDeposit,
    AutoReject
}

public record CodFraudEvaluationRequest(
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    string DeliveryAddress,
    string DeliveryCity,
    decimal OrderTotal,
    int ItemsCount,
    string? IpAddress = null,
    string? UserAgent = null
);

public record CodRiskFactor(
    string RuleId,
    string Description,
    int PointsAdded,
    string Severity // "Low", "Medium", "High", "Critical"
);

public record CodFraudEvaluationResult(
    int RiskScore, // 0 to 100
    CodRiskTier Tier,
    CodFraudAction RecommendedAction,
    List<CodRiskFactor> TriggeredRules,
    string AssessmentSummary,
    DateTime EvaluatedAtUtc
);

public interface ICodAntiFraudEngine
{
    Task<CodFraudEvaluationResult> EvaluateOrderRiskAsync(
        Guid tenantId,
        CodFraudEvaluationRequest req,
        CancellationToken ct = default);
}
