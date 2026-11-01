using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.CRM.RetentionConcierge;

/// <summary>
/// Contextual Multi-Armed Bandit engine using Thompson Sampling to optimize
/// retention goodwill offers and maximize retained Customer Lifetime Value (LTV).
/// </summary>
public sealed class RetentionOfferBanditService
{
    private sealed class ArmState
    {
        public required string ArmId { get; init; }
        public required DelightIncentiveType IncentiveType { get; init; }
        public double Alpha { get; set; } = 2.0; // Successes prior
        public double Beta { get; set; } = 2.0;  // Failures prior
        public int TotalTrials { get; set; }
        public decimal TotalLtvRetainedDzd { get; set; }
    }

    private readonly ConcurrentDictionary<DelightIncentiveType, ArmState> _arms = new();
    private readonly System.Threading.Lock _lock = new();

    public RetentionOfferBanditService()
    {
        InitializeDefaultArms();
    }

    private void InitializeDefaultArms()
    {
        _arms[DelightIncentiveType.ExpeditedShippingWaiver] = new ArmState
        {
            ArmId = "ARM-EXPEDITED-SHIP",
            IncentiveType = DelightIncentiveType.ExpeditedShippingWaiver,
            Alpha = 8.0,
            Beta = 3.0
        };

        _arms[DelightIncentiveType.BonusLoyaltyCredits] = new ArmState
        {
            ArmId = "ARM-LOYALTY-CREDIT",
            IncentiveType = DelightIncentiveType.BonusLoyaltyCredits,
            Alpha = 6.0,
            Beta = 4.0
        };

        _arms[DelightIncentiveType.PersonalizedDiscountCode] = new ArmState
        {
            ArmId = "ARM-DISCOUNT-15PCT",
            IncentiveType = DelightIncentiveType.PersonalizedDiscountCode,
            Alpha = 9.0,
            Beta = 3.0
        };

        _arms[DelightIncentiveType.ComplimentaryGiftBasket] = new ArmState
        {
            ArmId = "ARM-GIFT-BASKET",
            IncentiveType = DelightIncentiveType.ComplimentaryGiftBasket,
            Alpha = 5.0,
            Beta = 2.0
        };

        _arms[DelightIncentiveType.ExecutiveAccountManagerCall] = new ArmState
        {
            ArmId = "ARM-VIP-EXEC-CALL",
            IncentiveType = DelightIncentiveType.ExecutiveAccountManagerCall,
            Alpha = 4.0,
            Beta = 2.0
        };

        _arms[DelightIncentiveType.ExclusivePrivateSaleAccess] = new ArmState
        {
            ArmId = "ARM-PRIVATE-SALE",
            IncentiveType = DelightIncentiveType.ExclusivePrivateSaleAccess,
            Alpha = 5.0,
            Beta = 3.0
        };
    }

    /// <summary>
    /// Selects optimal retention incentive for the customer tier using Thompson Sampling.
    /// </summary>
    public DelightIncentiveType SelectBestIncentive(CustomerTier tier)
    {
        // Eligible incentives by customer tier
        DelightIncentiveType[] eligibleTypes = tier switch
        {
            CustomerTier.PlatinumVIP => [
                DelightIncentiveType.ExecutiveAccountManagerCall,
                DelightIncentiveType.ComplimentaryGiftBasket,
                DelightIncentiveType.ExclusivePrivateSaleAccess,
                DelightIncentiveType.PersonalizedDiscountCode
            ],
            CustomerTier.Gold => [
                DelightIncentiveType.ComplimentaryGiftBasket,
                DelightIncentiveType.PersonalizedDiscountCode,
                DelightIncentiveType.ExpeditedShippingWaiver,
                DelightIncentiveType.BonusLoyaltyCredits
            ],
            CustomerTier.Silver => [
                DelightIncentiveType.ExpeditedShippingWaiver,
                DelightIncentiveType.BonusLoyaltyCredits,
                DelightIncentiveType.PersonalizedDiscountCode
            ],
            _ => [
                DelightIncentiveType.ExpeditedShippingWaiver,
                DelightIncentiveType.BonusLoyaltyCredits
            ]
        };

        DelightIncentiveType bestArm = eligibleTypes[0];
        var bestSampleScore = -1.0;

        foreach (var incType in eligibleTypes)
        {
            if (!_arms.TryGetValue(incType, out var state))
            {
                continue;
            }

            double sample;
            lock (_lock)
            {
                sample = SampleBeta(state.Alpha, state.Beta);
            }

            if (sample > bestSampleScore)
            {
                bestSampleScore = sample;
                bestArm = incType;
            }
        }

        return bestArm;
    }

    /// <summary>
    /// Records the real-world outcome of a dispatched retention offer to refine the bandit model.
    /// </summary>
    public void RecordOutcome(DelightIncentiveType incentiveType, bool converted, decimal retainedLtvDzd = 0m)
    {
        if (!_arms.TryGetValue(incentiveType, out var state))
        {
            return;
        }

        lock (_lock)
        {
            state.TotalTrials++;
            if (converted)
            {
                state.Alpha += 1.0;
                state.TotalLtvRetainedDzd += retainedLtvDzd;
            }
            else
            {
                state.Beta += 1.0;
            }
        }
    }

    /// <summary>
    /// Retrieves current performance statistics for all bandit incentive arms.
    /// </summary>
    public IReadOnlyList<BanditArmStats> GetArmStatistics()
    {
        lock (_lock)
        {
            return _arms.Values.Select(a =>
            {
                var estConversion = (a.Alpha + a.Beta) > 0 ? a.Alpha / (a.Alpha + a.Beta) : 0.0;
                return new BanditArmStats(
                    ArmId: a.ArmId,
                    IncentiveType: a.IncentiveType,
                    AlphaSuccesses: Math.Round(a.Alpha, 1),
                    BetaFailures: Math.Round(a.Beta, 1),
                    TotalTrials: a.TotalTrials,
                    TotalLtvRetainedDzd: a.TotalLtvRetainedDzd,
                    EstimatedConversionRate: Math.Round(estConversion, 4)
                );
            }).ToList();
        }
    }

    /// <summary>
    /// Samples from a Beta distribution Beta(alpha, beta) using Johnk's generator or Gamma ratio.
    /// </summary>
    private static double SampleBeta(double alpha, double beta)
    {
        var x = SampleStandardGamma(alpha);
        var y = SampleStandardGamma(beta);
        if (x + y == 0.0) return 0.5;
        return x / (x + y);
    }

    /// <summary>
    /// Marsaglia and Tsang method for Gamma variates.
    /// </summary>
    private static double SampleStandardGamma(double shape)
    {
        if (shape < 1.0)
        {
            var u = Random.Shared.NextDouble();
            return SampleStandardGamma(shape + 1.0) * Math.Pow(u, 1.0 / shape);
        }

        var d = shape - (1.0 / 3.0);
        var c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double z;
            double v;
            do
            {
                z = SampleStandardNormal();
                v = 1.0 + (c * z);
            } while (v <= 0.0);

            v = v * v * v;
            var u = Random.Shared.NextDouble();

            if (u < 1.0 - (0.0331 * z * z * z * z))
            {
                return d * v;
            }

            if (Math.Log(u) < (0.5 * z * z) + d - (d * v) + (d * Math.Log(v)))
            {
                return d * v;
            }
        }
    }

    private static double SampleStandardNormal()
    {
        var u1 = 1.0 - Random.Shared.NextDouble();
        var u2 = 1.0 - Random.Shared.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
