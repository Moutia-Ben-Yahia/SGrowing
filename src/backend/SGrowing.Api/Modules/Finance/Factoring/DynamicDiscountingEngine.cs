using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Finance.Factoring;

/// <summary>
/// Core contract for sliding-scale dynamic discounting calculations and liquidity allocation.
/// </summary>
public interface IDynamicDiscountingEngine
{
    EarlyPaymentOffer CalculateEarlyPaymentOffer(
        InvoicePayable invoice,
        DateTime targetPaymentDateUtc,
        decimal annualAprTarget = 0.24m,
        bool applyDynamicCurve = false);

    IReadOnlyList<EarlyPaymentOffer> GenerateOffersForPayables(
        IEnumerable<InvoicePayable> invoices,
        decimal availableLiquidityDzd,
        DateTime? targetPaymentDateUtc = null,
        decimal annualAprTarget = 0.24m,
        bool applyDynamicCurve = false);

    EarlyPaymentOffer AcceptOffer(string offerId, string supplierTenantId);

    EarlyPaymentOffer? GetOffer(string offerId);

    IReadOnlyList<EarlyPaymentOffer> GetActiveOffers();

    FactoringSummaryResponse GetFactoringSummary();
}

/// <summary>
/// Production-grade implementation of sliding-scale dynamic discounting engine with
/// thread-safe APR yield calculation, liquidity optimization, and dynamic curves.
/// </summary>
public sealed class DynamicDiscountingEngine : IDynamicDiscountingEngine
{
    private const decimal DaysInCommercialYear = 360.0m;
    private readonly ConcurrentDictionary<string, EarlyPaymentOffer> _offers = new();
    private readonly Lock _statsLock = new();

    private decimal _totalInvoicesVolumeDzd;
    private decimal _totalEarlyPaidDzd;
    private decimal _totalBuyerSavingsDzd;
    private int _acceptedOffersCount;

    /// <summary>
    /// Computes a thread-safe sliding-scale early payment offer based on remaining days to maturity.
    /// Formula:
    ///   DiscountRate(t) = AnnualAprTarget * (DaysEarly / 360.0)
    ///   NetPayment = InvoiceAmount * (1.0 - DiscountRate)
    ///   BuyerSavings = InvoiceAmount * DiscountRate
    ///   AnnualizedYield = (DiscountRate / (1.0 - DiscountRate)) * (360.0 / DaysEarly)
    /// </summary>
    public EarlyPaymentOffer CalculateEarlyPaymentOffer(
        InvoicePayable invoice,
        DateTime targetPaymentDateUtc,
        decimal annualAprTarget = 0.24m,
        bool applyDynamicCurve = false)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (!invoice.ApprovedForPayment)
        {
            throw new InvalidOperationException($"Invoice '{invoice.InvoiceId}' is not approved for payment by the buyer.");
        }

        if (invoice.InvoiceAmountDzd <= 0m)
        {
            throw new ArgumentException("Invoice amount must be strictly greater than zero.", nameof(invoice));
        }

        if (annualAprTarget <= 0m || annualAprTarget > 3.0m)
        {
            throw new ArgumentOutOfRangeException(nameof(annualAprTarget), "Annual APR target must be positive and within realistic commercial bounds (e.g. 1% to 300%).");
        }

        var daysEarly = (invoice.DueDateUtc.Date - targetPaymentDateUtc.Date).Days;

        if (daysEarly <= 0)
        {
            throw new ArgumentException(
                $"Target payment date ({targetPaymentDateUtc:yyyy-MM-dd}) must be strictly prior to the invoice due date ({invoice.DueDateUtc:yyyy-MM-dd}). Days early is {daysEarly}.",
                nameof(targetPaymentDateUtc));
        }

        if (daysEarly > invoice.PaymentTermsDays)
        {
            throw new ArgumentException(
                $"Days early ({daysEarly}) cannot exceed the invoice payment terms ({invoice.PaymentTermsDays} days).",
                nameof(targetPaymentDateUtc));
        }

        // Apply dynamic curve if requested (e.g. liquidity-constrained buyer curve or short-window surcharge)
        var effectiveApr = applyDynamicCurve
            ? CalculateDynamicCurveApr(daysEarly, invoice.PaymentTermsDays, annualAprTarget)
            : annualAprTarget;

        // Core sliding scale math
        var discountFraction = effectiveApr * ((decimal)daysEarly / DaysInCommercialYear);
        var discountRatePct = Math.Round(discountFraction * 100m, 4, MidpointRounding.AwayFromZero);

        var buyerSavingsDzd = Math.Round(invoice.InvoiceAmountDzd * discountFraction, 2, MidpointRounding.AwayFromZero);
        var netPaymentAmountDzd = invoice.InvoiceAmountDzd - buyerSavingsDzd;

        // Annualized APR yield formula: (DiscountRate / (1 - DiscountRate)) * (360 / DaysEarly)
        var annualizedYieldFraction = (discountFraction / (1.0m - discountFraction)) * (DaysInCommercialYear / daysEarly);
        var annualizedAprYieldPct = Math.Round(annualizedYieldFraction * 100m, 4, MidpointRounding.AwayFromZero);

        var expirationDateUtc = targetPaymentDateUtc.Date > DateTime.UtcNow.Date
            ? targetPaymentDateUtc.Date.AddHours(23).AddMinutes(59).AddSeconds(59)
            : invoice.DueDateUtc.AddDays(-1);

        var offer = new EarlyPaymentOffer(
            OfferId: $"EP-OFFER-{Guid.NewGuid():N}",
            InvoiceId: invoice.InvoiceId,
            DaysEarly: daysEarly,
            DiscountRatePct: discountRatePct,
            AnnualizedAprYieldPct: annualizedAprYieldPct,
            NetPaymentAmountDzd: netPaymentAmountDzd,
            BuyerSavingsDzd: buyerSavingsDzd,
            ExpirationDateUtc: expirationDateUtc,
            FundingType: FactoringType.DynamicDiscounting,
            Status: InvoiceFundingStatus.PendingOffer
        );

        _offers[offer.OfferId] = offer;

        lock (_statsLock)
        {
            _totalInvoicesVolumeDzd += invoice.InvoiceAmountDzd;
        }

        return offer;
    }

    /// <summary>
    /// Evaluates multiple supplier payables, computes prospective offers, ranks by annualized APR yield,
    /// and generates offers constrained by buyer's available working capital liquidity pool.
    /// </summary>
    public IReadOnlyList<EarlyPaymentOffer> GenerateOffersForPayables(
        IEnumerable<InvoicePayable> invoices,
        decimal availableLiquidityDzd,
        DateTime? targetPaymentDateUtc = null,
        decimal annualAprTarget = 0.24m,
        bool applyDynamicCurve = false)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        if (availableLiquidityDzd <= 0m)
        {
            return [];
        }

        var evalDate = targetPaymentDateUtc ?? DateTime.UtcNow.Date.AddDays(1);
        var eligibleOffers = new List<EarlyPaymentOffer>();

        foreach (var invoice in invoices)
        {
            if (!invoice.ApprovedForPayment)
            {
                continue;
            }

            var daysEarly = (invoice.DueDateUtc.Date - evalDate.Date).Days;
            if (daysEarly <= 0 || daysEarly > invoice.PaymentTermsDays)
            {
                continue;
            }

            if (invoice.InvoiceAmountDzd <= 0m)
            {
                continue;
            }

            var offer = CalculateEarlyPaymentOffer(invoice, evalDate, annualAprTarget, applyDynamicCurve);
            eligibleOffers.Add(offer);
        }

        // Rank by highest annualized APR yield first, then buyer savings
        var rankedOffers = eligibleOffers
            .OrderByDescending(o => o.AnnualizedAprYieldPct)
            .ThenByDescending(o => o.BuyerSavingsDzd)
            .ToList();

        var allocatedOffers = new List<EarlyPaymentOffer>();
        var remainingLiquidity = availableLiquidityDzd;

        foreach (var offer in rankedOffers)
        {
            if (offer.NetPaymentAmountDzd <= remainingLiquidity)
            {
                allocatedOffers.Add(offer);
                remainingLiquidity -= offer.NetPaymentAmountDzd;
            }
        }

        return allocatedOffers;
    }

    /// <summary>
    /// Executes supplier acceptance of an early payment offer and updates factoring metrics.
    /// </summary>
    public EarlyPaymentOffer AcceptOffer(string offerId, string supplierTenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(offerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierTenantId);

        if (!_offers.TryGetValue(offerId, out var existingOffer))
        {
            throw new KeyNotFoundException($"Early payment offer '{offerId}' was not found.");
        }

        if (existingOffer.Status == InvoiceFundingStatus.OfferAccepted)
        {
            return existingOffer;
        }

        if (existingOffer.Status != InvoiceFundingStatus.PendingOffer)
        {
            throw new InvalidOperationException($"Offer '{offerId}' cannot be accepted because its status is '{existingOffer.Status}'.");
        }

        if (DateTime.UtcNow > existingOffer.ExpirationDateUtc)
        {
            var expired = existingOffer with { Status = InvoiceFundingStatus.MaturedAtFullTerm };
            _offers[offerId] = expired;
            throw new InvalidOperationException($"Offer '{offerId}' has expired on {existingOffer.ExpirationDateUtc:yyyy-MM-dd}.");
        }

        var acceptedOffer = existingOffer with { Status = InvoiceFundingStatus.OfferAccepted };
        _offers[offerId] = acceptedOffer;

        lock (_statsLock)
        {
            _totalEarlyPaidDzd += acceptedOffer.NetPaymentAmountDzd;
            _totalBuyerSavingsDzd += acceptedOffer.BuyerSavingsDzd;
            _acceptedOffersCount++;
        }

        return acceptedOffer;
    }

    /// <summary>
    /// Retrieves an offer by its unique identifier.
    /// </summary>
    public EarlyPaymentOffer? GetOffer(string offerId)
    {
        return _offers.TryGetValue(offerId, out var offer) ? offer : null;
    }

    /// <summary>
    /// Retrieves all currently active pending offers.
    /// </summary>
    public IReadOnlyList<EarlyPaymentOffer> GetActiveOffers()
    {
        return _offers.Values
            .Where(o => o.Status == InvoiceFundingStatus.PendingOffer && DateTime.UtcNow <= o.ExpirationDateUtc)
            .ToList();
    }

    /// <summary>
    /// Computes summary metrics across dynamic discounting operations.
    /// </summary>
    public FactoringSummaryResponse GetFactoringSummary()
    {
        lock (_statsLock)
        {
            var activeOffersCount = _offers.Values.Count(o => o.Status == InvoiceFundingStatus.PendingOffer);
            return new FactoringSummaryResponse(
                TotalInvoicesVolumeDzd: _totalInvoicesVolumeDzd,
                TotalEarlyPaidDzd: _totalEarlyPaidDzd,
                TotalBuyerSavingsDzd: _totalBuyerSavingsDzd,
                ActiveBankFinancedVolumeDzd: 0m,
                ActiveOffersCount: activeOffersCount,
                CompletedAssignmentsCount: _acceptedOffersCount
            );
        }
    }

    /// <summary>
    /// Computes dynamic curve APR adjustments based on maturity tier.
    /// Short-notice payments (e.g. &lt;= 10 days) apply a liquidity processing premium,
    /// while longer maturities scale smoothly to optimize working capital return.
    /// </summary>
    private static decimal CalculateDynamicCurveApr(int daysEarly, int paymentTermsDays, decimal baseAprTarget)
    {
        if (daysEarly <= 10)
        {
            // Shorter maturity premium (up to +15% over base APR for immediate execution)
            var surchargeFactor = 1.0m + (0.15m * ((10 - daysEarly) / 10.0m));
            return baseAprTarget * surchargeFactor;
        }

        if (paymentTermsDays > 0 && daysEarly >= (paymentTermsDays * 0.8m))
        {
            // Early accelerated tier: slight yield enhancement for prompt approval
            return baseAprTarget * 1.05m;
        }

        return baseAprTarget;
    }
}
