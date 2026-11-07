namespace SGrowing.Api.Modules.Commerce.B2B.ContractPricing;

/// <summary>
/// Defines the pricing tier classification for B2B contract quotes.
/// </summary>
public enum PriceTierType
{
    Standard,
    VolumeDiscount,
    CustomerSpecific,
    ContractFixed
}

/// <summary>
/// Defines the approval status workflow for enterprise B2B quotes.
/// </summary>
public enum QuoteApprovalStatus
{
    Draft,
    AutoApproved,
    PendingManagerApproval,
    PendingVpApproval,
    Approved,
    Rejected
}

/// <summary>
/// Represents a tiered volume discount band.
/// </summary>
public record VolumeDiscountTier(
    int MinQuantity,
    int? MaxQuantity,
    decimal UnitPriceSar,
    decimal DiscountPercent);

/// <summary>
/// Represents a contract item with associated volume discount tiers.
/// </summary>
public record ContractPriceListItem(
    string Sku,
    string ProductName,
    decimal BasePriceSar,
    List<VolumeDiscountTier> VolumeTiers,
    decimal? MinimumMarginPercent = 15m);

/// <summary>
/// Represents an account or tenant-level contract price list.
/// </summary>
public record B2BContractPriceList(
    Guid PriceListId,
    Guid TenantId,
    string Name,
    string Currency,
    Guid? TargetAccountId,
    bool IsDefault,
    List<ContractPriceListItem> Items,
    DateTime ValidFromUtc,
    DateTime ValidToUtc);

/// <summary>
/// Request payload to calculate real-time B2B tiered price for a SKU and quantity.
/// </summary>
public record CalculateB2BPriceRequest(
    Guid TenantId,
    Guid? AccountId,
    string Sku,
    int Quantity);

/// <summary>
/// Response payload for a calculated B2B unit and gross price.
/// </summary>
public record CalculateB2BPriceResponse(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPriceSar,
    decimal TotalGrossSar,
    decimal DiscountAmountSar,
    decimal EffectiveDiscountPercent,
    string AppliedPriceList);

/// <summary>
/// Request payload to generate a new B2B quote with approval routing.
/// </summary>
public record CreateB2BQuoteRequest(
    Guid TenantId,
    Guid AccountId,
    string ContactName,
    string ContactEmail,
    List<B2BQuoteLineItemRequest> Items,
    string Notes);

/// <summary>
/// Line item request for creating a B2B quote.
/// </summary>
public record B2BQuoteLineItemRequest(
    string Sku,
    int Quantity,
    decimal? RequestedUnitPriceSar = null);

/// <summary>
/// Generated enterprise quote with approval governance.
/// </summary>
public record B2BQuoteResponse(
    Guid QuoteId,
    Guid TenantId,
    string QuoteNumber,
    Guid AccountId,
    string ContactName,
    decimal TotalGrossSar,
    decimal TotalDiscountSar,
    decimal TotalNetSar,
    QuoteApprovalStatus Status,
    List<B2BQuoteLineItemResponse> Items,
    string RequiredApproverRole,
    DateTime CreatedAtUtc);

/// <summary>
/// Line item detail inside a quote response.
/// </summary>
public record B2BQuoteLineItemResponse(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPriceSar,
    decimal LineTotalSar,
    decimal MarginPercent);

/// <summary>
/// Request to approve or reject a pending B2B quote.
/// </summary>
public record ApproveB2BQuoteRequest(
    Guid TenantId,
    Guid QuoteId,
    string ApproverEmail,
    string ApproverRole,
    bool Approved,
    string? RejectionReason = null);
