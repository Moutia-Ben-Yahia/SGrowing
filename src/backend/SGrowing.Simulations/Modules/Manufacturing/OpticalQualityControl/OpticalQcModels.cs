namespace SGrowing.Api.Modules.Manufacturing.OpticalQualityControl;

public enum PackagingDefectType
{
    None,
    SurfaceScratchOrDent,
    SealPunctureOrLeak,
    BarcodeMisprintOrUnreadable,
    LabelMisalignment,
    ColorDiscoloration
}

public enum QualityDisposition
{
    Pristine,
    AcceptableWithMinorVariance,
    DefectiveRejected
}

public sealed record GoldenReferenceMasterProfile(
    string SkuCode,
    string ProductName,
    IReadOnlyList<double> NominalFeatureMeans,
    IReadOnlyList<double> FeatureVariances,
    double MinAcceptableSsim = 0.92
);

public sealed record InspectedItemScan(
    string ScanId,
    string ItemId,
    string SkuCode,
    int ConveyorLaneNumber,
    double ConveyorSpeedMps,
    double DistanceToDiverterMeters,
    IReadOnlyList<double> ExtractedVisualFeatures,
    double MeasuredSsimScore,
    DateTime ScannedAtUtc
);

public sealed record DefectClassificationResult(
    string ScanId,
    string ItemId,
    double MahalanobisDistance,
    double MeasuredSsimScore,
    QualityDisposition Disposition,
    PackagingDefectType PrimaryDefect,
    bool TriggerPneumaticEjection,
    int PneumaticFireDelayMs,
    string InspectionAdvisory,
    DateTime InspectedAtUtc
);

public sealed record InspectItemRequest(
    InspectedItemScan Scan,
    GoldenReferenceMasterProfile MasterProfile
);
