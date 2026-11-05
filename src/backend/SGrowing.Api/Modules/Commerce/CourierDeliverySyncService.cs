using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Common.Observability;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public record CourierDeliverySyncItemDto(
    Guid ParcelId,
    string TrackingNumber,
    string CourierDriverId,
    string WilayaCode,
    string Status, // "Delivered", "FailedAttempt", "Returned"
    decimal CashCollected,
    string? CustomerSignatureHash,
    DateTime RecordedAtUtc
);

public record CourierDeliverySyncBatchRequest(
    Guid TenantId,
    List<CourierDeliverySyncItemDto> Deliveries
);

public record CourierDeliverySyncBatchResult(
    int TotalReceived,
    int SuccessfullySynced,
    int DuplicatesIgnored,
    decimal TotalCashReconciled,
    List<Guid> SyncedParcelIds
);

public record DriverReconciliationSummary(
    string CourierDriverId,
    int TotalDeliveries,
    int SuccessfulDeliveries,
    int FailedAttempts,
    decimal TotalCashCollected
);

public interface ICourierDeliverySyncService
{
    Task<CourierDeliverySyncBatchResult> SyncBatchAsync(
        CourierDeliverySyncBatchRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<CourierDeliveryRecordEntity>> GetDriverDeliveriesAsync(
        Guid tenantId,
        string courierDriverId,
        CancellationToken ct = default);

    Task<DriverReconciliationSummary> GetDriverReconciliationSummaryAsync(
        Guid tenantId,
        string courierDriverId,
        CancellationToken ct = default);
}

/// <summary>
/// Offline-first courier delivery reconciliation service.
/// Processes batched PWA field syncs, reconciles Cash-on-Delivery (COD), and guarantees idempotent delivery ingestion.
/// </summary>
public class CourierDeliverySyncService : ICourierDeliverySyncService
{
    private readonly SGrowingDbContext _dbContext;
    private readonly ILogger<CourierDeliverySyncService>? _logger;

    public CourierDeliverySyncService(
        SGrowingDbContext dbContext,
        ILogger<CourierDeliverySyncService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CourierDeliverySyncBatchResult> SyncBatchAsync(
        CourierDeliverySyncBatchRequest request,
        CancellationToken ct = default)
    {
        if (request.Deliveries == null || request.Deliveries.Count == 0)
        {
            return new CourierDeliverySyncBatchResult(0, 0, 0, 0m, new List<Guid>());
        }

        var tenantId = request.TenantId;
        var totalReceived = request.Deliveries.Count;
        var successfullySynced = 0;
        var duplicatesIgnored = 0;
        var totalCash = 0m;
        var syncedParcelIds = new List<Guid>();

        // Load existing records for these parcels to ensure idempotency
        var incomingParcelIds = request.Deliveries.Select(d => d.ParcelId).Distinct().ToList();
        var existingRecords = await _dbContext.CourierDeliveryRecords
            .Where(c => c.TenantId == tenantId && incomingParcelIds.Contains(c.ParcelId))
            .ToListAsync(ct);

        var existingLookup = existingRecords
            .ToLookup(r => (r.ParcelId, r.RecordedAtUtc));

        foreach (var item in request.Deliveries)
        {
            // Check for exact duplicate sync
            if (existingLookup.Contains((item.ParcelId, item.RecordedAtUtc)))
            {
                duplicatesIgnored++;
                continue;
            }

            var record = new CourierDeliveryRecordEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ParcelId = item.ParcelId,
                TrackingNumber = item.TrackingNumber,
                CourierDriverId = item.CourierDriverId,
                WilayaCode = item.WilayaCode,
                Status = item.Status,
                CashCollected = item.CashCollected,
                CustomerSignatureHash = item.CustomerSignatureHash,
                RecordedAtUtc = item.RecordedAtUtc,
                SyncedAtUtc = DateTime.UtcNow,
                IsOfflineSync = true
            };

            _dbContext.CourierDeliveryRecords.Add(record);
            successfullySynced++;
            totalCash += item.CashCollected;
            syncedParcelIds.Add(item.ParcelId);

            // Record OpenTelemetry telemetry per Wilaya
            GrowthOsMetrics.IncrementCourierSync(1, item.WilayaCode);
        }

        if (successfullySynced > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
            _logger?.LogInformation(
                "Synced {Count} courier delivery records for tenant {TenantId}. Reconciled Cash: {Cash} DZD",
                successfullySynced, tenantId, totalCash);
        }

        return new CourierDeliverySyncBatchResult(
            TotalReceived: totalReceived,
            SuccessfullySynced: successfullySynced,
            DuplicatesIgnored: duplicatesIgnored,
            TotalCashReconciled: totalCash,
            SyncedParcelIds: syncedParcelIds
        );
    }

    public async Task<IReadOnlyList<CourierDeliveryRecordEntity>> GetDriverDeliveriesAsync(
        Guid tenantId,
        string courierDriverId,
        CancellationToken ct = default)
    {
        return await _dbContext.CourierDeliveryRecords
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.CourierDriverId == courierDriverId)
            .OrderByDescending(c => c.RecordedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<DriverReconciliationSummary> GetDriverReconciliationSummaryAsync(
        Guid tenantId,
        string courierDriverId,
        CancellationToken ct = default)
    {
        var records = await _dbContext.CourierDeliveryRecords
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.CourierDriverId == courierDriverId)
            .ToListAsync(ct);

        var total = records.Count;
        var successful = records.Count(r => r.Status.Equals("Delivered", StringComparison.OrdinalIgnoreCase));
        var failed = records.Count(r => !r.Status.Equals("Delivered", StringComparison.OrdinalIgnoreCase));
        var cash = records.Sum(r => r.CashCollected);

        return new DriverReconciliationSummary(
            CourierDriverId: courierDriverId,
            TotalDeliveries: total,
            SuccessfulDeliveries: successful,
            FailedAttempts: failed,
            TotalCashCollected: cash
        );
    }
}
