using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.CRM.RetentionConcierge;

/// <summary>
/// Autonomous retention concierge that orchestrates predictive churn detection,
/// selects optimal delight incentives, and dispatches proactive customer care packages.
/// </summary>
public sealed class ProactiveDelightConciergeService
{
    private readonly PredictiveChurnSurvivalEngine _survivalEngine;
    private readonly RetentionOfferBanditService _banditService;
    private readonly ConcurrentDictionary<string, DelightAction> _actions = new(StringComparer.OrdinalIgnoreCase);

    public ProactiveDelightConciergeService(
        PredictiveChurnSurvivalEngine survivalEngine,
        RetentionOfferBanditService banditService)
    {
        _survivalEngine = survivalEngine ?? throw new ArgumentNullException(nameof(survivalEngine));
        _banditService = banditService ?? throw new ArgumentNullException(nameof(banditService));
    }

    /// <summary>
    /// Evaluates a customer profile, calculates survival hazard, and autonomously dispatches delight intervention if hazard threshold is met.
    /// </summary>
    public DelightAction? EvaluateAndDispatchDelight(DispatchDelightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var profile = request.Profile;

        var prediction = _survivalEngine.PredictChurn(profile);

        // Intervene if hazard score is high (>= 0.50) or if force dispatch is explicitly requested
        if (prediction.HazardScore < 0.50 && !request.ForceDispatch)
        {
            return null; // Customer is in nominal retention zone; no intervention needed
        }

        // Select optimal incentive via contextual Thompson Sampling
        var incentiveType = _banditService.SelectBestIncentive(profile.Tier);

        var (costDzd, liftDzd, discountPct) = CalculateIncentiveEconomics(incentiveType, profile.AverageOrderValueDzd);
        var channel = DetermineOutreachChannel(profile.Tier);

        var actionId = $"ACT-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        var couponCode = $"RET-{profile.Tier.ToString()[..3].ToUpperInvariant()}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var action = new DelightAction(
            ActionId: actionId,
            CustomerId: profile.CustomerId,
            CustomerTier: profile.Tier,
            IncentiveType: incentiveType,
            EstimatedCostDzd: costDzd,
            ExpectedLtvLiftDzd: liftDzd,
            DiscountPercent: discountPct,
            CouponCode: couponCode,
            Channel: channel,
            Status: DelightActionStatus.Dispatched,
            GeneratedAtUtc: DateTime.UtcNow,
            DispatchedAtUtc: DateTime.UtcNow
        );

        _actions[actionId] = action;
        return action;
    }

    /// <summary>
    /// Marks a dispatched retention offer as redeemed by the customer and feeds conversion data back to the bandit.
    /// </summary>
    public bool RedeemAction(string actionId, decimal retainedLtvDzd = 0m)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        if (!_actions.TryGetValue(actionId, out var existingAction))
        {
            return false;
        }

        if (existingAction.Status == DelightActionStatus.Redeemed)
        {
            return true; // Already processed
        }

        var redeemed = existingAction with
        {
            Status = DelightActionStatus.Redeemed,
            RedeemedAtUtc = DateTime.UtcNow
        };

        _actions[actionId] = redeemed;
        _banditService.RecordOutcome(existingAction.IncentiveType, converted: true, retainedLtvDzd);
        return true;
    }

    /// <summary>
    /// Marks an action as expired without conversion to feed failure signal back to the bandit.
    /// </summary>
    public bool ExpireAction(string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        if (!_actions.TryGetValue(actionId, out var existingAction))
        {
            return false;
        }

        if (existingAction.Status != DelightActionStatus.Dispatched)
        {
            return false;
        }

        var expired = existingAction with
        {
            Status = DelightActionStatus.Expired
        };

        _actions[actionId] = expired;
        _banditService.RecordOutcome(existingAction.IncentiveType, converted: false);
        return true;
    }

    /// <summary>
    /// Retrieves a delight action by ID.
    /// </summary>
    public DelightAction? GetAction(string actionId)
    {
        _actions.TryGetValue(actionId, out var action);
        return action;
    }

    private static (decimal Cost, decimal Lift, decimal Discount) CalculateIncentiveEconomics(
        DelightIncentiveType type,
        decimal averageOrderValueDzd)
    {
        return type switch
        {
            DelightIncentiveType.ExpeditedShippingWaiver => (800m, 15000m, 0m),
            DelightIncentiveType.BonusLoyaltyCredits => (1500m, 22000m, 0m),
            DelightIncentiveType.PersonalizedDiscountCode => (Math.Round(averageOrderValueDzd * 0.12m, 2), 25000m, 12m),
            DelightIncentiveType.ComplimentaryGiftBasket => (6500m, 80000m, 0m),
            DelightIncentiveType.ExecutiveAccountManagerCall => (2500m, 120000m, 0m),
            DelightIncentiveType.ExclusivePrivateSaleAccess => (500m, 45000m, 15m),
            _ => (1000m, 10000m, 5m)
        };
    }

    private static DelightOutreachChannel DetermineOutreachChannel(CustomerTier tier)
    {
        return tier switch
        {
            CustomerTier.PlatinumVIP => DelightOutreachChannel.WhatsApp,
            CustomerTier.Gold => DelightOutreachChannel.WhatsApp,
            CustomerTier.Silver => DelightOutreachChannel.Sms,
            _ => DelightOutreachChannel.InAppPush
        };
    }
}
