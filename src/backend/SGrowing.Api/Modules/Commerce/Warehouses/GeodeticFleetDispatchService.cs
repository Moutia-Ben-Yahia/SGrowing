using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Commerce.Warehouses;

#region Domain Models & DTOs

public sealed class LogisticsHub
{
    public required Guid HubId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Address { get; init; }
    public required string City { get; init; }
    public required string Country { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required string Currency { get; init; }
    public bool IsActive { get; set; } = true;

    public LogisticsHub Clone() => new()
    {
        HubId = HubId,
        Code = Code,
        Name = Name,
        Address = Address,
        City = City,
        Country = Country,
        Latitude = Latitude,
        Longitude = Longitude,
        Currency = Currency,
        IsActive = IsActive
    };
}

public sealed class CourierDriver
{
    public required Guid DriverId { get; init; }
    public required string DriverCode { get; init; }
    public required string DriverName { get; init; }
    public required string PhoneNumber { get; init; }
    public required string VehicleType { get; init; }
    public required int MaxParcelCapacity { get; init; }
    public required double MaxWeightCapacityKg { get; init; }
    public required double BaseLatitude { get; init; }
    public required double BaseLongitude { get; init; }
    public required Guid AssignedHubId { get; init; }
    public bool IsActive { get; set; } = true;

    public CourierDriver Clone() => new()
    {
        DriverId = DriverId,
        DriverCode = DriverCode,
        DriverName = DriverName,
        PhoneNumber = PhoneNumber,
        VehicleType = VehicleType,
        MaxParcelCapacity = MaxParcelCapacity,
        MaxWeightCapacityKg = MaxWeightCapacityKg,
        BaseLatitude = BaseLatitude,
        BaseLongitude = BaseLongitude,
        AssignedHubId = AssignedHubId,
        IsActive = IsActive
    };
}

public sealed class DeliveryStop
{
    public Guid StopId { get; set; } = Guid.NewGuid();
    public required Guid HubId { get; init; }
    public required string TrackingNumber { get; init; }
    public required string CustomerName { get; init; }
    public required string CustomerPhone { get; init; }
    public required string DeliveryAddress { get; init; }
    public required string City { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double ParcelWeightKg { get; init; }
    public required decimal CodAmount { get; init; }
    public required string Currency { get; init; }
    public required TimeSpan TimeWindowStart { get; init; }
    public required TimeSpan TimeWindowEnd { get; init; }
    public string WindowLabel { get; init; } = "Morning";
    public string Priority { get; init; } = "Standard";
    public string Status { get; set; } = "Pending";

    public DeliveryStop Clone() => new()
    {
        StopId = StopId,
        HubId = HubId,
        TrackingNumber = TrackingNumber,
        CustomerName = CustomerName,
        CustomerPhone = CustomerPhone,
        DeliveryAddress = DeliveryAddress,
        City = City,
        Latitude = Latitude,
        Longitude = Longitude,
        ParcelWeightKg = ParcelWeightKg,
        CodAmount = CodAmount,
        Currency = Currency,
        TimeWindowStart = TimeWindowStart,
        TimeWindowEnd = TimeWindowEnd,
        WindowLabel = WindowLabel,
        Priority = Priority,
        Status = Status
    };
}

public sealed class RouteWaypoint
{
    public required int SequenceOrder { get; init; }
    public required Guid StopId { get; init; }
    public required string TrackingNumber { get; init; }
    public required string CustomerName { get; init; }
    public required string DeliveryAddress { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double ParcelWeightKg { get; init; }
    public required decimal CodAmount { get; init; }
    public required string Currency { get; init; }
    public required TimeSpan TimeWindowStart { get; init; }
    public required TimeSpan TimeWindowEnd { get; init; }
    public required string WindowLabel { get; init; }
    public required TimeSpan EstimatedArrival { get; init; }
    public required TimeSpan EstimatedDeparture { get; init; }
    public required int WaitMinutes { get; init; }
    public required int LateMinutes { get; init; }
    public required bool IsLate { get; init; }
    public required double LatePenaltyScore { get; init; }
    public required double LegDistanceKm { get; init; }
    public required double CumulativeDistanceKm { get; init; }
    public string Status { get; set; } = "Pending";
}

public sealed class CentroidCoordinates
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class DriverClusterRoute
{
    public required Guid ClusterId { get; init; }
    public required CourierDriver Driver { get; init; }
    public required int StopCount { get; init; }
    public required double TotalWeightKg { get; init; }
    public required double CapacityUtilizationPct { get; init; }
    public required double WeightUtilizationPct { get; init; }
    public required decimal TotalCodAmount { get; init; }
    public required string Currency { get; init; }
    public required double TotalRouteDistanceKm { get; init; }
    public required TimeSpan EstimatedStartTime { get; init; }
    public required TimeSpan EstimatedEndTime { get; init; }
    public required int TotalDurationMinutes { get; init; }
    public required int TotalLateMinutes { get; init; }
    public required double TotalLatePenalty { get; init; }
    public required List<RouteWaypoint> Waypoints { get; init; } = [];
    public CentroidCoordinates? Centroid { get; set; }
}

public sealed class FleetOptimizationRequest
{
    public required Guid HubId { get; init; }
    public List<Guid>? StopIds { get; init; }
    public List<Guid>? DriverIds { get; init; }
    public TimeSpan? DepartureTime { get; init; } = new TimeSpan(8, 30, 0);
    public double AverageSpeedKmh { get; init; } = 35.0;
    public int ServiceTimePerStopMinutes { get; init; } = 12;
    public double LatePenaltyPerMinute { get; init; } = 0.5;
    public int MaxIterations2Opt { get; init; } = 200;
}

public sealed class FleetOptimizationResult
{
    public Guid OptimizationId { get; init; } = Guid.NewGuid();
    public DateTime ExecutedAtUtc { get; init; } = DateTime.UtcNow;
    public required LogisticsHub Hub { get; init; }
    public required int TotalStopsAssigned { get; init; }
    public required int TotalUnassignedStops { get; init; }
    public required int TotalDriversUsed { get; init; }
    public required double TotalFleetDistanceKm { get; init; }
    public required int TotalFleetDurationMinutes { get; init; }
    public required decimal TotalFleetCodAmount { get; init; }
    public required string Currency { get; init; }
    public required double TotalFleetLatePenalty { get; init; }
    public required List<DriverClusterRoute> Routes { get; init; } = [];
    public required List<DeliveryStop> UnassignedStops { get; init; } = [];
}

public sealed class DriverRunsheetManifest
{
    public Guid RunsheetId { get; init; } = Guid.NewGuid();
    public required string RunsheetCode { get; init; }
    public required Guid HubId { get; init; }
    public required string HubName { get; init; }
    public required Guid DriverId { get; init; }
    public required string DriverCode { get; init; }
    public required string DriverName { get; init; }
    public required string VehicleType { get; init; }
    public DateTime DispatchedDateUtc { get; init; } = DateTime.UtcNow;
    public required int TotalStops { get; init; }
    public required double TotalDistanceKm { get; init; }
    public required int EstimatedDurationMinutes { get; init; }
    public required TimeSpan DepartureTime { get; init; }
    public required TimeSpan ReturnTime { get; init; }
    public required decimal TotalCodToCollect { get; init; }
    public required string Currency { get; init; }
    public string Status { get; set; } = "Generated"; // Generated, Dispatched, InProgress, Completed
    public required List<RouteWaypoint> ManifestStops { get; init; } = [];
}

public sealed class DispatchRunsheetsRequest
{
    public required Guid HubId { get; init; }
    public FleetOptimizationResult? OptimizationResult { get; init; }
    public List<Guid>? SpecificClusterIds { get; init; }
}

#endregion

#region Service Interface

public interface IGeodeticFleetDispatchService
{
    Task<IReadOnlyList<LogisticsHub>> GetLogisticsHubsAsync(CancellationToken ct = default);
    Task<LogisticsHub?> GetHubByIdAsync(Guid hubId, CancellationToken ct = default);
    Task<IReadOnlyList<CourierDriver>> GetActiveDriversAsync(Guid? hubId = null, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryStop>> GetDeliveryStopsAsync(Guid? hubId = null, string? status = null, CancellationToken ct = default);
    Task<DeliveryStop> AddDeliveryStopAsync(DeliveryStop stop, CancellationToken ct = default);

    double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2);
    double[,] ComputeDistanceMatrix(IReadOnlyList<(double Latitude, double Longitude)> coordinates);

    Task<FleetOptimizationResult> OptimizeFleetDispatchAsync(FleetOptimizationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<DriverRunsheetManifest>> DispatchRunsheetsAsync(DispatchRunsheetsRequest request, CancellationToken ct = default);
    Task<DriverRunsheetManifest?> GetRunsheetByIdAsync(Guid runsheetId, CancellationToken ct = default);
    Task<DriverRunsheetManifest?> GetRunsheetByCodeAsync(string runsheetCode, CancellationToken ct = default);
    Task<IReadOnlyList<DriverRunsheetManifest>> GetAllRunsheetsAsync(Guid? hubId = null, CancellationToken ct = default);

    void ResetToSeedData();
}

#endregion

#region Implementation

public sealed class GeodeticFleetDispatchService : IGeodeticFleetDispatchService
{
    private static readonly Lazy<GeodeticFleetDispatchService> _lazyInstance =
        new(() => new GeodeticFleetDispatchService());

    public static GeodeticFleetDispatchService Instance => _lazyInstance.Value;

    private const double EarthRadiusKm = 6371.0;
    private readonly object _syncLock = new();

    private readonly ConcurrentDictionary<Guid, LogisticsHub> _hubs = new();
    private readonly ConcurrentDictionary<Guid, CourierDriver> _drivers = new();
    private readonly ConcurrentDictionary<Guid, DeliveryStop> _stops = new();
    private readonly ConcurrentDictionary<Guid, DriverRunsheetManifest> _runsheets = new();
    private readonly ConcurrentDictionary<string, Guid> _runsheetCodeIndex = new(StringComparer.OrdinalIgnoreCase);

    public GeodeticFleetDispatchService()
    {
        InitializeSeedData();
    }

    public void ResetToSeedData()
    {
        lock (_syncLock)
        {
            _hubs.Clear();
            _drivers.Clear();
            _stops.Clear();
            _runsheets.Clear();
            _runsheetCodeIndex.Clear();
            InitializeSeedData();
        }
    }

    private void InitializeSeedData()
    {
        // Hub 1: Tunis Central Fulfillment Hub
        var tunisHubId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tunisHub = new LogisticsHub
        {
            HubId = tunisHubId,
            Code = "HUB-TUN-01",
            Name = "Tunis Central Fulfillment Hub",
            Address = "Zone Industrielle Charguia II, Rue des Entrepreneurs",
            City = "Tunis",
            Country = "Tunisia",
            Latitude = 36.8485,
            Longitude = 10.2035,
            Currency = "TND"
        };
        _hubs[tunisHubId] = tunisHub;

        // Hub 2: Dubai Logistics Park Hub
        var dubaiHubId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var dubaiHub = new LogisticsHub
        {
            HubId = dubaiHubId,
            Code = "HUB-DXB-01",
            Name = "Dubai Logistics Park Hub",
            Address = "Dubai South Aviation & Logistics City, Gate 4",
            City = "Dubai",
            Country = "UAE",
            Latitude = 24.8967,
            Longitude = 55.1611,
            Currency = "AED"
        };
        _hubs[dubaiHubId] = dubaiHub;

        // Driver 1: Sami Ben Amor (Tunis Van)
        var driver1Id = Guid.Parse("d1111111-1111-1111-1111-111111111111");
        _drivers[driver1Id] = new CourierDriver
        {
            DriverId = driver1Id,
            DriverCode = "DRV-01",
            DriverName = "Sami Ben Amor",
            PhoneNumber = "+216 98 123 456",
            VehicleType = "Delivery Van (Renault Master)",
            MaxParcelCapacity = 25,
            MaxWeightCapacityKg = 350.0,
            BaseLatitude = 36.8485,
            BaseLongitude = 10.2035,
            AssignedHubId = tunisHubId,
            IsActive = true
        };

        // Driver 2: Khaled Trabelsi (Tunis Motorcycle)
        var driver2Id = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        _drivers[driver2Id] = new CourierDriver
        {
            DriverId = driver2Id,
            DriverCode = "DRV-02",
            DriverName = "Khaled Trabelsi",
            PhoneNumber = "+216 55 987 654",
            VehicleType = "Express Motorcycle (Honda Cargo)",
            MaxParcelCapacity = 15,
            MaxWeightCapacityKg = 45.0,
            BaseLatitude = 36.8625,
            BaseLongitude = 10.1956,
            AssignedHubId = tunisHubId,
            IsActive = true
        };

        // Driver 3: Tariq Mansoor (Dubai Light Truck)
        var driver3Id = Guid.Parse("d3333333-3333-3333-3333-333333333333");
        _drivers[driver3Id] = new CourierDriver
        {
            DriverId = driver3Id,
            DriverCode = "DRV-03",
            DriverName = "Tariq Mansoor",
            PhoneNumber = "+971 50 456 7890",
            VehicleType = "Light Truck (Isuzu NPR)",
            MaxParcelCapacity = 45,
            MaxWeightCapacityKg = 750.0,
            BaseLatitude = 24.8967,
            BaseLongitude = 55.1611,
            AssignedHubId = dubaiHubId,
            IsActive = true
        };

        // 8 Stops for Tunis Hub
        var tunisStops = new List<DeliveryStop>
        {
            new()
            {
                StopId = Guid.Parse("c1000000-0000-0000-0000-000000000001"),
                HubId = tunisHubId,
                TrackingNumber = "TRK-TUN-1001",
                CustomerName = "Amine Rezgui",
                CustomerPhone = "+216 21 001 001",
                DeliveryAddress = "Avenue Habib Bourguiba, Centre Ville",
                City = "Tunis",
                Latitude = 36.8008,
                Longitude = 10.1800,
                ParcelWeightKg = 2.5,
                CodAmount = 85.00m,
                Currency = "TND",
                TimeWindowStart = new TimeSpan(9, 0, 0),
                TimeWindowEnd = new TimeSpan(12, 30, 0),
                WindowLabel = "Morning",
                Priority = "Standard"
            },
            new()
            {
                StopId = Guid.Parse("c1000000-0000-0000-0000-000000000002"),
                HubId = tunisHubId,
                TrackingNumber = "TRK-TUN-1002",
                CustomerName = "Ines Ben Salem",
                CustomerPhone = "+216 21 001 002",
                DeliveryAddress = "Rue Habib Thameur, La Marsa Plage",
                City = "La Marsa",
                Latitude = 36.8782,
                Longitude = 10.3247,
                ParcelWeightKg = 4.2,
                CodAmount = 140.00m,
                Currency = "TND",
                TimeWindowStart = new TimeSpan(9, 30, 0),
                TimeWindowEnd = new TimeSpan(13, 0, 0),
                WindowLabel = "Morning",
                Priority = "Express"
            },
            new()
            {
                StopId = Guid.Parse("c1000000-0000-0000-0000-000000000003"),
                HubId = tunisHubId,
                TrackingNumber = "TRK-TUN-1003",
                CustomerName = "Youssef Chahed",
                CustomerPhone = "+216 21 001 003",
                DeliveryAddress = "Colline de Byrsa, Carthage",
                City = "Carthage",
                Latitude = 36.8580,
                Longitude = 10.3308,
                ParcelWeightKg = 1.8,
                CodAmount = 65.50m,
                Currency = "TND",
                TimeWindowStart = new TimeSpan(9, 0, 0),
                TimeWindowEnd = new TimeSpan(13, 0, 0),
                WindowLabel = "Morning",
                Priority = "Standard"
            },
            new()
            {
                StopId = Guid.Parse("c1000000-0000-0000-0000-000000000004"),
                HubId = tunisHubId,
                TrackingNumber = "TRK-TUN-1004",
                CustomerName = "Leila Mansour",
                CustomerPhone = "+216 21 001 004",
                DeliveryAddress = "Avenue Taieb Mehiri, Ariana Supérieure",
                City = "Ariana",
                Latitude = 36.8663,
                Longitude = 10.1647,
                ParcelWeightKg = 6.0,
                CodAmount = 210.00m,
                Currency = "TND",
                TimeWindowStart = new TimeSpan(13, 0, 0),
                TimeWindowEnd = new TimeSpan(17, 30, 0),
                WindowLabel = "Afternoon",
                Priority = "Urgent"
            },
            new()
            {
                StopId = Guid.Parse("c1000000-0000-0000-0000-000000000005"),
                HubId = tunisHubId,
                TrackingNumber = "TRK-TUN-1005",
                CustomerName = "Mehdi Trabelsi",
                CustomerPhone = "+216 21 001 005",
                DeliveryAddress = "Rue Ahmed Tlili, Menzah 6",
                City = "Tunis",
                Latitude = 36.8402,
                Longitude = 10.1652,
                ParcelWeightKg = 3.1,
                CodAmount = 115.00m,
                Currency = "TND",
                TimeWindowStart = new TimeSpan(13, 0, 0),
                TimeWindowEnd = new TimeSpan(16, 30, 0),
                WindowLabel = "Afternoon",
                Priority = "Standard"
            },
            new()
            {
                StopId = Guid.Parse("c1000000-0000-0000-0000-000000000006"),
                HubId = tunisHubId,
                TrackingNumber = "TRK-TUN-1006",
                CustomerName = "Sarra Bouazizi",
                CustomerPhone = "+216 21 001 006",
                DeliveryAddress = "Avenue Hedi Nouira, Ennasr 2",
                City = "Ariana",
                Latitude = 36.8578,
                Longitude = 10.1558,
                ParcelWeightKg = 5.5,
                CodAmount = 195.00m,
                Currency = "TND",
                TimeWindowStart = new TimeSpan(13, 30, 0),
                TimeWindowEnd = new TimeSpan(18, 0, 0),
                WindowLabel = "Afternoon",
                Priority = "Standard"
            },
            new()
            {
                StopId = Guid.Parse("c1000000-0000-0000-0000-000000000007"),
                HubId = tunisHubId,
                TrackingNumber = "TRK-TUN-1007",
                CustomerName = "Foued Gharbi",
                CustomerPhone = "+216 21 001 007",
                DeliveryAddress = "Rue de la République, Ben Arous Centre",
                City = "Ben Arous",
                Latitude = 36.7531,
                Longitude = 10.2222,
                ParcelWeightKg = 8.4,
                CodAmount = 280.00m,
                Currency = "TND",
                TimeWindowStart = new TimeSpan(9, 0, 0),
                TimeWindowEnd = new TimeSpan(13, 0, 0),
                WindowLabel = "Morning",
                Priority = "Standard"
            },
            new()
            {
                StopId = Guid.Parse("c1000000-0000-0000-0000-000000000008"),
                HubId = tunisHubId,
                TrackingNumber = "TRK-TUN-1008",
                CustomerName = "Nourhene Khelifi",
                CustomerPhone = "+216 21 001 008",
                DeliveryAddress = "Avenue Habib Thameur, Megrine Coteaux",
                City = "Megrine",
                Latitude = 36.7689,
                Longitude = 10.2378,
                ParcelWeightKg = 3.8,
                CodAmount = 130.00m,
                Currency = "TND",
                TimeWindowStart = new TimeSpan(9, 30, 0),
                TimeWindowEnd = new TimeSpan(13, 30, 0),
                WindowLabel = "Morning",
                Priority = "Standard"
            }
        };

        foreach (var stop in tunisStops)
        {
            _stops[stop.StopId] = stop;
        }

        // 7 Stops for Dubai Hub
        var dubaiStops = new List<DeliveryStop>
        {
            new()
            {
                StopId = Guid.Parse("c2000000-0000-0000-0000-000000000001"),
                HubId = dubaiHubId,
                TrackingNumber = "TRK-DXB-2001",
                CustomerName = "Rashid Al Nuaimi",
                CustomerPhone = "+971 50 111 0001",
                DeliveryAddress = "Burj Khalifa Boulevard, Downtown Dubai",
                City = "Dubai",
                Latitude = 25.1972,
                Longitude = 55.2744,
                ParcelWeightKg = 3.5,
                CodAmount = 420.00m,
                Currency = "AED",
                TimeWindowStart = new TimeSpan(9, 0, 0),
                TimeWindowEnd = new TimeSpan(13, 0, 0),
                WindowLabel = "Morning",
                Priority = "Express"
            },
            new()
            {
                StopId = Guid.Parse("c2000000-0000-0000-0000-000000000002"),
                HubId = dubaiHubId,
                TrackingNumber = "TRK-DXB-2002",
                CustomerName = "Sophie Dubois",
                CustomerPhone = "+971 50 111 0002",
                DeliveryAddress = "Marina Walk Tower B, Dubai Marina",
                City = "Dubai",
                Latitude = 25.0784,
                Longitude = 55.1402,
                ParcelWeightKg = 5.0,
                CodAmount = 550.00m,
                Currency = "AED",
                TimeWindowStart = new TimeSpan(9, 30, 0),
                TimeWindowEnd = new TimeSpan(13, 30, 0),
                WindowLabel = "Morning",
                Priority = "Standard"
            },
            new()
            {
                StopId = Guid.Parse("c2000000-0000-0000-0000-000000000003"),
                HubId = dubaiHubId,
                TrackingNumber = "TRK-DXB-2003",
                CustomerName = "Kareem Al Mansoori",
                CustomerPhone = "+971 50 111 0003",
                DeliveryAddress = "Marasi Drive, Business Bay",
                City = "Dubai",
                Latitude = 25.1857,
                Longitude = 55.2678,
                ParcelWeightKg = 2.2,
                CodAmount = 290.00m,
                Currency = "AED",
                TimeWindowStart = new TimeSpan(9, 0, 0),
                TimeWindowEnd = new TimeSpan(13, 0, 0),
                WindowLabel = "Morning",
                Priority = "Standard"
            },
            new()
            {
                StopId = Guid.Parse("c2000000-0000-0000-0000-000000000004"),
                HubId = dubaiHubId,
                TrackingNumber = "TRK-DXB-2004",
                CustomerName = "Elena Rostova",
                CustomerPhone = "+971 50 111 0004",
                DeliveryAddress = "Rimal Sector, Jumeirah Beach Residence",
                City = "Dubai",
                Latitude = 25.0740,
                Longitude = 55.1320,
                ParcelWeightKg = 4.8,
                CodAmount = 480.00m,
                Currency = "AED",
                TimeWindowStart = new TimeSpan(13, 0, 0),
                TimeWindowEnd = new TimeSpan(17, 0, 0),
                WindowLabel = "Afternoon",
                Priority = "Urgent"
            },
            new()
            {
                StopId = Guid.Parse("c2000000-0000-0000-0000-000000000005"),
                HubId = dubaiHubId,
                TrackingNumber = "TRK-DXB-2005",
                CustomerName = "Vikram Mehta",
                CustomerPhone = "+971 50 111 0005",
                DeliveryAddress = "Street 23, Near Mall of Emirates, Al Barsha 1",
                City = "Dubai",
                Latitude = 25.1180,
                Longitude = 55.2000,
                ParcelWeightKg = 9.0,
                CodAmount = 720.00m,
                Currency = "AED",
                TimeWindowStart = new TimeSpan(13, 0, 0),
                TimeWindowEnd = new TimeSpan(17, 30, 0),
                WindowLabel = "Afternoon",
                Priority = "Standard"
            },
            new()
            {
                StopId = Guid.Parse("c2000000-0000-0000-0000-000000000006"),
                HubId = dubaiHubId,
                TrackingNumber = "TRK-DXB-2006",
                CustomerName = "Mohamed Sultan",
                CustomerPhone = "+971 50 111 0006",
                DeliveryAddress = "Al Rigga Road, Deira Creek",
                City = "Dubai",
                Latitude = 25.2630,
                Longitude = 55.3210,
                ParcelWeightKg = 6.5,
                CodAmount = 390.00m,
                Currency = "AED",
                TimeWindowStart = new TimeSpan(13, 30, 0),
                TimeWindowEnd = new TimeSpan(18, 0, 0),
                WindowLabel = "Afternoon",
                Priority = "Standard"
            },
            new()
            {
                StopId = Guid.Parse("c2000000-0000-0000-0000-000000000007"),
                HubId = dubaiHubId,
                TrackingNumber = "TRK-DXB-2007",
                CustomerName = "Zaid Fakhoury",
                CustomerPhone = "+971 50 111 0007",
                DeliveryAddress = "Fortune Executive Tower, Cluster T, JLT",
                City = "Dubai",
                Latitude = 25.0690,
                Longitude = 55.1450,
                ParcelWeightKg = 3.0,
                CodAmount = 340.00m,
                Currency = "AED",
                TimeWindowStart = new TimeSpan(14, 0, 0),
                TimeWindowEnd = new TimeSpan(18, 0, 0),
                WindowLabel = "Afternoon",
                Priority = "Standard"
            }
        };

        foreach (var stop in dubaiStops)
        {
            _stops[stop.StopId] = stop;
        }
    }

    #region Basic Queries

    public Task<IReadOnlyList<LogisticsHub>> GetLogisticsHubsAsync(CancellationToken ct = default)
    {
        var list = _hubs.Values.Select(h => h.Clone()).ToList();
        return Task.FromResult<IReadOnlyList<LogisticsHub>>(list);
    }

    public Task<LogisticsHub?> GetHubByIdAsync(Guid hubId, CancellationToken ct = default)
    {
        _hubs.TryGetValue(hubId, out var hub);
        return Task.FromResult(hub?.Clone());
    }

    public Task<IReadOnlyList<CourierDriver>> GetActiveDriversAsync(Guid? hubId = null, CancellationToken ct = default)
    {
        var query = _drivers.Values.Where(d => d.IsActive);
        if (hubId.HasValue && hubId.Value != Guid.Empty)
        {
            query = query.Where(d => d.AssignedHubId == hubId.Value);
        }
        var list = query.Select(d => d.Clone()).ToList();
        return Task.FromResult<IReadOnlyList<CourierDriver>>(list);
    }

    public Task<IReadOnlyList<DeliveryStop>> GetDeliveryStopsAsync(Guid? hubId = null, string? status = null, CancellationToken ct = default)
    {
        var query = _stops.Values.AsEnumerable();
        if (hubId.HasValue && hubId.Value != Guid.Empty)
        {
            query = query.Where(s => s.HubId == hubId.Value);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(s => string.Equals(s.Status, status, StringComparison.OrdinalIgnoreCase));
        }
        var list = query.Select(s => s.Clone()).ToList();
        return Task.FromResult<IReadOnlyList<DeliveryStop>>(list);
    }

    public Task<DeliveryStop> AddDeliveryStopAsync(DeliveryStop stop, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stop);
        var created = stop.Clone();
        if (created.StopId == Guid.Empty)
        {
            created.StopId = Guid.NewGuid();
        }
        _stops[created.StopId] = created;
        return Task.FromResult(created.Clone());
    }

    #endregion

    #region Geodetic Calculations

    public double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        if (Math.Abs(lat1 - lat2) < 1e-9 && Math.Abs(lon1 - lon2) < 1e-9)
            return 0.0;

        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double radLat1 = ToRadians(lat1);
        double radLat2 = ToRadians(lat2);

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(radLat1) * Math.Cos(radLat2) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        a = Math.Clamp(a, 0.0, 1.0);
        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return Math.Round(EarthRadiusKm * c, 4);
    }

    public double[,] ComputeDistanceMatrix(IReadOnlyList<(double Latitude, double Longitude)> coordinates)
    {
        int count = coordinates.Count;
        var matrix = new double[count, count];

        for (int i = 0; i < count; i++)
        {
            matrix[i, i] = 0.0;
            for (int j = i + 1; j < count; j++)
            {
                double dist = CalculateHaversineDistanceKm(
                    coordinates[i].Latitude, coordinates[i].Longitude,
                    coordinates[j].Latitude, coordinates[j].Longitude);

                matrix[i, j] = dist;
                matrix[j, i] = dist;
            }
        }

        return matrix;
    }

    private static double ToRadians(double deg) => deg * (Math.PI / 180.0);
    private static double ToDegrees(double rad) => rad * (180.0 / Math.PI);

    private static (double Lat, double Lon) ComputeGeodesicCentroid(IReadOnlyList<(double Lat, double Lon)> points)
    {
        if (points.Count == 0) return (0.0, 0.0);
        if (points.Count == 1) return points[0];

        double x = 0.0, y = 0.0, z = 0.0;
        foreach (var p in points)
        {
            double radLat = ToRadians(p.Lat);
            double radLon = ToRadians(p.Lon);
            x += Math.Cos(radLat) * Math.Cos(radLon);
            y += Math.Cos(radLat) * Math.Sin(radLon);
            z += Math.Sin(radLat);
        }

        double total = points.Count;
        x /= total;
        y /= total;
        z /= total;

        double centralLon = Math.Atan2(y, x);
        double centralHyp = Math.Sqrt(x * x + y * y);
        double centralLat = Math.Atan2(z, centralHyp);

        return (Math.Round(ToDegrees(centralLat), 6), Math.Round(ToDegrees(centralLon), 6));
    }

    #endregion

    #region Fleet Optimization (Geodesic K-Means + 2-Opt TSP + Time Windows)

    public Task<FleetOptimizationResult> OptimizeFleetDispatchAsync(FleetOptimizationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_hubs.TryGetValue(request.HubId, out var hub))
        {
            throw new InvalidOperationException($"Logistics Hub with ID {request.HubId} was not found.");
        }

        // Get available drivers for this hub
        var availableDrivers = _drivers.Values
            .Where(d => d.IsActive && d.AssignedHubId == request.HubId)
            .Select(d => d.Clone())
            .ToList();

        if (request.DriverIds != null && request.DriverIds.Count > 0)
        {
            availableDrivers = availableDrivers.Where(d => request.DriverIds.Contains(d.DriverId)).ToList();
        }

        // Get stops to allocate
        var candidateStops = _stops.Values
            .Where(s => s.HubId == request.HubId)
            .Select(s => s.Clone())
            .ToList();

        if (request.StopIds != null && request.StopIds.Count > 0)
        {
            candidateStops = candidateStops.Where(s => request.StopIds.Contains(s.StopId)).ToList();
        }

        // If no stops or no drivers, return empty result
        if (candidateStops.Count == 0 || availableDrivers.Count == 0)
        {
            return Task.FromResult(new FleetOptimizationResult
            {
                OptimizationId = Guid.NewGuid(),
                ExecutedAtUtc = DateTime.UtcNow,
                Hub = hub.Clone(),
                TotalStopsAssigned = 0,
                TotalUnassignedStops = candidateStops.Count,
                TotalDriversUsed = 0,
                TotalFleetDistanceKm = 0.0,
                TotalFleetDurationMinutes = 0,
                TotalFleetCodAmount = 0m,
                Currency = hub.Currency,
                TotalFleetLatePenalty = 0.0,
                Routes = [],
                UnassignedStops = candidateStops
            });
        }

        int kClusters = Math.Min(availableDrivers.Count, candidateStops.Count);
        var activeDrivers = availableDrivers.Take(kClusters).ToList();

        // 1. Geodesic Capacitated K-Means Spatial Clustering
        var (clusterAssignments, unassigned) = PerformCapacitatedGeodesicKMeans(hub, activeDrivers, candidateStops);

        // 2. 2-Opt TSP Route Optimization & Time Window Scheduling for each cluster
        var departureTime = request.DepartureTime ?? new TimeSpan(8, 30, 0);
        var routes = new List<DriverClusterRoute>();

        for (int i = 0; i < activeDrivers.Count; i++)
        {
            var driver = activeDrivers[i];
            var stopsInCluster = clusterAssignments[i];

            if (stopsInCluster.Count == 0)
            {
                continue;
            }

            // Run 2-Opt TSP
            var optimizedSequence = Solve2OptTsp(hub, stopsInCluster, request.MaxIterations2Opt);

            // Compute Time Windows, Arrival Times, Distances, Lateness, and COD
            var clusterRoute = BuildScheduledRoute(
                hub,
                driver,
                optimizedSequence,
                departureTime,
                request.AverageSpeedKmh,
                request.ServiceTimePerStopMinutes,
                request.LatePenaltyPerMinute);

            routes.Add(clusterRoute);
        }

        double totalFleetDistance = Math.Round(routes.Sum(r => r.TotalRouteDistanceKm), 2);
        int totalFleetDuration = routes.Count > 0 ? routes.Max(r => r.TotalDurationMinutes) : 0;
        decimal totalFleetCod = routes.Sum(r => r.TotalCodAmount);
        double totalFleetLatePenalty = Math.Round(routes.Sum(r => r.TotalLatePenalty), 2);

        var result = new FleetOptimizationResult
        {
            OptimizationId = Guid.NewGuid(),
            ExecutedAtUtc = DateTime.UtcNow,
            Hub = hub.Clone(),
            TotalStopsAssigned = routes.Sum(r => r.StopCount),
            TotalUnassignedStops = unassigned.Count,
            TotalDriversUsed = routes.Count,
            TotalFleetDistanceKm = totalFleetDistance,
            TotalFleetDurationMinutes = totalFleetDuration,
            TotalFleetCodAmount = totalFleetCod,
            Currency = hub.Currency,
            TotalFleetLatePenalty = totalFleetLatePenalty,
            Routes = routes,
            UnassignedStops = unassigned
        };

        return Task.FromResult(result);
    }

    private (List<List<DeliveryStop>> ClusterAssignments, List<DeliveryStop> Unassigned) PerformCapacitatedGeodesicKMeans(
        LogisticsHub hub,
        List<CourierDriver> drivers,
        List<DeliveryStop> stops)
    {
        int k = drivers.Count;
        var centroids = new List<(double Lat, double Lon)>(k);

        // Centroid initialization:
        // Try to initialize using driver bases if distinct, or K-Means++ over stops
        if (drivers.All(d => Math.Abs(d.BaseLatitude - hub.Latitude) > 1e-4 || Math.Abs(d.BaseLongitude - hub.Longitude) > 1e-4))
        {
            foreach (var d in drivers)
            {
                centroids.Add((d.BaseLatitude, d.BaseLongitude));
            }
        }
        else
        {
            // K-Means++ initialization
            // First centroid: stop furthest from hub
            var firstStop = stops.MaxBy(s => CalculateHaversineDistanceKm(hub.Latitude, hub.Longitude, s.Latitude, s.Longitude)) ?? stops[0];
            centroids.Add((firstStop.Latitude, firstStop.Longitude));

            while (centroids.Count < k)
            {
                double maxMinDist = -1.0;
                DeliveryStop? bestNext = null;

                foreach (var s in stops)
                {
                    double minDistToCentroids = centroids.Min(c => CalculateHaversineDistanceKm(s.Latitude, s.Longitude, c.Lat, c.Lon));
                    if (minDistToCentroids > maxMinDist)
                    {
                        maxMinDist = minDistToCentroids;
                        bestNext = s;
                    }
                }

                if (bestNext != null)
                {
                    centroids.Add((bestNext.Latitude, bestNext.Longitude));
                }
                else
                {
                    centroids.Add((stops[centroids.Count % stops.Count].Latitude, stops[centroids.Count % stops.Count].Longitude));
                }
            }
        }

        const int maxIterations = 25;
        List<List<DeliveryStop>> bestClusters = [];
        List<DeliveryStop> bestUnassigned = [];

        for (int iter = 0; iter < maxIterations; iter++)
        {
            var currentClusters = new List<List<DeliveryStop>>(k);
            var currentLoadsKg = new double[k];
            for (int i = 0; i < k; i++) currentClusters.Add([]);

            var currentUnassigned = new List<DeliveryStop>();

            // Sort stops: prioritize stops with tightest constraints or further from hub
            var prioritizedStops = stops
                .OrderByDescending(s => s.ParcelWeightKg)
                .ThenBy(s => s.TimeWindowEnd)
                .ToList();

            foreach (var stop in prioritizedStops)
            {
                // Find nearest centroid with remaining capacity
                int bestClusterIdx = -1;
                double bestDist = double.MaxValue;

                for (int cIdx = 0; cIdx < k; cIdx++)
                {
                    var driver = drivers[cIdx];
                    bool hasCapacity = (currentClusters[cIdx].Count + 1 <= driver.MaxParcelCapacity) &&
                                       (currentLoadsKg[cIdx] + stop.ParcelWeightKg <= driver.MaxWeightCapacityKg);

                    if (hasCapacity)
                    {
                        double dist = CalculateHaversineDistanceKm(stop.Latitude, stop.Longitude, centroids[cIdx].Lat, centroids[cIdx].Lon);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestClusterIdx = cIdx;
                        }
                    }
                }

                if (bestClusterIdx >= 0)
                {
                    currentClusters[bestClusterIdx].Add(stop);
                    currentLoadsKg[bestClusterIdx] += stop.ParcelWeightKg;
                }
                else
                {
                    // Overflow stop cannot fit within any driver capacity limit
                    currentUnassigned.Add(stop);
                }
            }

            bestClusters = currentClusters;
            bestUnassigned = currentUnassigned;

            // Recalculate centroids
            bool converged = true;
            for (int cIdx = 0; cIdx < k; cIdx++)
            {
                if (currentClusters[cIdx].Count > 0)
                {
                    var points = currentClusters[cIdx].Select(s => (s.Latitude, s.Longitude)).ToList();
                    var newCentroid = ComputeGeodesicCentroid(points);

                    double shift = CalculateHaversineDistanceKm(centroids[cIdx].Lat, centroids[cIdx].Lon, newCentroid.Lat, newCentroid.Lon);
                    if (shift > 0.001) // 1 meter movement threshold
                    {
                        converged = false;
                        centroids[cIdx] = newCentroid;
                    }
                }
            }

            if (converged) break;
        }

        return (bestClusters, bestUnassigned);
    }

    private List<DeliveryStop> Solve2OptTsp(LogisticsHub hub, List<DeliveryStop> stops, int maxIterations)
    {
        if (stops.Count <= 1)
        {
            return stops;
        }

        // 1. Initial Greedy Nearest-Neighbor Tour starting at Hub
        var remaining = new HashSet<DeliveryStop>(stops);
        var tour = new List<DeliveryStop>(stops.Count);

        double curLat = hub.Latitude;
        double curLon = hub.Longitude;

        while (remaining.Count > 0)
        {
            DeliveryStop? nearest = null;
            double minDist = double.MaxValue;

            foreach (var s in remaining)
            {
                double d = CalculateHaversineDistanceKm(curLat, curLon, s.Latitude, s.Longitude);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = s;
                }
            }

            if (nearest != null)
            {
                tour.Add(nearest);
                remaining.Remove(nearest);
                curLat = nearest.Latitude;
                curLon = nearest.Longitude;
            }
            else
            {
                break;
            }
        }

        if (tour.Count <= 2)
        {
            return tour;
        }

        // 2. 2-Opt Heuristic Edge Swapping
        // Tour: Hub (index 0) -> S_0 (1) -> S_1 (2) ... -> S_n-1 (n) -> Hub (n+1)
        int n = tour.Count;
        bool improved = true;
        int iter = 0;

        while (improved && iter < maxIterations)
        {
            improved = false;
            iter++;

            for (int i = 0; i < n - 1; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    // Evaluate 2-Opt Swap between edges (i-1, i) and (j, j+1)
                    var prevStop = i == 0 ? (hub.Latitude, hub.Longitude) : (tour[i - 1].Latitude, tour[i - 1].Longitude);
                    var nextStop = j == n - 1 ? (hub.Latitude, hub.Longitude) : (tour[j + 1].Latitude, tour[j + 1].Longitude);

                    var currentI = (tour[i].Latitude, tour[i].Longitude);
                    var currentJ = (tour[j].Latitude, tour[j].Longitude);

                    double currentCost = CalculateHaversineDistanceKm(prevStop.Latitude, prevStop.Longitude, currentI.Latitude, currentI.Longitude)
                                       + CalculateHaversineDistanceKm(currentJ.Latitude, currentJ.Longitude, nextStop.Latitude, nextStop.Longitude);

                    double swappedCost = CalculateHaversineDistanceKm(prevStop.Latitude, prevStop.Longitude, currentJ.Latitude, currentJ.Longitude)
                                       + CalculateHaversineDistanceKm(currentI.Latitude, currentI.Longitude, nextStop.Latitude, nextStop.Longitude);

                    if (swappedCost < currentCost - 1e-6)
                    {
                        tour.Reverse(i, j - i + 1);
                        improved = true;
                    }
                }
            }
        }

        return tour;
    }

    private DriverClusterRoute BuildScheduledRoute(
        LogisticsHub hub,
        CourierDriver driver,
        List<DeliveryStop> orderedStops,
        TimeSpan departureTime,
        double avgSpeedKmh,
        int serviceTimeMinutes,
        double latePenaltyPerMinute)
    {
        var waypoints = new List<RouteWaypoint>();
        double currentLat = hub.Latitude;
        double currentLon = hub.Longitude;
        double cumulativeDistance = 0.0;
        var currentTime = departureTime;

        double totalLatePenalty = 0.0;
        int totalLateMinutes = 0;

        for (int i = 0; i < orderedStops.Count; i++)
        {
            var stop = orderedStops[i];

            // Distance & travel time from previous location
            double legDist = CalculateHaversineDistanceKm(currentLat, currentLon, stop.Latitude, stop.Longitude);
            cumulativeDistance += legDist;

            double travelTimeMinutes = (legDist / Math.Max(avgSpeedKmh, 5.0)) * 60.0;
            var arrivalTime = currentTime.Add(TimeSpan.FromMinutes(travelTimeMinutes));

            // Wait time if arrived before time window start
            int waitMinutes = 0;
            TimeSpan serviceStartTime;
            if (arrivalTime < stop.TimeWindowStart)
            {
                waitMinutes = (int)Math.Ceiling((stop.TimeWindowStart - arrivalTime).TotalMinutes);
                serviceStartTime = stop.TimeWindowStart;
            }
            else
            {
                serviceStartTime = arrivalTime;
            }

            // Late evaluation if arrived after time window end
            int lateMinutes = 0;
            bool isLate = false;
            double latePenalty = 0.0;

            if (arrivalTime > stop.TimeWindowEnd)
            {
                lateMinutes = (int)Math.Ceiling((arrivalTime - stop.TimeWindowEnd).TotalMinutes);
                isLate = true;
                latePenalty = lateMinutes * latePenaltyPerMinute;
                totalLateMinutes += lateMinutes;
                totalLatePenalty += latePenalty;
            }

            var departureTimeFromStop = serviceStartTime.Add(TimeSpan.FromMinutes(serviceTimeMinutes));

            waypoints.Add(new RouteWaypoint
            {
                SequenceOrder = i + 1,
                StopId = stop.StopId,
                TrackingNumber = stop.TrackingNumber,
                CustomerName = stop.CustomerName,
                DeliveryAddress = stop.DeliveryAddress,
                Latitude = stop.Latitude,
                Longitude = stop.Longitude,
                ParcelWeightKg = stop.ParcelWeightKg,
                CodAmount = stop.CodAmount,
                Currency = stop.Currency,
                TimeWindowStart = stop.TimeWindowStart,
                TimeWindowEnd = stop.TimeWindowEnd,
                WindowLabel = stop.WindowLabel,
                EstimatedArrival = arrivalTime,
                EstimatedDeparture = departureTimeFromStop,
                WaitMinutes = waitMinutes,
                LateMinutes = lateMinutes,
                IsLate = isLate,
                LatePenaltyScore = Math.Round(latePenalty, 2),
                LegDistanceKm = Math.Round(legDist, 2),
                CumulativeDistanceKm = Math.Round(cumulativeDistance, 2),
                Status = "Pending"
            });

            currentLat = stop.Latitude;
            currentLon = stop.Longitude;
            currentTime = departureTimeFromStop;
        }

        // Return leg to Hub
        double returnLegDist = CalculateHaversineDistanceKm(currentLat, currentLon, hub.Latitude, hub.Longitude);
        double totalRouteDistanceKm = Math.Round(cumulativeDistance + returnLegDist, 2);
        double returnTravelMinutes = (returnLegDist / Math.Max(avgSpeedKmh, 5.0)) * 60.0;
        var finalReturnTime = currentTime.Add(TimeSpan.FromMinutes(returnTravelMinutes));

        int totalDurationMinutes = (int)Math.Ceiling((finalReturnTime - departureTime).TotalMinutes);
        double totalWeightKg = Math.Round(orderedStops.Sum(s => s.ParcelWeightKg), 2);
        decimal totalCod = orderedStops.Sum(s => s.CodAmount);

        double capUtilPct = driver.MaxParcelCapacity > 0
            ? Math.Round(((double)orderedStops.Count / driver.MaxParcelCapacity) * 100.0, 1)
            : 0.0;

        double weightUtilPct = driver.MaxWeightCapacityKg > 0
            ? Math.Round((totalWeightKg / driver.MaxWeightCapacityKg) * 100.0, 1)
            : 0.0;

        var (centroidLat, centroidLon) = ComputeGeodesicCentroid(orderedStops.Select(s => (s.Latitude, s.Longitude)).ToList());

        return new DriverClusterRoute
        {
            ClusterId = Guid.NewGuid(),
            Driver = driver,
            StopCount = orderedStops.Count,
            TotalWeightKg = totalWeightKg,
            CapacityUtilizationPct = capUtilPct,
            WeightUtilizationPct = weightUtilPct,
            TotalCodAmount = totalCod,
            Currency = hub.Currency,
            TotalRouteDistanceKm = totalRouteDistanceKm,
            EstimatedStartTime = departureTime,
            EstimatedEndTime = finalReturnTime,
            TotalDurationMinutes = totalDurationMinutes,
            TotalLateMinutes = totalLateMinutes,
            TotalLatePenalty = Math.Round(totalLatePenalty, 2),
            Waypoints = waypoints,
            Centroid = new CentroidCoordinates { Latitude = centroidLat, Longitude = centroidLon }
        };
    }

    #endregion

    #region Manifest Dispatch & Runsheets

    public async Task<IReadOnlyList<DriverRunsheetManifest>> DispatchRunsheetsAsync(DispatchRunsheetsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        FleetOptimizationResult optResult;
        if (request.OptimizationResult != null)
        {
            optResult = request.OptimizationResult;
        }
        else
        {
            optResult = await OptimizeFleetDispatchAsync(new FleetOptimizationRequest { HubId = request.HubId }, ct);
        }

        var runsheetsCreated = new List<DriverRunsheetManifest>();
        var dateStamp = DateTime.UtcNow.ToString("yyyyMMdd");

        lock (_syncLock)
        {
            foreach (var route in optResult.Routes)
            {
                if (request.SpecificClusterIds != null &&
                    request.SpecificClusterIds.Count > 0 &&
                    !request.SpecificClusterIds.Contains(route.ClusterId))
                {
                    continue;
                }

                // Clean driver code for manifest: DRV-01 -> DRV01
                string cleanDriverCode = Regex.Replace(route.Driver.DriverCode, @"[^\w]", "");
                string baseCode = $"RUN-{dateStamp}-{cleanDriverCode}";
                string runsheetCode = baseCode;

                int seq = 1;
                while (_runsheetCodeIndex.ContainsKey(runsheetCode))
                {
                    runsheetCode = $"{baseCode}-{seq:D2}";
                    seq++;
                }

                var runsheetId = Guid.NewGuid();
                var runsheet = new DriverRunsheetManifest
                {
                    RunsheetId = runsheetId,
                    RunsheetCode = runsheetCode,
                    HubId = optResult.Hub.HubId,
                    HubName = optResult.Hub.Name,
                    DriverId = route.Driver.DriverId,
                    DriverCode = route.Driver.DriverCode,
                    DriverName = route.Driver.DriverName,
                    VehicleType = route.Driver.VehicleType,
                    DispatchedDateUtc = DateTime.UtcNow,
                    TotalStops = route.StopCount,
                    TotalDistanceKm = route.TotalRouteDistanceKm,
                    EstimatedDurationMinutes = route.TotalDurationMinutes,
                    DepartureTime = route.EstimatedStartTime,
                    ReturnTime = route.EstimatedEndTime,
                    TotalCodToCollect = route.TotalCodAmount,
                    Currency = route.Currency,
                    Status = "Dispatched",
                    ManifestStops = route.Waypoints
                };

                _runsheets[runsheetId] = runsheet;
                _runsheetCodeIndex[runsheetCode] = runsheetId;

                // Update stops status to Dispatched
                foreach (var wp in route.Waypoints)
                {
                    if (_stops.TryGetValue(wp.StopId, out var existingStop))
                    {
                        existingStop.Status = "Dispatched";
                    }
                }

                runsheetsCreated.Add(runsheet);
            }
        }

        return runsheetsCreated;
    }

    public Task<DriverRunsheetManifest?> GetRunsheetByIdAsync(Guid runsheetId, CancellationToken ct = default)
    {
        _runsheets.TryGetValue(runsheetId, out var manifest);
        return Task.FromResult(manifest);
    }

    public Task<DriverRunsheetManifest?> GetRunsheetByCodeAsync(string runsheetCode, CancellationToken ct = default)
    {
        if (_runsheetCodeIndex.TryGetValue(runsheetCode, out var id) && _runsheets.TryGetValue(id, out var manifest))
        {
            return Task.FromResult<DriverRunsheetManifest?>(manifest);
        }
        return Task.FromResult<DriverRunsheetManifest?>(null);
    }

    public Task<IReadOnlyList<DriverRunsheetManifest>> GetAllRunsheetsAsync(Guid? hubId = null, CancellationToken ct = default)
    {
        var query = _runsheets.Values.AsEnumerable();
        if (hubId.HasValue && hubId.Value != Guid.Empty)
        {
            query = query.Where(r => r.HubId == hubId.Value);
        }
        var list = query.OrderByDescending(r => r.DispatchedDateUtc).ToList();
        return Task.FromResult<IReadOnlyList<DriverRunsheetManifest>>(list);
    }

    #endregion
}

#endregion
