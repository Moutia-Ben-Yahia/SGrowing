using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Finance.Factoring;

/// <summary>
/// Core contract for multi-bank reverse factoring consortium liquidity management and non-recourse assignments.
/// </summary>
public interface IReverseFactoringLiquidityPoolService
{
    BankAssignmentRecord AssignInvoiceToBank(
        InvoicePayable invoice,
        PartnerBankCode bankCode,
        decimal bankDiscountFeePct);

    BankAssignmentRecord? GetBankFundingStatus(string assignmentId);

    ConsortiumLiquiditySummary GetConsortiumLiquiditySummary();

    BankAssignmentRecord SettleAssignment(
        string assignmentId,
        InvoiceFundingStatus settlementStatus = InvoiceFundingStatus.SettledEarly);

    BankAssignmentRecord MarkDefaulted(string assignmentId);

    void UpdateBankCreditLimit(PartnerBankCode bankCode, decimal newCreditLineDzd);

    IReadOnlyList<BankAssignmentRecord> GetAssignmentsForBank(PartnerBankCode bankCode);

    IReadOnlyList<BankAssignmentRecord> GetAllAssignments();
}

/// <summary>
/// Internal mutable state for a partner bank facility.
/// </summary>
internal sealed class PartnerBankFacilityState
{
    public required PartnerBankCode BankCode { get; init; }
    public required string BankName { get; init; }
    public decimal TotalCreditLineDzd { get; set; }
    public decimal UtilizedCreditDzd { get; set; }
    public decimal BaseFeeRatePct { get; init; }

    public decimal AvailableLiquidityDzd => Math.Max(0m, TotalCreditLineDzd - UtilizedCreditDzd);

    public BankCreditFacility ToDto() => new(
        BankCode: BankCode,
        BankName: BankName,
        TotalCreditLineDzd: TotalCreditLineDzd,
        UtilizedCreditDzd: UtilizedCreditDzd,
        AvailableLiquidityDzd: AvailableLiquidityDzd,
        BaseFeeRatePct: BaseFeeRatePct
    );
}

/// <summary>
/// Production-grade implementation of multi-bank reverse factoring liquidity pool.
/// Manages credit line quotas, non-recourse covenant assignments, and bank fee settlements.
/// </summary>
public sealed class ReverseFactoringLiquidityPoolService : IReverseFactoringLiquidityPoolService
{
    private readonly Lock _poolLock = new();
    private readonly Dictionary<PartnerBankCode, PartnerBankFacilityState> _bankFacilities;
    private readonly ConcurrentDictionary<string, BankAssignmentRecord> _assignments = new();
    private readonly ConcurrentDictionary<string, decimal> _assignmentInvoiceAmounts = new();

    public ReverseFactoringLiquidityPoolService()
    {
        _bankFacilities = new Dictionary<PartnerBankCode, PartnerBankFacilityState>
        {
            [PartnerBankCode.BNA_Algeria] = new()
            {
                BankCode = PartnerBankCode.BNA_Algeria,
                BankName = "Banque Nationale d'Algérie (BNA)",
                TotalCreditLineDzd = 500_000_000m,
                UtilizedCreditDzd = 0m,
                BaseFeeRatePct = 1.75m
            },
            [PartnerBankCode.CPA_Algeria] = new()
            {
                BankCode = PartnerBankCode.CPA_Algeria,
                BankName = "Crédit Populaire d'Algérie (CPA)",
                TotalCreditLineDzd = 400_000_000m,
                UtilizedCreditDzd = 0m,
                BaseFeeRatePct = 1.85m
            },
            [PartnerBankCode.SocGen_Algeria] = new()
            {
                BankCode = PartnerBankCode.SocGen_Algeria,
                BankName = "Société Générale Algérie",
                TotalCreditLineDzd = 350_000_000m,
                UtilizedCreditDzd = 0m,
                BaseFeeRatePct = 2.10m
            },
            [PartnerBankCode.BNP_Algeria] = new()
            {
                BankCode = PartnerBankCode.BNP_Algeria,
                BankName = "BNP Paribas El Djazaïr",
                TotalCreditLineDzd = 300_000_000m,
                UtilizedCreditDzd = 0m,
                BaseFeeRatePct = 2.00m
            },
            [PartnerBankCode.DubaiIslamicBank_UAE] = new()
            {
                BankCode = PartnerBankCode.DubaiIslamicBank_UAE,
                BankName = "Dubai Islamic Bank (UAE Cross-Border Trade)",
                TotalCreditLineDzd = 600_000_000m,
                UtilizedCreditDzd = 0m,
                BaseFeeRatePct = 1.50m
            }
        };
    }

    /// <summary>
    /// Assigns an approved buyer invoice to a designated consortium partner bank.
    /// Under the non-recourse covenant, the bank assumes the buyer credit default risk,
    /// advancing the net payment to the supplier within 24 hours of assignment.
    /// </summary>
    public BankAssignmentRecord AssignInvoiceToBank(
        InvoicePayable invoice,
        PartnerBankCode bankCode,
        decimal bankDiscountFeePct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (!invoice.ApprovedForPayment)
        {
            throw new InvalidOperationException($"Invoice '{invoice.InvoiceId}' cannot be assigned to partner bank because it is not approved for payment by the buyer.");
        }

        if (invoice.InvoiceAmountDzd <= 0m)
        {
            throw new ArgumentException("Invoice amount must be strictly greater than zero.", nameof(invoice));
        }

        if (bankDiscountFeePct < 0m || bankDiscountFeePct >= 1.0m)
        {
            throw new ArgumentOutOfRangeException(nameof(bankDiscountFeePct), "Bank discount fee percentage must be between 0% and 100% (e.g. 0.015 for 1.5%).");
        }

        lock (_poolLock)
        {
            if (!_bankFacilities.TryGetValue(bankCode, out var facility))
            {
                throw new ArgumentException($"Partner bank code '{bankCode}' is not configured in the factoring consortium.", nameof(bankCode));
            }

            if (facility.AvailableLiquidityDzd < invoice.InvoiceAmountDzd)
            {
                throw new InvalidOperationException(
                    $"Partner bank {bankCode} has insufficient credit facility liquidity. " +
                    $"Required: {invoice.InvoiceAmountDzd:N2} DZD, Available: {facility.AvailableLiquidityDzd:N2} DZD.");
            }

            // Calculate bank discount fee and supplier net advance
            var bankFeeDzd = Math.Round(invoice.InvoiceAmountDzd * bankDiscountFeePct, 2, MidpointRounding.AwayFromZero);
            var financedAmountDzd = invoice.InvoiceAmountDzd - bankFeeDzd;

            // Reserve bank credit facility
            facility.UtilizedCreditDzd += invoice.InvoiceAmountDzd;

            var assignment = new BankAssignmentRecord(
                AssignmentId: $"ASGN-{bankCode}-{Guid.NewGuid():N}",
                InvoiceId: invoice.InvoiceId,
                PartnerBank: bankCode,
                FinancedAmountDzd: financedAmountDzd,
                BankFeeDzd: bankFeeDzd,
                NonRecourseProtection: true,
                AssignedAtUtc: DateTime.UtcNow,
                Status: InvoiceFundingStatus.AssignedToBank
            );

            _assignments[assignment.AssignmentId] = assignment;
            _assignmentInvoiceAmounts[assignment.AssignmentId] = invoice.InvoiceAmountDzd;

            return assignment;
        }
    }

    /// <summary>
    /// Retrieves current assignment status and financing details.
    /// </summary>
    public BankAssignmentRecord? GetBankFundingStatus(string assignmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignmentId);
        return _assignments.TryGetValue(assignmentId, out var assignment) ? assignment : null;
    }

    /// <summary>
    /// Computes real-time capacity and utilization across all consortium member banks.
    /// </summary>
    public ConsortiumLiquiditySummary GetConsortiumLiquiditySummary()
    {
        lock (_poolLock)
        {
            var facilities = _bankFacilities.Values.Select(f => f.ToDto()).ToList();
            var totalCapacity = facilities.Sum(f => f.TotalCreditLineDzd);
            var totalUtilized = facilities.Sum(f => f.UtilizedCreditDzd);
            var totalAvailable = facilities.Sum(f => f.AvailableLiquidityDzd);

            return new ConsortiumLiquiditySummary(
                TotalConsortiumCapacityDzd: totalCapacity,
                TotalUtilizedDzd: totalUtilized,
                AvailableConsortiumLiquidityDzd: totalAvailable,
                BankFacilities: facilities
            );
        }
    }

    /// <summary>
    /// Transitions an assignment to Settled status upon buyer payment or early clearance,
    /// releasing utilized credit back to the bank facility.
    /// </summary>
    public BankAssignmentRecord SettleAssignment(
        string assignmentId,
        InvoiceFundingStatus settlementStatus = InvoiceFundingStatus.SettledEarly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignmentId);

        lock (_poolLock)
        {
            if (!_assignments.TryGetValue(assignmentId, out var assignment))
            {
                throw new KeyNotFoundException($"Bank assignment record '{assignmentId}' was not found.");
            }

            if (assignment.Status == InvoiceFundingStatus.SettledEarly || assignment.Status == InvoiceFundingStatus.MaturedAtFullTerm)
            {
                return assignment;
            }

            if (_bankFacilities.TryGetValue(assignment.PartnerBank, out var facility) &&
                _assignmentInvoiceAmounts.TryGetValue(assignmentId, out var invoiceAmount))
            {
                facility.UtilizedCreditDzd = Math.Max(0m, facility.UtilizedCreditDzd - invoiceAmount);
            }

            var updated = assignment with { Status = settlementStatus };
            _assignments[assignmentId] = updated;

            return updated;
        }
    }

    /// <summary>
    /// Records buyer insolvency/default. Under the non-recourse covenant, the bank absorbs
    /// the loss and the supplier's financed payout is fully protected from clawback.
    /// </summary>
    public BankAssignmentRecord MarkDefaulted(string assignmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignmentId);

        lock (_poolLock)
        {
            if (!_assignments.TryGetValue(assignmentId, out var assignment))
            {
                throw new KeyNotFoundException($"Bank assignment record '{assignmentId}' was not found.");
            }

            var defaulted = assignment with { Status = InvoiceFundingStatus.Defaulted };
            _assignments[assignmentId] = defaulted;

            return defaulted;
        }
    }

    /// <summary>
    /// Dynamically expands or contracts a partner bank's committed credit facility.
    /// </summary>
    public void UpdateBankCreditLimit(PartnerBankCode bankCode, decimal newCreditLineDzd)
    {
        if (newCreditLineDzd < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(newCreditLineDzd), "Credit line capacity cannot be negative.");
        }

        lock (_poolLock)
        {
            if (!_bankFacilities.TryGetValue(bankCode, out var facility))
            {
                throw new ArgumentException($"Partner bank code '{bankCode}' is not configured in the factoring consortium.", nameof(bankCode));
            }

            facility.TotalCreditLineDzd = newCreditLineDzd;
        }
    }

    /// <summary>
    /// Returns all assignments associated with a specific partner bank.
    /// </summary>
    public IReadOnlyList<BankAssignmentRecord> GetAssignmentsForBank(PartnerBankCode bankCode)
    {
        return _assignments.Values
            .Where(a => a.PartnerBank == bankCode)
            .OrderByDescending(a => a.AssignedAtUtc)
            .ToList();
    }

    /// <summary>
    /// Returns all assignments across all partner banks.
    /// </summary>
    public IReadOnlyList<BankAssignmentRecord> GetAllAssignments()
    {
        return _assignments.Values
            .OrderByDescending(a => a.AssignedAtUtc)
            .ToList();
    }
}
