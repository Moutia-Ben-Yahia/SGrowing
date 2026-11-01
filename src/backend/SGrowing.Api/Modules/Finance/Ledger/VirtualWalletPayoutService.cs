using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SGrowing.Api.Modules.Finance.Ledger;

#region Virtual Wallet Domain Models & Exceptions

public enum WalletOwnerType
{
    Merchant = 1,
    Courier = 2,
    Affiliate = 3,
    Customer = 4,
    Platform = 5
}

public sealed record VirtualWallet
{
    public Guid WalletId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string EntityId { get; init; } = string.Empty;
    public WalletOwnerType OwnerType { get; init; }
    public string Currency { get; init; } = "DZD";
    public decimal AvailableBalance { get; init; }
    public decimal EscrowLockedBalance { get; init; }
    public decimal PendingPayoutBalance { get; init; }
    public decimal TotalBalance => AvailableBalance + EscrowLockedBalance + PendingPayoutBalance;
    public bool IsFraudHoldActive { get; init; }
    public string? FraudHoldReason { get; init; }
    public DateTimeOffset? FraudHoldAppliedAtUtc { get; init; }
    public int ActiveDisputeCount { get; init; }
    public IReadOnlyList<string> ActiveDisputeIds { get; init; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class FraudHoldBlockedException : Exception
{
    public Guid WalletId { get; }
    public string EntityId { get; }
    public string Reason { get; }
    public int ActiveDisputes { get; }

    public FraudHoldBlockedException(Guid walletId, string entityId, string reason, int activeDisputes)
        : base($"Payout initiation blocked by fraud hold on wallet '{walletId}' (Entity: '{entityId}'). Reason: {reason}. Active disputes: {activeDisputes}")
    {
        WalletId = walletId;
        EntityId = entityId;
        Reason = reason;
        ActiveDisputes = activeDisputes;
    }
}

public sealed class InsufficientFundsException : Exception
{
    public decimal RequestedAmount { get; }
    public decimal AvailableBalance { get; }
    public decimal EscrowLockedBalance { get; }

    public InsufficientFundsException(decimal requested, decimal available, decimal escrowLocked = 0m)
        : base($"Insufficient available balance for payout. Requested: {requested:F2}, Available: {available:F2} (Escrow Locked: {escrowLocked:F2})")
    {
        RequestedAmount = requested;
        AvailableBalance = available;
        EscrowLockedBalance = escrowLocked;
    }
}

public sealed class UnsupportedPayoutCurrencyException : Exception
{
    public string Currency { get; }
    public PayoutGatewayType GatewayType { get; }

    public UnsupportedPayoutCurrencyException(string currency, PayoutGatewayType gatewayType)
        : base($"Currency '{currency}' is not supported by payout gateway adapter '{gatewayType}'.")
    {
        Currency = currency;
        GatewayType = gatewayType;
    }
}

#endregion

#region Payout Gateway Types & Beneficiary

public enum PayoutGatewayType
{
    NorthAfrican = 1,
    International = 2
}

public enum PayoutMethod
{
    // North Africa / Algeria
    BaridiMob = 1,
    SatimCib = 2,
    AlgeriePosteCcp = 3,

    // International
    SepaCreditTransfer = 10,
    StripeConnect = 11,
    WiseTransfer = 12
}

public enum PayoutBatchStatus
{
    Draft = 1,
    Processing = 2,
    Settled = 3,
    Rejected = 4
}

public sealed record PayoutBeneficiary(
    string BeneficiaryName,
    PayoutMethod Method,
    string AccountOrCardNumber,
    string? BankOrPostalIdentifier = null,
    string? PhoneNumber = null,
    string? Email = null,
    string? NationalIdOrTaxId = null
);

public sealed record PayoutValidationResult(
    bool IsValid,
    string? FormattedIdentifier,
    string? ErrorMessage
);

public sealed record PayoutItem
{
    public Guid ItemId { get; init; } = Guid.NewGuid();
    public Guid WalletId { get; init; }
    public string EntityId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "DZD";
    public PayoutBeneficiary Beneficiary { get; init; } = null!;
    public string Reference { get; init; } = string.Empty;
    public string? ExternalTransactionId { get; init; }
}

public sealed record PayoutBatch
{
    public Guid BatchId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public PayoutGatewayType GatewayType { get; init; }
    public string Currency { get; init; } = "DZD";
    public decimal TotalAmount { get; init; }
    public int ItemCount => Items.Count;
    public IReadOnlyList<PayoutItem> Items { get; init; } = [];
    public PayoutBatchStatus Status { get; init; } = PayoutBatchStatus.Draft;
    public string FormattedBatchPayload { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public string? FailureReason { get; init; }
    public Guid? SettlementJournalEntryId { get; init; }
}

public sealed record CreatePayoutItemRequest(
    Guid WalletId,
    PayoutBeneficiary Beneficiary,
    decimal Amount,
    string Reference
);

public sealed record CreatePayoutBatchRequest(
    Guid TenantId,
    string IdempotencyKey,
    PayoutGatewayType GatewayType,
    string Currency,
    IReadOnlyList<CreatePayoutItemRequest> Items
);

#endregion

#region Payout Gateway Adapters

public interface IPayoutGatewayAdapter
{
    PayoutGatewayType GatewayType { get; }
    IReadOnlyCollection<string> SupportedCurrencies { get; }
    bool SupportsMethod(PayoutMethod method);
    Task<string> FormatBatchPayloadAsync(PayoutBatch batch, CancellationToken ct = default);
    Task<PayoutValidationResult> ValidateBeneficiaryAsync(PayoutBeneficiary beneficiary, PayoutMethod method, string currency);
}

/// <summary>
/// North African payout adapter handling BaridiMob mobile wallet, SATIM CIB card switches,
/// and Algérie Poste CCP accounts with automatic Clé verification and RIP 20-digit generation.
/// </summary>
public sealed class NorthAfricanPayoutAdapter : IPayoutGatewayAdapter
{
    public PayoutGatewayType GatewayType => PayoutGatewayType.NorthAfrican;

    public IReadOnlyCollection<string> SupportedCurrencies => ["DZD"];

    public bool SupportsMethod(PayoutMethod method) => method is PayoutMethod.BaridiMob
        or PayoutMethod.SatimCib
        or PayoutMethod.AlgeriePosteCcp;

    public Task<PayoutValidationResult> ValidateBeneficiaryAsync(PayoutBeneficiary beneficiary, PayoutMethod method, string currency)
    {
        if (!string.Equals(currency, "DZD", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new PayoutValidationResult(false, null, "North African adapter only supports DZD currency."));
        }

        switch (method)
        {
            case PayoutMethod.BaridiMob:
                // Algerian mobile number (05, 06, 07 prefix, 10 digits) or 16-digit EDAHABIA card
                var cleanedPhoneOrCard = Regex.Replace(beneficiary.AccountOrCardNumber ?? string.Empty, @"\s+", "");
                if (Regex.IsMatch(cleanedPhoneOrCard, @"^(05|06|07)\d{8}$"))
                {
                    return Task.FromResult(new PayoutValidationResult(true, cleanedPhoneOrCard, null));
                }
                if (Regex.IsMatch(cleanedPhoneOrCard, @"^(\d{16})$"))
                {
                    return Task.FromResult(new PayoutValidationResult(true, cleanedPhoneOrCard, null));
                }
                return Task.FromResult(new PayoutValidationResult(false, null, "Invalid BaridiMob mobile (must start with 05/06/07) or 16-digit Edahabia card number."));

            case PayoutMethod.SatimCib:
                // SATIM Interbank CIB card: 16 digits starting with standard Algerian BINs (e.g. 50, 62, 99)
                var cleanedCard = Regex.Replace(beneficiary.AccountOrCardNumber ?? string.Empty, @"\s+", "");
                if (Regex.IsMatch(cleanedCard, @"^\d{16}$"))
                {
                    return Task.FromResult(new PayoutValidationResult(true, cleanedCard, null));
                }
                return Task.FromResult(new PayoutValidationResult(false, null, "Invalid SATIM CIB card number (must be 16 numeric digits)."));

            case PayoutMethod.AlgeriePosteCcp:
                // Algerian CCP Account + Clé CCP -> Formats to 20-digit RIP (Relevé d'Identité Postale)
                var rawAccount = Regex.Replace(beneficiary.AccountOrCardNumber ?? string.Empty, @"[^\d]", "");
                var rawKey = Regex.Replace(beneficiary.BankOrPostalIdentifier ?? string.Empty, @"[^\d]", "");

                if (string.IsNullOrEmpty(rawAccount) || rawAccount.Length > 10)
                {
                    return Task.FromResult(new PayoutValidationResult(false, null, "Invalid CCP account number (maximum 10 digits)."));
                }

                // If key is not provided, calculate it: Key = (Account * 100) % 97
                string key = rawKey;
                if (string.IsNullOrEmpty(key) && long.TryParse(rawAccount, out var acctNum))
                {
                    long calcKey = (acctNum * 100) % 97;
                    key = calcKey.ToString("D2");
                }
                else if (key.Length < 2)
                {
                    key = key.PadLeft(2, '0');
                }

                // 20-digit Algerian postal RIP format:
                // "00799999" (8-digit code Algérie Poste) + 10-digit padded CCP + 2-digit Key = 20 digits
                string paddedAccount = rawAccount.PadLeft(10, '0');
                string rip = $"00799999{paddedAccount}{key}";

                if (rip.Length != 20)
                {
                    return Task.FromResult(new PayoutValidationResult(false, null, "Calculated Algerian RIP length is invalid."));
                }

                return Task.FromResult(new PayoutValidationResult(true, rip, null));

            default:
                return Task.FromResult(new PayoutValidationResult(false, null, $"Unsupported payout method '{method}' in North African adapter."));
        }
    }

    public Task<string> FormatBatchPayloadAsync(PayoutBatch batch, CancellationToken ct = default)
    {
        var records = new List<object>();

        foreach (var item in batch.Items)
        {
            var val = ValidateBeneficiaryAsync(item.Beneficiary, item.Beneficiary.Method, batch.Currency).GetAwaiter().GetResult();
            string identifier = val.FormattedIdentifier ?? item.Beneficiary.AccountOrCardNumber;

            records.Add(new
            {
                payout_item_id = item.ItemId,
                beneficiary_name = item.Beneficiary.BeneficiaryName,
                method = item.Beneficiary.Method.ToString(),
                destination_identifier = identifier,
                phone_number = item.Beneficiary.PhoneNumber,
                amount_dzd = item.Amount,
                currency = batch.Currency,
                reference = item.Reference,
                national_id = item.Beneficiary.NationalIdOrTaxId
            });
        }

        var payload = new
        {
            batch_id = batch.BatchId,
            tenant_id = batch.TenantId,
            idempotency_key = batch.IdempotencyKey,
            gateway_provider = "ALGERIE_POSTE_BARIDIMOB_SATIM_SWITCH",
            currency = batch.Currency,
            total_amount = batch.TotalAmount,
            record_count = batch.Items.Count,
            created_at_utc = batch.CreatedAtUtc.ToString("O"),
            transactions = records
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return Task.FromResult(json);
    }
}

/// <summary>
/// International payout adapter formatting ISO 20022 SEPA XML (pain.001.001.03),
/// Stripe Connect transfers, and Wise batch transfers.
/// </summary>
public sealed class InternationalPayoutAdapter : IPayoutGatewayAdapter
{
    public PayoutGatewayType GatewayType => PayoutGatewayType.International;

    public IReadOnlyCollection<string> SupportedCurrencies => ["EUR", "USD", "AED"];

    public bool SupportsMethod(PayoutMethod method) => method is PayoutMethod.SepaCreditTransfer
        or PayoutMethod.StripeConnect
        or PayoutMethod.WiseTransfer;

    public Task<PayoutValidationResult> ValidateBeneficiaryAsync(PayoutBeneficiary beneficiary, PayoutMethod method, string currency)
    {
        if (!SupportedCurrencies.Contains(currency.ToUpperInvariant()))
        {
            return Task.FromResult(new PayoutValidationResult(false, null, $"Currency '{currency}' is not supported by international adapter."));
        }

        switch (method)
        {
            case PayoutMethod.SepaCreditTransfer:
                var iban = Regex.Replace(beneficiary.AccountOrCardNumber ?? string.Empty, @"\s+", "").ToUpperInvariant();
                if (iban.Length is < 15 or > 34)
                {
                    return Task.FromResult(new PayoutValidationResult(false, null, "Invalid IBAN length for SEPA transfer."));
                }
                return Task.FromResult(new PayoutValidationResult(true, iban, null));

            case PayoutMethod.StripeConnect:
                var acct = beneficiary.AccountOrCardNumber ?? string.Empty;
                if (!acct.StartsWith("acct_", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new PayoutValidationResult(false, null, "Invalid Stripe Connect account ID (must start with 'acct_')."));
                }
                return Task.FromResult(new PayoutValidationResult(true, acct, null));

            case PayoutMethod.WiseTransfer:
                var wiseRecipient = beneficiary.AccountOrCardNumber ?? string.Empty;
                if (string.IsNullOrWhiteSpace(wiseRecipient))
                {
                    return Task.FromResult(new PayoutValidationResult(false, null, "Wise recipient profile identifier cannot be empty."));
                }
                return Task.FromResult(new PayoutValidationResult(true, wiseRecipient, null));

            default:
                return Task.FromResult(new PayoutValidationResult(false, null, $"Unsupported method '{method}'."));
        }
    }

    public Task<string> FormatBatchPayloadAsync(PayoutBatch batch, CancellationToken ct = default)
    {
        // If SEPA Credit Transfer in EUR, format ISO 20022 pain.001.001.03 XML
        if (batch.Currency.Equals("EUR", StringComparison.OrdinalIgnoreCase) &&
            batch.Items.All(i => i.Beneficiary.Method == PayoutMethod.SepaCreditTransfer))
        {
            return Task.FromResult(FormatSepaXml(batch));
        }

        // Otherwise format standardized multi-currency JSON payload for Stripe Connect / Wise
        var records = new List<object>();

        foreach (var item in batch.Items)
        {
            records.Add(new
            {
                payout_item_id = item.ItemId,
                beneficiary_name = item.Beneficiary.BeneficiaryName,
                method = item.Beneficiary.Method.ToString(),
                account_identifier = item.Beneficiary.AccountOrCardNumber,
                bank_identifier_bic = item.Beneficiary.BankOrPostalIdentifier,
                email = item.Beneficiary.Email,
                amount = item.Amount,
                currency = batch.Currency,
                reference = item.Reference
            });
        }

        var payload = new
        {
            batch_id = batch.BatchId,
            tenant_id = batch.TenantId,
            idempotency_key = batch.IdempotencyKey,
            gateway_type = batch.GatewayType.ToString(),
            currency = batch.Currency,
            total_amount = batch.TotalAmount,
            item_count = batch.Items.Count,
            created_at_utc = batch.CreatedAtUtc.ToString("O"),
            transfers = records
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return Task.FromResult(json);
    }

    private static string FormatSepaXml(PayoutBatch batch)
    {
        var msgId = $"SEPA-BATCH-{batch.BatchId:N}".ToUpperInvariant()[..20];
        var ctrlSum = batch.TotalAmount.ToString("F2", CultureInfo.InvariantCulture);
        var nbOfTxs = batch.Items.Count;
        var creationDate = batch.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<Document xmlns=\"urn:iso:std:iso:20022:tech:xsd:pain.001.001.03\">");
        sb.AppendLine("  <CstmrCdtTrfInitn>");
        sb.AppendLine("    <GrpHdr>");
        sb.AppendLine($"      <MsgId>{msgId}</MsgId>");
        sb.AppendLine($"      <CreDtTm>{creationDate}</CreDtTm>");
        sb.AppendLine($"      <NbOfTxs>{nbOfTxs}</NbOfTxs>");
        sb.AppendLine($"      <CtrlSum>{ctrlSum}</CtrlSum>");
        sb.AppendLine("      <InitgPty><Nm>SGROWING GLOBAL INC</Nm></InitgPty>");
        sb.AppendLine("    </GrpHdr>");
        sb.AppendLine("    <PmtInf>");
        sb.AppendLine($"      <PmtInfId>PMT-{batch.BatchId:N}</PmtInfId>");
        sb.AppendLine("      <PmtMtd>TRF</PmtMtd>");
        sb.AppendLine($"      <NbOfTxs>{nbOfTxs}</NbOfTxs>");
        sb.AppendLine($"      <CtrlSum>{ctrlSum}</CtrlSum>");
        sb.AppendLine("      <PmtTpInf><SvcLvl><Cd>SEPA</Cd></SvcLvl></PmtTpInf>");
        sb.AppendLine($"      <ReqdExctnDt>{batch.CreatedAtUtc:yyyy-MM-dd}</ReqdExctnDt>");
        sb.AppendLine("      <Dbtr><Nm>SGROWING ESCROW TRUST</Nm></Dbtr>");
        sb.AppendLine("      <DbtrAcct><Id><IBAN>FR7630006000011234567890189</IBAN></Id></DbtrAcct>");
        sb.AppendLine("      <DbtrAgt><FinInstnId><BIC>BNPAFR21</BIC></FinInstnId></DbtrAgt>");

        foreach (var item in batch.Items)
        {
            var amt = item.Amount.ToString("F2", CultureInfo.InvariantCulture);
            var bic = !string.IsNullOrWhiteSpace(item.Beneficiary.BankOrPostalIdentifier)
                ? item.Beneficiary.BankOrPostalIdentifier
                : "NOTPROVIDED";

            sb.AppendLine("      <CdtTrfTxInf>");
            sb.AppendLine("        <PmtId>");
            sb.AppendLine($"          <EndToEndId>{item.Reference}</EndToEndId>");
            sb.AppendLine("        </PmtId>");
            sb.AppendLine($"        <Amt><InstdAmt Ccy=\"EUR\">{amt}</InstdAmt></Amt>");
            sb.AppendLine("        <CdtrAgt><FinInstnId><BIC>" + bic + "</BIC></FinInstnId></CdtrAgt>");
            sb.AppendLine($"        <Cdtr><Nm>{item.Beneficiary.BeneficiaryName}</Nm></Cdtr>");
            sb.AppendLine($"        <CdtrAcct><Id><IBAN>{item.Beneficiary.AccountOrCardNumber}</IBAN></Id></CdtrAcct>");
            sb.AppendLine($"        <RmtInf><Ustrd>SGrowing Payout Ref: {item.Reference}</Ustrd></RmtInf>");
            sb.AppendLine("      </CdtTrfTxInf>");
        }

        sb.AppendLine("    </PmtInf>");
        sb.AppendLine("  </CstmrCdtTrfInitn>");
        sb.AppendLine("</Document>");

        return sb.ToString();
    }
}

#endregion

#region Virtual Wallet & Payout Service Interface

public interface IVirtualWalletPayoutService
{
    Task<VirtualWallet> GetOrCreateWalletAsync(Guid tenantId, string entityId, WalletOwnerType ownerType, string currency = "DZD", CancellationToken ct = default);
    Task<VirtualWallet> GetWalletAsync(Guid walletId, CancellationToken ct = default);
    Task<VirtualWallet?> FindWalletByEntityAsync(Guid tenantId, string entityId, string currency = "DZD", CancellationToken ct = default);
    Task<VirtualWallet> CreditAvailableBalanceAsync(Guid walletId, decimal amount, string referenceType, string referenceId, string description, CancellationToken ct = default);
    Task<VirtualWallet> LockEscrowBalanceAsync(Guid walletId, decimal amount, string referenceId, CancellationToken ct = default);
    Task<VirtualWallet> ReleaseEscrowToAvailableAsync(Guid walletId, decimal escrowToUnlock, decimal availableToCredit, string referenceId, CancellationToken ct = default);
    Task<VirtualWallet> UnlockEscrowBalanceAsync(Guid walletId, decimal amount, string referenceId, CancellationToken ct = default);
    Task<VirtualWallet> SetFraudHoldAsync(Guid walletId, bool isHold, string reason, CancellationToken ct = default);
    Task<VirtualWallet> RecordDisputeAsync(Guid walletId, string disputeId, bool isOpened, CancellationToken ct = default);
    Task<PayoutBatch> CreatePayoutBatchAsync(CreatePayoutBatchRequest request, CancellationToken ct = default);
    Task<PayoutBatch> ConfirmPayoutBatchProcessedAsync(Guid batchId, string externalReference, CancellationToken ct = default);
    Task<PayoutBatch> RejectPayoutBatchAsync(Guid batchId, string failureReason, CancellationToken ct = default);
    Task<PayoutBatch?> GetPayoutBatchByIdAsync(Guid batchId, CancellationToken ct = default);
}

#endregion

#region Virtual Wallet & Payout Service Implementation

public sealed class VirtualWalletPayoutService : IVirtualWalletPayoutService
{
    private readonly ConcurrentDictionary<Guid, VirtualWallet> _wallets = new();
    private readonly ConcurrentDictionary<(Guid TenantId, string IdempotencyKey), PayoutBatch> _idempotentBatches = new();
    private readonly ConcurrentDictionary<Guid, PayoutBatch> _batchesById = new();
    private readonly IEnumerable<IPayoutGatewayAdapter> _adapters;
    private readonly IDoubleEntryLedgerService _ledgerService;
    private readonly Lock _walletSyncLock = new();

    public VirtualWalletPayoutService(
        IEnumerable<IPayoutGatewayAdapter> adapters,
        IDoubleEntryLedgerService ledgerService)
    {
        _adapters = adapters;
        _ledgerService = ledgerService;
    }

    public Task<VirtualWallet> GetOrCreateWalletAsync(Guid tenantId, string entityId, WalletOwnerType ownerType, string currency = "DZD", CancellationToken ct = default)
    {
        lock (_walletSyncLock)
        {
            var existing = _wallets.Values.FirstOrDefault(w =>
                w.TenantId == tenantId &&
                string.Equals(w.EntityId, entityId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(w.Currency, currency, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                return Task.FromResult(existing);
            }

            var wallet = new VirtualWallet
            {
                WalletId = Guid.NewGuid(),
                TenantId = tenantId,
                EntityId = entityId,
                OwnerType = ownerType,
                Currency = currency.ToUpperInvariant(),
                AvailableBalance = 0m,
                EscrowLockedBalance = 0m,
                PendingPayoutBalance = 0m,
                IsFraudHoldActive = false,
                ActiveDisputeCount = 0
            };

            _wallets[wallet.WalletId] = wallet;
            return Task.FromResult(wallet);
        }
    }

    public Task<VirtualWallet> GetWalletAsync(Guid walletId, CancellationToken ct = default)
    {
        if (_wallets.TryGetValue(walletId, out var wallet))
        {
            return Task.FromResult(wallet);
        }

        throw new KeyNotFoundException($"Virtual wallet '{walletId}' not found.");
    }

    public Task<VirtualWallet?> FindWalletByEntityAsync(Guid tenantId, string entityId, string currency = "DZD", CancellationToken ct = default)
    {
        var match = _wallets.Values.FirstOrDefault(w =>
            w.TenantId == tenantId &&
            string.Equals(w.EntityId, entityId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(w.Currency, currency, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(match);
    }

    public Task<VirtualWallet> CreditAvailableBalanceAsync(Guid walletId, decimal amount, string referenceType, string referenceId, string description, CancellationToken ct = default)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount cannot be negative.");
        }

        lock (_walletSyncLock)
        {
            if (!_wallets.TryGetValue(walletId, out var wallet))
            {
                throw new KeyNotFoundException($"Wallet '{walletId}' not found.");
            }

            var updated = wallet with
            {
                AvailableBalance = wallet.AvailableBalance + amount,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            _wallets[walletId] = updated;
            return Task.FromResult(updated);
        }
    }

    public Task<VirtualWallet> LockEscrowBalanceAsync(Guid walletId, decimal amount, string referenceId, CancellationToken ct = default)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Escrow lock amount must be positive.");
        }

        lock (_walletSyncLock)
        {
            if (!_wallets.TryGetValue(walletId, out var wallet))
            {
                throw new KeyNotFoundException($"Wallet '{walletId}' not found.");
            }

            var updated = wallet with
            {
                EscrowLockedBalance = wallet.EscrowLockedBalance + amount,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            _wallets[walletId] = updated;
            return Task.FromResult(updated);
        }
    }

    public Task<VirtualWallet> ReleaseEscrowToAvailableAsync(Guid walletId, decimal escrowToUnlock, decimal availableToCredit, string referenceId, CancellationToken ct = default)
    {
        if (escrowToUnlock < 0m || availableToCredit < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(escrowToUnlock), "Amounts must be non-negative.");
        }

        lock (_walletSyncLock)
        {
            if (!_wallets.TryGetValue(walletId, out var wallet))
            {
                throw new KeyNotFoundException($"Wallet '{walletId}' not found.");
            }

            decimal newEscrow = Math.Max(0m, wallet.EscrowLockedBalance - escrowToUnlock);
            decimal newAvailable = wallet.AvailableBalance + availableToCredit;

            var updated = wallet with
            {
                EscrowLockedBalance = newEscrow,
                AvailableBalance = newAvailable,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            _wallets[walletId] = updated;
            return Task.FromResult(updated);
        }
    }

    public Task<VirtualWallet> UnlockEscrowBalanceAsync(Guid walletId, decimal amount, string referenceId, CancellationToken ct = default)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Unlock amount must be positive.");
        }

        lock (_walletSyncLock)
        {
            if (!_wallets.TryGetValue(walletId, out var wallet))
            {
                throw new KeyNotFoundException($"Wallet '{walletId}' not found.");
            }

            decimal newEscrow = Math.Max(0m, wallet.EscrowLockedBalance - amount);

            var updated = wallet with
            {
                EscrowLockedBalance = newEscrow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            _wallets[walletId] = updated;
            return Task.FromResult(updated);
        }
    }

    public Task<VirtualWallet> SetFraudHoldAsync(Guid walletId, bool isHold, string reason, CancellationToken ct = default)
    {
        lock (_walletSyncLock)
        {
            if (!_wallets.TryGetValue(walletId, out var wallet))
            {
                throw new KeyNotFoundException($"Wallet '{walletId}' not found.");
            }

            var updated = wallet with
            {
                IsFraudHoldActive = isHold,
                FraudHoldReason = isHold ? reason : null,
                FraudHoldAppliedAtUtc = isHold ? DateTimeOffset.UtcNow : null,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            _wallets[walletId] = updated;
            return Task.FromResult(updated);
        }
    }

    public Task<VirtualWallet> RecordDisputeAsync(Guid walletId, string disputeId, bool isOpened, CancellationToken ct = default)
    {
        lock (_walletSyncLock)
        {
            if (!_wallets.TryGetValue(walletId, out var wallet))
            {
                throw new KeyNotFoundException($"Wallet '{walletId}' not found.");
            }

            var disputes = new List<string>(wallet.ActiveDisputeIds);
            if (isOpened)
            {
                if (!disputes.Contains(disputeId))
                {
                    disputes.Add(disputeId);
                }
            }
            else
            {
                disputes.Remove(disputeId);
            }

            var updated = wallet with
            {
                ActiveDisputeCount = disputes.Count,
                ActiveDisputeIds = disputes.AsReadOnly(),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            _wallets[walletId] = updated;
            return Task.FromResult(updated);
        }
    }

    public async Task<PayoutBatch> CreatePayoutBatchAsync(CreatePayoutBatchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required for payout batch creation.", nameof(request));
        }

        if (request.Items.Count == 0)
        {
            throw new ArgumentException("Payout batch must contain at least one item.", nameof(request));
        }

        // 1. Idempotency Check: if identical batch was already submitted, return existing batch immediately
        if (_idempotentBatches.TryGetValue((request.TenantId, request.IdempotencyKey), out var existingBatch))
        {
            return existingBatch;
        }

        // 2. Select appropriate Gateway Adapter
        var adapter = _adapters.FirstOrDefault(a => a.GatewayType == request.GatewayType)
            ?? throw new InvalidOperationException($"No payout adapter registered for gateway type '{request.GatewayType}'.");

        if (!adapter.SupportedCurrencies.Contains(request.Currency.ToUpperInvariant()))
        {
            throw new UnsupportedPayoutCurrencyException(request.Currency, request.GatewayType);
        }

        lock (_walletSyncLock)
        {
            // Re-check idempotency under lock
            if (_idempotentBatches.TryGetValue((request.TenantId, request.IdempotencyKey), out var doubleCheckBatch))
            {
                return doubleCheckBatch;
            }

            // 3. Inspect Wallets: Fraud Hold checks and Available Balance checks
            var itemsToProcess = new List<PayoutItem>(request.Items.Count);
            decimal totalBatchAmount = 0m;

            foreach (var itemReq in request.Items)
            {
                if (itemReq.Amount <= 0m)
                {
                    throw new ArgumentOutOfRangeException(nameof(request), $"Payout item amount ({itemReq.Amount}) must be positive.");
                }

                if (!_wallets.TryGetValue(itemReq.WalletId, out var wallet))
                {
                    throw new KeyNotFoundException($"Wallet '{itemReq.WalletId}' not found.");
                }

                if (!string.Equals(wallet.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Wallet currency '{wallet.Currency}' does not match payout batch currency '{request.Currency}'.");
                }

                // FRAUD HOLD CHECK
                if (wallet.IsFraudHoldActive || wallet.ActiveDisputeCount > 0)
                {
                    string reason = wallet.IsFraudHoldActive
                        ? (wallet.FraudHoldReason ?? "Active security/fraud hold applied.")
                        : $"Entity has {wallet.ActiveDisputeCount} unresolved disputes/chargebacks.";

                    throw new FraudHoldBlockedException(wallet.WalletId, wallet.EntityId, reason, wallet.ActiveDisputeCount);
                }

                // AVAILABLE BALANCE CHECK
                if (wallet.AvailableBalance < itemReq.Amount)
                {
                    throw new InsufficientFundsException(itemReq.Amount, wallet.AvailableBalance, wallet.EscrowLockedBalance);
                }

                totalBatchAmount += itemReq.Amount;

                itemsToProcess.Add(new PayoutItem
                {
                    ItemId = Guid.NewGuid(),
                    WalletId = itemReq.WalletId,
                    EntityId = wallet.EntityId,
                    Amount = itemReq.Amount,
                    Currency = request.Currency,
                    Beneficiary = itemReq.Beneficiary,
                    Reference = itemReq.Reference
                });
            }

            // 4. Move funds: AvailableBalance -> PendingPayoutBalance
            foreach (var item in itemsToProcess)
            {
                var wallet = _wallets[item.WalletId];
                _wallets[item.WalletId] = wallet with
                {
                    AvailableBalance = wallet.AvailableBalance - item.Amount,
                    PendingPayoutBalance = wallet.PendingPayoutBalance + item.Amount,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
            }

            // 5. Build Batch and Format Payload
            var batch = new PayoutBatch
            {
                BatchId = Guid.NewGuid(),
                TenantId = request.TenantId,
                IdempotencyKey = request.IdempotencyKey,
                GatewayType = request.GatewayType,
                Currency = request.Currency,
                TotalAmount = totalBatchAmount,
                Items = itemsToProcess.AsReadOnly(),
                Status = PayoutBatchStatus.Processing,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            // Format payload with adapter
            var formattedPayload = adapter.FormatBatchPayloadAsync(batch, ct).GetAwaiter().GetResult();
            batch = batch with { FormattedBatchPayload = formattedPayload };

            _batchesById[batch.BatchId] = batch;
            _idempotentBatches[(request.TenantId, request.IdempotencyKey)] = batch;

            return batch;
        }
    }

    public async Task<PayoutBatch> ConfirmPayoutBatchProcessedAsync(Guid batchId, string externalReference, CancellationToken ct = default)
    {
        PayoutBatch batch;

        lock (_walletSyncLock)
        {
            if (!_batchesById.TryGetValue(batchId, out var existing))
            {
                throw new KeyNotFoundException($"Payout batch '{batchId}' not found.");
            }

            if (existing.Status == PayoutBatchStatus.Settled)
            {
                return existing;
            }

            // Deduct PendingPayoutBalance from wallets
            foreach (var item in existing.Items)
            {
                if (_wallets.TryGetValue(item.WalletId, out var wallet))
                {
                    _wallets[item.WalletId] = wallet with
                    {
                        PendingPayoutBalance = Math.Max(0m, wallet.PendingPayoutBalance - item.Amount),
                        UpdatedAtUtc = DateTimeOffset.UtcNow
                    };
                }
            }

            batch = existing with
            {
                Status = PayoutBatchStatus.Settled,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };

            _batchesById[batchId] = batch;
            _idempotentBatches[(batch.TenantId, batch.IdempotencyKey)] = batch;
        }

        // Post Double-Entry Journal:
        // Debit: Accounts Payable (2000 Merchant or 2100 Courier or 2200 Affiliate)
        // Credit: Cash & Courier Float (1000)
        var journalLines = new List<JournalLineRequest>();

        foreach (var item in batch.Items)
        {
            string liabilityAccount = StandardAccountCodes.MerchantAccountsPayable;
            if (_wallets.TryGetValue(item.WalletId, out var w))
            {
                liabilityAccount = w.OwnerType switch
                {
                    WalletOwnerType.Courier => StandardAccountCodes.CourierRemittancePayable,
                    WalletOwnerType.Affiliate => StandardAccountCodes.AffiliateCommissionPayable,
                    _ => StandardAccountCodes.MerchantAccountsPayable
                };
            }

            journalLines.Add(new JournalLineRequest(
                AccountCode: liabilityAccount,
                Debit: item.Amount,
                Credit: 0m,
                Description: $"Payout disbursement for {item.Beneficiary.BeneficiaryName} ({item.Reference})",
                EntityId: item.EntityId
            ));
        }

        journalLines.Add(new JournalLineRequest(
            AccountCode: StandardAccountCodes.CashAndCourierFloat,
            Debit: 0m,
            Credit: batch.TotalAmount,
            Description: $"Cash outflow for payout batch {batch.BatchId} via {batch.GatewayType}"
        ));

        var postReq = new PostJournalEntryRequest(
            TenantId: batch.TenantId,
            ReferenceType: "PayoutDisbursement",
            ReferenceId: batch.BatchId.ToString(),
            Description: $"Payout batch disbursement ({externalReference})",
            Lines: journalLines,
            Currency: batch.Currency
        );

        var entry = await _ledgerService.PostEntryAsync(postReq, ct);

        lock (_walletSyncLock)
        {
            batch = batch with { SettlementJournalEntryId = entry.EntryId };
            _batchesById[batchId] = batch;
            _idempotentBatches[(batch.TenantId, batch.IdempotencyKey)] = batch;
        }

        return batch;
    }

    public Task<PayoutBatch> RejectPayoutBatchAsync(Guid batchId, string failureReason, CancellationToken ct = default)
    {
        lock (_walletSyncLock)
        {
            if (!_batchesById.TryGetValue(batchId, out var existing))
            {
                throw new KeyNotFoundException($"Payout batch '{batchId}' not found.");
            }

            if (existing.Status == PayoutBatchStatus.Settled)
            {
                throw new InvalidOperationException($"Cannot reject a payout batch that has already been settled.");
            }

            // Restore funds: PendingPayoutBalance -> AvailableBalance
            foreach (var item in existing.Items)
            {
                if (_wallets.TryGetValue(item.WalletId, out var wallet))
                {
                    _wallets[item.WalletId] = wallet with
                    {
                        PendingPayoutBalance = Math.Max(0m, wallet.PendingPayoutBalance - item.Amount),
                        AvailableBalance = wallet.AvailableBalance + item.Amount,
                        UpdatedAtUtc = DateTimeOffset.UtcNow
                    };
                }
            }

            var updated = existing with
            {
                Status = PayoutBatchStatus.Rejected,
                FailureReason = failureReason,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };

            _batchesById[batchId] = updated;
            _idempotentBatches[(updated.TenantId, updated.IdempotencyKey)] = updated;
            return Task.FromResult(updated);
        }
    }

    public Task<PayoutBatch?> GetPayoutBatchByIdAsync(Guid batchId, CancellationToken ct = default)
    {
        _batchesById.TryGetValue(batchId, out var batch);
        return Task.FromResult(batch);
    }
}

#endregion
