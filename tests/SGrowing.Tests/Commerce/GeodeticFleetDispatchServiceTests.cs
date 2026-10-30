using System.Text.RegularExpressions;
using SGrowing.Api.Modules.Commerce.Warehouses;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class GeodeticFleetDispatchServiceTests
{
    private readonly GeodeticFleetDispatchService _service;
    private static readonly Guid TunisHubId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DubaiHubId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public GeodeticFleetDispatchServiceTests()
    {
        _service = new GeodeticFleetDispatchService();
        _service.ResetToSeedData();
    }

    #region 1. Spherical Haversine Distance & Matrix Tests

    [Fact]
    public void CalculateHaversineDistanceKm_IdenticalCoordinates_ReturnsZero()
    {
        double dist = _service.CalculateHaversineDistanceKm(36.8065, 10.1815, 36.8065, 10.1815);
        Assert.Equal(0.0, dist);
    }

    [Fact]
    public void CalculateHaversineDistanceKm_KnownTunisCoordinates_ReturnsAccurateDistance()
    {
        // Tunis Center (36.8008, 10.1800) to La Marsa Plage (36.8782, 10.3247) ~ 15.4 km
        double dist = _service.CalculateHaversineDistanceKm(36.8008, 10.1800, 36.8782, 10.3247);
        Assert.InRange(dist, 14.5, 16.5);
    }

    [Fact]
    public void CalculateHaversineDistanceKm_KnownDubaiCoordinates_ReturnsAccurateDistance()
    {
        // Dubai Marina (25.0784, 55.1402) to Downtown Burj Khalifa (25.1972, 55.2744) ~ 18.7 km
        double dist = _service.CalculateHaversineDistanceKm(25.0784, 55.1402, 25.1972, 55.2744);
        Assert.InRange(dist, 17.5, 20.0);
    }

    [Fact]
    public void CalculateHaversineDistanceKm_IsSymmetric()
    {
        double d1 = _service.CalculateHaversineDistanceKm(36.8008, 10.1800, 36.8782, 10.3247);
        double d2 = _service.CalculateHaversineDistanceKm(36.8782, 10.3247, 36.8008, 10.1800);
        Assert.Equal(d1, d2, 4);
    }

    [Fact]
    public void CalculateHaversineDistanceKm_EquatorOneDegree_MatchesEarthCircumference()
    {
        // 1 degree longitude at equator is approx 111.19 km (6371 * pi / 180 = ~111.19)
        double dist = _service.CalculateHaversineDistanceKm(0.0, 0.0, 0.0, 1.0);
        Assert.InRange(dist, 111.0, 111.5);
    }

    [Fact]
    public void ComputeDistanceMatrix_GeneratesSymmetricMatrixWithZeroDiagonal()
    {
        var locations = new List<(double Latitude, double Longitude)>
        {
            (36.8485, 10.2035), // Tunis Hub
            (36.8008, 10.1800), // Centre Ville
            (36.8782, 10.3247), // Marsa
            (36.8580, 10.3308)  // Carthage
        };

        var matrix = _service.ComputeDistanceMatrix(locations);

        Assert.Equal(4, matrix.GetLength(0));
        Assert.Equal(4, matrix.GetLength(1));

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(0.0, matrix[i, i]);
            for (int j = 0; j < 4; j++)
            {
                Assert.Equal(matrix[i, j], matrix[j, i], 4);
                if (i != j)
                {
                    Assert.True(matrix[i, j] > 0.0);
                }
            }
        }
    }

    [Fact]
    public void ComputeDistanceMatrix_SingleLocation_ReturnsZeroMatrix()
    {
        var locations = new List<(double Latitude, double Longitude)> { (25.1972, 55.2744) };
        var matrix = _service.ComputeDistanceMatrix(locations);

        Assert.Equal(1, matrix.GetLength(0));
        Assert.Equal(0.0, matrix[0, 0]);
    }

    #endregion

    #region 2. Seed Data & Queries Tests

    [Fact]
    public async Task GetLogisticsHubsAsync_ReturnsTunisAndDubaiHubs()
    {
        var hubs = await _service.GetLogisticsHubsAsync();

        Assert.Equal(2, hubs.Count);
        Assert.Contains(hubs, h => h.Code == "HUB-TUN-01" && h.Currency == "TND");
        Assert.Contains(hubs, h => h.Code == "HUB-DXB-01" && h.Currency == "AED");
    }

    [Fact]
    public async Task GetActiveDriversAsync_FiltersByHub()
    {
        var allDrivers = await _service.GetActiveDriversAsync();
        var tunisDrivers = await _service.GetActiveDriversAsync(TunisHubId);
        var dubaiDrivers = await _service.GetActiveDriversAsync(DubaiHubId);

        Assert.Equal(3, allDrivers.Count);
        Assert.Equal(2, tunisDrivers.Count);
        Assert.Single(dubaiDrivers);
        Assert.Equal("DRV-03", dubaiDrivers[0].DriverCode);
    }

    [Fact]
    public async Task GetDeliveryStopsAsync_ReturnsSeededStopsByHub()
    {
        var tunisStops = await _service.GetDeliveryStopsAsync(TunisHubId);
        var dubaiStops = await _service.GetDeliveryStopsAsync(DubaiHubId);

        Assert.Equal(8, tunisStops.Count);
        Assert.Equal(7, dubaiStops.Count);
        Assert.All(tunisStops, s => Assert.Equal("TND", s.Currency));
        Assert.All(dubaiStops, s => Assert.Equal("AED", s.Currency));
    }

    [Fact]
    public async Task AddDeliveryStopAsync_PersistsNewStop()
    {
        var newStop = new DeliveryStop
        {
            HubId = TunisHubId,
            TrackingNumber = "TRK-TUN-9999",
            CustomerName = "Nidhal Saidi",
            CustomerPhone = "+216 22 999 888",
            DeliveryAddress = "Boulevard du 14 Janvier, Sousse",
            City = "Sousse",
            Latitude = 35.8256,
            Longitude = 10.6084,
            ParcelWeightKg = 3.2,
            CodAmount = 175.50m,
            Currency = "TND",
            TimeWindowStart = new TimeSpan(10, 0, 0),
            TimeWindowEnd = new TimeSpan(14, 0, 0),
            WindowLabel = "Morning",
            Priority = "Urgent",
            Status = "Pending"
        };

        var created = await _service.AddDeliveryStopAsync(newStop);

        Assert.NotEqual(Guid.Empty, created.StopId);
        var retrieved = await _service.GetDeliveryStopsAsync(TunisHubId);
        Assert.Contains(retrieved, s => s.TrackingNumber == "TRK-TUN-9999");
    }

    #endregion

    #region 3. Geodesic Capacitated K-Means Spatial Clustering Tests

    [Fact]
    public async Task OptimizeFleetDispatchAsync_ThrowsOnNonExistentHub()
    {
        var invalidReq = new FleetOptimizationRequest
        {
            HubId = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.OptimizeFleetDispatchAsync(invalidReq));
    }

    [Fact]
    public async Task OptimizeFleetDispatchAsync_NoStops_ReturnsEmptyOptimizationResult()
    {
        var emptyReq = new FleetOptimizationRequest
        {
            HubId = TunisHubId,
            StopIds = [Guid.NewGuid()] // non-existent stop
        };

        var result = await _service.OptimizeFleetDispatchAsync(emptyReq);

        Assert.Equal(0, result.TotalStopsAssigned);
        Assert.Equal(0, result.TotalDriversUsed);
        Assert.Empty(result.Routes);
    }

    [Fact]
    public async Task OptimizeFleetDispatchAsync_TunisHub_PartitionsStopsBetweenDrivers()
    {
        var req = new FleetOptimizationRequest
        {
            HubId = TunisHubId,
            DepartureTime = new TimeSpan(8, 30, 0),
            AverageSpeedKmh = 35.0
        };

        var result = await _service.OptimizeFleetDispatchAsync(req);

        Assert.NotNull(result);
        Assert.Equal("Tunis Central Fulfillment Hub", result.Hub.Name);
        Assert.Equal(8, result.TotalStopsAssigned + result.TotalUnassignedStops);
        Assert.True(result.Routes.Count >= 1);
        Assert.True(result.TotalFleetDistanceKm > 0.0);
        Assert.True(result.TotalFleetCodAmount > 0m);

        // Verify that waypoints across routes match assigned stops
        int totalWaypoints = result.Routes.Sum(r => r.Waypoints.Count);
        Assert.Equal(result.TotalStopsAssigned, totalWaypoints);
    }

    [Fact]
    public async Task OptimizeFleetDispatchAsync_RespectsDriverParcelCapacityConstraints()
    {
        // Create an isolated service with a driver that has capacity = 2 parcels
        var customService = new GeodeticFleetDispatchService();
        customService.ResetToSeedData();

        var hub = (await customService.GetLogisticsHubsAsync()).First(h => h.HubId == TunisHubId);
        var stops = await customService.GetDeliveryStopsAsync(hub.HubId);

        // Optimize restricting to Driver 2 (motorcycle max 15 parcels)
        var drivers = await customService.GetActiveDriversAsync(hub.HubId);
        var motoDriver = drivers.First(d => d.VehicleType.Contains("Motorcycle"));

        var req = new FleetOptimizationRequest
        {
            HubId = hub.HubId,
            DriverIds = [motoDriver.DriverId]
        };

        var result = await customService.OptimizeFleetDispatchAsync(req);

        Assert.Single(result.Routes);
        Assert.True(result.Routes[0].StopCount <= motoDriver.MaxParcelCapacity);
        Assert.True(result.Routes[0].TotalWeightKg <= motoDriver.MaxWeightCapacityKg);
    }

    [Fact]
    public async Task OptimizeFleetDispatchAsync_DriverWeightCapacityOverflow_PlacesInUnassigned()
    {
        var customService = new GeodeticFleetDispatchService();
        customService.ResetToSeedData();

        var hub = (await customService.GetLogisticsHubsAsync()).First(h => h.HubId == TunisHubId);

        // Add a giant parcel that exceeds all vehicle capacities (1500kg)
        var hugeStop = new DeliveryStop
        {
            HubId = hub.HubId,
            TrackingNumber = "TRK-HUGE-01",
            CustomerName = "Heavy Freight",
            CustomerPhone = "+216 00 000 000",
            DeliveryAddress = "Heavy Cargo Yard",
            City = "Tunis",
            Latitude = 36.8500,
            Longitude = 10.2100,
            ParcelWeightKg = 1500.0,
            CodAmount = 1000m,
            Currency = "TND",
            TimeWindowStart = new TimeSpan(9, 0, 0),
            TimeWindowEnd = new TimeSpan(17, 0, 0)
        };
        await customService.AddDeliveryStopAsync(hugeStop);

        var result = await customService.OptimizeFleetDispatchAsync(new FleetOptimizationRequest
        {
            HubId = hub.HubId,
            StopIds = [hugeStop.StopId]
        });

        Assert.Equal(1, result.TotalUnassignedStops);
        Assert.Contains(result.UnassignedStops, s => s.TrackingNumber == "TRK-HUGE-01");
        Assert.Empty(result.Routes);
    }

    #endregion

    #region 4. 2-Opt TSP Route Optimization Tests

    [Fact]
    public async Task Solve2OptTsp_RouteStartsAndEndsAtHub()
    {
        var req = new FleetOptimizationRequest
        {
            HubId = DubaiHubId,
            DepartureTime = new TimeSpan(9, 0, 0)
        };

        var result = await _service.OptimizeFleetDispatchAsync(req);

        Assert.NotEmpty(result.Routes);
        var route = result.Routes[0];

        // The first waypoint sequence is 1 and route begins from Hub
        Assert.Equal(1, route.Waypoints[0].SequenceOrder);
        // Distance includes return leg to Hub
        Assert.True(route.TotalRouteDistanceKm > route.Waypoints.Last().CumulativeDistanceKm);
    }

    [Fact]
    public async Task Solve2OptTsp_SingleStop_FormsRoundTripToHub()
    {
        var allStops = await _service.GetDeliveryStopsAsync(TunisHubId);
        var singleStop = allStops[0];

        var req = new FleetOptimizationRequest
        {
            HubId = TunisHubId,
            StopIds = [singleStop.StopId]
        };

        var result = await _service.OptimizeFleetDispatchAsync(req);

        Assert.Single(result.Routes);
        var route = result.Routes[0];
        Assert.Single(route.Waypoints);

        var hub = (await _service.GetLogisticsHubsAsync()).First(h => h.HubId == TunisHubId);
        double oneWayDist = _service.CalculateHaversineDistanceKm(hub.Latitude, hub.Longitude, singleStop.Latitude, singleStop.Longitude);
        double expectedRoundTrip = Math.Round(oneWayDist * 2.0, 2);

        Assert.Equal(expectedRoundTrip, route.TotalRouteDistanceKm, 1);
    }

    [Fact]
    public async Task Solve2OptTsp_VisitsEveryAssignedStopExactlyOnce()
    {
        var req = new FleetOptimizationRequest { HubId = DubaiHubId };
        var result = await _service.OptimizeFleetDispatchAsync(req);

        var visitedStopIds = result.Routes.SelectMany(r => r.Waypoints.Select(w => w.StopId)).ToList();
        var uniqueStopIds = visitedStopIds.Distinct().ToList();

        Assert.Equal(visitedStopIds.Count, uniqueStopIds.Count);
        Assert.Equal(result.TotalStopsAssigned, visitedStopIds.Count);
    }

    [Fact]
    public async Task Solve2OptTsp_CalculatesNonNegativeLegDistances()
    {
        var req = new FleetOptimizationRequest { HubId = TunisHubId };
        var result = await _service.OptimizeFleetDispatchAsync(req);

        foreach (var route in result.Routes)
        {
            foreach (var wp in route.Waypoints)
            {
                Assert.True(wp.LegDistanceKm >= 0.0);
                Assert.True(wp.CumulativeDistanceKm >= wp.LegDistanceKm);
            }
        }
    }

    #endregion

    #region 5. Time Window Constraints & Scheduling Tests

    [Fact]
    public async Task TimeWindowConstraints_EarlyArrival_CalculatesWaitingMinutes()
    {
        // Stop with TimeWindowStart at 11:00 AM, departure at 08:30 AM
        var stop = new DeliveryStop
        {
            HubId = TunisHubId,
            TrackingNumber = "TRK-WAIT-01",
            CustomerName = "Late Morning Customer",
            CustomerPhone = "+216 00 000 000",
            DeliveryAddress = "Near Hub Stop",
            City = "Tunis",
            Latitude = 36.8490, // Very close to Hub (36.8485, 10.2035)
            Longitude = 10.2040,
            ParcelWeightKg = 1.0,
            CodAmount = 50m,
            Currency = "TND",
            TimeWindowStart = new TimeSpan(11, 0, 0),
            TimeWindowEnd = new TimeSpan(13, 0, 0),
            WindowLabel = "Morning"
        };
        await _service.AddDeliveryStopAsync(stop);

        var result = await _service.OptimizeFleetDispatchAsync(new FleetOptimizationRequest
        {
            HubId = TunisHubId,
            StopIds = [stop.StopId],
            DepartureTime = new TimeSpan(8, 30, 0)
        });

        var wp = result.Routes[0].Waypoints[0];
        Assert.True(wp.EstimatedArrival < new TimeSpan(9, 0, 0));
        Assert.True(wp.WaitMinutes > 60); // Must wait ~2 hours until 11:00
        Assert.Equal(new TimeSpan(11, 0, 0), wp.EstimatedDeparture.Subtract(TimeSpan.FromMinutes(12)));
        Assert.False(wp.IsLate);
        Assert.Equal(0, wp.LateMinutes);
    }

    [Fact]
    public async Task TimeWindowConstraints_LateArrival_CalculatesLateMinutesAndPenalty()
    {
        // Stop with morning window that ends at 09:00 AM, but departure is at 10:00 AM
        var lateStop = new DeliveryStop
        {
            HubId = TunisHubId,
            TrackingNumber = "TRK-LATE-01",
            CustomerName = "Missed Window Customer",
            CustomerPhone = "+216 00 000 000",
            DeliveryAddress = "Far Southern Suburb",
            City = "Ben Arous",
            Latitude = 36.7000,
            Longitude = 10.2000,
            ParcelWeightKg = 2.0,
            CodAmount = 80m,
            Currency = "TND",
            TimeWindowStart = new TimeSpan(8, 0, 0),
            TimeWindowEnd = new TimeSpan(9, 0, 0),
            WindowLabel = "Morning"
        };
        await _service.AddDeliveryStopAsync(lateStop);

        var result = await _service.OptimizeFleetDispatchAsync(new FleetOptimizationRequest
        {
            HubId = TunisHubId,
            StopIds = [lateStop.StopId],
            DepartureTime = new TimeSpan(10, 0, 0),
            LatePenaltyPerMinute = 1.5
        });

        var wp = result.Routes[0].Waypoints[0];
        Assert.True(wp.IsLate);
        Assert.True(wp.LateMinutes > 60); // At least 60 minutes past 09:00
        Assert.Equal(Math.Round(wp.LateMinutes * 1.5, 2), wp.LatePenaltyScore);
        Assert.True(result.Routes[0].TotalLatePenalty > 0.0);
    }

    [Fact]
    public async Task TimeWindowConstraints_OnTimeDelivery_ZeroLatePenalty()
    {
        // Normal Morning window 09:00 - 13:00, depart at 09:00 to nearby stop
        var normalStop = new DeliveryStop
        {
            HubId = TunisHubId,
            TrackingNumber = "TRK-ONTIME-01",
            CustomerName = "On Time Customer",
            CustomerPhone = "+216 00 000 000",
            DeliveryAddress = "Nearby Avenue",
            City = "Tunis",
            Latitude = 36.8500,
            Longitude = 10.2050,
            ParcelWeightKg = 1.5,
            CodAmount = 60m,
            Currency = "TND",
            TimeWindowStart = new TimeSpan(9, 0, 0),
            TimeWindowEnd = new TimeSpan(13, 0, 0)
        };
        await _service.AddDeliveryStopAsync(normalStop);

        var result = await _service.OptimizeFleetDispatchAsync(new FleetOptimizationRequest
        {
            HubId = TunisHubId,
            StopIds = [normalStop.StopId],
            DepartureTime = new TimeSpan(9, 0, 0)
        });

        var wp = result.Routes[0].Waypoints[0];
        Assert.False(wp.IsLate);
        Assert.Equal(0, wp.LateMinutes);
        Assert.Equal(0.0, wp.LatePenaltyScore);
    }

    #endregion

    #region 6. Driver Runsheet Manifest Generation & COD Tally Tests

    [Fact]
    public async Task DispatchRunsheetsAsync_GeneratesManifestWithValidCodeFormat()
    {
        var optResult = await _service.OptimizeFleetDispatchAsync(new FleetOptimizationRequest
        {
            HubId = TunisHubId
        });

        var manifests = await _service.DispatchRunsheetsAsync(new DispatchRunsheetsRequest
        {
            HubId = TunisHubId,
            OptimizationResult = optResult
        });

        Assert.NotEmpty(manifests);
        foreach (var m in manifests)
        {
            Assert.Matches(@"^RUN-\d{8}-DRV\w+", m.RunsheetCode);
            Assert.Equal("Dispatched", m.Status);
            Assert.Equal(TunisHubId, m.HubId);
            Assert.True(m.TotalStops > 0);
            Assert.True(m.TotalDistanceKm > 0.0);
        }
    }

    [Fact]
    public async Task DispatchRunsheetsAsync_CalculatesExactCodTally()
    {
        var optResult = await _service.OptimizeFleetDispatchAsync(new FleetOptimizationRequest
        {
            HubId = DubaiHubId
        });

        var manifests = await _service.DispatchRunsheetsAsync(new DispatchRunsheetsRequest
        {
            HubId = DubaiHubId,
            OptimizationResult = optResult
        });

        Assert.NotEmpty(manifests);
        var manifest = manifests[0];

        decimal expectedCod = manifest.ManifestStops.Sum(s => s.CodAmount);
        Assert.Equal(expectedCod, manifest.TotalCodToCollect);
        Assert.Equal("AED", manifest.Currency);
    }

    [Fact]
    public async Task DispatchRunsheetsAsync_UpdatesDeliveryStopsStatusToDispatched()
    {
        var optResult = await _service.OptimizeFleetDispatchAsync(new FleetOptimizationRequest
        {
            HubId = DubaiHubId
        });

        var manifests = await _service.DispatchRunsheetsAsync(new DispatchRunsheetsRequest
        {
            HubId = DubaiHubId,
            OptimizationResult = optResult
        });

        var dispatchedStopIds = manifests.SelectMany(m => m.ManifestStops.Select(s => s.StopId)).ToList();
        var allStops = await _service.GetDeliveryStopsAsync(DubaiHubId);

        foreach (var stopId in dispatchedStopIds)
        {
            var stop = allStops.First(s => s.StopId == stopId);
            Assert.Equal("Dispatched", stop.Status);
        }
    }

    [Fact]
    public async Task GetRunsheetByIdAndCode_RetrievesManifestAccurately()
    {
        var manifests = await _service.DispatchRunsheetsAsync(new DispatchRunsheetsRequest
        {
            HubId = TunisHubId
        });

        var target = manifests[0];

        var byId = await _service.GetRunsheetByIdAsync(target.RunsheetId);
        var byCode = await _service.GetRunsheetByCodeAsync(target.RunsheetCode);

        Assert.NotNull(byId);
        Assert.NotNull(byCode);
        Assert.Equal(target.RunsheetId, byId.RunsheetId);
        Assert.Equal(target.RunsheetCode, byCode.RunsheetCode);
    }

    [Fact]
    public async Task GetAllRunsheetsAsync_FiltersByHubId()
    {
        await _service.DispatchRunsheetsAsync(new DispatchRunsheetsRequest { HubId = TunisHubId });
        await _service.DispatchRunsheetsAsync(new DispatchRunsheetsRequest { HubId = DubaiHubId });

        var tunisManifests = await _service.GetAllRunsheetsAsync(TunisHubId);
        var dubaiManifests = await _service.GetAllRunsheetsAsync(DubaiHubId);

        Assert.All(tunisManifests, m => Assert.Equal(TunisHubId, m.HubId));
        Assert.All(dubaiManifests, m => Assert.Equal(DubaiHubId, m.HubId));
    }

    [Fact]
    public async Task ResetToSeedData_RestoresFactorySeedValues()
    {
        var manifests = await _service.DispatchRunsheetsAsync(new DispatchRunsheetsRequest { HubId = TunisHubId });
        Assert.NotEmpty(manifests);

        _service.ResetToSeedData();

        var freshManifests = await _service.GetAllRunsheetsAsync();
        var stops = await _service.GetDeliveryStopsAsync(TunisHubId);

        Assert.Empty(freshManifests);
        Assert.All(stops, s => Assert.Equal("Pending", s.Status));
    }

    #endregion

    #region 7. Thread-Safety & Concurrent Execution Tests

    [Fact]
    public async Task ConcurrentOptimizations_ExecuteWithoutStateCorruption()
    {
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var hubId = (i % 2 == 0) ? TunisHubId : DubaiHubId;
            var optResult = await _service.OptimizeFleetDispatchAsync(new FleetOptimizationRequest
            {
                HubId = hubId,
                DepartureTime = new TimeSpan(8, 0, 0).Add(TimeSpan.FromMinutes(i * 5))
            });

            Assert.NotNull(optResult);
            Assert.True(optResult.TotalDriversUsed >= 1);
            return optResult;
        });

        var results = await Task.WhenAll(tasks);
        Assert.Equal(10, results.Length);
    }

    [Fact]
    public async Task ConcurrentManifestDispatches_GenerateUniqueCodesSafely()
    {
        _service.ResetToSeedData();

        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            return await _service.DispatchRunsheetsAsync(new DispatchRunsheetsRequest
            {
                HubId = TunisHubId
            });
        });

        var allManifestLists = await Task.WhenAll(tasks);
        var allCodes = allManifestLists.SelectMany(list => list.Select(m => m.RunsheetCode)).ToList();

        // Every manifest code must be globally unique
        Assert.Equal(allCodes.Count, allCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    #endregion
}
