using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.Connectors;

public record ShopifyOrderSyncResult(
    bool Success,
    string OrderId,
    Guid DealId,
    Guid ShipmentId,
    string OrderNumber,
    decimal Amount,
    string Currency,
    string Message
);

public record CatalogSyncResult(
    bool Success,
    int ProductsSynced,
    int VariantsSynced,
    string StoreDomain,
    string Message
);

public record ConnectorStatusDto(
    string Platform,
    bool IsConnected,
    string StoreDomain,
    int SyncedOrdersCount,
    int ActiveDealsCount,
    int ShipmentsDispatchedCount,
    int CatalogProductsCount,
    DateTime? LastSyncedAtUtc
);

public interface IShopifyConnectorService
{
    bool VerifyWebhookSignature(string rawBody, string secret, string hmacHeader);
    Task<ShopifyOrderSyncResult> ProcessOrderWebhookAsync(Guid tenantId, string topic, string rawJson, CancellationToken cancellationToken = default);
    Task<CatalogSyncResult> TriggerCatalogSyncAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ConnectorStatusDto> GetConnectorStatusAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
