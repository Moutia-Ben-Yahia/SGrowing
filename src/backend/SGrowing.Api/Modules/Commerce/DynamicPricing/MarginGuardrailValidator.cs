namespace SGrowing.Api.Modules.Commerce.DynamicPricing;

#region Models & DTOs

/// <summary>
/// Cost and profit margin profile used to calculate non-negotiable floor prices
/// and protect against unprofitable transactions or predatory pricing traps.
/// </summary>
public record MarginCostProfile(
    decimal Cogs,
    decimal EstimatedShippingCost = 0m,
    decimal PaymentGatewayFixedFee = 0m,
    decimal PaymentGatewayFeePercentage = 0m, // E.g., 0.025 for 2.5%
    decimal MinMarginAmount = 0m,
    decimal MinMarginPercentage = 0.12m,       // Default 12% minimum gross margin hurdle
    decimal? ReferenceMsrp = null,
    decimal MaxDiscountFromMsrpPercentage = 0.50m); // 50% max discount from MSRP to guard against rogue scrapers

/// <summary>
/// Specific margin violation or pricing anomaly detected by the guardrail engine.
/// </summary>
public record MarginViolationAlert(
    string AlertCode,
    string Severity,
    string Message,
    decimal ProposedPrice,
    decimal FloorPrice,
    decimal VarianceAmount);

/// <summary>
/// Detailed evaluation of proposed pricing against non-negotiable business margin guardrails.
/// </summary>
public record MarginValidationResult(
    bool IsValid,
    decimal ProposedPrice,
    decimal FloorPrice,
    decimal BreakEvenCost,
    decimal EstimatedNetMargin,
    decimal EstimatedMarginPercentage,
    decimal RecommendedClampedPrice,
    IReadOnlyList<MarginViolationAlert> Alerts);

/// <summary>
/// Exception thrown when proposed price breaches a non-negotiable margin floor during strict enforcement.
/// </summary>
public class MarginGuardrailViolationException : InvalidOperationException
{
    public MarginValidationResult ValidationResult { get; }

    public MarginGuardrailViolationException(MarginValidationResult validationResult)
        : base($"Proposed price ({validationResult.ProposedPrice:C2}) violates margin floor ({validationResult.FloorPrice:C2}). Violations: {string.Join("; ", validationResult.Alerts.Select(a => $"{a.AlertCode}: {a.Message}"))}")
    {
        ValidationResult = validationResult;
    }
}

#endregion

/// <summary>
/// Contract for non-negotiable margin guardrail validation, break-even analysis, and floor price protection.
/// </summary>
public interface IMarginGuardrailValidator
{
    decimal CalculateFloorPrice(MarginCostProfile profile);
    MarginValidationResult Validate(decimal proposedPrice, MarginCostProfile profile);
    decimal EnforceGuardrail(decimal proposedPrice, MarginCostProfile profile);
}

/// <summary>
/// Validates proposed prices against COGS, estimated fulfillment costs, payment gateway fees,
/// and profit margin hurdles to prevent loss-leader bleeding or predatory undercutting.
/// </summary>
public sealed class MarginGuardrailValidator : IMarginGuardrailValidator
{
    /// <summary>
    /// Calculates the non-negotiable minimum price floor:
    /// FloorPrice = (COGS + EstimatedShipping + GatewayFixedFee + MinMarginAmount) / (1 - GatewayFee% - MinMargin%)
    /// Ensures that after paying gateway processing and fulfillment, the merchant receives at least MinMarginAmount
    /// and achieves at least MinMarginPercentage.
    /// </summary>
    public decimal CalculateFloorPrice(MarginCostProfile profile)
    {
        if (profile.Cogs < 0m)
            throw new ArgumentOutOfRangeException(nameof(profile.Cogs), "COGS cannot be negative.");

        decimal directCosts = profile.Cogs + Math.Max(0m, profile.EstimatedShippingCost) + Math.Max(0m, profile.PaymentGatewayFixedFee);
        decimal minMarginAmt = Math.Max(0m, profile.MinMarginAmount);

        // Safeguard gateway fee percentage within sensible bounds [0, 0.40]
        decimal gatewayPct = Math.Clamp(profile.PaymentGatewayFeePercentage, 0m, 0.40m);
        decimal minMarginPct = Math.Clamp(profile.MinMarginPercentage, 0m, 0.80m);

        // 1. Floor based on fixed dollar minimum margin
        decimal floorFromAmount = (gatewayPct < 1.0m)
            ? (directCosts + minMarginAmt) / (1.0m - gatewayPct)
            : directCosts + minMarginAmt;

        // 2. Floor based on required minimum percentage margin hurdle:
        // Price * (1 - gatewayPct) - directCosts >= Price * minMarginPct
        // Price * (1 - gatewayPct - minMarginPct) >= directCosts
        decimal denominatorPct = 1.0m - gatewayPct - minMarginPct;
        decimal floorFromPercentage = denominatorPct > 0.05m
            ? directCosts / denominatorPct
            : directCosts / 0.50m;

        // The effective floor is the highest of both constraints
        decimal effectiveFloor = Math.Max(floorFromAmount, floorFromPercentage);

        return Math.Round(effectiveFloor, 2);
    }

    /// <summary>
    /// Evaluates a proposed price against cost structures and flags any violations.
    /// </summary>
    public MarginValidationResult Validate(decimal proposedPrice, MarginCostProfile profile)
    {
        decimal floorPrice = CalculateFloorPrice(profile);
        decimal gatewayFee = (proposedPrice * Math.Clamp(profile.PaymentGatewayFeePercentage, 0m, 0.40m))
                             + Math.Max(0m, profile.PaymentGatewayFixedFee);

        decimal breakEvenCost = Math.Round(profile.Cogs + Math.Max(0m, profile.EstimatedShippingCost) + gatewayFee, 2);
        decimal netMargin = Math.Round(proposedPrice - breakEvenCost, 2);
        decimal marginPercentage = proposedPrice > 0m
            ? Math.Round((netMargin / proposedPrice) * 100m, 2)
            : -100m;

        var alerts = new List<MarginViolationAlert>();

        // 1. Direct COGS breach (selling below cost of product)
        if (proposedPrice < profile.Cogs)
        {
            alerts.Add(new MarginViolationAlert(
                AlertCode: "BELOW_COGS",
                Severity: "Critical",
                Message: $"Proposed price ({proposedPrice:C2}) is lower than direct product unit cost ({profile.Cogs:C2}). Immediate product loss.",
                ProposedPrice: proposedPrice,
                FloorPrice: floorPrice,
                VarianceAmount: profile.Cogs - proposedPrice));
        }

        // 2. Break-even cost breach (negative contribution margin)
        if (proposedPrice < breakEvenCost)
        {
            alerts.Add(new MarginViolationAlert(
                AlertCode: "NEGATIVE_CONTRIBUTION_MARGIN",
                Severity: "Critical",
                Message: $"Proposed price ({proposedPrice:C2}) is below break-even cost ({breakEvenCost:C2}). Net contribution margin is negative ({netMargin:C2}).",
                ProposedPrice: proposedPrice,
                FloorPrice: floorPrice,
                VarianceAmount: breakEvenCost - proposedPrice));
        }

        // 3. Below non-negotiable floor price
        if (proposedPrice < floorPrice)
        {
            alerts.Add(new MarginViolationAlert(
                AlertCode: "BELOW_MARGIN_FLOOR",
                Severity: "Critical",
                Message: $"Proposed price ({proposedPrice:C2}) dips below the required floor price ({floorPrice:C2}). Required buffer deficit: {floorPrice - proposedPrice:C2}.",
                ProposedPrice: proposedPrice,
                FloorPrice: floorPrice,
                VarianceAmount: floorPrice - proposedPrice));
        }

        // 4. Below minimum margin percentage hurdle
        decimal targetPercentage = profile.MinMarginPercentage * 100m;
        if (marginPercentage < targetPercentage)
        {
            alerts.Add(new MarginViolationAlert(
                AlertCode: "BELOW_PERCENTAGE_HURDLE",
                Severity: "High",
                Message: $"Net margin of {marginPercentage:F1}% fails to meet the minimum required hurdle of {targetPercentage:F1}%.",
                ProposedPrice: proposedPrice,
                FloorPrice: floorPrice,
                VarianceAmount: Math.Max(0m, floorPrice - proposedPrice)));
        }

        // 5. Predatory undercutting or rogue scraping glitch detection (excessive discount from MSRP)
        if (profile.ReferenceMsrp.HasValue && profile.ReferenceMsrp.Value > 0m)
        {
            decimal maxDiscountLimit = profile.ReferenceMsrp.Value * (1.0m - profile.MaxDiscountFromMsrpPercentage);
            if (proposedPrice < maxDiscountLimit)
            {
                alerts.Add(new MarginViolationAlert(
                    AlertCode: "PREDATORY_UNDERCUT_RISK",
                    Severity: "High",
                    Message: $"Proposed price ({proposedPrice:C2}) represents an extreme discount exceeding {profile.MaxDiscountFromMsrpPercentage:P0} from MSRP ({profile.ReferenceMsrp.Value:C2}). Potential scraping anomaly or predatory dumping.",
                    ProposedPrice: proposedPrice,
                    FloorPrice: floorPrice,
                    VarianceAmount: maxDiscountLimit - proposedPrice));
            }
        }

        bool isValid = alerts.Count == 0;
        decimal recommendedClamped = isValid ? proposedPrice : Math.Max(proposedPrice, floorPrice);

        return new MarginValidationResult(
            IsValid: isValid,
            ProposedPrice: proposedPrice,
            FloorPrice: floorPrice,
            BreakEvenCost: breakEvenCost,
            EstimatedNetMargin: netMargin,
            EstimatedMarginPercentage: marginPercentage,
            RecommendedClampedPrice: recommendedClamped,
            Alerts: alerts);
    }

    /// <summary>
    /// Enforces the margin guardrail: returns valid proposed price, or throws if non-negotiable guardrails are breached.
    /// </summary>
    public decimal EnforceGuardrail(decimal proposedPrice, MarginCostProfile profile)
    {
        var result = Validate(proposedPrice, profile);
        if (!result.IsValid)
        {
            throw new MarginGuardrailViolationException(result);
        }
        return proposedPrice;
    }
}
