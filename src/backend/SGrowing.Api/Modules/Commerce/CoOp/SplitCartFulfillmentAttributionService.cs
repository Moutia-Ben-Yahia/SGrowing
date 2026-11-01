using System.Threading;

namespace SGrowing.Api.Modules.Commerce.CoOp;

/// <summary>
/// Unifies multi-merchant checkout carts into single consumer transactions while splitting backend fulfillment,
/// proportionally allocating shipping fees, and executing precise merchant net payout attribution.
/// </summary>
public class SplitCartFulfillmentAttributionService : ISplitCartFulfillmentAttributionService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, SplitCartOrder> _orders = new(StringComparer.OrdinalIgnoreCase);

    // Track original fee percentages per order for accurate refund clawbacks
    private readonly Dictionary<string, (decimal PlatformFeePct, decimal CoOpCommissionPct)> _orderFeeRates = new(StringComparer.OrdinalIgnoreCase);

    public int TotalOrders
    {
        get
        {
            lock (_lock)
            {
                return _orders.Count;
            }
        }
    }

    public SplitCartOrder SplitCartAndAllocate(SplitCartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            throw new ArgumentException("CustomerId is required.", nameof(request));
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new ArgumentException("Cart must contain at least one item.", nameof(request));
        }

        if (request.BaseShippingRateDzd < 0)
        {
            throw new ArgumentException("BaseShippingRateDzd cannot be negative.", nameof(request));
        }

        string orderId = $"ord-{Guid.NewGuid():N}"[..16];

        // 1. Group items by MerchantId
        var itemsByMerchant = request.Items
            .GroupBy(i => i.MerchantId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 2. Compute shipping allocation metric per merchant (Weight in Kg + Volume in m3 * 200)
        var merchantMetrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in itemsByMerchant)
        {
            double totalWeight = group.Sum(i => i.WeightKg * i.Quantity);
            double totalVolume = group.Sum(i => i.VolumeM3 * i.Quantity);
            double metric = totalWeight + (totalVolume * 200.0);

            // Default to minimal non-zero metric if weight/volume are zero
            merchantMetrics[group.Key] = metric > 0.0001 ? metric : 1.0;
        }

        double totalMetricsSum = merchantMetrics.Values.Sum();

        // 3. Allocate shipping fees proportionally across merchants
        var shippingAllocations = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        decimal allocatedSum = 0m;

        foreach (var (merchantId, metric) in merchantMetrics)
        {
            decimal alloc = totalMetricsSum > 0
                ? Math.Round(request.BaseShippingRateDzd * (decimal)(metric / totalMetricsSum), 2, MidpointRounding.AwayFromZero)
                : 0m;
            shippingAllocations[merchantId] = alloc;
            allocatedSum += alloc;
        }

        // Adjust any cent discrepancy onto the merchant with the largest allocation
        decimal discrepancy = request.BaseShippingRateDzd - allocatedSum;
        if (discrepancy != 0m && shippingAllocations.Count > 0)
        {
            var largestMerchantKey = shippingAllocations.MaxBy(kvp => kvp.Value).Key;
            shippingAllocations[largestMerchantKey] += discrepancy;
        }

        // 4. Build sub-orders and compute financial attribution
        var subOrders = new List<SubOrder>();
        var netAttributionMap = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        decimal totalItemsDzd = 0m;

        foreach (var group in itemsByMerchant)
        {
            string merchantId = group.Key;
            var merchantItems = group.ToList();

            decimal itemsSubtotal = merchantItems.Sum(i => i.UnitPriceDzd * i.Quantity);
            totalItemsDzd += itemsSubtotal;

            decimal allocatedShipping = shippingAllocations.GetValueOrDefault(merchantId, 0m);

            decimal platformFee = Math.Round(itemsSubtotal * request.PlatformFeePct, 2, MidpointRounding.AwayFromZero);
            decimal coOpCommission = Math.Round(itemsSubtotal * request.CoOpCommissionPct, 2, MidpointRounding.AwayFromZero);

            // Merchant Net Payout = ItemsSubtotal + AllocatedShipping - PlatformFee - CoOpCommission
            decimal netPayout = itemsSubtotal + allocatedShipping - platformFee - coOpCommission;

            var subOrder = new SubOrder(
                SubOrderId: $"{orderId}-{merchantId}".ToLowerInvariant(),
                ParentOrderId: orderId,
                MerchantId: merchantId,
                Items: merchantItems,
                ItemsSubtotalDzd: itemsSubtotal,
                AllocatedShippingDzd: allocatedShipping,
                PlatformFeeDzd: platformFee,
                CoOpCommissionDzd: coOpCommission,
                MerchantNetPayoutDzd: netPayout,
                Status: SettlementSplitStatus.PendingFulfillment
            );

            subOrders.Add(subOrder);
            netAttributionMap[merchantId] = netPayout;
        }

        var splitOrder = new SplitCartOrder(
            OrderId: orderId,
            CustomerId: request.CustomerId,
            TotalItemsDzd: totalItemsDzd,
            TotalShippingDzd: request.BaseShippingRateDzd,
            GrandTotalDzd: totalItemsDzd + request.BaseShippingRateDzd,
            SubOrders: subOrders,
            NetAttributionMap: netAttributionMap,
            OverallStatus: SettlementSplitStatus.PendingFulfillment,
            CreatedAtUtc: DateTime.UtcNow
        );

        lock (_lock)
        {
            _orders[orderId] = splitOrder;
            _orderFeeRates[orderId] = (request.PlatformFeePct, request.CoOpCommissionPct);
        }

        return splitOrder;
    }

    public SplitCartOrder? GetOrder(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId)) return null;

        lock (_lock)
        {
            return _orders.GetValueOrDefault(orderId);
        }
    }

    public SplitCartOrder UpdateOrderStatus(string orderId, SettlementSplitStatus status)
    {
        ArgumentNullException.ThrowIfNull(orderId);

        lock (_lock)
        {
            if (!_orders.TryGetValue(orderId, out var existingOrder))
            {
                throw new KeyNotFoundException($"Order '{orderId}' was not found.");
            }

            var updatedSubOrders = existingOrder.SubOrders
                .Select(s => s with { Status = status })
                .ToList();

            var updatedOrder = existingOrder with
            {
                OverallStatus = status,
                SubOrders = updatedSubOrders
            };

            _orders[orderId] = updatedOrder;
            return updatedOrder;
        }
    }

    public RefundAllocationResult ProcessPartialRefund(PartialRefundRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.OrderId))
            throw new ArgumentException("OrderId is required.", nameof(request));

        if (request.QuantityToRefund <= 0)
            throw new ArgumentException("QuantityToRefund must be greater than zero.", nameof(request));

        lock (_lock)
        {
            if (!_orders.TryGetValue(request.OrderId, out var existingOrder))
            {
                throw new KeyNotFoundException($"Order '{request.OrderId}' was not found.");
            }

            var subOrder = existingOrder.SubOrders.FirstOrDefault(s =>
                string.Equals(s.MerchantId, request.MerchantId, StringComparison.OrdinalIgnoreCase));

            if (subOrder == null)
            {
                throw new ArgumentException($"Merchant '{request.MerchantId}' has no sub-order under order '{request.OrderId}'.", nameof(request));
            }

            var item = subOrder.Items.FirstOrDefault(i =>
                string.Equals(i.SkuId, request.SkuId, StringComparison.OrdinalIgnoreCase));

            if (item == null)
            {
                throw new ArgumentException($"SKU '{request.SkuId}' does not belong to merchant '{request.MerchantId}' in order '{request.OrderId}'.", nameof(request));
            }

            if (request.QuantityToRefund > item.Quantity)
            {
                throw new ArgumentException($"Cannot refund {request.QuantityToRefund} units of SKU '{request.SkuId}'. Purchased: {item.Quantity}.", nameof(request));
            }

            var (platformFeePct, coOpCommissionPct) = _orderFeeRates.GetValueOrDefault(request.OrderId, (0.05m, 0.03m));

            // 1. Calculate item refund
            decimal itemRefundAmount = item.UnitPriceDzd * request.QuantityToRefund;

            // 2. Shipping refund proration:
            // If all items for this merchant are cancelled, refund entire allocated shipping.
            // Otherwise, prorate by weight.
            int totalMerchantItemCount = subOrder.Items.Sum(i => i.Quantity);
            decimal shippingRefundAmount = 0m;

            if (request.QuantityToRefund == totalMerchantItemCount)
            {
                shippingRefundAmount = subOrder.AllocatedShippingDzd;
            }
            else
            {
                double totalMerchantWeight = subOrder.Items.Sum(i => i.WeightKg * i.Quantity);
                double refundedWeight = item.WeightKg * request.QuantityToRefund;

                if (totalMerchantWeight > 0.0001)
                {
                    shippingRefundAmount = Math.Round(
                        subOrder.AllocatedShippingDzd * (decimal)(refundedWeight / totalMerchantWeight),
                        2,
                        MidpointRounding.AwayFromZero);
                }
            }

            decimal totalCustomerRefund = itemRefundAmount + shippingRefundAmount;

            // 3. Platform & Co-op commission clawback
            decimal clawbackPlatformFee = Math.Round(itemRefundAmount * platformFeePct, 2, MidpointRounding.AwayFromZero);
            decimal clawbackCoOpCommission = Math.Round(itemRefundAmount * coOpCommissionPct, 2, MidpointRounding.AwayFromZero);

            // Merchant net debit = totalCustomerRefund - platformClawback - coOpClawback
            decimal merchantDebit = totalCustomerRefund - clawbackPlatformFee - clawbackCoOpCommission;

            // 4. Update the sub-order and overall order
            var remainingItems = new List<CartItem>();
            foreach (var ci in subOrder.Items)
            {
                if (string.Equals(ci.SkuId, request.SkuId, StringComparison.OrdinalIgnoreCase))
                {
                    int remainingQty = ci.Quantity - request.QuantityToRefund;
                    if (remainingQty > 0)
                    {
                        remainingItems.Add(ci with { Quantity = remainingQty });
                    }
                }
                else
                {
                    remainingItems.Add(ci);
                }
            }

            decimal updatedSubtotal = subOrder.ItemsSubtotalDzd - itemRefundAmount;
            decimal updatedShipping = Math.Max(0m, subOrder.AllocatedShippingDzd - shippingRefundAmount);
            decimal updatedPlatformFee = Math.Max(0m, subOrder.PlatformFeeDzd - clawbackPlatformFee);
            decimal updatedCoOpFee = Math.Max(0m, subOrder.CoOpCommissionDzd - clawbackCoOpCommission);
            decimal updatedNetPayout = updatedSubtotal + updatedShipping - updatedPlatformFee - updatedCoOpFee;

            var updatedSubOrder = subOrder with
            {
                Items = remainingItems,
                ItemsSubtotalDzd = updatedSubtotal,
                AllocatedShippingDzd = updatedShipping,
                PlatformFeeDzd = updatedPlatformFee,
                CoOpCommissionDzd = updatedCoOpFee,
                MerchantNetPayoutDzd = updatedNetPayout,
                Status = SettlementSplitStatus.PartialRefundDisputed
            };

            var updatedSubOrders = existingOrder.SubOrders
                .Select(s => string.Equals(s.MerchantId, request.MerchantId, StringComparison.OrdinalIgnoreCase) ? updatedSubOrder : s)
                .ToList();

            var updatedAttributionMap = new Dictionary<string, decimal>(existingOrder.NetAttributionMap, StringComparer.OrdinalIgnoreCase)
            {
                [request.MerchantId] = updatedNetPayout
            };

            var updatedOrder = existingOrder with
            {
                TotalItemsDzd = existingOrder.TotalItemsDzd - itemRefundAmount,
                TotalShippingDzd = existingOrder.TotalShippingDzd - shippingRefundAmount,
                GrandTotalDzd = existingOrder.GrandTotalDzd - totalCustomerRefund,
                SubOrders = updatedSubOrders,
                NetAttributionMap = updatedAttributionMap,
                OverallStatus = SettlementSplitStatus.PartialRefundDisputed
            };

            _orders[request.OrderId] = updatedOrder;

            return new RefundAllocationResult(
                OrderId: request.OrderId,
                MerchantId: request.MerchantId,
                SkuId: request.SkuId,
                QuantityRefunded: request.QuantityToRefund,
                ItemRefundAmountDzd: itemRefundAmount,
                ShippingRefundAmountDzd: shippingRefundAmount,
                TotalCustomerRefundDzd: totalCustomerRefund,
                ClawbackPlatformFeeDzd: clawbackPlatformFee,
                ClawbackCoOpCommissionDzd: clawbackCoOpCommission,
                MerchantDebitDzd: merchantDebit,
                UpdatedStatus: SettlementSplitStatus.PartialRefundDisputed
            );
        }
    }

    public IReadOnlyList<SubOrder> GetMerchantSubOrders(string merchantId)
    {
        if (string.IsNullOrWhiteSpace(merchantId)) return [];

        lock (_lock)
        {
            return _orders.Values
                .SelectMany(o => o.SubOrders)
                .Where(s => string.Equals(s.MerchantId, merchantId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
