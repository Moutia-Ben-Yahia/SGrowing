using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Gateways.Android;

public class AndroidSmsGatewayService : IAndroidSmsGatewayService
{
    private readonly SGrowingDbContext _db;

    public AndroidSmsGatewayService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<AndroidSmsGatewayDevice> RegisterDeviceAsync(
        Guid tenantId,
        string deviceName,
        string deviceIdentifier,
        string fcmToken,
        string sim1 = "Ooredoo",
        string sim2 = "Orange",
        int dailyQuota = 500)
    {
        var existing = await _db.AndroidSmsGatewayDevices
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.DeviceIdentifier == deviceIdentifier.Trim());

        if (existing == null)
        {
            existing = AndroidSmsGatewayDevice.Create(
                tenantId,
                deviceName,
                deviceIdentifier,
                fcmToken,
                sim1,
                sim2,
                dailyQuota);
            _db.AndroidSmsGatewayDevices.Add(existing);
        }
        else
        {
            existing.DeviceName = deviceName.Trim();
            existing.FcmToken = fcmToken.Trim();
            existing.Sim1Operator = sim1.Trim();
            existing.Sim2Operator = sim2.Trim();
            existing.DailyQuotaLimit = dailyQuota;
            existing.IsOnline = true;
            existing.LastHeartbeatUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> RecordHeartbeatAsync(Guid tenantId, string deviceIdentifier, int batteryPercent)
    {
        var device = await _db.AndroidSmsGatewayDevices
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.DeviceIdentifier == deviceIdentifier.Trim());

        if (device == null) return false;

        device.RecordHeartbeat(batteryPercent);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<AndroidSmsDispatchResultDto> DispatchSmsAsync(
        Guid tenantId,
        string recipientPhone,
        string messageBody,
        int? preferredSimSlot = null)
    {
        var device = await _db.AndroidSmsGatewayDevices
            .Where(d => d.TenantId == tenantId && d.IsOnline && d.DailySentCount < d.DailyQuotaLimit)
            .OrderBy(d => d.DailySentCount)
            .FirstOrDefaultAsync();

        if (device == null)
        {
            return new AndroidSmsDispatchResultDto(
                false,
                null,
                null,
                null,
                1,
                recipientPhone,
                "Failed",
                "No active online Android gateway handset available with remaining quota");
        }

        int selectedSlot = preferredSimSlot ?? 1;
        var dispatch = AndroidSmsDispatchRecord.Create(
            tenantId,
            device.Id,
            recipientPhone,
            messageBody,
            selectedSlot);

        device.DailySentCount++;
        _db.AndroidSmsDispatchRecords.Add(dispatch);
        await _db.SaveChangesAsync();

        SGrowingTelemetry.AndroidSmsDispatchedCounter.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId.ToString()),
            new KeyValuePair<string, object?>("device_id", device.Id.ToString()));

        return new AndroidSmsDispatchResultDto(
            true,
            dispatch.Id,
            device.Id,
            device.DeviceName,
            selectedSlot,
            recipientPhone,
            "Dispatched",
            $"SMS successfully dispatched via Android handset '{device.DeviceName}' (SIM {selectedSlot})");
    }

    public async Task<List<AndroidSmsGatewayDevice>> ListDevicesAsync(Guid tenantId)
    {
        return await _db.AndroidSmsGatewayDevices
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.IsOnline)
            .ThenByDescending(d => d.LastHeartbeatUtc)
            .ToListAsync();
    }

    public async Task<List<AndroidSmsDispatchRecord>> GetDispatchHistoryAsync(Guid tenantId, int limit = 50)
    {
        return await _db.AndroidSmsDispatchRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.SentAtUtc)
            .Take(limit)
            .ToListAsync();
    }
}
