using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.Connectors;

public record WooCommerceOrderSyncResult(
    bool Success,
    string OrderId,
    Guid DealId,
    Guid ShipmentId,
    string OrderNumber,
    decimal Amount,
    string Currency,
    int LineItemsCount,
    string Message
);

public interface IWooCommerceConnectorService
{
    bool VerifyWebhookSignature(string rawBody, string secret, string signatureHeader);
    Task<WooCommerceOrderSyncResult> ProcessOrderWebhookAsync(Guid tenantId, string eventName, string rawJson, CancellationToken cancellationToken = default);
    Task<CatalogSyncResult> TriggerCatalogSyncAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ConnectorStatusDto> GetConnectorStatusAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
