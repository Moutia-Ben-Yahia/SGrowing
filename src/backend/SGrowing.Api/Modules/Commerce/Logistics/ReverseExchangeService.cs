using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Commerce.Logistics;

public enum QuarantineItemCondition
{
    Pristine = 1,
    DamagedBox = 2,
    ItemDefective = 3,
    WrongItemReturned = 4
}

public enum ReverseExchangeStatus
{
    Initiated = 1,
    WaybillsGenerated = 2,
    DispatchedForDoorstepSwap = 3,
    SwapCompletedAtDoorstep = 4,
    ReturnedToQuarantine = 5,
    CompletedPristine = 6,
    CompletedDamagedBoxDiscount = 7,
    CompletedDefectiveVerified = 8,
    QuarantinedFraudIncident = 9,
    Rejected = 10
}

public enum RefundProcessingStatus
{
    None = 0,
    PendingDoorstepInspection = 1,
    ApprovedAndProcessed = 2,
    ProcessedWithRepackagingDeduction = 3,
    BlockedDueToFraud = 4
}

public record DualLinkedWaybills(
    string ForwardWaybillNumber,
    string ReverseWaybillNumber,
    Guid LinkedExchangeId,
    DateTime GeneratedAtUtc);

public record ReverseExchangeInitiationRequest(
    Guid TenantId,
    Guid OriginalOrderId,
    string CustomerName,
    string CustomerPhone,
    string DeliveryAddress,
    string City,
    string OriginalSku,
    decimal OriginalSkuPrice,
    string ReplacementSku,
    decimal ReplacementSkuPrice,
    string ExchangeReason,
    string CourierName = "Aramex");

public record DoorstepSwapRequest(
    Guid TenantId,
    Guid ExchangeId,
    string ScannedReverseWaybill,
    decimal CashCollected,
    bool ReplacementHandedOver,
    string? CourierAgentId = null);

public record QuarantineInspectionRequest(
    Guid TenantId,
    Guid ExchangeId,
    string ScannedReturnBarcode,
    QuarantineItemCondition Condition,
    string InspectorNotes,
    string InspectorId);

public record ReverseCodFraudIncident(
    Guid IncidentId,
    Guid ExchangeId,
    Guid TenantId,
    string CustomerPhone,
    string Reason,
    DateTime ReportedAtUtc,
    string ActionTaken);

public class ReverseExchangeManifest
{
    public Guid ExchangeId { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OriginalOrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string CourierName { get; set; } = string.Empty;
    public string OriginalSku { get; set; } = string.Empty;
    public decimal OriginalSkuPrice { get; set; }
    public string ReplacementSku { get; set; } = string.Empty;
    public decimal ReplacementSkuPrice { get; set; }
    public string ExchangeReason { get; set; } = string.Empty;

    public DualLinkedWaybills Waybills { get; set; } = default!;

    public decimal CashDifference { get; set; }
    public bool CustomerOwesDifference { get; set; }
    public decimal AmountDueFromCustomer { get; set; }
    public decimal RefundDueToCustomer { get; set; }
    public decimal CashCollectedAtDoorstep { get; set; }

    public ReverseExchangeStatus Status { get; set; } = ReverseExchangeStatus.Initiated;
    public RefundProcessingStatus RefundStatus { get; set; } = RefundProcessingStatus.None;
    public bool HandoverConfirmed { get; set; }
    public bool ReturnCollectedConfirmed { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DoorstepCompletedAtUtc { get; set; }
    public DateTime? QuarantinedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public QuarantineItemCondition? InspectionCondition { get; set; }
    public string? InspectorNotes { get; set; }
    public bool RestockedToInventory { get; set; }
    public ReverseCodFraudIncident? FraudIncident { get; set; }
}

public record QuarantineInspectionResult(
    Guid ExchangeId,
    QuarantineItemCondition Condition,
    ReverseExchangeStatus NewStatus,
    RefundProcessingStatus RefundStatus,
    decimal RefundAmountIssued,
    bool RestockedToInventory,
    bool FraudIncidentLogged,
    ReverseCodFraudIncident? FraudIncident,
    string SummaryMessage);

public interface IReverseExchangeService
{
    Task<ReverseExchangeManifest> InitiateReverseExchangeAsync(
        Guid tenantId,
        ReverseExchangeInitiationRequest request,
        CancellationToken ct = default);

    Task<ReverseExchangeManifest> ExecuteDoorstepSwapAsync(
        Guid tenantId,
        DoorstepSwapRequest request,
        CancellationToken ct = default);

    Task<QuarantineInspectionResult> ProcessQuarantineInspectionAsync(
        Guid tenantId,
        QuarantineInspectionRequest request,
        CancellationToken ct = default);

    Task<ReverseExchangeManifest?> GetExchangeManifestAsync(
        Guid tenantId,
        Guid exchangeId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ReverseExchangeManifest>> ListExchangesAsync(
        Guid tenantId,
        ReverseExchangeStatus? status = null,
        CancellationToken ct = default);
}

public class ReverseExchangeService : IReverseExchangeService
{
    private readonly ConcurrentDictionary<(Guid TenantId, Guid ExchangeId), ReverseExchangeManifest> _manifests = new();

    public Task<ReverseExchangeManifest> InitiateReverseExchangeAsync(
        Guid tenantId,
        ReverseExchangeInitiationRequest request,
        CancellationToken ct = default)
    {
        var exchangeId = Guid.NewGuid();
        var shortId = exchangeId.ToString("N")[..8].ToUpperInvariant();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        var fwdWaybill = $"WB-FWD-{shortId}-{timestamp}";
        var revWaybill = $"WB-REV-{shortId}-{timestamp}";
        var waybills = new DualLinkedWaybills(fwdWaybill, revWaybill, exchangeId, DateTime.UtcNow);

        // Cash difference calculation
        decimal cashDiff = request.ReplacementSkuPrice - request.OriginalSkuPrice;
        bool customerOwes = cashDiff > 0m;
        decimal amountDue = customerOwes ? cashDiff : 0m;
        decimal refundDue = !customerOwes && cashDiff < 0m ? Math.Abs(cashDiff) : 0m;
        var initialRefundStatus = refundDue > 0m
            ? RefundProcessingStatus.PendingDoorstepInspection
            : RefundProcessingStatus.None;

        var manifest = new ReverseExchangeManifest
        {
            ExchangeId = exchangeId,
            TenantId = tenantId,
            OriginalOrderId = request.OriginalOrderId,
            CustomerName = request.CustomerName.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            DeliveryAddress = request.DeliveryAddress.Trim(),
            City = request.City.Trim(),
            CourierName = request.CourierName.Trim(),
            OriginalSku = request.OriginalSku.Trim(),
            OriginalSkuPrice = request.OriginalSkuPrice,
            ReplacementSku = request.ReplacementSku.Trim(),
            ReplacementSkuPrice = request.ReplacementSkuPrice,
            ExchangeReason = request.ExchangeReason.Trim(),
            Waybills = waybills,
            CashDifference = cashDiff,
            CustomerOwesDifference = customerOwes,
            AmountDueFromCustomer = amountDue,
            RefundDueToCustomer = refundDue,
            CashCollectedAtDoorstep = 0m,
            Status = ReverseExchangeStatus.DispatchedForDoorstepSwap,
            RefundStatus = initialRefundStatus,
            HandoverConfirmed = false,
            ReturnCollectedConfirmed = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _manifests[(tenantId, exchangeId)] = manifest;
        return Task.FromResult(manifest);
    }

    public Task<ReverseExchangeManifest> ExecuteDoorstepSwapAsync(
        Guid tenantId,
        DoorstepSwapRequest request,
        CancellationToken ct = default)
    {
        if (!_manifests.TryGetValue((tenantId, request.ExchangeId), out var manifest))
        {
            throw new KeyNotFoundException($"Reverse exchange manifest {request.ExchangeId} not found for tenant {tenantId}.");
        }

        // Strict Enforcement: Courier delivers replacement SKU ONLY upon scanning and collecting the returned package
        var scannedCode = request.ScannedReverseWaybill?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(scannedCode) ||
            !string.Equals(scannedCode, manifest.Waybills.ReverseWaybillNumber, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Doorstep handover blocked: Courier must scan and verify the valid return package waybill ({manifest.Waybills.ReverseWaybillNumber}). Scanned: '{scannedCode}'.");
        }

        if (!request.ReplacementHandedOver)
        {
            throw new InvalidOperationException("Doorstep swap incomplete: Replacement package handover was not confirmed.");
        }

        if (manifest.CustomerOwesDifference && request.CashCollected < manifest.AmountDueFromCustomer)
        {
            throw new InvalidOperationException(
                $"Doorstep handover blocked: Customer owes cash difference of {manifest.AmountDueFromCustomer:C2}, but courier collected only {request.CashCollected:C2}.");
        }

        manifest.CashCollectedAtDoorstep = request.CashCollected;
        manifest.HandoverConfirmed = true;
        manifest.ReturnCollectedConfirmed = true;
        manifest.DoorstepCompletedAtUtc = DateTime.UtcNow;
        manifest.Status = ReverseExchangeStatus.SwapCompletedAtDoorstep;

        return Task.FromResult(manifest);
    }

    public Task<QuarantineInspectionResult> ProcessQuarantineInspectionAsync(
        Guid tenantId,
        QuarantineInspectionRequest request,
        CancellationToken ct = default)
    {
        if (!_manifests.TryGetValue((tenantId, request.ExchangeId), out var manifest))
        {
            throw new KeyNotFoundException($"Reverse exchange manifest {request.ExchangeId} not found for tenant {tenantId}.");
        }

        var scanned = request.ScannedReturnBarcode?.Trim() ?? string.Empty;
        bool barcodeMatches = string.Equals(scanned, manifest.Waybills.ReverseWaybillNumber, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(scanned, manifest.OriginalSku, StringComparison.OrdinalIgnoreCase);

        if (!barcodeMatches)
        {
            throw new InvalidOperationException(
                $"Quarantine intake failed: Scanned barcode '{scanned}' does not match return waybill '{manifest.Waybills.ReverseWaybillNumber}' or original SKU '{manifest.OriginalSku}'.");
        }

        manifest.QuarantinedAtUtc = DateTime.UtcNow;
        manifest.InspectionCondition = request.Condition;
        manifest.InspectorNotes = request.InspectorNotes.Trim();

        QuarantineInspectionResult result;

        switch (request.Condition)
        {
            case QuarantineItemCondition.Pristine:
                manifest.RestockedToInventory = true;
                manifest.Status = ReverseExchangeStatus.CompletedPristine;
                decimal pristineRefund = manifest.RefundDueToCustomer;
                manifest.RefundStatus = pristineRefund > 0m
                    ? RefundProcessingStatus.ApprovedAndProcessed
                    : RefundProcessingStatus.None;
                manifest.CompletedAtUtc = DateTime.UtcNow;

                result = new QuarantineInspectionResult(
                    manifest.ExchangeId,
                    request.Condition,
                    manifest.Status,
                    manifest.RefundStatus,
                    pristineRefund,
                    RestockedToInventory: true,
                    FraudIncidentLogged: false,
                    FraudIncident: null,
                    SummaryMessage: $"Return inspected as Pristine. Item restocked to inventory. Refund of ${pristineRefund:F2} released.");
                break;

            case QuarantineItemCondition.DamagedBox:
                manifest.RestockedToInventory = true;
                manifest.Status = ReverseExchangeStatus.CompletedDamagedBoxDiscount;
                decimal packagingFee = manifest.RefundDueToCustomer > 5m ? 5m : 0m;
                decimal discountedRefund = Math.Max(0m, manifest.RefundDueToCustomer - packagingFee);
                manifest.RefundStatus = discountedRefund > 0m
                    ? RefundProcessingStatus.ProcessedWithRepackagingDeduction
                    : RefundProcessingStatus.None;
                manifest.CompletedAtUtc = DateTime.UtcNow;

                result = new QuarantineInspectionResult(
                    manifest.ExchangeId,
                    request.Condition,
                    manifest.Status,
                    manifest.RefundStatus,
                    discountedRefund,
                    RestockedToInventory: true,
                    FraudIncidentLogged: false,
                    FraudIncident: null,
                    SummaryMessage: $"Return packaging damaged. Restocked to open-box inventory. Refund adjusted to ${discountedRefund:F2}.");
                break;

            case QuarantineItemCondition.ItemDefective:
                manifest.RestockedToInventory = false;
                manifest.Status = ReverseExchangeStatus.CompletedDefectiveVerified;
                decimal defectiveRefund = manifest.RefundDueToCustomer;
                manifest.RefundStatus = defectiveRefund > 0m
                    ? RefundProcessingStatus.ApprovedAndProcessed
                    : RefundProcessingStatus.None;
                manifest.CompletedAtUtc = DateTime.UtcNow;

                result = new QuarantineInspectionResult(
                    manifest.ExchangeId,
                    request.Condition,
                    manifest.Status,
                    manifest.RefundStatus,
                    defectiveRefund,
                    RestockedToInventory: false,
                    FraudIncidentLogged: false,
                    FraudIncident: null,
                    SummaryMessage: $"Manufacturer defect verified. Routed to supplier RMA quarantine. Customer refund of ${defectiveRefund:F2} processed.");
                break;

            case QuarantineItemCondition.WrongItemReturned:
                manifest.RestockedToInventory = false;
                manifest.Status = ReverseExchangeStatus.QuarantinedFraudIncident;
                manifest.RefundStatus = RefundProcessingStatus.BlockedDueToFraud;
                manifest.CompletedAtUtc = DateTime.UtcNow;

                var incident = new ReverseCodFraudIncident(
                    Guid.NewGuid(),
                    manifest.ExchangeId,
                    tenantId,
                    manifest.CustomerPhone,
                    $"Wrong item or counterfeit/empty parcel returned during reverse exchange. Inspector note: '{request.InspectorNotes}'.",
                    DateTime.UtcNow,
                    "RefundBlocked_CustomerBlacklistFlagged");

                manifest.FraudIncident = incident;

                result = new QuarantineInspectionResult(
                    manifest.ExchangeId,
                    request.Condition,
                    manifest.Status,
                    manifest.RefundStatus,
                    RefundAmountIssued: 0m,
                    RestockedToInventory: false,
                    FraudIncidentLogged: true,
                    FraudIncident: incident,
                    SummaryMessage: "SECURITY ALERT: Wrong or counterfeit item returned. Customer flagged for COD fraud; all refunds blocked.");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(request.Condition), request.Condition, "Unknown quarantine condition.");
        }

        return Task.FromResult(result);
    }

    public Task<ReverseExchangeManifest?> GetExchangeManifestAsync(
        Guid tenantId,
        Guid exchangeId,
        CancellationToken ct = default)
    {
        _manifests.TryGetValue((tenantId, exchangeId), out var manifest);
        return Task.FromResult(manifest);
    }

    public Task<IReadOnlyList<ReverseExchangeManifest>> ListExchangesAsync(
        Guid tenantId,
        ReverseExchangeStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _manifests.Values.Where(m => m.TenantId == tenantId);
        if (status.HasValue)
        {
            query = query.Where(m => m.Status == status.Value);
        }

        IReadOnlyList<ReverseExchangeManifest> list = query.OrderByDescending(m => m.CreatedAtUtc).ToList();
        return Task.FromResult(list);
    }
}
