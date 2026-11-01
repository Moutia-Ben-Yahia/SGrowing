using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGrowing.Api.Modules.Finance.Ledger;

#region Chart of Accounts & Classifications

/// <summary>
/// Standard Chart of Accounts codes for SGrowing FinTech Core Banking &amp; Multi-Party Escrow.
/// </summary>
public static class StandardAccountCodes
{
    /// <summary>Liquid operating cash, bank float, and COD cash held in transit by courier logistics.</summary>
    public const string CashAndCourierFloat = "1000";

    /// <summary>Restricted customer cash held in escrow custody pending courier Proof of Delivery (POD) (Custodial Escrow Liability).</summary>
    public const string CustomerEscrowHeld = "1100";

    /// <summary>Net payables owed to merchants for fulfilled and delivered orders.</summary>
    public const string MerchantAccountsPayable = "2000";

    /// <summary>Delivery fees and shipping remittances payable to courier delivery partners.</summary>
    public const string CourierRemittancePayable = "2100";

    /// <summary>Performance marketing and creator affiliate commissions payable.</summary>
    public const string AffiliateCommissionPayable = "2200";

    /// <summary>Platform SaaS take-rate revenue recognized upon successful order delivery settlement.</summary>
    public const string PlatformSaaSTakeRateRevenue = "4000";

    /// <summary>Return logistics, damaged goods freight, and courier penalty expenses.</summary>
    public const string ShippingAndFreightExpense = "5000";

    private static readonly Dictionary<string, AccountMetadata> Accounts = new()
    {
        [CashAndCourierFloat] = new(CashAndCourierFloat, "Cash & Courier Float", AccountCategory.Asset, "Liquid operating cash, bank float, and COD courier cash float"),
        [CustomerEscrowHeld] = new(CustomerEscrowHeld, "Customer Escrow Held", AccountCategory.Liability, "Restricted cash held in escrow custody pending proof of delivery"),
        [MerchantAccountsPayable] = new(MerchantAccountsPayable, "Merchant Accounts Payable", AccountCategory.Liability, "Net trade payables owed to merchants"),
        [CourierRemittancePayable] = new(CourierRemittancePayable, "Courier Remittance Payable", AccountCategory.Liability, "Logistics fees owed to courier partners"),
        [AffiliateCommissionPayable] = new(AffiliateCommissionPayable, "Affiliate Commission Payable", AccountCategory.Liability, "Commissions payable to affiliate marketers"),
        [PlatformSaaSTakeRateRevenue] = new(PlatformSaaSTakeRateRevenue, "Platform SaaS Take Rate Revenue", AccountCategory.Revenue, "Platform fee revenue recognized on fulfillment"),
        [ShippingAndFreightExpense] = new(ShippingAndFreightExpense, "Shipping & Freight Expense", AccountCategory.Expense, "Reverse freight and shipping expense charges")
    };

    public static AccountMetadata GetMetadata(string accountCode)
    {
        if (Accounts.TryGetValue(accountCode, out var meta))
        {
            return meta;
        }

        // Infer category by leading digit (standard accounting hierarchy)
        var category = accountCode.FirstOrDefault() switch
        {
            '1' => AccountCategory.Asset,
            '2' => AccountCategory.Liability,
            '3' => AccountCategory.Equity,
            '4' => AccountCategory.Revenue,
            '5' => AccountCategory.Expense,
            _ => AccountCategory.Asset
        };

        return new AccountMetadata(accountCode, $"Account {accountCode}", category, "Dynamic ledger account");
    }
}

public enum AccountCategory
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public sealed record AccountMetadata(string Code, string Name, AccountCategory Category, string Description);

#endregion

#region Exceptions

public sealed class UnbalancedJournalEntryException : Exception
{
    public decimal TotalDebits { get; }
    public decimal TotalCredits { get; }
    public decimal Difference { get; }
    public string ReferenceId { get; }

    public UnbalancedJournalEntryException(decimal debits, decimal credits, string referenceId)
        : base($"Unbalanced journal entry for reference '{referenceId}': Total Debits ({debits:F4}) does not match Total Credits ({credits:F4}). Imbalance: {Math.Abs(debits - credits):F4}")
    {
        TotalDebits = debits;
        TotalCredits = credits;
        Difference = Math.Abs(debits - credits);
        ReferenceId = referenceId;
    }
}

public sealed class LedgerTamperDetectedException : Exception
{
    public long SequenceNumber { get; }
    public Guid EntryId { get; }

    public LedgerTamperDetectedException(long sequenceNumber, Guid entryId, string message)
        : base($"Ledger tamper detected at sequence #{sequenceNumber} (Entry {entryId}): {message}")
    {
        SequenceNumber = sequenceNumber;
        EntryId = entryId;
    }
}

#endregion

#region Models & DTOs

public sealed record JournalLine
{
    public Guid LineId { get; init; } = Guid.NewGuid();
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public string Currency { get; init; } = "DZD";
    public string? Description { get; init; }
    public string? EntityId { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed record JournalEntry
{
    public Guid EntryId { get; init; } = Guid.NewGuid();
    public long SequenceNumber { get; init; }
    public Guid TenantId { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public string ReferenceType { get; init; } = string.Empty;
    public string ReferenceId { get; init; } = string.Empty;
    public string Currency { get; init; } = "DZD";
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<JournalLine> Lines { get; init; } = [];
    public decimal TotalDebits { get; init; }
    public decimal TotalCredits { get; init; }
    public string IntegrityHash { get; init; } = string.Empty;
    public string? PreviousHash { get; init; }
    public bool IsReversed { get; init; }
    public Guid? ReversalOfEntryId { get; init; }
    public Guid? ReversedByEntryId { get; init; }
}

public sealed record PostJournalEntryRequest(
    Guid TenantId,
    string ReferenceType,
    string ReferenceId,
    string Description,
    IReadOnlyList<JournalLineRequest> Lines,
    string Currency = "DZD"
);

public sealed record JournalLineRequest(
    string AccountCode,
    decimal Debit,
    decimal Credit,
    string? Description = null,
    string? EntityId = null,
    IReadOnlyDictionary<string, string>? Metadata = null
);

public sealed record AccountBalanceDto(
    string AccountCode,
    string AccountName,
    AccountCategory Category,
    string Currency,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal NormalBalance
);

public sealed record TrialBalanceEntryDto(
    string AccountCode,
    string AccountName,
    AccountCategory Category,
    decimal DebitBalance,
    decimal CreditBalance
);

public sealed record LedgerTrialBalanceDto(
    Guid TenantId,
    string Currency,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<TrialBalanceEntryDto> Entries,
    decimal TotalDebitBalances,
    decimal TotalCreditBalances,
    bool IsBalanced
);

#endregion

#region Service Interface

public interface IDoubleEntryLedgerService
{
    Task<JournalEntry> PostEntryAsync(PostJournalEntryRequest request, CancellationToken ct = default);
    Task<JournalEntry> ReverseEntryAsync(Guid entryId, string reason, CancellationToken ct = default);
    Task<decimal> GetAccountBalanceAsync(Guid tenantId, string accountCode, string currency = "DZD", CancellationToken ct = default);
    Task<AccountBalanceDto> GetAccountBalanceDetailAsync(Guid tenantId, string accountCode, string currency = "DZD", CancellationToken ct = default);
    Task<LedgerTrialBalanceDto> GetTrialBalanceAsync(Guid tenantId, string currency = "DZD", CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntry>> GetEntriesByReferenceAsync(Guid tenantId, string referenceId, CancellationToken ct = default);
    Task<JournalEntry?> GetEntryByIdAsync(Guid entryId, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntry>> GetJournalEntriesAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<bool> VerifyIntegrityChainAsync(Guid tenantId, CancellationToken ct = default);
}

#endregion

#region Implementation

public sealed class DoubleEntryLedgerService : IDoubleEntryLedgerService
{
    private readonly ConcurrentDictionary<Guid, List<JournalEntry>> _tenantLedgers = new();
    private readonly ConcurrentDictionary<Guid, long> _tenantSequences = new();
    private readonly ConcurrentDictionary<Guid, string> _tenantLastHashes = new();
    private readonly Lock _ledgerSyncLock = new();

    public Task<JournalEntry> PostEntryAsync(PostJournalEntryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Lines.Count == 0)
        {
            throw new ArgumentException("A journal entry must contain at least two journal lines.", nameof(request));
        }

        // Validate each line and compute totals with high decimal precision
        decimal totalDebits = 0m;
        decimal totalCredits = 0m;

        var compiledLines = new List<JournalLine>(request.Lines.Count);

        foreach (var lineReq in request.Lines)
        {
            if (lineReq.Debit < 0m || lineReq.Credit < 0m)
            {
                throw new ArgumentException($"Line for account '{lineReq.AccountCode}' has negative amounts: Debit={lineReq.Debit}, Credit={lineReq.Credit}");
            }

            if (lineReq.Debit == 0m && lineReq.Credit == 0m)
            {
                throw new ArgumentException($"Line for account '{lineReq.AccountCode}' has zero debit and credit.");
            }

            if (lineReq.Debit > 0m && lineReq.Credit > 0m)
            {
                throw new ArgumentException($"Line for account '{lineReq.AccountCode}' cannot have both debit and credit. Split into separate lines.");
            }

            var meta = StandardAccountCodes.GetMetadata(lineReq.AccountCode);

            compiledLines.Add(new JournalLine
            {
                AccountCode = lineReq.AccountCode,
                AccountName = meta.Name,
                Debit = lineReq.Debit,
                Credit = lineReq.Credit,
                Currency = request.Currency,
                Description = lineReq.Description,
                EntityId = lineReq.EntityId,
                Metadata = lineReq.Metadata
            });

            totalDebits += lineReq.Debit;
            totalCredits += lineReq.Credit;
        }

        // Strict Fowler double-entry invariant: Sum(Debits) - Sum(Credits) == 0
        if (totalDebits != totalCredits || totalDebits <= 0m)
        {
            throw new UnbalancedJournalEntryException(totalDebits, totalCredits, request.ReferenceId);
        }

        JournalEntry entry;

        lock (_ledgerSyncLock)
        {
            long sequenceNumber = _tenantSequences.AddOrUpdate(request.TenantId, 1, (_, current) => current + 1);
            _tenantLastHashes.TryGetValue(request.TenantId, out var previousHash);

            var timestamp = DateTimeOffset.UtcNow;
            var hash = ComputeIntegrityHash(sequenceNumber, request.TenantId, timestamp, request.ReferenceType, request.ReferenceId, request.Currency, previousHash, compiledLines);

            entry = new JournalEntry
            {
                EntryId = Guid.NewGuid(),
                SequenceNumber = sequenceNumber,
                TenantId = request.TenantId,
                TimestampUtc = timestamp,
                ReferenceType = request.ReferenceType,
                ReferenceId = request.ReferenceId,
                Currency = request.Currency,
                Description = request.Description,
                Lines = compiledLines.AsReadOnly(),
                TotalDebits = totalDebits,
                TotalCredits = totalCredits,
                IntegrityHash = hash,
                PreviousHash = previousHash
            };

            var ledger = _tenantLedgers.GetOrAdd(request.TenantId, _ => []);
            ledger.Add(entry);
            _tenantLastHashes[request.TenantId] = hash;
        }

        return Task.FromResult(entry);
    }

    public Task<JournalEntry> ReverseEntryAsync(Guid entryId, string reason, CancellationToken ct = default)
    {
        JournalEntry? originalEntry = null;
        Guid tenantId = Guid.Empty;

        lock (_ledgerSyncLock)
        {
            foreach (var (tid, entries) in _tenantLedgers)
            {
                originalEntry = entries.FirstOrDefault(e => e.EntryId == entryId);
                if (originalEntry != null)
                {
                    tenantId = tid;
                    break;
                }
            }

            if (originalEntry == null)
            {
                throw new KeyNotFoundException($"Journal entry '{entryId}' was not found.");
            }

            if (originalEntry.IsReversed)
            {
                throw new InvalidOperationException($"Journal entry '{entryId}' has already been reversed by entry '{originalEntry.ReversedByEntryId}'.");
            }
        }

        // Build inverse lines: Debits become Credits, Credits become Debits
        var reversalLines = originalEntry.Lines.Select(line => new JournalLineRequest(
            AccountCode: line.AccountCode,
            Debit: line.Credit,
            Credit: line.Debit,
            Description: $"Reversal of line {line.LineId}: {reason}",
            EntityId: line.EntityId,
            Metadata: line.Metadata
        )).ToList();

        var reversalRequest = new PostJournalEntryRequest(
            TenantId: tenantId,
            ReferenceType: "Reversal",
            ReferenceId: $"REV-{originalEntry.ReferenceId}",
            Description: $"Reversal of Entry #{originalEntry.SequenceNumber} ({originalEntry.EntryId}): {reason}",
            Lines: reversalLines,
            Currency: originalEntry.Currency
        );

        // Post the reversal
        var reversalEntry = PostEntryAsync(reversalRequest, ct).GetAwaiter().GetResult();

        lock (_ledgerSyncLock)
        {
            var ledger = _tenantLedgers[tenantId];
            var originalIndex = ledger.FindIndex(e => e.EntryId == entryId);
            if (originalIndex >= 0)
            {
                ledger[originalIndex] = originalEntry with
                {
                    IsReversed = true,
                    ReversedByEntryId = reversalEntry.EntryId
                };
            }

            var reversalIndex = ledger.FindIndex(e => e.EntryId == reversalEntry.EntryId);
            if (reversalIndex >= 0)
            {
                ledger[reversalIndex] = reversalEntry with
                {
                    ReversalOfEntryId = originalEntry.EntryId
                };
                reversalEntry = ledger[reversalIndex];
            }
        }

        return Task.FromResult(reversalEntry);
    }

    public Task<decimal> GetAccountBalanceAsync(Guid tenantId, string accountCode, string currency = "DZD", CancellationToken ct = default)
    {
        var detail = GetAccountBalanceDetailAsync(tenantId, accountCode, currency, ct).GetAwaiter().GetResult();
        return Task.FromResult(detail.NormalBalance);
    }

    public Task<AccountBalanceDto> GetAccountBalanceDetailAsync(Guid tenantId, string accountCode, string currency = "DZD", CancellationToken ct = default)
    {
        decimal debits = 0m;
        decimal credits = 0m;

        lock (_ledgerSyncLock)
        {
            if (_tenantLedgers.TryGetValue(tenantId, out var entries))
            {
                foreach (var entry in entries)
                {
                    if (!string.Equals(entry.Currency, currency, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var line in entry.Lines)
                    {
                        if (string.Equals(line.AccountCode, accountCode, StringComparison.OrdinalIgnoreCase))
                        {
                            debits += line.Debit;
                            credits += line.Credit;
                        }
                    }
                }
            }
        }

        var meta = StandardAccountCodes.GetMetadata(accountCode);

        // Calculate normal balance:
        // Asset & Expense: Normal Balance = Debits - Credits
        // Liability, Equity, Revenue: Normal Balance = Credits - Debits
        decimal normalBalance = meta.Category switch
        {
            AccountCategory.Asset => debits - credits,
            AccountCategory.Expense => debits - credits,
            AccountCategory.Liability => credits - debits,
            AccountCategory.Revenue => credits - debits,
            AccountCategory.Equity => credits - debits,
            _ => debits - credits
        };

        var result = new AccountBalanceDto(
            AccountCode: accountCode,
            AccountName: meta.Name,
            Category: meta.Category,
            Currency: currency,
            TotalDebits: debits,
            TotalCredits: credits,
            NormalBalance: normalBalance
        );

        return Task.FromResult(result);
    }

    public Task<LedgerTrialBalanceDto> GetTrialBalanceAsync(Guid tenantId, string currency = "DZD", CancellationToken ct = default)
    {
        var accountsMap = new Dictionary<string, (decimal Debits, decimal Credits)>();

        lock (_ledgerSyncLock)
        {
            if (_tenantLedgers.TryGetValue(tenantId, out var entries))
            {
                foreach (var entry in entries)
                {
                    if (!string.Equals(entry.Currency, currency, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var line in entry.Lines)
                    {
                        if (!accountsMap.TryGetValue(line.AccountCode, out var val))
                        {
                            val = (0m, 0m);
                        }

                        accountsMap[line.AccountCode] = (val.Debits + line.Debit, val.Credits + line.Credit);
                    }
                }
            }
        }

        var trialEntries = new List<TrialBalanceEntryDto>();
        decimal totalDebitBalances = 0m;
        decimal totalCreditBalances = 0m;

        foreach (var (code, (debits, credits)) in accountsMap.OrderBy(kv => kv.Key))
        {
            var meta = StandardAccountCodes.GetMetadata(code);
            decimal net = debits - credits;

            decimal debitBal = 0m;
            decimal creditBal = 0m;

            if (net > 0m)
            {
                debitBal = net;
            }
            else if (net < 0m)
            {
                creditBal = Math.Abs(net);
            }

            trialEntries.Add(new TrialBalanceEntryDto(
                AccountCode: code,
                AccountName: meta.Name,
                Category: meta.Category,
                DebitBalance: debitBal,
                CreditBalance: creditBal
            ));

            totalDebitBalances += debitBal;
            totalCreditBalances += creditBal;
        }

        bool isBalanced = Math.Abs(totalDebitBalances - totalCreditBalances) == 0m;

        var result = new LedgerTrialBalanceDto(
            TenantId: tenantId,
            Currency: currency,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Entries: trialEntries.AsReadOnly(),
            TotalDebitBalances: totalDebitBalances,
            TotalCreditBalances: totalCreditBalances,
            IsBalanced: isBalanced
        );

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<JournalEntry>> GetEntriesByReferenceAsync(Guid tenantId, string referenceId, CancellationToken ct = default)
    {
        lock (_ledgerSyncLock)
        {
            if (_tenantLedgers.TryGetValue(tenantId, out var entries))
            {
                var matches = entries.Where(e => string.Equals(e.ReferenceId, referenceId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.SequenceNumber)
                    .ToList();
                return Task.FromResult<IReadOnlyList<JournalEntry>>(matches);
            }
        }

        return Task.FromResult<IReadOnlyList<JournalEntry>>([]);
    }

    public Task<JournalEntry?> GetEntryByIdAsync(Guid entryId, CancellationToken ct = default)
    {
        lock (_ledgerSyncLock)
        {
            foreach (var entries in _tenantLedgers.Values)
            {
                var match = entries.FirstOrDefault(e => e.EntryId == entryId);
                if (match != null)
                {
                    return Task.FromResult<JournalEntry?>(match);
                }
            }
        }

        return Task.FromResult<JournalEntry?>(null);
    }

    public Task<IReadOnlyList<JournalEntry>> GetJournalEntriesAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        lock (_ledgerSyncLock)
        {
            if (_tenantLedgers.TryGetValue(tenantId, out var entries))
            {
                var page = entries.OrderByDescending(e => e.SequenceNumber)
                    .Skip(skip)
                    .Take(take)
                    .ToList();
                return Task.FromResult<IReadOnlyList<JournalEntry>>(page);
            }
        }

        return Task.FromResult<IReadOnlyList<JournalEntry>>([]);
    }

    public Task<bool> VerifyIntegrityChainAsync(Guid tenantId, CancellationToken ct = default)
    {
        lock (_ledgerSyncLock)
        {
            if (!_tenantLedgers.TryGetValue(tenantId, out var entries) || entries.Count == 0)
            {
                return Task.FromResult(true);
            }

            string? expectedPrevHash = null;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                // Verify previous hash link
                if (entry.PreviousHash != expectedPrevHash)
                {
                    throw new LedgerTamperDetectedException(entry.SequenceNumber, entry.EntryId, $"PreviousHash mismatch. Expected '{expectedPrevHash}' but found '{entry.PreviousHash}'");
                }

                // Recompute hash
                var computedHash = ComputeIntegrityHash(
                    entry.SequenceNumber,
                    entry.TenantId,
                    entry.TimestampUtc,
                    entry.ReferenceType,
                    entry.ReferenceId,
                    entry.Currency,
                    entry.PreviousHash,
                    entry.Lines
                );

                if (computedHash != entry.IntegrityHash)
                {
                    throw new LedgerTamperDetectedException(entry.SequenceNumber, entry.EntryId, $"IntegrityHash mismatch. Computed '{computedHash}' but found '{entry.IntegrityHash}'");
                }

                expectedPrevHash = entry.IntegrityHash;
            }

            return Task.FromResult(true);
        }
    }

    private static string ComputeIntegrityHash(
        long sequenceNumber,
        Guid tenantId,
        DateTimeOffset timestamp,
        string referenceType,
        string referenceId,
        string currency,
        string? previousHash,
        IReadOnlyList<JournalLine> lines)
    {
        var sb = new StringBuilder();
        sb.Append(sequenceNumber).Append('|')
          .Append(tenantId).Append('|')
          .Append(timestamp.ToUnixTimeMilliseconds()).Append('|')
          .Append(referenceType).Append('|')
          .Append(referenceId).Append('|')
          .Append(currency).Append('|')
          .Append(previousHash ?? "GENESIS").Append('|');

        foreach (var line in lines.OrderBy(l => l.AccountCode).ThenBy(l => l.Debit).ThenBy(l => l.Credit))
        {
            sb.Append(line.AccountCode).Append(':')
              .Append(line.Debit.ToString("F4")).Append(':')
              .Append(line.Credit.ToString("F4")).Append(';');
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(hashBytes);
    }
}

#endregion
