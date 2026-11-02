namespace SGrowing.Api.Modules.Finance.CdsIndexTrancheCorrelation;

public interface ICdsIndexTrancheEngine
{
    CdsIndexTrancheEvaluation AnalyzeTranche(CdsIndexTrancheTelemetry telemetry);
}

public class CdsIndexTrancheEngine : ICdsIndexTrancheEngine
{
    public CdsIndexTrancheEvaluation AnalyzeTranche(CdsIndexTrancheTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Index-CDS basis = Spread_index - Mean(Spread_constituents)
        double basis = telemetry.IndexSpreadBasisPoints - telemetry.AverageConstituentCdsSpreadBps;

        bool isBasisNormal = Math.Abs(basis) <= 5.0;
        bool isCorrelationMonotonic = telemetry.BaseCorrelationSlopePerDetachmentPoint >= 0.0 && telemetry.TrancheExpectedLossDensity >= 0.0;
        bool isEquitySolvent = telemetry.EquityTrancheDelta <= 0.85;

        if (Math.Abs(basis) > 7.5)
        {
            regimes.Add("CDX_INDEX_BASIS_ARBITRAGE_DISLOCATION");
            mitigations.Add("Execute negative basis package by selling index protection and purchasing single-name CDS basket or vice-versa.");
        }

        if (!isCorrelationMonotonic)
        {
            regimes.Add("BASE_CORRELATION_SMILE_ARBITRAGE_VIOLATION");
            mitigations.Add("Re-interpolate base correlation curve using monotonic splines (e.g. Kruger or Fritsch-Carlson) to eliminate negative tranche loss densities.");
        }

        if (!isEquitySolvent)
        {
            regimes.Add("EQUITY_TRANCHE_FIRST_LOSS_EXHAUSTION");
            mitigations.Add("Hedge long equity tranche risk via out-of-the-money credit spread swaption puts and adjust subordination attachment points.");
        }

        bool isArbitrageFree = isBasisNormal && isCorrelationMonotonic && isEquitySolvent;

        return new CdsIndexTrancheEvaluation
        {
            TranchePortfolioUid = telemetry.TranchePortfolioUid,
            IndexCdsBasisBps = Math.Round(basis, 2),
            IsIndexCdsBasisNormal = isBasisNormal,
            IsBaseCorrelationMonotonic = isCorrelationMonotonic,
            IsEquityTrancheSolvent = isEquitySolvent,
            IsTranchePricingArbitrageFree = isArbitrageFree,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
