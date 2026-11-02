namespace SGrowing.Api.Modules.Finance.FxHedging;

/// <summary>
/// Autonomous FX triangular arbitrage scanner detecting cross-currency price misalignments
/// across EUR, USD, DZD, and AED currency pairs.
/// </summary>
public sealed class TriangularArbitrageDetectorService
{
    private const decimal MinExecutableSpreadBps = 15.0m; // 15 basis points to cover transaction costs

    public TriangularArbitrageOpportunity DetectTriangularMisalignment(
        CurrencyCode baseCurrency,
        CurrencyCode intermediate,
        CurrencyCode quote,
        decimal rateBaseToIntermediate,
        decimal rateIntermediateToQuote,
        decimal marketRateBaseToQuote)
    {
        if (rateBaseToIntermediate <= 0 || rateIntermediateToQuote <= 0 || marketRateBaseToQuote <= 0)
        {
            throw new ArgumentOutOfRangeException("All FX rates must be positive.");
        }

        // Synthetic rate = Rate(A/B) * Rate(B/C)
        var syntheticRate = rateBaseToIntermediate * rateIntermediateToQuote;

        // Discrepancy in Basis Points (bps): |Synthetic / Market - 1| * 10,000
        var ratio = syntheticRate / marketRateBaseToQuote;
        var diff = Math.Abs(ratio - 1.0m);
        var discrepancyBps = diff * 10000.0m;

        var isExecutable = discrepancyBps >= MinExecutableSpreadBps;

        return new TriangularArbitrageOpportunity(
            OpportunityId: $"ARB-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Base: baseCurrency,
            Intermediate: intermediate,
            Quote: quote,
            RateBaseToIntermediate: rateBaseToIntermediate,
            RateIntermediateToQuote: rateIntermediateToQuote,
            MarketRateBaseToQuote: marketRateBaseToQuote,
            SyntheticRateBaseToQuote: Math.Round(syntheticRate, 6),
            DiscrepancyBps: Math.Round(discrepancyBps, 1),
            IsExecutableSpread: isExecutable,
            DetectedAtUtc: DateTime.UtcNow
        );
    }
}
