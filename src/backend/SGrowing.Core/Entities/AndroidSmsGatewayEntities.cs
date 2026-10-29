namespace SGrowing.Core.Entities;

public class AndroidSmsGatewayDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceIdentifier { get; set; } = string.Empty; // e.g. IMEI or AndroidId
    public string FcmToken { get; set; } = string.Empty;
    public string Sim1Operator { get; set; } = "Ooredoo";
    public string Sim2Operator { get; set; } = "Orange";
    public int BatteryPercent { get; set; } = 95;
    public int DailySentCount { get; set; } = 0;
    public int DailyQuotaLimit { get; set; } = 500;
    public bool IsOnline { get; set; } = true;
    public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;

    public static AndroidSmsGatewayDevice Create(
        Guid tenantId,
        string deviceName,
        string deviceIdentifier,
        string fcmToken,
        string sim1 = "Ooredoo",
        string sim2 = "Orange",
        int dailyQuota = 500)
    {
        return new AndroidSmsGatewayDevice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviceName = deviceName.Trim(),
            DeviceIdentifier = deviceIdentifier.Trim(),
            FcmToken = fcmToken.Trim(),
            Sim1Operator = sim1.Trim(),
            Sim2Operator = sim2.Trim(),
            BatteryPercent = 100,
            DailySentCount = 0,
            DailyQuotaLimit = dailyQuota,
            IsOnline = true,
            LastHeartbeatUtc = DateTime.UtcNow
        };
    }

    public void RecordHeartbeat(int batteryPercent)
    {
        BatteryPercent = Math.Clamp(batteryPercent, 0, 100);
        IsOnline = true;
        LastHeartbeatUtc = DateTime.UtcNow;
    }
}

public class AndroidSmsDispatchRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }
    public int SimSlot { get; set; } = 1; // 1 or 2
    public string RecipientPhone { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string DispatchStatus { get; set; } = "Dispatched"; // "Queued", "Dispatched", "Delivered", "Failed"
    public string? ErrorMessage { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAtUtc { get; set; }

    public static AndroidSmsDispatchRecord Create(
        Guid tenantId,
        Guid deviceId,
        string recipientPhone,
        string messageBody,
        int simSlot = 1)
    {
        return new AndroidSmsDispatchRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviceId = deviceId,
            SimSlot = simSlot == 2 ? 2 : 1,
            RecipientPhone = recipientPhone.Trim(),
            MessageBody = messageBody.Trim(),
            DispatchStatus = "Dispatched",
            SentAtUtc = DateTime.UtcNow
        };
    }
}
