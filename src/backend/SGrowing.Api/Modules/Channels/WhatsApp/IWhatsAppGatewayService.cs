using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public interface IWhatsAppGatewayService
{
    Task<IEnumerable<WhatsAppInstanceRecord>> GetInstancesAsync(Guid tenantId, CancellationToken ct = default);
    Task<WhatsAppInstanceRecord> CreateInstanceAsync(Guid tenantId, CreateWhatsAppInstanceRequest req, CancellationToken ct = default);
    Task<PairingCodeResult?> GeneratePairingCodeAsync(Guid tenantId, string instanceName, RequestPairingCodeRequest req, CancellationToken ct = default);
    Task<QrCodeResult?> GenerateQrCodeAsync(Guid tenantId, string instanceName, CancellationToken ct = default);
    Task<WhatsAppInstanceRecord?> ConnectInstanceAsync(Guid tenantId, string instanceName, CancellationToken ct = default);
    Task<WhatsAppInstanceRecord?> DisconnectInstanceAsync(Guid tenantId, string instanceName, CancellationToken ct = default);
    Task<WhatsAppMessageDispatchResult?> DispatchMessageAsync(Guid tenantId, string instanceName, DispatchWhatsAppMessageRequest req, CancellationToken ct = default);
    Task<WhatsAppMessageDispatchResult?> DispatchInteractiveButtonsAsync(Guid tenantId, string instanceName, SendInteractiveButtonsRequest req, CancellationToken ct = default);
    Task<WhatsAppMessageDispatchResult?> DispatchInteractiveListAsync(Guid tenantId, string instanceName, SendInteractiveListRequest req, CancellationToken ct = default);
}
