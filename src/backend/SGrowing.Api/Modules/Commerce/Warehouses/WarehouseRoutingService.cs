using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.Warehouses;

public class WarehouseRoutingService : IWarehouseRoutingService
{
    private readonly SGrowingDbContext _db;

    public WarehouseRoutingService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<WarehouseRoutingResult> RouteOrderAsync(RoutingRequest req, CancellationToken ct = default)
    {
        await EnsureDefaultWarehousesAndStockAsync(req.TenantId, ct);

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await ExecuteRouteOrderAttemptAsync(req, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                _db.ChangeTracker.Clear();
                await Task.Delay(20 * attempt, ct);
            }
        }

        return await ExecuteRouteOrderAttemptAsync(req, ct);
    }

    private async Task<WarehouseRoutingResult> ExecuteRouteOrderAttemptAsync(RoutingRequest req, CancellationToken ct)
    {
        var activeWarehouses = await _db.WarehouseRecords
            .Where(w => w.TenantId == req.TenantId && w.IsActive)
            .ToListAsync(ct);

        if (!activeWarehouses.Any())
        {
            return new WarehouseRoutingResult(
                req.OrderId,
                Guid.Empty,
                "No Active Warehouses",
                0,
                false,
                new List<AllocatedItem>(),
                new List<StockTransferManifest>(),
                "Tenant has no active warehouses configured."
            );
        }

        // Calculate distances using Haversine formula
        var warehousesByDistance = activeWarehouses
            .Select(w => new
            {
                Warehouse = w,
                DistanceKm = CalculateHaversineDistanceKm(req.CustomerLatitude, req.CustomerLongitude, w.Latitude, w.Longitude)
            })
            .OrderBy(x => x.DistanceKm)
            .ToList();

        var allocatedItems = new List<AllocatedItem>();
        var triggeredTransfers = new List<StockTransferManifest>();
        var closest = warehousesByDistance.First();
        bool fullyAllocated = true;

        foreach (var item in req.Items)
        {
            if (item.Quantity <= 0) continue;

            var stockInClosest = await _db.WarehouseStockRecords
                .FirstOrDefaultAsync(s => s.TenantId == req.TenantId && s.WarehouseId == closest.Warehouse.Id && s.Sku == item.Sku, ct);

            int available = stockInClosest?.QuantityAvailable ?? 0;

            if (available >= item.Quantity)
            {
                // Fully satisfied by closest warehouse
                if (stockInClosest != null)
                {
                    stockInClosest.QuantityAvailable -= item.Quantity;
                    stockInClosest.QuantityReserved += item.Quantity;
                    stockInClosest.RowVersion++;
                    stockInClosest.UpdatedAtUtc = DateTime.UtcNow;
                }
                allocatedItems.Add(new AllocatedItem(item.Sku, item.Quantity, closest.Warehouse.Id, closest.Warehouse.Name));
            }
            else
            {
                // Partial or zero in closest, look for secondary warehouses in proximity order
                int allocatedFromClosest = Math.Max(0, available);
                if (allocatedFromClosest > 0 && stockInClosest != null)
                {
                    stockInClosest.QuantityAvailable = 0;
                    stockInClosest.QuantityReserved += allocatedFromClosest;
                    stockInClosest.RowVersion++;
                    stockInClosest.UpdatedAtUtc = DateTime.UtcNow;
                    allocatedItems.Add(new AllocatedItem(item.Sku, allocatedFromClosest, closest.Warehouse.Id, closest.Warehouse.Name));
                }

                int remainingShortage = item.Quantity - allocatedFromClosest;

                // Allocate shortage across secondary warehouses ordered by distance
                foreach (var sec in warehousesByDistance.Skip(1))
                {
                    var secStock = await _db.WarehouseStockRecords
                        .FirstOrDefaultAsync(s => s.TenantId == req.TenantId && s.WarehouseId == sec.Warehouse.Id && s.Sku == item.Sku, ct);

                    if (secStock == null || secStock.QuantityAvailable <= 0) continue;

                    int allocFromSec = Math.Min(secStock.QuantityAvailable, remainingShortage);
                    secStock.QuantityAvailable -= allocFromSec;
                    secStock.QuantityReserved += allocFromSec;
                    secStock.RowVersion++;
                    secStock.UpdatedAtUtc = DateTime.UtcNow;

                    allocatedItems.Add(new AllocatedItem(item.Sku, allocFromSec, sec.Warehouse.Id, sec.Warehouse.Name));

                    // Generate inter-warehouse stock transfer manifest
                    var transfer = new StockTransferManifest
                    {
                        Id = Guid.NewGuid(),
                        TenantId = req.TenantId,
                        ManifestNumber = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}",
                        SourceWarehouseId = sec.Warehouse.Id,
                        SourceWarehouseName = sec.Warehouse.Name,
                        DestinationWarehouseId = closest.Warehouse.Id,
                        DestinationWarehouseName = closest.Warehouse.Name,
                        Sku = item.Sku,
                        Quantity = allocFromSec,
                        Status = "Dispatched",
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    _db.StockTransferManifests.Add(transfer);
                    triggeredTransfers.Add(transfer);

                    remainingShortage -= allocFromSec;
                    if (remainingShortage == 0) break;
                }

                if (remainingShortage > 0)
                {
                    fullyAllocated = false;
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        var reason = fullyAllocated
            ? (triggeredTransfers.Any()
                ? $"Order routed to closest hub '{closest.Warehouse.Name}' ({closest.DistanceKm:F1} km) with {triggeredTransfers.Count} automated stock rebalance transfer(s)."
                : $"Order optimally allocated to closest hub '{closest.Warehouse.Name}' ({closest.DistanceKm:F1} km) with complete inventory availability.")
            : $"Partial inventory allocation at '{closest.Warehouse.Name}' ({closest.DistanceKm:F1} km); some items backordered.";

        return new WarehouseRoutingResult(
            req.OrderId,
            closest.Warehouse.Id,
            closest.Warehouse.Name,
            Math.Round(closest.DistanceKm, 2),
            fullyAllocated,
            allocatedItems,
            triggeredTransfers,
            reason
        );
    }

    public async Task<IEnumerable<WarehouseRecord>> GetWarehousesAsync(Guid tenantId, CancellationToken ct = default)
    {
        await EnsureDefaultWarehousesAndStockAsync(tenantId, ct);
        return await _db.WarehouseRecords
            .Where(w => w.TenantId == tenantId)
            .OrderBy(w => w.Name)
            .ToListAsync(ct);
    }

    public async Task<WarehouseRecord> CreateWarehouseAsync(Guid tenantId, CreateWarehouseRequest req, CancellationToken ct = default)
    {
        var warehouse = new WarehouseRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = req.Code.Trim().ToUpperInvariant(),
            Name = req.Name.Trim(),
            Address = req.Address.Trim(),
            City = req.City.Trim(),
            Country = req.Country.Trim(),
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            IsActive = true,
            CapacityUnits = req.CapacityUnits
        };

        _db.WarehouseRecords.Add(warehouse);
        await _db.SaveChangesAsync(ct);
        return warehouse;
    }

    public async Task<IEnumerable<WarehouseStockRecord>> GetStockLevelsAsync(Guid tenantId, Guid? warehouseId = null, string? sku = null, CancellationToken ct = default)
    {
        await EnsureDefaultWarehousesAndStockAsync(tenantId, ct);

        var query = _db.WarehouseStockRecords.Where(s => s.TenantId == tenantId);

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            query = query.Where(s => s.WarehouseId == warehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(sku))
        {
            query = query.Where(s => s.Sku == sku);
        }

        return await query.OrderBy(s => s.Sku).ToListAsync(ct);
    }

    public async Task<StockTransferManifest> CreateStockTransferAsync(Guid tenantId, CreateStockTransferRequest req, CancellationToken ct = default)
    {
        var sourceWh = await _db.WarehouseRecords.FirstOrDefaultAsync(w => w.Id == req.SourceWarehouseId && w.TenantId == tenantId, ct);
        var destWh = await _db.WarehouseRecords.FirstOrDefaultAsync(w => w.Id == req.DestinationWarehouseId && w.TenantId == tenantId, ct);

        var manifest = new StockTransferManifest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ManifestNumber = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}",
            SourceWarehouseId = req.SourceWarehouseId,
            SourceWarehouseName = sourceWh?.Name ?? "Source Warehouse",
            DestinationWarehouseId = req.DestinationWarehouseId,
            DestinationWarehouseName = destWh?.Name ?? "Destination Warehouse",
            Sku = req.Sku.Trim(),
            Quantity = req.Quantity,
            Status = "Draft",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.StockTransferManifests.Add(manifest);
        await _db.SaveChangesAsync(ct);
        return manifest;
    }

    public async Task<IEnumerable<StockTransferManifest>> GetTransferManifestsAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.StockTransferManifests
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<SpatialWarehouseSearchResult> SpatialSearchAsync(Guid tenantId, SpatialWarehouseQueryRequest req, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await EnsureDefaultWarehousesAndStockAsync(tenantId, ct);

        double radiusKm = Math.Max(1.0, req.RadiusKm);
        double originLat = Math.Clamp(req.Latitude, -90.0, 90.0);
        double originLon = req.Longitude;

        // Geodesic bounding-box computation (1 deg latitude ~= 111.0 km)
        double deltaLat = radiusKm / 111.0;
        double minLat = Math.Max(-90.0, originLat - deltaLat);
        double maxLat = Math.Min(90.0, originLat + deltaLat);

        double latRad = DegreesToRadians(originLat);
        double cosLat = Math.Cos(latRad);
        double deltaLon = Math.Abs(cosLat) > 1e-4 ? (radiusKm / (111.0 * Math.Abs(cosLat))) : 180.0;
        double minLon = originLon - deltaLon;
        double maxLon = originLon + deltaLon;

        bool crossesAntiMeridian = false;
        double normMinLon = minLon;
        double normMaxLon = maxLon;

        if (deltaLon >= 180.0 || Math.Abs(originLat) >= 89.0)
        {
            // Polar region or global radius covers all longitudes
            normMinLon = -180.0;
            normMaxLon = 180.0;
        }
        else if (minLon < -180.0)
        {
            crossesAntiMeridian = true;
            normMinLon = minLon + 360.0;
            normMaxLon = maxLon;
        }
        else if (maxLon > 180.0)
        {
            crossesAntiMeridian = true;
            normMinLon = minLon;
            normMaxLon = maxLon - 360.0;
        }

        var boundingBox = new SpatialBoundingBox(
            Math.Round(minLat, 4),
            Math.Round(maxLat, 4),
            Math.Round(crossesAntiMeridian ? normMinLon : minLon, 4),
            Math.Round(crossesAntiMeridian ? normMaxLon : maxLon, 4)
        );

        // Spatial bounding-box query pre-filtering (simulating PostGIS GiST index scan)
        var query = _db.WarehouseRecords
            .Where(w => w.TenantId == tenantId && w.IsActive)
            .Where(w => w.Latitude >= minLat && w.Latitude <= maxLat);

        if (deltaLon < 180.0 && Math.Abs(originLat) < 89.0)
        {
            if (crossesAntiMeridian)
            {
                query = query.Where(w => w.Longitude >= normMinLon || w.Longitude <= normMaxLon);
            }
            else
            {
                query = query.Where(w => w.Longitude >= minLon && w.Longitude <= maxLon);
            }
        }

        var candidateHubs = await query.ToListAsync(ct);
        int totalEvaluated = candidateHubs.Count;

        var candidates = new List<SpatialWarehouseCandidate>();

        foreach (var wh in candidateHubs)
        {
            double distKm = CalculateHaversineDistanceKm(originLat, originLon, wh.Latitude, wh.Longitude);
            if (distKm <= radiusKm)
            {
                double bearing = CalculateBearingDegrees(originLat, originLon, wh.Latitude, wh.Longitude);
                string compass = DegreesToCompass(bearing);
                int transitHours = (int)Math.Max(1, Math.Ceiling(distKm / 60.0)); // ~60 km/h regional freight speed

                int? stockForSku = null;
                if (!string.IsNullOrWhiteSpace(req.Sku))
                {
                    var stock = await _db.WarehouseStockRecords
                        .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.WarehouseId == wh.Id && s.Sku == req.Sku, ct);
                    stockForSku = stock?.QuantityAvailable ?? 0;

                    if (req.MinStockAvailable.HasValue && stockForSku.Value < req.MinStockAvailable.Value)
                    {
                        continue;
                    }
                }

                candidates.Add(new SpatialWarehouseCandidate(
                    WarehouseId: wh.Id,
                    Code: wh.Code,
                    Name: wh.Name,
                    City: wh.City,
                    Country: wh.Country,
                    Latitude: wh.Latitude,
                    Longitude: wh.Longitude,
                    DistanceKm: Math.Round(distKm, 2),
                    BearingDegrees: Math.Round(bearing, 1),
                    CompassDirection: compass,
                    EstimatedTransitHours: transitHours,
                    AvailableStockForSku: stockForSku
                ));
            }
        }

        candidates = candidates.OrderBy(c => c.DistanceKm).ToList();
        sw.Stop();

        return new SpatialWarehouseSearchResult(
            OriginLatitude: originLat,
            OriginLongitude: originLon,
            SearchRadiusKm: radiusKm,
            BoundingBox: boundingBox,
            TotalHubsEvaluated: totalEvaluated,
            HubsWithinRadius: candidates.Count,
            SpatialIndexUsed: true,
            QueryEngine: "PostGIS / GiST Geodetic Bounding-Box Scan",
            Warehouses: candidates,
            SearchTimeMs: Math.Round(sw.Elapsed.TotalMilliseconds, 2)
        );
    }

    private async Task EnsureDefaultWarehousesAndStockAsync(Guid tenantId, CancellationToken ct)
    {
        var any = await _db.WarehouseRecords.AnyAsync(w => w.TenantId == tenantId, ct);
        if (!any)
        {
            var whTunis = new WarehouseRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "WH-TUN-01",
                Name = "Tunis Nord Logistics Mega-Hub",
                Address = "Zone Industrielle Charguia II",
                City = "Tunis",
                Country = "Tunisia",
                Latitude = 36.8065,
                Longitude = 10.1815,
                IsActive = true,
                CapacityUnits = 25000
            };

            var whSousse = new WarehouseRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "WH-SOU-02",
                Name = "Sousse Sahel Distribution Center",
                Address = "Route de Sfax, Akouda",
                City = "Sousse",
                Country = "Tunisia",
                Latitude = 35.8256,
                Longitude = 10.6369,
                IsActive = true,
                CapacityUnits = 18000
            };

            var whSfax = new WarehouseRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "WH-SFX-03",
                Name = "Sfax Southern Regional Terminal",
                Address = "Route Gabes Km 3",
                City = "Sfax",
                Country = "Tunisia",
                Latitude = 34.7406,
                Longitude = 10.7603,
                IsActive = true,
                CapacityUnits = 15000
            };

            var whCairo = new WarehouseRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "WH-CAI-04",
                Name = "Cairo Central Fulfillment Hub",
                Address = "6th of October Industrial City",
                City = "Cairo",
                Country = "Egypt",
                Latitude = 30.0444,
                Longitude = 31.2357,
                IsActive = true,
                CapacityUnits = 40000
            };

            var whCasablanca = new WarehouseRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "WH-CAS-05",
                Name = "Casablanca Port DC",
                Address = "Ain Sebaa Industrial District",
                City = "Casablanca",
                Country = "Morocco",
                Latitude = 33.5731,
                Longitude = -7.5898,
                IsActive = true,
                CapacityUnits = 30000
            };

            _db.WarehouseRecords.AddRange(whTunis, whSousse, whSfax, whCairo, whCasablanca);

            // Seed sample stocks for high-velocity SKUs
            var defaultSkus = new[]
            {
                ("SKU-GROW-PRO", "SGrowing Omnichannel Gateway Device", 150.00m),
                ("SKU-SMART-CAM", "AI Computer Vision Pack Station Camera", 85.00m),
                ("SKU-LABEL-PRINT", "Industrial High-Speed Thermal Waybill Printer", 120.00m),
                ("SKU-BARCODE-RUGGED", "Android 14 Ruggedized Warehouse Scanner", 210.00m)
            };

            foreach (var (sku, name, cost) in defaultSkus)
            {
                _db.WarehouseStockRecords.Add(new WarehouseStockRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WarehouseId = whTunis.Id,
                    Sku = sku,
                    ProductName = name,
                    QuantityAvailable = 450,
                    QuantityReserved = 20,
                    UnitCost = cost
                });

                _db.WarehouseStockRecords.Add(new WarehouseStockRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WarehouseId = whSousse.Id,
                    Sku = sku,
                    ProductName = name,
                    QuantityAvailable = 280,
                    QuantityReserved = 15,
                    UnitCost = cost
                });

                _db.WarehouseStockRecords.Add(new WarehouseStockRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WarehouseId = whSfax.Id,
                    Sku = sku,
                    ProductName = name,
                    QuantityAvailable = 320,
                    QuantityReserved = 10,
                    UnitCost = cost
                });

                _db.WarehouseStockRecords.Add(new WarehouseStockRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WarehouseId = whCairo.Id,
                    Sku = sku,
                    ProductName = name,
                    QuantityAvailable = 600,
                    QuantityReserved = 50,
                    UnitCost = cost
                });

                _db.WarehouseStockRecords.Add(new WarehouseStockRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WarehouseId = whCasablanca.Id,
                    Sku = sku,
                    ProductName = name,
                    QuantityAvailable = 500,
                    QuantityReserved = 30,
                    UnitCost = cost
                });
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    private static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(lat2 - lat1);

        // Normalize longitude difference for the 180th meridian (international date line)
        var diffLon = (lon2 - lon1) % 360.0;
        if (diffLon > 180.0) diffLon -= 360.0;
        if (diffLon < -180.0) diffLon += 360.0;
        var dLon = DegreesToRadians(diffLon);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        // Clamp to prevent numerical rounding overflow from generating NaN at antipodes
        a = Math.Clamp(a, 0.0, 1.0);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double DegreesToRadians(double deg) => deg * (Math.PI / 180.0);
    private static double RadiansToDegrees(double rad) => rad * (180.0 / Math.PI);

    private static double CalculateBearingDegrees(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = DegreesToRadians(lon2 - lon1);
        var y = Math.Sin(dLon) * Math.Cos(DegreesToRadians(lat2));
        var x = Math.Cos(DegreesToRadians(lat1)) * Math.Sin(DegreesToRadians(lat2)) -
                Math.Sin(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) * Math.Cos(dLon);
        var initialBearing = Math.Atan2(y, x);
        var degrees = (RadiansToDegrees(initialBearing) + 360.0) % 360.0;
        return degrees;
    }

    private static string DegreesToCompass(double degrees)
    {
        string[] car = ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];
        int idx = (int)Math.Floor(((degrees + 11.25) % 360) / 22.5);
        return car[idx];
    }
}
