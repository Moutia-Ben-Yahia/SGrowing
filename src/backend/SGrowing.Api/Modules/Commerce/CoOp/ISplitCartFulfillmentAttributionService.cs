namespace SGrowing.Api.Modules.Commerce.CoOp;

/// <summary>
/// Service managing multi-merchant unified cart checkouts, proportional logistics allocation,
/// merchant net attribution, and line-item partial refund processing.
/// </summary>
public interface ISplitCartFulfillmentAttributionService
{
    /// <summary>
    /// Total active orders tracked in memory.
    /// </summary>
    int TotalOrders { get; }

    /// <summary>
    /// Processes a multi-merchant cart checkout, creating unified split orders with proportional shipping and fee attribution.
    /// </summary>
    SplitCartOrder SplitCartAndAllocate(SplitCartRequest request);

    /// <summary>
    /// Retrieves a split cart order by its unique ID.
    /// </summary>
    SplitCartOrder? GetOrder(string orderId);

    /// <summary>
    /// Updates the settlement status of an existing order and its sub-orders.
    /// </summary>
    SplitCartOrder UpdateOrderStatus(string orderId, SettlementSplitStatus status);

    /// <summary>
    /// Processes a partial or complete item cancellation/refund, recalculating shipping proration and fee clawbacks.
    /// </summary>
    RefundAllocationResult ProcessPartialRefund(PartialRefundRequest request);

    /// <summary>
    /// Retrieves all sub-orders assigned to a specific merchant.
    /// </summary>
    IReadOnlyList<SubOrder> GetMerchantSubOrders(string merchantId);
}
