using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;

public class TenantRebillingService : ITenantRebillingService
{
    private readonly SGrowingDbContext _db;

    public TenantRebillingService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<TenantRebillingConfig> SetRebillingConfigAsync(
        Guid agencyTenantId,
        Guid subAccountTenantId,
        decimal smsMultiplier,
        decimal voiceMultiplier,
        decimal aiMultiplier)
    {
        var existing = await _db.TenantRebillingConfigs
            .FirstOrDefaultAsync(r => r.AgencyTenantId == agencyTenantId && r.SubAccountTenantId == subAccountTenantId);

        if (existing == null)
        {
            existing = TenantRebillingConfig.Create(agencyTenantId, subAccountTenantId, smsMultiplier, voiceMultiplier, aiMultiplier);
            _db.TenantRebillingConfigs.Add(existing);
        }
        else
        {
            existing.SmsMarkupMultiplier = Math.Max(1.0m, smsMultiplier);
            existing.VoiceMarkupMultiplier = Math.Max(1.0m, voiceMultiplier);
            existing.AiTokenMarkupMultiplier = Math.Max(1.0m, aiMultiplier);
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<TenantRebillingConfig?> GetRebillingConfigAsync(Guid agencyTenantId, Guid subAccountTenantId)
    {
        return await _db.TenantRebillingConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.AgencyTenantId == agencyTenantId && r.SubAccountTenantId == subAccountTenantId);
    }

    public async Task<RebillingCalculationResult> CalculateRebilledCostAsync(
        Guid agencyTenantId,
        Guid subAccountTenantId,
        string serviceType,
        decimal wholesaleCost)
    {
        var config = await GetRebillingConfigAsync(agencyTenantId, subAccountTenantId);
        var multiplier = config != null
            ? serviceType.ToLowerInvariant() switch
            {
                "sms" => config.SmsMarkupMultiplier,
                "voice" => config.VoiceMarkupMultiplier,
                "ai" or "aitoken" or "aitokens" => config.AiTokenMarkupMultiplier,
                _ => 1.20m
            }
            : 1.25m; // default 25% agency markup

        var retailCharge = Math.Round(wholesaleCost * multiplier, 4);
        var profit = Math.Round(retailCharge - wholesaleCost, 4);

        return new RebillingCalculationResult(
            serviceType,
            wholesaleCost,
            multiplier,
            retailCharge,
            profit);
    }
}
