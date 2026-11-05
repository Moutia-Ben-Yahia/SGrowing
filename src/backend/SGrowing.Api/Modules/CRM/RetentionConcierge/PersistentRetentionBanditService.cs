using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.CRM.RetentionConcierge;

public record BanditArmSelection(
    string ArmId,
    string IncentiveType,
    double SampledValue,
    double Alpha,
    double Beta
);

public interface IPersistentRetentionBanditService
{
    Task<BanditArmSelection> SelectBestIncentiveArmAsync(Guid tenantId, CancellationToken ct = default);
    Task RecordRewardAsync(Guid tenantId, string armId, bool success, decimal retainedLtvDzd = 0m, CancellationToken ct = default);
    Task<IReadOnlyList<RetentionBanditArmEntity>> GetArmStatesAsync(Guid tenantId, CancellationToken ct = default);
}

public class PersistentRetentionBanditService : IPersistentRetentionBanditService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<PersistentRetentionBanditService>? _logger;
    private readonly Random _random = new();

    public PersistentRetentionBanditService(
        SGrowingDbContext db,
        ILogger<PersistentRetentionBanditService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BanditArmSelection> SelectBestIncentiveArmAsync(Guid tenantId, CancellationToken ct = default)
    {
        var arms = await _db.RetentionBanditArms
            .Where(a => a.TenantId == tenantId)
            .ToListAsync(ct);

        if (arms.Count == 0)
        {
            // Seed default contextual bandit arms for tenant
            arms = await SeedDefaultArmsAsync(tenantId, ct);
        }

        BanditArmSelection? best = null;
        double maxSample = -1.0;

        foreach (var arm in arms)
        {
            var sample = SampleBeta(arm.Alpha, arm.Beta);
            if (sample > maxSample)
            {
                maxSample = sample;
                best = new BanditArmSelection(
                    ArmId: arm.ArmId,
                    IncentiveType: arm.IncentiveType,
                    SampledValue: Math.Round(sample, 4),
                    Alpha: arm.Alpha,
                    Beta: arm.Beta);
            }
        }

        return best ?? new BanditArmSelection("ARM-DEFAULT", "PersonalizedDiscount", 0.5, 2.0, 2.0);
    }

    public async Task RecordRewardAsync(Guid tenantId, string armId, bool success, decimal retainedLtvDzd = 0m, CancellationToken ct = default)
    {
        var arm = await _db.RetentionBanditArms
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.ArmId == armId, ct);

        if (arm == null) return;

        arm.TotalTrials++;
        if (success)
        {
            arm.Alpha += 1.0;
            arm.TotalLtvRetainedDzd += retainedLtvDzd;
        }
        else
        {
            arm.Beta += 1.0;
        }
        arm.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger?.LogInformation("Recorded {Outcome} reward for bandit arm {ArmId} (Tenant {TenantId}). New alpha={Alpha}, beta={Beta}",
            success ? "SUCCESS" : "FAILURE", armId, tenantId, arm.Alpha, arm.Beta);
    }

    public async Task<IReadOnlyList<RetentionBanditArmEntity>> GetArmStatesAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.RetentionBanditArms
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.TotalLtvRetainedDzd)
            .ToListAsync(ct);
    }

    private async Task<List<RetentionBanditArmEntity>> SeedDefaultArmsAsync(Guid tenantId, CancellationToken ct)
    {
        var defaultArms = new List<RetentionBanditArmEntity>
        {
            new() { Id = Guid.NewGuid(), TenantId = tenantId, ArmId = "ARM-EXPEDITED-SHIP", IncentiveType = "ExpeditedShippingWaiver", Alpha = 8.0, Beta = 3.0 },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, ArmId = "ARM-LOYALTY-CREDIT", IncentiveType = "BonusLoyaltyCredits", Alpha = 6.0, Beta = 4.0 },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, ArmId = "ARM-DISCOUNT-15PCT", IncentiveType = "PersonalizedDiscountCode", Alpha = 9.0, Beta = 3.0 },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, ArmId = "ARM-GIFT-BASKET", IncentiveType = "ComplimentaryGiftBasket", Alpha = 5.0, Beta = 2.0 }
        };

        await _db.RetentionBanditArms.AddRangeAsync(defaultArms, ct);
        await _db.SaveChangesAsync(ct);
        return defaultArms;
    }

    private double SampleBeta(double alpha, double beta)
    {
        // Marsaglia & Tsang Gamma generator approximation for Beta distribution
        double gammaA = SampleGamma(alpha);
        double gammaB = SampleGamma(beta);
        if (gammaA + gammaB == 0) return 0.5;
        return gammaA / (gammaA + gammaB);
    }

    private double SampleGamma(double shape)
    {
        if (shape < 1.0)
        {
            return SampleGamma(shape + 1.0) * Math.Pow(_random.NextDouble(), 1.0 / shape);
        }

        double d = shape - 1.0 / 3.0;
        double c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double z = NormalSample();
            double v = 1.0 + c * z;
            if (v <= 0.0) continue;

            v = v * v * v;
            double u = _random.NextDouble();

            if (u < 1.0 - 0.0331 * z * z * z * z)
                return d * v;

            if (Math.Log(u) < 0.5 * z * z + d * (1.0 - v + Math.Log(v)))
                return d * v;
        }
    }

    private double NormalSample()
    {
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
