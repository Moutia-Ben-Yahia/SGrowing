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

namespace SGrowing.Api.Modules.Commerce.DynamicPricing;

public record ElasticityModelEvaluation(
    string ProductId,
    decimal BasePrice,
    decimal Elasticity,
    string Category,
    decimal OptimalPrice,
    decimal MaxContributionMargin,
    decimal RecommendedDiscountFloorPercent
);

public interface IPersistentPriceElasticityEngine
{
    Task<DynamicElasticityPricingEntity> ComputeAndPersistOptimalPriceAsync(
        Guid tenantId,
        string productId,
        decimal basePrice,
        decimal baseDemand,
        decimal unitCost,
        decimal elasticity,
        CancellationToken ct = default);

    Task<DynamicElasticityPricingEntity?> GetProductModelAsync(
        Guid tenantId,
        string productId,
        CancellationToken ct = default);
}

public class PersistentPriceElasticityEngine : IPersistentPriceElasticityEngine
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<PersistentPriceElasticityEngine>? _logger;
    private readonly ConcurrentDictionary<string, DynamicElasticityPricingEntity> _l1Cache = new();

    public PersistentPriceElasticityEngine(
        SGrowingDbContext db,
        ILogger<PersistentPriceElasticityEngine>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DynamicElasticityPricingEntity> ComputeAndPersistOptimalPriceAsync(
        Guid tenantId,
        string productId,
        decimal basePrice,
        decimal baseDemand,
        decimal unitCost,
        decimal elasticity,
        CancellationToken ct = default)
    {
        // Category classification
        var absE = Math.Abs(elasticity);
        var category = absE switch
        {
            < 0.9m => "Inelastic",
            <= 1.1m => "Unitary",
            _ => "Elastic"
        };

        // Contribution margin maximization search across candidate prices
        decimal bestPrice = basePrice;
        decimal maxMargin = (basePrice - unitCost) * baseDemand;

        // Test prices from 70% to 150% of base price
        for (decimal p = basePrice * 0.7m; p <= basePrice * 1.5m; p += Math.Max(0.5m, basePrice * 0.05m))
        {
            if (p <= unitCost) continue;

            var priceDeltaPct = (p - basePrice) / basePrice;
            var demandFactor = 1.0m + (elasticity * priceDeltaPct);
            var estimatedDemand = Math.Max(0, baseDemand * demandFactor);
            var margin = (p - unitCost) * estimatedDemand;

            if (margin > maxMargin)
            {
                maxMargin = margin;
                bestPrice = p;
            }
        }

        decimal discountFloor = category switch
        {
            "Inelastic" => 3.0m,
            "Unitary" => 10.0m,
            _ => 20.0m
        };

        var existing = await _db.PricingElasticityModels
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProductId == productId, ct);

        if (existing != null)
        {
            existing.BasePrice = basePrice;
            existing.CurrentElasticityScore = elasticity;
            existing.ElasticityCategory = category;
            existing.OptimalPrice = Math.Round(bestPrice, 2);
            existing.MaxContributionMargin = Math.Round(maxMargin, 2);
            existing.RecommendedDiscountFloorPercent = discountFloor;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            existing = new DynamicElasticityPricingEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = productId,
                BasePrice = basePrice,
                CurrentElasticityScore = elasticity,
                ElasticityCategory = category,
                OptimalPrice = Math.Round(bestPrice, 2),
                MaxContributionMargin = Math.Round(maxMargin, 2),
                RecommendedDiscountFloorPercent = discountFloor,
                UpdatedAtUtc = DateTime.UtcNow
            };
            await _db.PricingElasticityModels.AddAsync(existing, ct);
        }

        await _db.SaveChangesAsync(ct);

        _l1Cache[$"{tenantId}_{productId}"] = existing;
        _logger?.LogInformation("Persisted price elasticity model for product {ProductId}: Base={BasePrice}, Optimal={OptimalPrice}, Margin={Margin}",
            productId, basePrice, existing.OptimalPrice, existing.MaxContributionMargin);

        return existing;
    }

    public async Task<DynamicElasticityPricingEntity?> GetProductModelAsync(
        Guid tenantId,
        string productId,
        CancellationToken ct = default)
    {
        var cacheKey = $"{tenantId}_{productId}";
        if (_l1Cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var entity = await _db.PricingElasticityModels
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProductId == productId, ct);

        if (entity != null)
            _l1Cache[cacheKey] = entity;

        return entity;
    }
}
