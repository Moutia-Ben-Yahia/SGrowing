using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Manufacturing.OpticalQualityControl;
using Xunit;

namespace SGrowing.Tests.Manufacturing;

public sealed class OpticalQcTests
{
    private readonly VisualDefectAnomalyClassifierEngine _engine = new();

    private static GoldenReferenceMasterProfile CreateMasterProfile()
    {
        return new GoldenReferenceMasterProfile(
            SkuCode: "SKU-PHARMA-BOX-01",
            ProductName: "Insulin Vial Packaging Box",
            NominalFeatureMeans: [100.0, 50.0, 80.0, 120.0, 10.0, 200.0],
            FeatureVariances: [4.0, 2.0, 5.0, 6.0, 1.0, 8.0], // Variance = sigma^2
            MinAcceptableSsim: 0.95
        );
    }

    [Fact]
    public void InspectItem_PristineFeatures_ReturnsPristineDispositionAndNoEjection()
    {
        // Arrange: Scanned item with tiny perturbations well within 1 standard deviation
        var scan = new InspectedItemScan(
            ScanId: "SCAN-001",
            ItemId: "ITEM-BOX-101",
            SkuCode: "SKU-PHARMA-BOX-01",
            ConveyorLaneNumber: 1,
            ConveyorSpeedMps: 1.5,
            DistanceToDiverterMeters: 1.2,
            ExtractedVisualFeatures: [100.5, 50.2, 79.8, 120.3, 10.1, 200.4],
            MeasuredSsimScore: 0.985,
            ScannedAtUtc: DateTime.UtcNow
        );

        var request = new InspectItemRequest(scan, CreateMasterProfile());

        // Act
        var result = _engine.InspectAndClassifyItem(request);

        // Assert
        result.Disposition.Should().Be(QualityDisposition.Pristine);
        result.PrimaryDefect.Should().Be(PackagingDefectType.None);
        result.MahalanobisDistance.Should().BeLessThan(2.0);
        result.TriggerPneumaticEjection.Should().BeFalse();
        result.PneumaticFireDelayMs.Should().Be(0);
        result.InspectionAdvisory.Should().Contain("QUALITY PASS");
    }

    [Fact]
    public void InspectItem_SeverelyDeviatedSeal_ClassifiesSealPunctureAndCalculatesPneumaticFireDelay()
    {
        // Arrange: Feature index 2 (Seal Gradient) severely broken (e.g. 15 standard deviations away)
        var scan = new InspectedItemScan(
            ScanId: "SCAN-DEFECT-02",
            ItemId: "ITEM-PUNCTURE-99",
            SkuCode: "SKU-PHARMA-BOX-01",
            ConveyorLaneNumber: 2,
            ConveyorSpeedMps: 1.5, // 1.5 m/s
            DistanceToDiverterMeters: 1.2, // 1.2m distance -> delay should be 1.2 / 1.5 = 800ms
            ExtractedVisualFeatures: [100.0, 50.0, 25.0, 120.0, 10.0, 200.0], // Feature 2 delta = -55 -> ratio = 55^2 / 5 = 605
            MeasuredSsimScore: 0.720, // Low SSIM
            ScannedAtUtc: DateTime.UtcNow
        );

        var request = new InspectItemRequest(scan, CreateMasterProfile());

        // Act
        var result = _engine.InspectAndClassifyItem(request);

        // Assert
        result.Disposition.Should().Be(QualityDisposition.DefectiveRejected);
        result.PrimaryDefect.Should().Be(PackagingDefectType.SealPunctureOrLeak);
        result.MahalanobisDistance.Should().BeGreaterThan(10.0);
        result.TriggerPneumaticEjection.Should().BeTrue();
        result.PneumaticFireDelayMs.Should().Be(800);
        result.InspectionAdvisory.Should().Contain("DEFECT REJECTED");
    }

    [Fact]
    public void InspectItem_MinorDeviation_PassesWithWarning()
    {
        // Arrange: Mahalanobis distance between 2.5 and 4.0 with acceptable SSIM (0.90)
        var scan = new InspectedItemScan(
            ScanId: "SCAN-WARN-03",
            ItemId: "ITEM-MINOR-03",
            SkuCode: "SKU-PHARMA-BOX-01",
            ConveyorLaneNumber: 1,
            ConveyorSpeedMps: 1.2,
            DistanceToDiverterMeters: 1.0,
            ExtractedVisualFeatures: [104.0, 52.0, 83.0, 122.0, 11.0, 204.0],
            MeasuredSsimScore: 0.90,
            ScannedAtUtc: DateTime.UtcNow
        );

        var request = new InspectItemRequest(scan, CreateMasterProfile());

        // Act
        var result = _engine.InspectAndClassifyItem(request);

        // Assert
        result.Disposition.Should().Be(QualityDisposition.AcceptableWithMinorVariance);
        result.TriggerPneumaticEjection.Should().BeFalse();
        result.InspectionAdvisory.Should().Contain("MINOR VARIANCE");
    }

    [Fact]
    public void AddOpticalQualityControlServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOpticalQualityControlServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(VisualDefectAnomalyClassifierEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
