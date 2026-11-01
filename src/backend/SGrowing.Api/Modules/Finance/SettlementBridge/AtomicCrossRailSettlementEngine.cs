using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Finance.SettlementBridge;

/// <summary>
/// Contract interface for Hash Time Locked Contracts (HTLC) and dual-rail atomic cross-currency settlement.
/// </summary>
public interface IAtomicCrossRailSettlementEngine
{
    /// <summary>
    /// Computes the SHA-256 hash lock (H) for a given preimage secret (S).
    /// </summary>
    string ComputeHashLock(string secret);

    /// <summary>
    /// Cryptographically validates whether SHA-256(secret) equals the expected hash lock.
    /// </summary>
    bool VerifyPreimage(string secret, string expectedHashHex);

    /// <summary>
    /// Creates and locks funds in an individual HTLC contract.
    /// </summary>
    HtlcContract CreateContract(
        string sender,
        string recipient,
        decimal amount,
        CurrencyCode currency,
        PaymentRail rail,
        string hashLockHex,
        TimeSpan timeToLive,
        string? linkedSwapId = null);

    /// <summary>
    /// Retrieves an HTLC contract by identifier.
    /// </summary>
    HtlcContract GetContract(string contractId);

    /// <summary>
    /// Lists all registered HTLC contracts.
    /// </summary>
    IReadOnlyList<HtlcContract> GetAllContracts();

    /// <summary>
    /// Claims locked funds by revealing the valid preimage secret before expiry.
    /// </summary>
    HtlcContract ClaimContract(string contractId, string secret);

    /// <summary>
    /// Refunds locked funds to the sender if the contract lock time has expired.
    /// </summary>
    HtlcContract RefundExpiredContract(string contractId, DateTime? currentUtc = null);

    /// <summary>
    /// Initiates a dual-rail atomic swap locking funds across source and target rails with identical hashlock.
    /// </summary>
    CrossRailSwap InitiateSwap(
        string initiator,
        string counterparty,
        PaymentRail sourceRail,
        PaymentRail targetRail,
        CurrencyCode sourceCurrency,
        CurrencyCode targetCurrency,
        decimal sourceAmount,
        decimal targetAmount,
        string hashLockHex,
        TimeSpan timeToLive);

    /// <summary>
    /// Retrieves a cross-rail swap by identifier.
    /// </summary>
    CrossRailSwap GetSwap(string swapId);

    /// <summary>
    /// Lists all tracked cross-rail swaps.
    /// </summary>
    IReadOnlyList<CrossRailSwap> GetAllSwaps();

    /// <summary>
    /// Confirms that the counterparty has locked the target leg, moving state to BothLocked.
    /// </summary>
    CrossRailSwap ConfirmCounterpartyLeg(string swapId);

    /// <summary>
    /// Atomically executes claims on both swap legs using the revealed secret preimage.
    /// </summary>
    CrossRailSwap ClaimSwap(string swapId, string secret);

    /// <summary>
    /// Atomically aborts and refunds both swap legs if expiry has elapsed without a valid claim.
    /// </summary>
    CrossRailSwap RefundSwap(string swapId, DateTime? currentUtc = null);
}

/// <summary>
/// Production engine implementing HTLC state machine and dual-rail atomic cross-currency swaps.
/// Uses native System.Threading.Lock for zero-overhead high-concurrency thread safety.
/// </summary>
public sealed class AtomicCrossRailSettlementEngine : IAtomicCrossRailSettlementEngine
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, HtlcContract> _contracts = [];
    private readonly Dictionary<string, CrossRailSwap> _swaps = [];

    /// <inheritdoc />
    public string ComputeHashLock(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        byte[] bytes = IsHexString(secret)
            ? Convert.FromHexString(secret)
            : Encoding.UTF8.GetBytes(secret);

        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <inheritdoc />
    public bool VerifyPreimage(string secret, string expectedHashHex)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(expectedHashHex))
            return false;

        // 1. Verify against UTF-8 byte encoding
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(secret);
        string utf8Hash = Convert.ToHexStringLower(SHA256.HashData(utf8Bytes));
        if (string.Equals(utf8Hash, expectedHashHex, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. Verify against raw hex decoding if secret is formatted as hex
        if (IsHexString(secret))
        {
            byte[] hexBytes = Convert.FromHexString(secret);
            string hexHash = Convert.ToHexStringLower(SHA256.HashData(hexBytes));
            if (string.Equals(hexHash, expectedHashHex, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public HtlcContract CreateContract(
        string sender,
        string recipient,
        decimal amount,
        CurrencyCode currency,
        PaymentRail rail,
        string hashLockHex,
        TimeSpan timeToLive,
        string? linkedSwapId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sender);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashLockHex);

        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Contract amount must be strictly positive.");

        if (timeToLive <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Time-to-live must be positive.");

        var contractId = $"htlc_{Guid.NewGuid():N}";
        var expiryUtc = DateTime.UtcNow.Add(timeToLive);

        var contract = new HtlcContract(
            ContractId: contractId,
            Sender: sender,
            Recipient: recipient,
            Amount: amount,
            Currency: currency,
            HashLockHex: hashLockHex.ToLowerInvariant(),
            ExpiryTimestampUtc: expiryUtc,
            Status: HtlcStatus.ActiveLocked,
            ClaimSecretHex: null,
            Rail: rail,
            SettledAtUtc: null,
            LinkedSwapId: linkedSwapId
        );

        lock (_lock)
        {
            _contracts[contractId] = contract;
        }

        return contract;
    }

    /// <inheritdoc />
    public HtlcContract GetContract(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);

        lock (_lock)
        {
            if (_contracts.TryGetValue(contractId, out var contract))
                return contract;
        }

        throw new KeyNotFoundException($"HTLC contract '{contractId}' was not found.");
    }

    /// <inheritdoc />
    public IReadOnlyList<HtlcContract> GetAllContracts()
    {
        lock (_lock)
        {
            return [.. _contracts.Values];
        }
    }

    /// <inheritdoc />
    public HtlcContract ClaimContract(string contractId, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        lock (_lock)
        {
            if (!_contracts.TryGetValue(contractId, out var contract))
                throw new KeyNotFoundException($"HTLC contract '{contractId}' was not found.");

            // Replay & Status Protection
            if (contract.Status == HtlcStatus.Claimed)
                throw new InvalidOperationException($"Contract '{contractId}' has already been claimed.");

            if (contract.Status == HtlcStatus.RefundedExpired)
                throw new InvalidOperationException($"Contract '{contractId}' has already been refunded.");

            if (contract.Status != HtlcStatus.ActiveLocked)
                throw new InvalidOperationException($"Contract '{contractId}' is in status {contract.Status} and cannot be claimed.");

            // Expiration Protection: Cannot claim after lock time T
            if (DateTime.UtcNow >= contract.ExpiryTimestampUtc)
                throw new InvalidOperationException($"Contract '{contractId}' has expired at {contract.ExpiryTimestampUtc:O} and cannot be claimed.");

            // Cryptographic Validation: SHA-256(S) == H
            if (!VerifyPreimage(secret, contract.HashLockHex))
                throw new ArgumentException($"Invalid preimage for contract '{contractId}'. Hash does not match hash lock.");

            var claimedContract = contract with
            {
                Status = HtlcStatus.Claimed,
                ClaimSecretHex = secret,
                SettledAtUtc = DateTime.UtcNow
            };

            _contracts[contractId] = claimedContract;
            return claimedContract;
        }
    }

    /// <inheritdoc />
    public HtlcContract RefundExpiredContract(string contractId, DateTime? currentUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);

        var now = currentUtc ?? DateTime.UtcNow;

        lock (_lock)
        {
            if (!_contracts.TryGetValue(contractId, out var contract))
                throw new KeyNotFoundException($"HTLC contract '{contractId}' was not found.");

            // Replay & Status Protection
            if (contract.Status == HtlcStatus.Claimed)
                throw new InvalidOperationException($"Contract '{contractId}' has already been claimed.");

            if (contract.Status == HtlcStatus.RefundedExpired)
                throw new InvalidOperationException($"Contract '{contractId}' has already been refunded.");

            // Expiration check: Lock time T must have elapsed
            if (now < contract.ExpiryTimestampUtc)
                throw new InvalidOperationException($"Lock time T has not expired yet. Contract '{contractId}' expires at {contract.ExpiryTimestampUtc:O}.");

            var refundedContract = contract with
            {
                Status = HtlcStatus.RefundedExpired,
                SettledAtUtc = now
            };

            _contracts[contractId] = refundedContract;
            return refundedContract;
        }
    }

    /// <inheritdoc />
    public CrossRailSwap InitiateSwap(
        string initiator,
        string counterparty,
        PaymentRail sourceRail,
        PaymentRail targetRail,
        CurrencyCode sourceCurrency,
        CurrencyCode targetCurrency,
        decimal sourceAmount,
        decimal targetAmount,
        string hashLockHex,
        TimeSpan timeToLive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initiator);
        ArgumentException.ThrowIfNullOrWhiteSpace(counterparty);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashLockHex);

        if (sourceAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(sourceAmount), "Source amount must be positive.");

        if (targetAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(targetAmount), "Target amount must be positive.");

        if (timeToLive <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Time-to-live must be positive.");

        var swapId = $"swap_{Guid.NewGuid():N}";
        var normalizedHashLock = hashLockHex.ToLowerInvariant();
        var expiryUtc = DateTime.UtcNow.Add(timeToLive);

        // Create Leg 1: Source Rail HTLC (Initiator -> Counterparty)
        var sourceContract = CreateContract(
            sender: initiator,
            recipient: counterparty,
            amount: sourceAmount,
            currency: sourceCurrency,
            rail: sourceRail,
            hashLockHex: normalizedHashLock,
            timeToLive: timeToLive,
            linkedSwapId: swapId
        );

        // Create Leg 2: Target Rail HTLC (Counterparty -> Initiator) with identical hashlock
        var targetContract = CreateContract(
            sender: counterparty,
            recipient: initiator,
            amount: targetAmount,
            currency: targetCurrency,
            rail: targetRail,
            hashLockHex: normalizedHashLock,
            timeToLive: timeToLive,
            linkedSwapId: swapId
        );

        decimal exchangeRate = Math.Round(targetAmount / sourceAmount, 6);

        var swap = new CrossRailSwap(
            SwapId: swapId,
            Initiator: initiator,
            Counterparty: counterparty,
            SourceRail: sourceRail,
            TargetRail: targetRail,
            SourceCurrency: sourceCurrency,
            TargetCurrency: targetCurrency,
            SourceAmount: sourceAmount,
            TargetAmount: targetAmount,
            ExchangeRate: exchangeRate,
            HashLockHex: normalizedHashLock,
            SourceContractId: sourceContract.ContractId,
            TargetContractId: targetContract.ContractId,
            ExpiryUtc: expiryUtc,
            Status: SwapStatus.BothLocked,
            SecretHex: null,
            CreatedAtUtc: DateTime.UtcNow,
            CompletedAtUtc: null
        );

        lock (_lock)
        {
            _swaps[swapId] = swap;
        }

        return swap;
    }

    /// <inheritdoc />
    public CrossRailSwap GetSwap(string swapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(swapId);

        lock (_lock)
        {
            if (_swaps.TryGetValue(swapId, out var swap))
                return swap;
        }

        throw new KeyNotFoundException($"Cross-rail swap '{swapId}' was not found.");
    }

    /// <inheritdoc />
    public IReadOnlyList<CrossRailSwap> GetAllSwaps()
    {
        lock (_lock)
        {
            return [.. _swaps.Values];
        }
    }

    /// <inheritdoc />
    public CrossRailSwap ConfirmCounterpartyLeg(string swapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(swapId);

        lock (_lock)
        {
            if (!_swaps.TryGetValue(swapId, out var swap))
                throw new KeyNotFoundException($"Cross-rail swap '{swapId}' was not found.");

            if (swap.Status != SwapStatus.Initiated)
                return swap;

            var updated = swap with { Status = SwapStatus.BothLocked };
            _swaps[swapId] = updated;
            return updated;
        }
    }

    /// <inheritdoc />
    public CrossRailSwap ClaimSwap(string swapId, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(swapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        lock (_lock)
        {
            if (!_swaps.TryGetValue(swapId, out var swap))
                throw new KeyNotFoundException($"Cross-rail swap '{swapId}' was not found.");

            if (swap.Status == SwapStatus.Completed)
                throw new InvalidOperationException($"Swap '{swapId}' has already been claimed and completed.");

            if (swap.Status == SwapStatus.AbortedRefunded)
                throw new InvalidOperationException($"Swap '{swapId}' has already been refunded.");

            // Dual-rail coordination: verify secret validity
            if (!VerifyPreimage(secret, swap.HashLockHex))
                throw new ArgumentException($"Invalid preimage for swap '{swapId}'. Hash does not match swap hash lock.");

            // Claim target leg first, then source leg
            ClaimContract(swap.TargetContractId, secret);
            ClaimContract(swap.SourceContractId, secret);

            var completedSwap = swap with
            {
                Status = SwapStatus.Completed,
                SecretHex = secret,
                CompletedAtUtc = DateTime.UtcNow
            };

            _swaps[swapId] = completedSwap;
            return completedSwap;
        }
    }

    /// <inheritdoc />
    public CrossRailSwap RefundSwap(string swapId, DateTime? currentUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(swapId);

        var now = currentUtc ?? DateTime.UtcNow;

        lock (_lock)
        {
            if (!_swaps.TryGetValue(swapId, out var swap))
                throw new KeyNotFoundException($"Cross-rail swap '{swapId}' was not found.");

            if (swap.Status == SwapStatus.Completed)
                throw new InvalidOperationException($"Swap '{swapId}' has already been claimed.");

            if (swap.Status == SwapStatus.AbortedRefunded)
                throw new InvalidOperationException($"Swap '{swapId}' has already been refunded.");

            // Refund both legs
            var sourceContract = GetContract(swap.SourceContractId);
            if (sourceContract.Status == HtlcStatus.ActiveLocked)
            {
                RefundExpiredContract(swap.SourceContractId, now);
            }

            var targetContract = GetContract(swap.TargetContractId);
            if (targetContract.Status == HtlcStatus.ActiveLocked)
            {
                RefundExpiredContract(swap.TargetContractId, now);
            }

            var refundedSwap = swap with
            {
                Status = SwapStatus.AbortedRefunded,
                CompletedAtUtc = now
            };

            _swaps[swapId] = refundedSwap;
            return refundedSwap;
        }
    }

    private static bool IsHexString(string value)
    {
        if (value.Length % 2 != 0 || value.Length == 0)
            return false;

        foreach (char c in value)
        {
            if (!char.IsAsciiHexDigit(c))
                return false;
        }

        return true;
    }
}
