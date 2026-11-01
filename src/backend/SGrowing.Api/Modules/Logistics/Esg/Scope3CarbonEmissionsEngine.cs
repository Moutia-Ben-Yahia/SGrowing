using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Logistics.Esg;

/// <summary>
/// Thread-safe carbon footprint emissions calculator adhering to the GHG Protocol Corporate Standard.
/// </summary>
public sealed class Scope3CarbonEmissionsEngine(
    IEsgComplianceAuditLedger? auditLedger = null,
    ICarbonOffsetTokenizationService? offsetService = null) : IScope3CarbonEmissionsEngine
{
    // Modal emission factors in kg CO2e / (ton * km)
    public const double DieselVanFactor = 0.210;
    public const double ElectricVehicleFactor = 0.045;
    public const double AirFreightFactor = 0.602;
    public const double MaritimeContainerFactor = 0.015;
    public const double RailFreightFactor = 0.028;

    // Packaging lifecycle emission factors in kg CO2e / kg
    public const double StandardPolyFactor = 2.80;
    public const double CardboardRecycledFactor = 0.95;
    public const double BiodegradablePolyFactor = 0.35;

    // Cold storage warehouse refrigeration factor in kg CO2e / (day * m^3)
    public const double ColdStorageFactor = 0.082;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<ShipmentCarbonFootprint>> _tenantShipments = [];

    /// <inheritdoc />
    public ShipmentCarbonFootprint CalculateShipmentFootprint(CalculateCarbonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ShipmentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantId);

        if (request.ConsignmentWeightKg < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Consignment weight cannot be negative.");
        }

        if (request.DistanceKm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Distance cannot be negative.");
        }

        if (request.PackagingWeightKg < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Packaging weight cannot be negative.");
        }

        if (request.ColdStorageDays < 0 || request.ColdStorageVolumeM3 < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Cold storage parameters cannot be negative.");
        }

        // 1. Freight transport emissions
        var tonnage = request.ConsignmentWeightKg / 1000.0;
        var modalFactor = GetTransportFactor(request.Mode);
        var transportCo2eKg = tonnage * request.DistanceKm * modalFactor;

        // 2. Packaging life-cycle emissions
        var packagingFactor = GetPackagingFactor(request.Packaging);
        var packagingCo2eKg = request.PackagingWeightKg * packagingFactor;

        // 3. Cold storage refrigeration emissions (Scope 2 purchased electricity)
        var coldStorageCo2eKg = request.ColdStorageDays * request.ColdStorageVolumeM3 * ColdStorageFactor;

        // 4. Scope assignment: Direct company fleet vs third-party freight
        var isDirectFleet = request.IsDirectCompanyFleet ?? (request.Mode is TransportMode.RoadDieselVan or TransportMode.RoadElectricVehicle);

        double scope1;
        double scope3;

        if (isDirectFleet)
        {
            scope1 = transportCo2eKg;
            scope3 = packagingCo2eKg;
        }
        else
        {
            scope1 = 0;
            scope3 = transportCo2eKg + packagingCo2eKg;
        }

        var scope2 = coldStorageCo2eKg;

        scope1 = Math.Round(scope1, 4);
        scope2 = Math.Round(scope2, 4);
        scope3 = Math.Round(scope3, 4);
        var total = Math.Round(scope1 + scope2 + scope3, 4);
        var timestampUtc = DateTime.UtcNow;

        var footprint = new ShipmentCarbonFootprint(
            ShipmentId: request.ShipmentId,
            ConsignmentWeightKg: request.ConsignmentWeightKg,
            DistanceKm: request.DistanceKm,
            Mode: request.Mode,
            Packaging: request.Packaging,
            Scope1Co2eKg: scope1,
            Scope2Co2eKg: scope2,
            Scope3Co2eKg: scope3,
            TotalCo2eKg: total,
            CalculationTimestampUtc: timestampUtc,
            TenantId: request.TenantId,
            PackagingWeightKg: request.PackagingWeightKg,
            ColdStorageDays: request.ColdStorageDays,
            ColdStorageVolumeM3: request.ColdStorageVolumeM3,
            IsDirectCompanyFleet: isDirectFleet);

        lock (_lock)
        {
            if (!_tenantShipments.TryGetValue(request.TenantId, out var list))
            {
                list = [];
                _tenantShipments[request.TenantId] = list;
            }
            list.Add(footprint);
        }

        // 5. Optionally record in immutable audit ledger
        if (auditLedger != null)
        {
            if (scope1 > 0)
            {
                auditLedger.RecordEsgEvent(request.TenantId, "SHIPMENT_SCOPE1", scope1, request.ShipmentId);
            }
            if (scope2 > 0)
            {
                auditLedger.RecordEsgEvent(request.TenantId, "COLD_STORAGE_SCOPE2", scope2, request.ShipmentId);
            }
            if (scope3 > 0)
            {
                auditLedger.RecordEsgEvent(request.TenantId, "FREIGHT_SCOPE3", scope3, request.ShipmentId);
            }
        }

        return footprint;
    }

    /// <inheritdoc />
    public EsgBalanceSheet GetAggregatedEmissions(string tenantId, int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (auditLedger != null)
        {
            return auditLedger.GenerateAuditBalanceSheet(tenantId, year, "DEFAULT_AUTHORITY_KEY");
        }

        lock (_lock)
        {
            if (!_tenantShipments.TryGetValue(tenantId, out var list))
            {
                return new EsgBalanceSheet(
                    TenantId: tenantId,
                    ReportingYear: year,
                    TotalScope1Kg: 0,
                    TotalScope2Kg: 0,
                    TotalScope3Kg: 0,
                    TotalGrossCo2eKg: 0,
                    TotalOffsetsKg: 0,
                    NetEmissionsCo2eKg: 0,
                    CarbonNeutralityAchieved: true,
                    MerkleAuditRoot: ComputeEmptyHash());
            }

            var yearShipments = list.Where(s => s.CalculationTimestampUtc.Year == year).ToList();

            var totalScope1 = Math.Round(yearShipments.Sum(s => s.Scope1Co2eKg), 4);
            var totalScope2 = Math.Round(yearShipments.Sum(s => s.Scope2Co2eKg), 4);
            var totalScope3 = Math.Round(yearShipments.Sum(s => s.Scope3Co2eKg), 4);
            var totalGross = Math.Round(totalScope1 + totalScope2 + totalScope3, 4);

            var offsets = offsetService?.GetTotalOffsetKg(tenantId) ?? 0;
            offsets = Math.Round(offsets, 4);
            var netEmissions = Math.Round(totalGross - offsets, 4);
            var neutralityAchieved = netEmissions <= 0.0001;

            return new EsgBalanceSheet(
                TenantId: tenantId,
                ReportingYear: year,
                TotalScope1Kg: totalScope1,
                TotalScope2Kg: totalScope2,
                TotalScope3Kg: totalScope3,
                TotalGrossCo2eKg: totalGross,
                TotalOffsetsKg: offsets,
                NetEmissionsCo2eKg: netEmissions,
                CarbonNeutralityAchieved: neutralityAchieved,
                MerkleAuditRoot: ComputeEmptyHash());
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ShipmentCarbonFootprint> GetShipmentsForTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        lock (_lock)
        {
            if (_tenantShipments.TryGetValue(tenantId, out var list))
            {
                return [.. list];
            }

            return [];
        }
    }

    public static double GetTransportFactor(TransportMode mode) => mode switch
    {
        TransportMode.RoadDieselVan => DieselVanFactor,
        TransportMode.RoadElectricVehicle => ElectricVehicleFactor,
        TransportMode.AirFreight => AirFreightFactor,
        TransportMode.MaritimeContainer => MaritimeContainerFactor,
        TransportMode.RailFreight => RailFreightFactor,
        _ => DieselVanFactor
    };

    public static double GetPackagingFactor(PackagingType packaging) => packaging switch
    {
        PackagingType.StandardPoly => StandardPolyFactor,
        PackagingType.CardboardRecycled => CardboardRecycledFactor,
        PackagingType.BiodegradablePoly => BiodegradablePolyFactor,
        _ => StandardPolyFactor
    };

    private static string ComputeEmptyHash()
    {
        return Convert.ToHexString(SHA256.HashData(ReadOnlySpan<byte>.Empty)).ToLowerInvariant();
    }
}
