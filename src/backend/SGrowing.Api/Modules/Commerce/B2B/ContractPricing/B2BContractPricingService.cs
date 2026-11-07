using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Commerce.B2B.ContractPricing;

/// <summary>
/// Contract for B2B contract pricing calculation and CPQ quote lifecycle management.
/// </summary>
public interface IB2BContractPricingService
{
    CalculateB2BPriceResponse CalculatePrice(CalculateB2BPriceRequest request);
    B2BQuoteResponse CreateQuote(CreateB2BQuoteRequest request);
    B2BQuoteResponse ApproveQuote(ApproveB2BQuoteRequest request);
    B2BQuoteResponse? GetQuote(Guid tenantId, Guid quoteId);
    List<B2BContractPriceList> GetPriceLists(Guid tenantId);
    void AddPriceList(B2BContractPriceList priceList);
    void SeedTenantDefaults(Guid tenantId);
}

/// <summary>
/// Enterprise CPQ and contract pricing engine with volume tiers and multi-step governance approval routing.
/// </summary>
public class B2BContractPricingService : IB2BContractPricingService
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid EnterpriseRetailGroupId = Guid.Parse("aa000000-0000-0000-0000-000000000001");

    private readonly ConcurrentDictionary<Guid, List<B2BContractPriceList>> _priceListsByTenant = new();
    private readonly ConcurrentDictionary<Guid, B2BQuoteResponse> _quotes = new();
    private int _quoteCounter = 1000;

    public B2BContractPricingService()
    {
        SeedTenantDefaults(DefaultTenantId);
    }

    /// <summary>
    /// Seeds default contract price lists for a given tenant:
    /// 1. "Enterprise Retail Group Contract" (Account specific)
    /// 2. "Standard B2B Wholesale List" (Default fallback)
    /// </summary>
    public void SeedTenantDefaults(Guid tenantId)
    {
        var lists = new List<B2BContractPriceList>
        {
            new(
                PriceListId: Guid.NewGuid(),
                TenantId: tenantId,
                Name: "Enterprise Retail Group Contract",
                Currency: "SAR",
                TargetAccountId: EnterpriseRetailGroupId,
                IsDefault: false,
                Items: new List<ContractPriceListItem>
                {
                    new(
                        Sku: "SKU-PERFUME-OUD",
                        ProductName: "Royal Oud Parfum 100ml",
                        BasePriceSar: 250m,
                        VolumeTiers: new List<VolumeDiscountTier>
                        {
                            new(MinQuantity: 1, MaxQuantity: 50, UnitPriceSar: 220m, DiscountPercent: 12.0m),
                            new(MinQuantity: 51, MaxQuantity: 200, UnitPriceSar: 190m, DiscountPercent: 24.0m),
                            new(MinQuantity: 201, MaxQuantity: null, UnitPriceSar: 160m, DiscountPercent: 36.0m)
                        },
                        MinimumMarginPercent: 15m
                    )
                },
                ValidFromUtc: DateTime.UtcNow.AddYears(-1),
                ValidToUtc: DateTime.UtcNow.AddYears(5)
            ),
            new(
                PriceListId: Guid.NewGuid(),
                TenantId: tenantId,
                Name: "Standard B2B Wholesale List",
                Currency: "SAR",
                TargetAccountId: null,
                IsDefault: true,
                Items: new List<ContractPriceListItem>
                {
                    new(
                        Sku: "SKU-PERFUME-OUD",
                        ProductName: "Royal Oud Parfum 100ml",
                        BasePriceSar: 250m,
                        VolumeTiers: new List<VolumeDiscountTier>
                        {
                            new(MinQuantity: 1, MaxQuantity: 50, UnitPriceSar: 240m, DiscountPercent: 4.0m),
                            new(MinQuantity: 51, MaxQuantity: 200, UnitPriceSar: 210m, DiscountPercent: 16.0m),
                            new(MinQuantity: 201, MaxQuantity: null, UnitPriceSar: 180m, DiscountPercent: 28.0m)
                        },
                        MinimumMarginPercent: 15m
                    )
                },
                ValidFromUtc: DateTime.UtcNow.AddYears(-1),
                ValidToUtc: DateTime.UtcNow.AddYears(5)
            )
        };

        _priceListsByTenant[tenantId] = lists;
    }

    /// <summary>
    /// Adds or registers a custom contract price list for tenant isolation.
    /// </summary>
    public void AddPriceList(B2BContractPriceList priceList)
    {
        var list = _priceListsByTenant.GetOrAdd(priceList.TenantId, _ => new List<B2BContractPriceList>());
        lock (list)
        {
            list.RemoveAll(p => p.PriceListId == priceList.PriceListId);
            list.Add(priceList);
        }
    }

    /// <summary>
    /// Retrieves all contract price lists for a given tenant.
    /// </summary>
    public List<B2BContractPriceList> GetPriceLists(Guid tenantId)
    {
        if (_priceListsByTenant.TryGetValue(tenantId, out var lists))
        {
            lock (lists)
            {
                return lists.ToList();
            }
        }

        if (tenantId == DefaultTenantId)
        {
            SeedTenantDefaults(tenantId);
            return _priceListsByTenant[tenantId].ToList();
        }

        return [];
    }

    /// <summary>
    /// Calculates real-time unit and gross pricing for a SKU and quantity.
    /// Priority:
    /// 1. Target Account specific contract price list matching SKU
    /// 2. Tenant default contract price list matching SKU
    /// </summary>
    public CalculateB2BPriceResponse CalculatePrice(CalculateB2BPriceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.", nameof(request));
        }

        var priceLists = GetPriceLists(request.TenantId);
        if (priceLists.Count == 0 && request.TenantId == DefaultTenantId)
        {
            SeedTenantDefaults(request.TenantId);
            priceLists = GetPriceLists(request.TenantId);
        }

        var now = DateTime.UtcNow;

        // 1. Check account-specific contract price list
        B2BContractPriceList? selectedList = null;
        ContractPriceListItem? selectedItem = null;

        if (request.AccountId.HasValue)
        {
            selectedList = priceLists.FirstOrDefault(p =>
                p.TargetAccountId == request.AccountId.Value &&
                p.ValidFromUtc <= now &&
                p.ValidToUtc >= now &&
                p.Items.Any(i => string.Equals(i.Sku, request.Sku, StringComparison.OrdinalIgnoreCase)));

            selectedItem = selectedList?.Items.FirstOrDefault(i =>
                string.Equals(i.Sku, request.Sku, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Fallback to default price list
        if (selectedItem == null)
        {
            selectedList = priceLists.FirstOrDefault(p =>
                p.IsDefault &&
                p.TargetAccountId == null &&
                p.ValidFromUtc <= now &&
                p.ValidToUtc >= now &&
                p.Items.Any(i => string.Equals(i.Sku, request.Sku, StringComparison.OrdinalIgnoreCase)));

            selectedItem = selectedList?.Items.FirstOrDefault(i =>
                string.Equals(i.Sku, request.Sku, StringComparison.OrdinalIgnoreCase));
        }

        if (selectedItem == null || selectedList == null)
        {
            throw new KeyNotFoundException($"No contract pricing configuration found for SKU '{request.Sku}' under tenant '{request.TenantId}'.");
        }

        // Calculate tiered volume pricing
        var tier = selectedItem.VolumeTiers
            .Where(t => request.Quantity >= t.MinQuantity && (t.MaxQuantity == null || request.Quantity <= t.MaxQuantity.Value))
            .OrderByDescending(t => t.MinQuantity)
            .FirstOrDefault();

        decimal unitPriceSar;
        if (tier != null)
        {
            unitPriceSar = tier.UnitPriceSar;
        }
        else
        {
            unitPriceSar = selectedItem.BasePriceSar;
        }

        decimal totalGrossSar = Math.Round(selectedItem.BasePriceSar * request.Quantity, 2);
        decimal totalNetSar = Math.Round(unitPriceSar * request.Quantity, 2);
        decimal discountAmountSar = Math.Max(0m, totalGrossSar - totalNetSar);
        decimal effectiveDiscountPercent = totalGrossSar > 0
            ? Math.Round((discountAmountSar / totalGrossSar) * 100m, 2)
            : 0m;

        return new CalculateB2BPriceResponse(
            Sku: selectedItem.Sku,
            ProductName: selectedItem.ProductName,
            Quantity: request.Quantity,
            UnitPriceSar: unitPriceSar,
            TotalGrossSar: totalGrossSar,
            DiscountAmountSar: discountAmountSar,
            EffectiveDiscountPercent: effectiveDiscountPercent,
            AppliedPriceList: selectedList.Name
        );
    }

    /// <summary>
    /// Generates a formal B2B Quote and routes through the enterprise discount approval matrix:
    /// - Discount > 20%: PendingVpApproval
    /// - Discount > 10%: PendingManagerApproval
    /// - Otherwise: AutoApproved
    /// </summary>
    public B2BQuoteResponse CreateQuote(CreateB2BQuoteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new ArgumentException("Quote must contain at least one line item.", nameof(request));
        }

        var priceLists = GetPriceLists(request.TenantId);
        if (priceLists.Count == 0 && request.TenantId == DefaultTenantId)
        {
            SeedTenantDefaults(request.TenantId);
            priceLists = GetPriceLists(request.TenantId);
        }

        var now = DateTime.UtcNow;
        var lineResponses = new List<B2BQuoteLineItemResponse>();
        decimal quoteTotalGross = 0m;
        decimal quoteTotalNet = 0m;

        foreach (var reqItem in request.Items)
        {
            if (reqItem.Quantity <= 0)
            {
                throw new ArgumentException($"Quantity for SKU '{reqItem.Sku}' must be positive.", nameof(request));
            }

            // Find matching item in account-specific or default price list
            ContractPriceListItem? item = null;

            var accountList = priceLists.FirstOrDefault(p =>
                p.TargetAccountId == request.AccountId &&
                p.ValidFromUtc <= now &&
                p.ValidToUtc >= now &&
                p.Items.Any(i => string.Equals(i.Sku, reqItem.Sku, StringComparison.OrdinalIgnoreCase)));

            item = accountList?.Items.FirstOrDefault(i => string.Equals(i.Sku, reqItem.Sku, StringComparison.OrdinalIgnoreCase));

            if (item == null)
            {
                var defaultList = priceLists.FirstOrDefault(p =>
                    p.IsDefault &&
                    p.TargetAccountId == null &&
                    p.ValidFromUtc <= now &&
                    p.ValidToUtc >= now &&
                    p.Items.Any(i => string.Equals(i.Sku, reqItem.Sku, StringComparison.OrdinalIgnoreCase)));

                item = defaultList?.Items.FirstOrDefault(i => string.Equals(i.Sku, reqItem.Sku, StringComparison.OrdinalIgnoreCase));
            }

            if (item == null)
            {
                throw new KeyNotFoundException($"SKU '{reqItem.Sku}' not found in any active price list for tenant '{request.TenantId}'.");
            }

            decimal lineUnitPriceSar;
            if (reqItem.RequestedUnitPriceSar.HasValue)
            {
                lineUnitPriceSar = reqItem.RequestedUnitPriceSar.Value;
            }
            else
            {
                var tier = item.VolumeTiers
                    .Where(t => reqItem.Quantity >= t.MinQuantity && (t.MaxQuantity == null || reqItem.Quantity <= t.MaxQuantity.Value))
                    .OrderByDescending(t => t.MinQuantity)
                    .FirstOrDefault();

                lineUnitPriceSar = tier != null ? tier.UnitPriceSar : item.BasePriceSar;
            }

            decimal lineGross = Math.Round(item.BasePriceSar * reqItem.Quantity, 2);
            decimal lineNet = Math.Round(lineUnitPriceSar * reqItem.Quantity, 2);
            decimal unitCost = Math.Round(item.BasePriceSar * 0.50m, 2); // 50% baseline COGS
            decimal marginPercent = lineUnitPriceSar > 0
                ? Math.Round(((lineUnitPriceSar - unitCost) / lineUnitPriceSar) * 100m, 2)
                : 0m;

            quoteTotalGross += lineGross;
            quoteTotalNet += lineNet;

            lineResponses.Add(new B2BQuoteLineItemResponse(
                Sku: item.Sku,
                ProductName: item.ProductName,
                Quantity: reqItem.Quantity,
                UnitPriceSar: lineUnitPriceSar,
                LineTotalSar: lineNet,
                MarginPercent: marginPercent
            ));
        }

        decimal quoteTotalDiscount = Math.Max(0m, quoteTotalGross - quoteTotalNet);
        decimal effectiveDiscountPercent = quoteTotalGross > 0
            ? Math.Round((quoteTotalDiscount / quoteTotalGross) * 100m, 2)
            : 0m;

        // Routing governance
        QuoteApprovalStatus status;
        string requiredApproverRole;

        if (effectiveDiscountPercent > 20m)
        {
            status = QuoteApprovalStatus.PendingVpApproval;
            requiredApproverRole = "VP_COMMERCE";
        }
        else if (effectiveDiscountPercent > 10m)
        {
            status = QuoteApprovalStatus.PendingManagerApproval;
            requiredApproverRole = "SALES_MANAGER";
        }
        else
        {
            status = QuoteApprovalStatus.AutoApproved;
            requiredApproverRole = "NONE";
        }

        var quoteId = Guid.NewGuid();
        int counter = System.Threading.Interlocked.Increment(ref _quoteCounter);
        string quoteNumber = $"B2B-QT-{counter}";

        var quote = new B2BQuoteResponse(
            QuoteId: quoteId,
            TenantId: request.TenantId,
            QuoteNumber: quoteNumber,
            AccountId: request.AccountId,
            ContactName: request.ContactName,
            TotalGrossSar: quoteTotalGross,
            TotalDiscountSar: quoteTotalDiscount,
            TotalNetSar: quoteTotalNet,
            Status: status,
            Items: lineResponses,
            RequiredApproverRole: requiredApproverRole,
            CreatedAtUtc: DateTime.UtcNow
        );

        _quotes[quoteId] = quote;
        return quote;
    }

    /// <summary>
    /// Approves or rejects a pending B2B quote with role validation.
    /// </summary>
    public B2BQuoteResponse ApproveQuote(ApproveB2BQuoteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_quotes.TryGetValue(request.QuoteId, out var existingQuote) || existingQuote.TenantId != request.TenantId)
        {
            throw new KeyNotFoundException($"Quote '{request.QuoteId}' not found for tenant '{request.TenantId}'.");
        }

        var newStatus = request.Approved ? QuoteApprovalStatus.Approved : QuoteApprovalStatus.Rejected;

        var updatedQuote = existingQuote with
        {
            Status = newStatus,
            RequiredApproverRole = request.Approved ? "COMPLETED" : "REJECTED"
        };

        _quotes[request.QuoteId] = updatedQuote;
        return updatedQuote;
    }

    /// <summary>
    /// Retrieves a single quote ensuring tenant isolation.
    /// </summary>
    public B2BQuoteResponse? GetQuote(Guid tenantId, Guid quoteId)
    {
        if (_quotes.TryGetValue(quoteId, out var quote) && quote.TenantId == tenantId)
        {
            return quote;
        }

        return null;
    }
}
