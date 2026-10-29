using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Gateways.Android;

public record AndroidSmsDispatchResultDto(
    bool IsSuccess,
    Guid? DispatchId,
    Guid? DeviceId,
    string? DeviceName,
    int SimSlot,
    string RecipientPhone,
    string Status,
    string Message
);

public interface IAndroidSmsGatewayService
{
    Task<AndroidSmsGatewayDevice> RegisterDeviceAsync(
        Guid tenantId,
        string deviceName,
        string deviceIdentifier,
        string fcmToken,
        string sim1 = "Ooredoo",
        string sim2 = "Orange",
        int dailyQuota = 500);

    Task<bool> RecordHeartbeatAsync(
        Guid tenantId,
        string deviceIdentifier,
        int batteryPercent);

    Task<AndroidSmsDispatchResultDto> DispatchSmsAsync(
        Guid tenantId,
        string recipientPhone,
        string messageBody,
        int? preferredSimSlot = null);

    Task<List<AndroidSmsGatewayDevice>> ListDevicesAsync(Guid tenantId);

    Task<List<AndroidSmsDispatchRecord>> GetDispatchHistoryAsync(Guid tenantId, int limit = 50);
}
