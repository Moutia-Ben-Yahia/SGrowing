namespace SGrowing.Api.Modules.Manufacturing.OpticalQualityControl;

/// <summary>
/// Autonomous optical quality control and visual defect classification engine.
/// Evaluates Mahalanobis feature distance and SSIM metrics at line speeds >120 items/min.
/// </summary>
public sealed class VisualDefectAnomalyClassifierEngine
{
    private const double PristineMahalanobisThreshold = 2.5;
    private const double RejectionMahalanobisThreshold = 4.0;

    public DefectClassificationResult InspectAndClassifyItem(InspectItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scan = request.Scan;
        var master = request.MasterProfile;

        if (scan.ExtractedVisualFeatures.Count != master.NominalFeatureMeans.Count)
        {
            throw new ArgumentException("Feature dimension mismatch between scanned item and golden reference master.");
        }

        // 1. Calculate Mahalanobis Distance against Golden Master
        var sumSquares = 0.0;
        var maxVarianceIndex = 0;
        var maxVarianceRatio = 0.0;

        for (var i = 0; i < scan.ExtractedVisualFeatures.Count; i++)
        {
            var delta = scan.ExtractedVisualFeatures[i] - master.NominalFeatureMeans[i];
            var variance = Math.Max(0.0001, master.FeatureVariances[i]);
            var ratio = (delta * delta) / variance;

            sumSquares += ratio;

            if (ratio > maxVarianceRatio)
            {
                maxVarianceRatio = ratio;
                maxVarianceIndex = i;
            }
        }

        var mahalanobisDist = Math.Sqrt(sumSquares);

        // 2. Evaluate Disposition & Identify Defect
        QualityDisposition disposition;
        PackagingDefectType defect;
        bool triggerEjection;
        string advisory;

        if (mahalanobisDist <= PristineMahalanobisThreshold && scan.MeasuredSsimScore >= master.MinAcceptableSsim)
        {
            disposition = QualityDisposition.Pristine;
            defect = PackagingDefectType.None;
            triggerEjection = false;
            advisory = $"QUALITY PASS: Flawless item. Mahalanobis: {mahalanobisDist:F2}, SSIM: {scan.MeasuredSsimScore:F3}.";
        }
        else if (mahalanobisDist <= RejectionMahalanobisThreshold && scan.MeasuredSsimScore >= 0.85)
        {
            disposition = QualityDisposition.AcceptableWithMinorVariance;
            defect = PackagingDefectType.None;
            triggerEjection = false;
            advisory = $"QUALITY PASS (MINOR VARIANCE): Mahalanobis {mahalanobisDist:F2} within tolerance. SSIM: {scan.MeasuredSsimScore:F3}.";
        }
        else
        {
            disposition = QualityDisposition.DefectiveRejected;
            triggerEjection = true;

            // Map feature index to root defect
            defect = maxVarianceIndex switch
            {
                0 or 1 => PackagingDefectType.SurfaceScratchOrDent,
                2 => PackagingDefectType.SealPunctureOrLeak,
                3 => PackagingDefectType.BarcodeMisprintOrUnreadable,
                4 => PackagingDefectType.LabelMisalignment,
                _ => PackagingDefectType.ColorDiscoloration
            };

            advisory = $"DEFECT REJECTED: {defect}. Mahalanobis: {mahalanobisDist:F2} > {RejectionMahalanobisThreshold:F1} or SSIM: {scan.MeasuredSsimScore:F3} < 0.85.";
        }

        // 3. Compute Pneumatic Diverter Solenoid Timing
        var fireDelayMs = 0;
        if (triggerEjection && scan.ConveyorSpeedMps > 0.05)
        {
            fireDelayMs = (int)Math.Round((scan.DistanceToDiverterMeters / scan.ConveyorSpeedMps) * 1000.0);
        }

        return new DefectClassificationResult(
            ScanId: scan.ScanId,
            ItemId: scan.ItemId,
            MahalanobisDistance: Math.Round(mahalanobisDist, 2),
            MeasuredSsimScore: Math.Round(scan.MeasuredSsimScore, 3),
            Disposition: disposition,
            PrimaryDefect: defect,
            TriggerPneumaticEjection: triggerEjection,
            PneumaticFireDelayMs: fireDelayMs,
            InspectionAdvisory: advisory,
            InspectedAtUtc: DateTime.UtcNow
        );
    }
}
