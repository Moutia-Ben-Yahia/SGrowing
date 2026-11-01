using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Logistics.Esg;
using Xunit;

namespace SGrowing.Tests.Logistics;

/// <summary>
/// Comprehensive unit test suite for Phase 46:
/// Autonomous Energy Grid Integration, Carbon Offsetting &amp; Scope-3 ESG Ledger.
/// </summary>
public sealed class Scope3EsgCarbonLedgerTests
{
    private const string TestAuthorityKey = "ALGERIA_ESG_REGULATORY_SECRET_KEY_2026_!#%";
    private const string AltAuthorityKey = "WRONG_UNAUTHORIZED_KEY_999";

    private readonly EsgComplianceAuditLedger _auditLedger = new();
    private readonly CarbonOffsetTokenizationService _tokenizationService;
    private readonly Scope3CarbonEmissionsEngine _emissionsEngine;

    public Scope3EsgCarbonLedgerTests()
    {
        _tokenizationService = new CarbonOffsetTokenizationService(_auditLedger);
        _emissionsEngine = new Scope3CarbonEmissionsEngine(_auditLedger, _tokenizationService);
    }

    #region 1. Carbon Emission Formulas Across Transport Modes & Weights

    [Fact]
    public void CalculateFootprint_RoadDieselVan_CalculatesAccuratelyAsScope1()
    {
        // 2,500 kg (2.5 metric tons), 100 km @ 0.210 kg CO2e / (ton * km) = 52.5 kg CO2e
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-DZ-1001",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: 2500,
            DistanceKm: 100,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly,
            PackagingWeightKg: 0,
            ColdStorageDays: 0,
            ColdStorageVolumeM3: 0,
            IsDirectCompanyFleet: true);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope1Co2eKg.Should().Be(52.5);
        footprint.Scope2Co2eKg.Should().Be(0);
        footprint.Scope3Co2eKg.Should().Be(0);
        footprint.TotalCo2eKg.Should().Be(52.5);
        footprint.IsDirectCompanyFleet.Should().BeTrue();
    }

    [Fact]
    public void CalculateFootprint_RoadElectricVehicle_CalculatesLowerEmissions()
    {
        // 2,500 kg (2.5 tons), 100 km @ 0.045 kg CO2e / (ton * km) = 11.25 kg CO2e
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-DZ-1002",
            TenantId: "TEN-ORAN-01",
            ConsignmentWeightKg: 2500,
            DistanceKm: 100,
            Mode: TransportMode.RoadElectricVehicle,
            Packaging: PackagingType.CardboardRecycled,
            PackagingWeightKg: 0,
            ColdStorageDays: 0,
            ColdStorageVolumeM3: 0,
            IsDirectCompanyFleet: true);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope1Co2eKg.Should().Be(11.25);
        footprint.TotalCo2eKg.Should().Be(11.25);

        // EV emissions should be significantly lower than Diesel (11.25 vs 52.5) -> ~78.5% reduction
        var reductionPct = (52.5 - 11.25) / 52.5 * 100.0;
        reductionPct.Should().BeApproximately(78.57, 0.1);
    }

    [Fact]
    public void CalculateFootprint_AirFreight_CalculatesHighIntensityScope3Emissions()
    {
        // 1,500 kg (1.5 tons), 2,000 km @ 0.602 kg CO2e / (ton * km) = 1,806.0 kg CO2e
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-DZ-1003",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: 1500,
            DistanceKm: 2000,
            Mode: TransportMode.AirFreight,
            Packaging: PackagingType.StandardPoly,
            PackagingWeightKg: 0,
            ColdStorageDays: 0,
            ColdStorageVolumeM3: 0,
            IsDirectCompanyFleet: false);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope1Co2eKg.Should().Be(0);
        footprint.Scope2Co2eKg.Should().Be(0);
        footprint.Scope3Co2eKg.Should().Be(1806.0);
        footprint.TotalCo2eKg.Should().Be(1806.0);
    }

    [Fact]
    public void CalculateFootprint_MaritimeContainer_CalculatesLowIntensityEmissions()
    {
        // 20,000 kg (20 tons), 5,000 km @ 0.015 kg CO2e / (ton * km) = 1,500.0 kg CO2e
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-DZ-1004",
            TenantId: "TEN-ANNABA-01",
            ConsignmentWeightKg: 20000,
            DistanceKm: 5000,
            Mode: TransportMode.MaritimeContainer,
            Packaging: PackagingType.StandardPoly,
            PackagingWeightKg: 0,
            ColdStorageDays: 0,
            ColdStorageVolumeM3: 0,
            IsDirectCompanyFleet: false);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope1Co2eKg.Should().Be(0);
        footprint.Scope3Co2eKg.Should().Be(1500.0);
        footprint.TotalCo2eKg.Should().Be(1500.0);
    }

    [Fact]
    public void CalculateFootprint_RailFreight_CalculatesAccurately()
    {
        // 10,000 kg (10 tons), 800 km @ 0.028 kg CO2e / (ton * km) = 224.0 kg CO2e
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-DZ-1005",
            TenantId: "TEN-CONSTANTINE-01",
            ConsignmentWeightKg: 10000,
            DistanceKm: 800,
            Mode: TransportMode.RailFreight,
            Packaging: PackagingType.StandardPoly,
            PackagingWeightKg: 0,
            ColdStorageDays: 0,
            ColdStorageVolumeM3: 0,
            IsDirectCompanyFleet: false);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope1Co2eKg.Should().Be(0);
        footprint.Scope3Co2eKg.Should().Be(224.0);
        footprint.TotalCo2eKg.Should().Be(224.0);
    }

    [Fact]
    public void CalculateFootprint_ZeroDistance_YieldsZeroTransportEmissions()
    {
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-DZ-ZERO-DIST",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: 5000,
            DistanceKm: 0,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly,
            PackagingWeightKg: 0);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope1Co2eKg.Should().Be(0);
        footprint.TotalCo2eKg.Should().Be(0);
    }

    [Fact]
    public void CalculateFootprint_ZeroWeight_YieldsZeroTransportEmissions()
    {
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-DZ-ZERO-WT",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: 0,
            DistanceKm: 500,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly,
            PackagingWeightKg: 0);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope1Co2eKg.Should().Be(0);
        footprint.TotalCo2eKg.Should().Be(0);
    }

    [Fact]
    public void CalculateFootprint_NegativeWeight_ThrowsArgumentOutOfRangeException()
    {
        var act = () => _emissionsEngine.CalculateShipmentFootprint(new CalculateCarbonRequest(
            ShipmentId: "SHP-ERR-1",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: -100,
            DistanceKm: 100,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculateFootprint_NegativeDistance_ThrowsArgumentOutOfRangeException()
    {
        var act = () => _emissionsEngine.CalculateShipmentFootprint(new CalculateCarbonRequest(
            ShipmentId: "SHP-ERR-2",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: 100,
            DistanceKm: -50,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculateFootprint_NullOrEmptyShipmentId_ThrowsArgumentException()
    {
        var act = () => _emissionsEngine.CalculateShipmentFootprint(new CalculateCarbonRequest(
            ShipmentId: "",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: 100,
            DistanceKm: 50,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly));

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region 2. Packaging Emission Variances & Life-Cycle Analysis

    [Fact]
    public void CalculateFootprint_StandardPolyPackaging_Applies2_80KgFactor()
    {
        // 50 kg StandardPoly @ 2.80 kg CO2e / kg = 140.0 kg CO2e
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-PKG-POLY",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: 0,
            DistanceKm: 0,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly,
            PackagingWeightKg: 50);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope3Co2eKg.Should().Be(140.0);
        footprint.TotalCo2eKg.Should().Be(140.0);
    }

    [Fact]
    public void CalculateFootprint_CardboardRecycledPackaging_Applies0_95KgFactor()
    {
        // 50 kg CardboardRecycled @ 0.95 kg CO2e / kg = 47.5 kg CO2e
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-PKG-CARDBOARD",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: 0,
            DistanceKm: 0,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.CardboardRecycled,
            PackagingWeightKg: 50);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope3Co2eKg.Should().Be(47.5);
        footprint.TotalCo2eKg.Should().Be(47.5);
    }

    [Fact]
    public void CalculateFootprint_BiodegradablePolyPackaging_Applies0_35KgFactor()
    {
        // 50 kg BiodegradablePoly @ 0.35 kg CO2e / kg = 17.5 kg CO2e
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-PKG-BIO",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: 0,
            DistanceKm: 0,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.BiodegradablePoly,
            PackagingWeightKg: 50);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope3Co2eKg.Should().Be(17.5);
        footprint.TotalCo2eKg.Should().Be(17.5);
    }

    [Fact]
    public void CalculateFootprint_PackagingComparison_DemonstratesMaterialDecarbonization()
    {
        const double packagingKg = 100.0;

        var poly = _emissionsEngine.CalculateShipmentFootprint(new CalculateCarbonRequest(
            "SHP-C1", "TEN-A", 0, 0, TransportMode.RoadDieselVan, PackagingType.StandardPoly, packagingKg));

        var bio = _emissionsEngine.CalculateShipmentFootprint(new CalculateCarbonRequest(
            "SHP-C2", "TEN-A", 0, 0, TransportMode.RoadDieselVan, PackagingType.BiodegradablePoly, packagingKg));

        poly.Scope3Co2eKg.Should().Be(280.0);
        bio.Scope3Co2eKg.Should().Be(35.0);

        var savings = poly.Scope3Co2eKg - bio.Scope3Co2eKg;
        savings.Should().Be(245.0);
        (savings / poly.Scope3Co2eKg * 100.0).Should().Be(87.5); // 87.5% carbon reduction
    }

    [Fact]
    public void CalculateFootprint_ZeroPackagingWeight_YieldsZeroPackagingEmissions()
    {
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-PKG-ZERO",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: 1000,
            DistanceKm: 10,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly,
            PackagingWeightKg: 0);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope3Co2eKg.Should().Be(0);
    }

    [Fact]
    public void CalculateFootprint_NegativePackagingWeight_ThrowsArgumentOutOfRangeException()
    {
        var act = () => _emissionsEngine.CalculateShipmentFootprint(new CalculateCarbonRequest(
            ShipmentId: "SHP-PKG-NEG",
            TenantId: "TEN-ALGIERS-01",
            ConsignmentWeightKg: 100,
            DistanceKm: 10,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly,
            PackagingWeightKg: -5));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region 3. Cold Storage Refrigeration Emissions (Scope 2)

    [Fact]
    public void CalculateFootprint_ColdStorageRefrigeration_CalculatesAccurately()
    {
        // 10 days * 50 m^3 @ 0.082 kg CO2e / (day * m^3) = 41.0 kg CO2e (Scope 2)
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-COLD-01",
            TenantId: "TEN-SETIF-01",
            ConsignmentWeightKg: 0,
            DistanceKm: 0,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly,
            PackagingWeightKg: 0,
            ColdStorageDays: 10,
            ColdStorageVolumeM3: 50);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope1Co2eKg.Should().Be(0);
        footprint.Scope2Co2eKg.Should().Be(41.0);
        footprint.Scope3Co2eKg.Should().Be(0);
        footprint.TotalCo2eKg.Should().Be(41.0);
    }

    [Fact]
    public void CalculateFootprint_ZeroColdStorageDays_YieldsZeroScope2Emissions()
    {
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-COLD-ZERO",
            TenantId: "TEN-SETIF-01",
            ConsignmentWeightKg: 0,
            DistanceKm: 0,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly,
            PackagingWeightKg: 0,
            ColdStorageDays: 0,
            ColdStorageVolumeM3: 50);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope2Co2eKg.Should().Be(0);
        footprint.TotalCo2eKg.Should().Be(0);
    }

    [Fact]
    public void CalculateFootprint_NegativeColdStorageParameters_ThrowsArgumentOutOfRangeException()
    {
        var act1 = () => _emissionsEngine.CalculateShipmentFootprint(new CalculateCarbonRequest(
            ShipmentId: "SHP-COLD-ERR1",
            TenantId: "TEN-A",
            ConsignmentWeightKg: 100,
            DistanceKm: 10,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly,
            ColdStorageDays: -1,
            ColdStorageVolumeM3: 10));

        var act2 = () => _emissionsEngine.CalculateShipmentFootprint(new CalculateCarbonRequest(
            ShipmentId: "SHP-COLD-ERR2",
            TenantId: "TEN-A",
            ConsignmentWeightKg: 100,
            DistanceKm: 10,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.StandardPoly,
            ColdStorageDays: 5,
            ColdStorageVolumeM3: -10));

        act1.Should().Throw<ArgumentOutOfRangeException>();
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculateFootprint_CombinedLogisticsLifecycle_AggregatesAllThreeScopesCorrectly()
    {
        // 1. Direct Fleet Diesel: 2,000 kg (2.0 t) * 200 km * 0.210 = 84.0 kg CO2e (Scope 1)
        // 2. Cold Storage: 5 days * 20 m^3 * 0.082 = 8.2 kg CO2e (Scope 2)
        // 3. Packaging: 10 kg BiodegradablePoly * 0.35 = 3.5 kg CO2e (Scope 3)
        // Total = 84.0 + 8.2 + 3.5 = 95.7 kg CO2e
        var request = new CalculateCarbonRequest(
            ShipmentId: "SHP-LIFECYCLE-99",
            TenantId: "TEN-PHARMA-01",
            ConsignmentWeightKg: 2000,
            DistanceKm: 200,
            Mode: TransportMode.RoadDieselVan,
            Packaging: PackagingType.BiodegradablePoly,
            PackagingWeightKg: 10,
            ColdStorageDays: 5,
            ColdStorageVolumeM3: 20,
            IsDirectCompanyFleet: true);

        var footprint = _emissionsEngine.CalculateShipmentFootprint(request);

        footprint.Scope1Co2eKg.Should().Be(84.0);
        footprint.Scope2Co2eKg.Should().Be(8.2);
        footprint.Scope3Co2eKg.Should().Be(3.5);
        footprint.TotalCo2eKg.Should().Be(95.7);
    }

    #endregion

    #region 4. Carbon Offset Pricing, Tokenization & Permanent Retirement

    [Fact]
    public void RetireOffsetCredits_OneMetricTon_PricedAt3500Dzd()
    {
        // 1,000 kg CO2e = 1 Metric Ton = 3,500.00 DZD
        var cert = _tokenizationService.RetireOffsetCredits(
            tenantId: "TEN-SOLAR-01",
            kgToOffset: 1000.0,
            projectType: OffsetProjectType.AlgerianHighPlateauSolar,
            secretKey: TestAuthorityKey);

        cert.Should().NotBeNull();
        cert.TenantId.Should().Be("TEN-SOLAR-01");
        cert.ProjectType.Should().Be(OffsetProjectType.AlgerianHighPlateauSolar);
        cert.TotalKgOffset.Should().Be(1000.0);
        cert.CostDzd.Should().Be(3500.00m);
        cert.CertificateId.Should().StartWith("CERT-TEN-SOLAR-01-");
        cert.RetirementTransactionId.Should().StartWith("TX-RET-");
        cert.CryptographicProofHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RetireOffsetCredits_FractionalTon_PricedProportionallyInDzd()
    {
        // 250 kg = 0.25 MT -> 0.25 * 3,500 = 875.00 DZD
        var cert = _tokenizationService.RetireOffsetCredits(
            tenantId: "TEN-SOLAR-02",
            kgToOffset: 250.0,
            projectType: OffsetProjectType.SaharanAfforestation,
            secretKey: TestAuthorityKey);

        cert.TotalKgOffset.Should().Be(250.0);
        cert.CostDzd.Should().Be(875.00m);
    }

    [Fact]
    public void RetireOffsetCredits_LargeVolume_PricedAccurately()
    {
        // 15,450 kg = 15.45 MT -> 15.45 * 3,500 = 54,075.00 DZD
        var cert = _tokenizationService.RetireOffsetCredits(
            tenantId: "TEN-SOLAR-03",
            kgToOffset: 15450.0,
            projectType: OffsetProjectType.MediterraneanReforestation,
            secretKey: TestAuthorityKey);

        cert.TotalKgOffset.Should().Be(15450.0);
        cert.CostDzd.Should().Be(54075.00m);
    }

    [Fact]
    public void RetireOffsetCredits_GeneratesCryptographicHmacProof()
    {
        var cert = _tokenizationService.RetireOffsetCredits(
            tenantId: "TEN-HMAC-TEST",
            kgToOffset: 500.0,
            projectType: OffsetProjectType.GoldStandardWind,
            secretKey: TestAuthorityKey);

        // Recompute expected HMAC
        var expectedPayload = $"{cert.CertificateId}|{cert.TenantId}|{cert.ProjectType}|{cert.TotalKgOffset:F4}|{cert.CostDzd:F2}|{cert.RetirementTransactionId}|{cert.IssuedAtUtc:O}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestAuthorityKey));
        var expectedHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(expectedPayload))).ToLowerInvariant();

        cert.CryptographicProofHash.Should().Be(expectedHash);
    }

    [Fact]
    public void RetireOffsetCredits_ZeroOrNegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        var actZero = () => _tokenizationService.RetireOffsetCredits(
            tenantId: "TEN-A",
            kgToOffset: 0,
            projectType: OffsetProjectType.AlgerianHighPlateauSolar,
            secretKey: TestAuthorityKey);

        var actNegative = () => _tokenizationService.RetireOffsetCredits(
            tenantId: "TEN-A",
            kgToOffset: -500,
            projectType: OffsetProjectType.AlgerianHighPlateauSolar,
            secretKey: TestAuthorityKey);

        actZero.Should().Throw<ArgumentOutOfRangeException>();
        actNegative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RetireOffsetCredits_EmptyTenantOrSecretKey_ThrowsArgumentException()
    {
        var act1 = () => _tokenizationService.RetireOffsetCredits("", 100, OffsetProjectType.AlgerianHighPlateauSolar, TestAuthorityKey);
        var act2 = () => _tokenizationService.RetireOffsetCredits("TEN-A", 100, OffsetProjectType.AlgerianHighPlateauSolar, "");

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RetireOffsetCredits_EnforcesUniqueRetirementTransactionIds()
    {
        var cert1 = _tokenizationService.RetireOffsetCredits("TEN-UNIQ", 100, OffsetProjectType.AlgerianHighPlateauSolar, TestAuthorityKey);
        var cert2 = _tokenizationService.RetireOffsetCredits("TEN-UNIQ", 100, OffsetProjectType.AlgerianHighPlateauSolar, TestAuthorityKey);

        cert1.CertificateId.Should().NotBe(cert2.CertificateId);
        cert1.RetirementTransactionId.Should().NotBe(cert2.RetirementTransactionId);
        cert1.CryptographicProofHash.Should().NotBe(cert2.CryptographicProofHash);
    }

    [Fact]
    public void GetCertificate_ExistingCertificate_ReturnsCertificateDetails()
    {
        var created = _tokenizationService.RetireOffsetCredits("TEN-GET", 750, OffsetProjectType.SaharanAfforestation, TestAuthorityKey);

        var fetched = _tokenizationService.GetCertificate(created.CertificateId);

        fetched.Should().NotBeNull();
        fetched!.CertificateId.Should().Be(created.CertificateId);
        fetched.TotalKgOffset.Should().Be(750);
        fetched.CostDzd.Should().Be(2625.00m);
    }

    [Fact]
    public void GetCertificate_NonExistent_ReturnsNull()
    {
        var result = _tokenizationService.GetCertificate("NON-EXISTENT-CERT-ID-1234");
        result.Should().BeNull();
    }

    [Fact]
    public void GetCertificatesForTenant_ReturnsAllRetiredCertificates()
    {
        const string tenant = "TEN-MULTI-CERT";
        _tokenizationService.RetireOffsetCredits(tenant, 200, OffsetProjectType.AlgerianHighPlateauSolar, TestAuthorityKey);
        _tokenizationService.RetireOffsetCredits(tenant, 400, OffsetProjectType.SaharanAfforestation, TestAuthorityKey);
        _tokenizationService.RetireOffsetCredits(tenant, 600, OffsetProjectType.GoldStandardWind, TestAuthorityKey);

        var certs = _tokenizationService.GetCertificatesForTenant(tenant);

        certs.Should().HaveCount(3);
        certs.Sum(c => c.TotalKgOffset).Should().Be(1200);
    }

    [Fact]
    public void GetTotalOffsetKg_SumsAllRetiredCertificatesAccurately()
    {
        const string tenant = "TEN-SUM-TEST";
        _tokenizationService.RetireOffsetCredits(tenant, 500.5, OffsetProjectType.AlgerianHighPlateauSolar, TestAuthorityKey);
        _tokenizationService.RetireOffsetCredits(tenant, 1200.25, OffsetProjectType.MediterraneanReforestation, TestAuthorityKey);

        var total = _tokenizationService.GetTotalOffsetKg(tenant);

        total.Should().Be(1700.75);
    }

    #endregion

    #region 5. Merkle Audit Log Chaining, Balance Sheet & Tamper Detection

    [Fact]
    public void RecordEsgEvent_ChainsPreviousHashToCurrentHashAccurately()
    {
        const string tenant = "TEN-CHAIN-01";

        var evt0 = _auditLedger.RecordEsgEvent(tenant, "EMISSION_0", 100.0, "META-0");
        var evt1 = _auditLedger.RecordEsgEvent(tenant, "EMISSION_1", 200.0, "META-1");
        var evt2 = _auditLedger.RecordEsgEvent(tenant, "OFFSET_2", -150.0, "META-2");

        evt0.SequenceIndex.Should().Be(0);
        evt0.PreviousHash.Should().Be("0000000000000000000000000000000000000000000000000000000000000000");

        evt1.SequenceIndex.Should().Be(1);
        evt1.PreviousHash.Should().Be(evt0.CurrentHash);

        evt2.SequenceIndex.Should().Be(2);
        evt2.PreviousHash.Should().Be(evt1.CurrentHash);

        var trail = _auditLedger.GetAuditTrail(tenant);
        trail.Should().HaveCount(3);
    }

    [Fact]
    public void VerifyLedgerIntegrity_UnmodifiedLedger_ReturnsTrue()
    {
        const string tenant = "TEN-INTACT-01";
        _auditLedger.RecordEsgEvent(tenant, "EMISSION_A", 50.0, "A");
        _auditLedger.RecordEsgEvent(tenant, "EMISSION_B", 75.0, "B");
        _auditLedger.RecordEsgEvent(tenant, "OFFSET_C", -100.0, "C");

        var isValid = _auditLedger.VerifyLedgerIntegrity(tenant);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void SimulateTamper_ModifyingEventPayload_TriggersIntegrityFailure()
    {
        const string tenant = "TEN-TAMPER-01";
        _auditLedger.RecordEsgEvent(tenant, "SCOPE1_EMISSION", 100.0, "HASH1");
        _auditLedger.RecordEsgEvent(tenant, "SCOPE2_EMISSION", 200.0, "HASH2");
        _auditLedger.RecordEsgEvent(tenant, "SCOPE3_EMISSION", 300.0, "HASH3");

        _auditLedger.VerifyLedgerIntegrity(tenant).Should().BeTrue();

        // Maliciously tamper with event at sequence 1 (change 200 kg to 50 kg)
        _auditLedger.SimulateTamper(tenant, 1, 50.0);

        // Verification must detect the tampering
        _auditLedger.VerifyLedgerIntegrity(tenant).Should().BeFalse();
    }

    [Fact]
    public void VerifyAuditCertificate_ValidCertificateAndSecret_ReturnsVerifiedAudited()
    {
        const string tenant = "TEN-CERT-VERIFY";
        var cert = _tokenizationService.RetireOffsetCredits(
            tenantId: tenant,
            kgToOffset: 1500.0,
            projectType: OffsetProjectType.AlgerianHighPlateauSolar,
            secretKey: TestAuthorityKey);

        var status = _auditLedger.VerifyAuditCertificate(cert.CertificateId, TestAuthorityKey);

        status.Should().Be(AuditCertificationStatus.VerifiedAudited);
    }

    [Fact]
    public void VerifyAuditCertificate_InvalidSecretKey_ReturnsTamperDetected()
    {
        const string tenant = "TEN-CERT-INVALID-KEY";
        var cert = _tokenizationService.RetireOffsetCredits(
            tenantId: tenant,
            kgToOffset: 1500.0,
            projectType: OffsetProjectType.SaharanAfforestation,
            secretKey: TestAuthorityKey);

        var status = _auditLedger.VerifyAuditCertificate(cert.CertificateId, AltAuthorityKey);

        status.Should().Be(AuditCertificationStatus.TamperDetected);
    }

    [Fact]
    public void VerifyAuditCertificate_TamperedLedgerEvent_ReturnsTamperDetected()
    {
        const string tenant = "TEN-CERT-TAMPERED-CHAIN";
        var cert = _tokenizationService.RetireOffsetCredits(
            tenantId: tenant,
            kgToOffset: 1000.0,
            projectType: OffsetProjectType.GoldStandardWind,
            secretKey: TestAuthorityKey);

        _auditLedger.VerifyAuditCertificate(cert.CertificateId, TestAuthorityKey)
            .Should().Be(AuditCertificationStatus.VerifiedAudited);

        // Tamper with ledger
        _auditLedger.SimulateTamper(tenant, 0, -500.0);

        // Certificate verification must now fail with TamperDetected
        _auditLedger.VerifyAuditCertificate(cert.CertificateId, TestAuthorityKey)
            .Should().Be(AuditCertificationStatus.TamperDetected);
    }

    [Fact]
    public void VerifyAuditCertificate_UnknownCertificateId_ReturnsTamperDetected()
    {
        var status = _auditLedger.VerifyAuditCertificate("UNKNOWN-CERT-ID-XYZ", TestAuthorityKey);
        status.Should().Be(AuditCertificationStatus.TamperDetected);
    }

    [Fact]
    public void GenerateAuditBalanceSheet_EmptyLedger_ReturnsNeutralWithEmptyMerkleRoot()
    {
        var sheet = _auditLedger.GenerateAuditBalanceSheet("TEN-EMPTY-01", 2026, TestAuthorityKey);

        sheet.TenantId.Should().Be("TEN-EMPTY-01");
        sheet.ReportingYear.Should().Be(2026);
        sheet.TotalGrossCo2eKg.Should().Be(0);
        sheet.TotalOffsetsKg.Should().Be(0);
        sheet.NetEmissionsCo2eKg.Should().Be(0);
        sheet.CarbonNeutralityAchieved.Should().BeTrue();
        sheet.MerkleAuditRoot.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateAuditBalanceSheet_EmissionsExceedOffsets_CarbonNeutralityIsFalse()
    {
        const string tenant = "TEN-DEFICIT-01";
        const int year = 2026;

        // Record 5,000 kg gross emissions
        _auditLedger.RecordEsgEvent(tenant, "SCOPE1_EMISSION", 2000.0, "S1");
        _auditLedger.RecordEsgEvent(tenant, "SCOPE2_EMISSION", 1500.0, "S2");
        _auditLedger.RecordEsgEvent(tenant, "SCOPE3_EMISSION", 1500.0, "S3");

        // Retire only 2,000 kg offsets
        _tokenizationService.RetireOffsetCredits(tenant, 2000.0, OffsetProjectType.AlgerianHighPlateauSolar, TestAuthorityKey);

        var sheet = _auditLedger.GenerateAuditBalanceSheet(tenant, year, TestAuthorityKey);

        sheet.TotalScope1Kg.Should().Be(2000.0);
        sheet.TotalScope2Kg.Should().Be(1500.0);
        sheet.TotalScope3Kg.Should().Be(1500.0);
        sheet.TotalGrossCo2eKg.Should().Be(5000.0);
        sheet.TotalOffsetsKg.Should().Be(2000.0);
        sheet.NetEmissionsCo2eKg.Should().Be(3000.0);
        sheet.CarbonNeutralityAchieved.Should().BeFalse();
        sheet.MerkleAuditRoot.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateAuditBalanceSheet_OffsetsExceedEmissions_CarbonNeutralityIsTrue()
    {
        const string tenant = "TEN-NEUTRAL-01";
        const int year = 2026;

        // Record 2,500 kg gross emissions
        _auditLedger.RecordEsgEvent(tenant, "SCOPE1_EMISSION", 1000.0, "S1");
        _auditLedger.RecordEsgEvent(tenant, "SCOPE2_EMISSION", 500.0, "S2");
        _auditLedger.RecordEsgEvent(tenant, "SCOPE3_EMISSION", 1000.0, "S3");

        // Retire 3,000 kg offsets (over-offsetting)
        _tokenizationService.RetireOffsetCredits(tenant, 3000.0, OffsetProjectType.MediterraneanReforestation, TestAuthorityKey);

        var sheet = _auditLedger.GenerateAuditBalanceSheet(tenant, year, TestAuthorityKey);

        sheet.TotalGrossCo2eKg.Should().Be(2500.0);
        sheet.TotalOffsetsKg.Should().Be(3000.0);
        sheet.NetEmissionsCo2eKg.Should().Be(-500.0);
        sheet.CarbonNeutralityAchieved.Should().BeTrue();
    }

    [Fact]
    public void GenerateAuditBalanceSheet_MultiYearSeparation_FiltersByRequestedYear()
    {
        const string tenant = "TEN-MULTI-YEAR";
        var currentYear = DateTime.UtcNow.Year;

        _auditLedger.RecordEsgEvent(tenant, "SCOPE1_EMISSION", 400.0, "HIST");

        var sheetCurrent = _auditLedger.GenerateAuditBalanceSheet(tenant, currentYear, TestAuthorityKey);
        var sheetPast = _auditLedger.GenerateAuditBalanceSheet(tenant, currentYear - 5, TestAuthorityKey);

        sheetCurrent.TotalGrossCo2eKg.Should().Be(400.0);
        sheetPast.TotalGrossCo2eKg.Should().Be(0.0);
    }

    #endregion

    #region 6. Concurrency & Thread-Safety

    [Fact]
    public void CalculateShipmentFootprint_ConcurrentCalculations_ThreadSafeAndAccurate()
    {
        const string tenant = "TEN-PARALLEL-CALC";
        const int taskCount = 100;

        Parallel.For(0, taskCount, i =>
        {
            var req = new CalculateCarbonRequest(
                ShipmentId: $"SHP-CONCUR-{i}",
                TenantId: tenant,
                ConsignmentWeightKg: 1000,
                DistanceKm: 100,
                Mode: TransportMode.RoadDieselVan,
                Packaging: PackagingType.StandardPoly,
                PackagingWeightKg: 5,
                IsDirectCompanyFleet: true);

            var res = _emissionsEngine.CalculateShipmentFootprint(req);
            res.TotalCo2eKg.Should().BeGreaterThan(0);
        });

        var recorded = _emissionsEngine.GetShipmentsForTenant(tenant);
        recorded.Should().HaveCount(taskCount);
    }

    [Fact]
    public void RetireOffsetCredits_ConcurrentRetirements_MaintainsExactTotalOffsetsWithoutDoubleCounting()
    {
        const string tenant = "TEN-PARALLEL-OFFSET";
        const int taskCount = 50;
        const double kgPerTask = 200.0;

        Parallel.For(0, taskCount, i =>
        {
            var cert = _tokenizationService.RetireOffsetCredits(
                tenantId: tenant,
                kgToOffset: kgPerTask,
                projectType: OffsetProjectType.AlgerianHighPlateauSolar,
                secretKey: TestAuthorityKey);

            cert.Should().NotBeNull();
            cert.TotalKgOffset.Should().Be(kgPerTask);
        });

        var total = _tokenizationService.GetTotalOffsetKg(tenant);
        total.Should().Be(taskCount * kgPerTask); // 50 * 200 = 10,000 kg

        var certs = _tokenizationService.GetCertificatesForTenant(tenant);
        certs.Should().HaveCount(taskCount);
    }

    #endregion

    #region 7. Dependency Injection Registration (Zero BuildServiceProvider)

    [Fact]
    public void AddEsgServices_RegistersAllServicesAsSingletonsWithoutCallingBuildServiceProvider()
    {
        // Enforce boundary requirement: Assert on ServiceCollection descriptors without calling BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddEsgServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IEsgComplianceAuditLedger) &&
            d.ImplementationType == typeof(EsgComplianceAuditLedger) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ICarbonOffsetTokenizationService) &&
            d.ImplementationType == typeof(CarbonOffsetTokenizationService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IScope3CarbonEmissionsEngine) &&
            d.ImplementationType == typeof(Scope3CarbonEmissionsEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
