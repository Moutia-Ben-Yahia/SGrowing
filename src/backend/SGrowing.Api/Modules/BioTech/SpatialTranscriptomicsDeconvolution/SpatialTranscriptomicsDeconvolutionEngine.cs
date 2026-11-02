namespace SGrowing.Api.Modules.BioTech.SpatialTranscriptomicsDeconvolution;

public class SpatialTranscriptomicsDeconvolutionEngine
{
    private const double TlsDistanceThresholdUm = 50.0;

    public SpatialTranscriptomicsAnalysisResult DeconvolveSpatialSlide(SpatialTranscriptomicsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SlideId))
        {
            throw new ArgumentException("Slide ID cannot be null or empty.", nameof(request));
        }

        var spots = request.CellSpots ?? [];
        int totalCells = spots.Count;

        if (totalCells == 0)
        {
            return new SpatialTranscriptomicsAnalysisResult(
                request.SlideId,
                0,
                0.0,
                true,
                [],
                false,
                0.0,
                "No cells provided in spatial slide request.");
        }

        long totalBarcodes = spots.Sum(s => (long)s.DetectedBarcodeCount);
        long totalMiscalls = spots.Sum(s => (long)s.OpticalMiscallCount);
        double globalMiscallRate = totalBarcodes > 0 ? ((double)totalMiscalls / totalBarcodes) * 100.0 : 0.0;
        bool isOpticalAcceptable = globalMiscallRate <= 1.0;

        var typeCounts = new Dictionary<string, int>
        {
            ["T-Cell"] = 0,
            ["B-Cell"] = 0,
            ["Macrophage"] = 0,
            ["Tumor/Epithelial"] = 0,
            ["Endothelial"] = 0,
            ["Stroma/Other"] = 0
        };

        var tCells = new List<CellSpotTelemetry>();
        var bCells = new List<CellSpotTelemetry>();

        foreach (var spot in spots)
        {
            string assignedType = AssignCellType(spot.MarkerGeneCounts);
            typeCounts[assignedType]++;

            if (assignedType == "T-Cell")
            {
                tCells.Add(spot);
            }
            else if (assignedType == "B-Cell")
            {
                bCells.Add(spot);
            }
        }

        var fractions = typeCounts.ToDictionary(
            kvp => kvp.Key,
            kvp => Math.Round((double)kvp.Value / totalCells, 3));

        // Evaluate Tertiary Lymphoid Structure (TLS) spatial co-localization
        int proximateBtPairs = 0;
        foreach (var b in bCells)
        {
            foreach (var t in tCells)
            {
                double dist = Math.Sqrt(Math.Pow(b.SpatialCoordinateXUm - t.SpatialCoordinateXUm, 2) + Math.Pow(b.SpatialCoordinateYUm - t.SpatialCoordinateYUm, 2));
                if (dist <= TlsDistanceThresholdUm)
                {
                    proximateBtPairs++;
                }
            }
        }

        double tlsScore = Math.Round((double)proximateBtPairs / Math.Max(totalCells * 0.1, 1.0) * 2.5, 2);
        bool hasTls = proximateBtPairs >= 3 && bCells.Count >= 2 && tCells.Count >= 2;

        string summary;
        if (!isOpticalAcceptable)
        {
            summary = $"OPTICAL QC FAILURE: Miscall rate {globalMiscallRate:F2}% exceeds 1.0% threshold. Probable optical diffraction crowding or imaging round photobleaching.";
        }
        else if (hasTls)
        {
            summary = $"ACTIVE TERTIARY LYMPHOID STRUCTURE (TLS) DETECTED: {proximateBtPairs} proximate B-T cell pairs (TLS Score: {tlsScore:F1}) within {TlsDistanceThresholdUm} µm neighborhood. Favorable immune infiltration prognostic profile.";
        }
        else
        {
            summary = $"SPATIAL PROFILE NOMINAL: Deconvolved {totalCells} cells across {request.TissueType}. Optical miscall rate {globalMiscallRate:F3}% with diffuse immune infiltration.";
        }

        return new SpatialTranscriptomicsAnalysisResult(
            request.SlideId,
            totalCells,
            Math.Round(globalMiscallRate, 3),
            isOpticalAcceptable,
            fractions,
            hasTls,
            tlsScore,
            summary);
    }

    private static string AssignCellType(Dictionary<string, int>? markers)
    {
        if (markers == null || markers.Count == 0)
        {
            return "Stroma/Other";
        }

        int cd3e = markers.GetValueOrDefault("CD3E", 0);
        int ms4a1 = markers.GetValueOrDefault("MS4A1", 0);
        int cd68 = markers.GetValueOrDefault("CD68", 0);
        int epcam = markers.GetValueOrDefault("EPCAM", 0);
        int pecam1 = markers.GetValueOrDefault("PECAM1", 0);

        int max = Math.Max(cd3e, Math.Max(ms4a1, Math.Max(cd68, Math.Max(epcam, pecam1))));
        if (max == 0)
        {
            return "Stroma/Other";
        }

        if (max == cd3e) return "T-Cell";
        if (max == ms4a1) return "B-Cell";
        if (max == cd68) return "Macrophage";
        if (max == epcam) return "Tumor/Epithelial";
        return "Endothelial";
    }
}
