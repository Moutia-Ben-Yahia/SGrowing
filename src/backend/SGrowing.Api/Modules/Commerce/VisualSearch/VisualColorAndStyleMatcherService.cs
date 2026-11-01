using System.Globalization;

namespace SGrowing.Api.Modules.Commerce.VisualSearch;

/// <summary>
/// Contract for visual aesthetics analysis, CIELAB/RGB color proximity, color harmony, and silhouette style matching.
/// </summary>
public interface IVisualColorAndStyleMatcherService
{
    /// <summary>
    /// Extracts the dominant color palette from raw RGB pixel data or color histogram bytes.
    /// </summary>
    IReadOnlyList<DominantColor> ExtractColorPalette(byte[] rgbHistogramData, int maxColors = 5);

    /// <summary>
    /// Matches indexed products whose dominant colors are within the specified Euclidean distance from a target hex color.
    /// </summary>
    IReadOnlyList<ColorMatchItem> MatchByColor(string targetHex, double maxDistance = 50.0);

    /// <summary>
    /// Matches candidate products from an explicit collection whose dominant colors are within the specified distance.
    /// </summary>
    IReadOnlyList<ColorMatchItem> MatchByColor(string targetHex, IEnumerable<VisualEmbeddingRecord> records, double maxDistance = 50.0);

    /// <summary>
    /// Calculates the style affinity coefficient (0.0 to 1.0) between two garment silhouette types.
    /// </summary>
    double CalculateStyleAffinity(string silhouetteA, string silhouetteB);

    /// <summary>
    /// Computes color harmony score (0.0 to 1.0) between two dominant colors (complementary, monochromatic, analogous).
    /// </summary>
    double CalculateColorHarmony(DominantColor colorA, DominantColor colorB);

    /// <summary>
    /// Computes color harmony score (0.0 to 1.0) between two hex color codes.
    /// </summary>
    double CalculateColorHarmony(string hexA, string hexB);

    /// <summary>
    /// Computes Euclidean distance in RGB color space: sqrt((R1 - R2)^2 + (G1 - G2)^2 + (B1 - B2)^2).
    /// </summary>
    double CalculateRgbDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2);

    /// <summary>
    /// Computes Euclidean distance in RGB color space between two hex strings.
    /// </summary>
    double CalculateRgbDistance(string hexA, string hexB);

    /// <summary>
    /// Computes perceptual color distance (Delta E) in CIELAB color space.
    /// </summary>
    double CalculateCielabDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2);

    /// <summary>
    /// Computes perceptual color distance (Delta E) in CIELAB color space between two hex strings.
    /// </summary>
    double CalculateCielabDistance(string hexA, string hexB);

    /// <summary>
    /// Maps an RGB triplet to the closest canonical commerce color name and canonical representation.
    /// </summary>
    DominantColor MapToCanonicalColor(byte red, byte green, byte blue, double prominencePct = 1.0);

    /// <summary>
    /// Filters product records by silhouette type compatibility and color proximity.
    /// </summary>
    IReadOnlyList<VisualEmbeddingRecord> FilterByStyleAndColor(
        IEnumerable<VisualEmbeddingRecord> candidates,
        string? targetSilhouette,
        string? targetHex,
        double maxColorDistance = 50.0,
        double minStyleAffinity = 0.5);
}

/// <summary>
/// Production-grade implementation of visual color matching, CIELAB/RGB distance, color harmony scoring, and silhouette affinity.
/// </summary>
public sealed class VisualColorAndStyleMatcherService(IVisualEmbeddingSearchEngine searchEngine) : IVisualColorAndStyleMatcherService
{
    private readonly IVisualEmbeddingSearchEngine _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));

    /// <summary>
    /// Parameterless constructor for testing and standalone instantiation.
    /// </summary>
    public VisualColorAndStyleMatcherService() : this(new VisualEmbeddingSearchEngine())
    {
    }

    private static readonly (string Name, string Hex, byte R, byte G, byte B)[] CanonicalPalette =
    [
        (CanonicalCommerceColors.Noir, "#000000", 0, 0, 0),
        (CanonicalCommerceColors.Blanc, "#FFFFFF", 255, 255, 255),
        (CanonicalCommerceColors.Beige, "#F5F5DC", 245, 245, 220),
        (CanonicalCommerceColors.Bordeaux, "#800020", 128, 0, 32),
        (CanonicalCommerceColors.Olive, "#808000", 128, 128, 0),
        (CanonicalCommerceColors.Camel, "#C19A6B", 193, 154, 107),
        (CanonicalCommerceColors.Khaki, "#C3B091", 195, 176, 145),
        (CanonicalCommerceColors.Navy, "#000080", 0, 0, 128),
        (CanonicalCommerceColors.Emerald, "#50C878", 80, 200, 120)
    ];

    private static readonly Dictionary<(string, string), double> SilhouetteAffinityMap = new()
    {
        { (SilhouetteTypes.LooseFit, SilhouetteTypes.Boxy), 0.88 },
        { (SilhouetteTypes.Boxy, SilhouetteTypes.LooseFit), 0.88 },

        { (SilhouetteTypes.SlimFit, SilhouetteTypes.Standard), 0.85 },
        { (SilhouetteTypes.Standard, SilhouetteTypes.SlimFit), 0.85 },

        { (SilhouetteTypes.ALine, SilhouetteTypes.SlimFit), 0.78 },
        { (SilhouetteTypes.SlimFit, SilhouetteTypes.ALine), 0.78 },

        { (SilhouetteTypes.ALine, SilhouetteTypes.Standard), 0.75 },
        { (SilhouetteTypes.Standard, SilhouetteTypes.ALine), 0.75 },

        { (SilhouetteTypes.Boxy, SilhouetteTypes.Standard), 0.70 },
        { (SilhouetteTypes.Standard, SilhouetteTypes.Boxy), 0.70 },

        { (SilhouetteTypes.ALine, SilhouetteTypes.LooseFit), 0.65 },
        { (SilhouetteTypes.LooseFit, SilhouetteTypes.ALine), 0.65 },

        { (SilhouetteTypes.ALine, SilhouetteTypes.Boxy), 0.60 },
        { (SilhouetteTypes.Boxy, SilhouetteTypes.ALine), 0.60 },

        { (SilhouetteTypes.Boxy, SilhouetteTypes.SlimFit), 0.50 },
        { (SilhouetteTypes.SlimFit, SilhouetteTypes.Boxy), 0.50 },

        { (SilhouetteTypes.LooseFit, SilhouetteTypes.SlimFit), 0.45 },
        { (SilhouetteTypes.SlimFit, SilhouetteTypes.LooseFit), 0.45 }
    };

    /// <inheritdoc/>
    public IReadOnlyList<DominantColor> ExtractColorPalette(byte[] rgbHistogramData, int maxColors = 5)
    {
        if (rgbHistogramData == null || rgbHistogramData.Length == 0)
        {
            return
            [
                new DominantColor("#000000", 0, 0, 0, 0.50, CanonicalCommerceColors.Noir),
                new DominantColor("#FFFFFF", 255, 255, 255, 0.50, CanonicalCommerceColors.Blanc)
            ];
        }

        if (maxColors <= 0)
        {
            maxColors = 5;
        }

        // Quantize RGB bytes into 512 buckets (3 bits per channel: 8x8x8)
        var bucketCounts = new int[512];
        var bucketSumR = new long[512];
        var bucketSumG = new long[512];
        var bucketSumB = new long[512];
        long totalPixels = 0;

        int step = rgbHistogramData.Length >= 4 && rgbHistogramData.Length % 4 == 0 ? 4 : 3;

        for (int i = 0; i + 2 < rgbHistogramData.Length; i += step)
        {
            byte r = rgbHistogramData[i];
            byte g = rgbHistogramData[i + 1];
            byte b = rgbHistogramData[i + 2];

            int binR = r >> 5; // 0..7
            int binG = g >> 5; // 0..7
            int binB = b >> 5; // 0..7
            int binIndex = (binR << 6) | (binG << 3) | binB;

            bucketCounts[binIndex]++;
            bucketSumR[binIndex] += r;
            bucketSumG[binIndex] += g;
            bucketSumB[binIndex] += b;
            totalPixels++;
        }

        if (totalPixels == 0)
        {
            return
            [
                new DominantColor("#000000", 0, 0, 0, 1.0, CanonicalCommerceColors.Noir)
            ];
        }

        var sortedBins = new List<(int BinIndex, int Count)>(512);
        for (int i = 0; i < 512; i++)
        {
            if (bucketCounts[i] > 0)
            {
                sortedBins.Add((i, bucketCounts[i]));
            }
        }

        sortedBins.Sort((a, b) => b.Count.CompareTo(a.Count));

        int take = Math.Min(maxColors, sortedBins.Count);
        var result = new List<DominantColor>(take);

        double totalProminence = 0.0;
        var rawColors = new List<(byte R, byte G, byte B, double RawProminence)>(take);

        for (int i = 0; i < take; i++)
        {
            int bin = sortedBins[i].BinIndex;
            int count = sortedBins[i].Count;

            byte avgR = (byte)(bucketSumR[bin] / count);
            byte avgG = (byte)(bucketSumG[bin] / count);
            byte avgB = (byte)(bucketSumB[bin] / count);
            double prominence = (double)count / totalPixels;

            totalProminence += prominence;
            rawColors.Add((avgR, avgG, avgB, prominence));
        }

        // Normalize prominence so sum is 1.0
        foreach (var (r, g, b, rawProm) in rawColors)
        {
            double normalizedProminence = totalProminence > 0.0
                ? Math.Round(rawProm / totalProminence, 4)
                : Math.Round(1.0 / take, 4);

            var dominant = MapToCanonicalColor(r, g, b, normalizedProminence);
            result.Add(dominant);
        }

        return result;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ColorMatchItem> MatchByColor(string targetHex, double maxDistance = 50.0)
    {
        var records = _searchEngine.GetAllRecords();
        return MatchByColor(targetHex, records, maxDistance);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ColorMatchItem> MatchByColor(string targetHex, IEnumerable<VisualEmbeddingRecord> records, double maxDistance = 50.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetHex);
        ArgumentNullException.ThrowIfNull(records);

        var (targetR, targetG, targetB) = ParseHexColor(targetHex);
        var matches = new List<ColorMatchItem>();

        foreach (var record in records)
        {
            if (record.ColorPalette == null || record.ColorPalette.Length == 0)
            {
                continue;
            }

            DominantColor? bestColor = null;
            double bestDist = double.MaxValue;

            foreach (var col in record.ColorPalette)
            {
                double dist = CalculateCielabDistance(targetR, targetG, targetB, col.Red, col.Green, col.Blue);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestColor = col;
                }
            }

            if (bestColor != null && bestDist <= maxDistance)
            {
                matches.Add(new ColorMatchItem(
                    ProductId: record.ProductId,
                    Category: record.Category,
                    MatchedColor: bestColor,
                    ColorDistance: Math.Round(bestDist, 2),
                    DominantColors: record.ColorPalette));
            }
        }

        matches.Sort((a, b) => a.ColorDistance.CompareTo(b.ColorDistance));
        return matches;
    }

    /// <inheritdoc/>
    public double CalculateStyleAffinity(string silhouetteA, string silhouetteB)
    {
        if (string.IsNullOrWhiteSpace(silhouetteA) || string.IsNullOrWhiteSpace(silhouetteB))
        {
            return 0.50;
        }

        string sA = silhouetteA.Trim();
        string sB = silhouetteB.Trim();

        if (string.Equals(sA, sB, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        foreach (var ((k1, k2), affinity) in SilhouetteAffinityMap)
        {
            if (string.Equals(sA, k1, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(sB, k2, StringComparison.OrdinalIgnoreCase))
            {
                return affinity;
            }
        }

        return 0.50;
    }

    /// <inheritdoc/>
    public double CalculateColorHarmony(DominantColor colorA, DominantColor colorB)
    {
        ArgumentNullException.ThrowIfNull(colorA);
        ArgumentNullException.ThrowIfNull(colorB);

        return CalculateColorHarmony(colorA.HexCode, colorB.HexCode);
    }

    /// <inheritdoc/>
    public double CalculateColorHarmony(string hexA, string hexB)
    {
        var (r1, g1, b1) = ParseHexColor(hexA);
        var (r2, g2, b2) = ParseHexColor(hexB);

        var (h1, s1, l1) = RgbToHsl(r1, g1, b1);
        var (h2, s2, l2) = RgbToHsl(r2, g2, b2);

        // Neutral color check: Black, White, Grey, or very low saturation harmonizes universally
        bool isNeutral1 = s1 < 0.12 || l1 < 0.10 || l1 > 0.90;
        bool isNeutral2 = s2 < 0.12 || l2 < 0.10 || l2 > 0.90;

        if (isNeutral1 || isNeutral2)
        {
            return 0.92;
        }

        // Circular hue difference [0, 180]
        double deltaH = Math.Abs(h1 - h2);
        if (deltaH > 180.0)
        {
            deltaH = 360.0 - deltaH;
        }

        // 1. Monochromatic harmony (same hue family, deltaH <= 15 deg)
        if (deltaH <= 15.0)
        {
            return 1.0 - (deltaH / 15.0) * 0.08; // 0.92 to 1.0
        }

        // 2. Analogous harmony (deltaH between 15 and 45 deg)
        if (deltaH <= 45.0)
        {
            double factor = (deltaH - 15.0) / 30.0;
            return 0.95 - (factor * 0.12); // 0.83 to 0.95
        }

        // 3. Complementary harmony (deltaH around 180 deg)
        double compDiff = Math.Abs(deltaH - 180.0);
        if (compDiff <= 25.0)
        {
            return 1.0 - (compDiff / 25.0) * 0.15; // 0.85 to 1.0
        }

        // 4. Split-Complementary harmony (deltaH around 150 deg)
        double splitDiff = Math.Abs(deltaH - 150.0);
        if (splitDiff <= 20.0)
        {
            return 0.88 - (splitDiff / 20.0) * 0.12; // 0.76 to 0.88
        }

        // 5. Triadic harmony (deltaH around 120 deg)
        double triadDiff = Math.Abs(deltaH - 120.0);
        if (triadDiff <= 20.0)
        {
            return 0.82 - (triadDiff / 20.0) * 0.12; // 0.70 to 0.82
        }

        // Clashing hues with moderate/high saturation
        return Math.Clamp(0.55 - (deltaH / 180.0) * 0.20, 0.35, 0.60);
    }

    /// <inheritdoc/>
    public double CalculateRgbDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        double dr = r1 - r2;
        double dg = g1 - g2;
        double db = b1 - b2;
        return Math.Sqrt((dr * dr) + (dg * dg) + (db * db));
    }

    /// <inheritdoc/>
    public double CalculateRgbDistance(string hexA, string hexB)
    {
        var (r1, g1, b1) = ParseHexColor(hexA);
        var (r2, g2, b2) = ParseHexColor(hexB);
        return CalculateRgbDistance(r1, g1, b1, r2, g2, b2);
    }

    /// <inheritdoc/>
    public double CalculateCielabDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var (l1, a1, b_1) = RgbToLab(r1, g1, b1);
        var (l2, a2, b_2) = RgbToLab(r2, g2, b2);

        double dl = l1 - l2;
        double da = a1 - a2;
        double db = b_1 - b_2;

        return Math.Sqrt((dl * dl) + (da * da) + (db * db));
    }

    /// <inheritdoc/>
    public double CalculateCielabDistance(string hexA, string hexB)
    {
        var (r1, g1, b1) = ParseHexColor(hexA);
        var (r2, g2, b2) = ParseHexColor(hexB);
        return CalculateCielabDistance(r1, g1, b1, r2, g2, b2);
    }

    /// <inheritdoc/>
    public DominantColor MapToCanonicalColor(byte red, byte green, byte blue, double prominencePct = 1.0)
    {
        string hex = $"#{red:X2}{green:X2}{blue:X2}";
        string closestName = CanonicalCommerceColors.Noir;
        double minDistance = double.MaxValue;

        foreach (var canonical in CanonicalPalette)
        {
            double dist = CalculateCielabDistance(red, green, blue, canonical.R, canonical.G, canonical.B);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestName = canonical.Name;
            }
        }

        return new DominantColor(
            HexCode: hex,
            Red: red,
            Green: green,
            Blue: blue,
            ProminencePct: Math.Clamp(prominencePct, 0.0, 1.0),
            ColorName: closestName);
    }

    /// <inheritdoc/>
    public IReadOnlyList<VisualEmbeddingRecord> FilterByStyleAndColor(
        IEnumerable<VisualEmbeddingRecord> candidates,
        string? targetSilhouette,
        string? targetHex,
        double maxColorDistance = 50.0,
        double minStyleAffinity = 0.5)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        (byte R, byte G, byte B)? targetRgb = !string.IsNullOrWhiteSpace(targetHex)
            ? ParseHexColor(targetHex)
            : null;

        var result = new List<VisualEmbeddingRecord>();

        foreach (var record in candidates)
        {
            if (!string.IsNullOrWhiteSpace(targetSilhouette))
            {
                double affinity = CalculateStyleAffinity(targetSilhouette, record.SilhouetteType);
                if (affinity < minStyleAffinity)
                {
                    continue;
                }
            }

            if (targetRgb.HasValue)
            {
                bool colorMatched = false;
                if (record.ColorPalette != null)
                {
                    foreach (var col in record.ColorPalette)
                    {
                        double dist = CalculateCielabDistance(
                            targetRgb.Value.R, targetRgb.Value.G, targetRgb.Value.B,
                            col.Red, col.Green, col.Blue);

                        if (dist <= maxColorDistance)
                        {
                            colorMatched = true;
                            break;
                        }
                    }
                }

                if (!colorMatched)
                {
                    continue;
                }
            }

            result.Add(record);
        }

        return result;
    }

    private static (byte R, byte G, byte B) ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return (0, 0, 0);
        }

        string cleanHex = hex.Trim().TrimStart('#');
        if (cleanHex.Length == 3)
        {
            cleanHex = $"{cleanHex[0]}{cleanHex[0]}{cleanHex[1]}{cleanHex[1]}{cleanHex[2]}{cleanHex[2]}";
        }

        if (cleanHex.Length != 6 ||
            !byte.TryParse(cleanHex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) ||
            !byte.TryParse(cleanHex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) ||
            !byte.TryParse(cleanHex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return (0, 0, 0);
        }

        return (r, g, b);
    }

    private static (double H, double S, double L) RgbToHsl(byte r, byte g, byte b)
    {
        double redNorm = r / 255.0;
        double greenNorm = g / 255.0;
        double blueNorm = b / 255.0;

        double max = Math.Max(redNorm, Math.Max(greenNorm, blueNorm));
        double min = Math.Min(redNorm, Math.Min(greenNorm, blueNorm));
        double delta = max - min;

        double l = (max + min) / 2.0;
        double h = 0.0;
        double s = 0.0;

        if (delta > 1e-6)
        {
            s = l < 0.5 ? delta / (max + min) : delta / (2.0 - max - min);

            if (Math.Abs(max - redNorm) < 1e-6)
            {
                h = ((greenNorm - blueNorm) / delta) + (greenNorm < blueNorm ? 6.0 : 0.0);
            }
            else if (Math.Abs(max - greenNorm) < 1e-6)
            {
                h = ((blueNorm - redNorm) / delta) + 2.0;
            }
            else
            {
                h = ((redNorm - greenNorm) / delta) + 4.0;
            }

            h *= 60.0;
        }

        return (h, s, l);
    }

    private static (double L, double A, double B) RgbToLab(byte r, byte g, byte b)
    {
        // 1. Linearize sRGB
        double rLin = LinearizeChannel(r / 255.0);
        double gLin = LinearizeChannel(g / 255.0);
        double bLin = LinearizeChannel(b / 255.0);

        // 2. Convert to CIEXYZ using standard D65 illuminant
        double x = (rLin * 0.4124564 + gLin * 0.3575761 + bLin * 0.1804375) * 100.0;
        double y = (rLin * 0.2126729 + gLin * 0.7151522 + bLin * 0.0721750) * 100.0;
        double z = (rLin * 0.0193339 + gLin * 0.1191920 + bLin * 0.9503041) * 100.0;

        // 3. Convert XYZ to CIELAB using D65 reference white (Xn=95.047, Yn=100.000, Zn=108.883)
        double fx = PivotXyz(x / 95.047);
        double fy = PivotXyz(y / 100.000);
        double fz = PivotXyz(z / 108.883);

        double l = Math.Max(0.0, (116.0 * fy) - 16.0);
        double a = 500.0 * (fx - fy);
        double labB = 200.0 * (fy - fz);

        return (l, a, labB);
    }

    private static double LinearizeChannel(double val)
    {
        return val > 0.04045
            ? Math.Pow((val + 0.055) / 1.055, 2.4)
            : val / 12.92;
    }

    private static double PivotXyz(double t)
    {
        return t > 0.008856451679
            ? Math.Pow(t, 1.0 / 3.0)
            : (7.787037037 * t) + (16.0 / 116.0);
    }
}
