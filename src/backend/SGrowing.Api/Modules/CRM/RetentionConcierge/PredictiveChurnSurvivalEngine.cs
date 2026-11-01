namespace SGrowing.Api.Modules.CRM.RetentionConcierge;

/// <summary>
/// Survival analysis engine modeling customer churn probability using Cox Proportional Hazards
/// and Weibull survival distributions to compute time-to-churn and risk factor attribution.
/// </summary>
public sealed class PredictiveChurnSurvivalEngine
{
    private const double BaselineHazardRate = 0.005; // Base daily hazard
    private const double WeibullShape = 1.15;       // Increasing hazard over time

    // Cox proportional hazard regression coefficients
    private const double BetaRecency = 0.35;
    private const double BetaNegativeSentiment = 1.25;
    private const double BetaReturnRate = 0.85;
    private const double BetaAppInactivity = 0.60;
    private const double BetaUnopenedEmails = 0.30;

    /// <summary>
    /// Predicts survival probabilities, median days until churn, and risk factors for a customer.
    /// </summary>
    public ChurnSurvivalPrediction PredictChurn(CustomerEngagementProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // Normalize features
        var xRecency = Math.Max(0.0, profile.DaysSinceLastOrder / 30.0);
        var xNegativeSentiment = Math.Max(0.0, -profile.SupportTicketSentimentScore);
        var xReturnRate = Math.Clamp(profile.ReturnRateRatio, 0.0, 1.0);
        var xAppInactivity = Math.Clamp((6.0 - profile.AppSessionsLast30Days) / 6.0, 0.0, 1.0);
        var xUnopenedEmails = Math.Clamp(profile.UnopenedMarketingEmailsCount / 10.0, 0.0, 1.0);

        // Linear predictor: beta^T * x
        var logHazardRatio = (BetaRecency * xRecency) +
                             (BetaNegativeSentiment * xNegativeSentiment) +
                             (BetaReturnRate * xReturnRate) +
                             (BetaAppInactivity * xAppInactivity) +
                             (BetaUnopenedEmails * xUnopenedEmails);

        var hazardMultiplier = Math.Exp(logHazardRatio);

        // Survival function: S(t) = exp(-lambda_0 * t^gamma * exp(beta^T * x))
        var s30 = Math.Exp(-BaselineHazardRate * Math.Pow(30.0, WeibullShape) * hazardMultiplier);
        var s90 = Math.Exp(-BaselineHazardRate * Math.Pow(90.0, WeibullShape) * hazardMultiplier);

        s30 = Math.Clamp(s30, 0.0, 1.0);
        s90 = Math.Clamp(s90, 0.0, 1.0);

        // Hazard score over 30 days: 1 - S(30)
        var hazardScore = Math.Round(1.0 - s30, 4);

        // Estimate median days until churn: S(t) = 0.50 => t = (ln(2) / (lambda_0 * M))^(1/gamma)
        var ln2 = Math.Log(2.0);
        var denom = BaselineHazardRate * hazardMultiplier;
        var estDays = denom > 0.0 ? Math.Pow(ln2 / denom, 1.0 / WeibullShape) : 365.0;
        var clampedDays = (int)Math.Clamp(Math.Round(estDays), 1.0, 730.0);

        var riskLevel = hazardScore switch
        {
            >= 0.75 => ChurnRiskLevel.CriticalImminent,
            >= 0.50 => ChurnRiskLevel.High,
            >= 0.20 => ChurnRiskLevel.Moderate,
            _ => ChurnRiskLevel.Minimal
        };

        // Identify contributing risk factors
        var factors = new List<string>();
        if (profile.DaysSinceLastOrder >= 60)
        {
            factors.Add($"Severe Order Recency Drop ({profile.DaysSinceLastOrder} days since last purchase)");
        }
        else if (profile.DaysSinceLastOrder >= 35)
        {
            factors.Add($"Noticeable Purchase Cadence Delay ({profile.DaysSinceLastOrder} days)");
        }

        if (profile.SupportTicketSentimentScore <= -0.30)
        {
            factors.Add($"High Frustration in Support Interactions (Sentiment: {profile.SupportTicketSentimentScore:F2})");
        }

        if (profile.ReturnRateRatio >= 0.25)
        {
            factors.Add($"Elevated Product Return Rate ({profile.ReturnRateRatio * 100:F0}%)");
        }

        if (profile.AppSessionsLast30Days <= 2)
        {
            factors.Add($"Significant Mobile App Inactivity ({profile.AppSessionsLast30Days} sessions in 30 days)");
        }

        if (profile.UnopenedMarketingEmailsCount >= 5)
        {
            factors.Add($"Ignored Email Engagement ({profile.UnopenedMarketingEmailsCount} unopened emails)");
        }

        if (factors.Count == 0)
        {
            factors.Add("Nominal Engagement Profile");
        }

        return new ChurnSurvivalPrediction(
            CustomerId: profile.CustomerId,
            HazardScore: hazardScore,
            RiskLevel: riskLevel,
            EstimatedDaysUntilChurn: clampedDays,
            SurvivalProbabilityAt30Days: Math.Round(s30, 4),
            SurvivalProbabilityAt90Days: Math.Round(s90, 4),
            ContributingRiskFactors: factors,
            CalculatedAtUtc: DateTime.UtcNow
        );
    }
}
