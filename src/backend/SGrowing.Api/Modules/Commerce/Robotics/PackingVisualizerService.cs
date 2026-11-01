using System.Globalization;
using System.Text;

namespace SGrowing.Api.Modules.Commerce.Robotics;

/// <summary>
/// Service for generating SVG packing diagrams and 3D visual metadata for warehouse pack stations.
/// </summary>
public class PackingVisualizerService : IPackingVisualizerService
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <inheritdoc />
    public string GeneratePackingSvg(PackingSolution solution)
    {
        var sb = new StringBuilder();
        var carton = solution.CartonSpec;

        double w = Math.Max(carton.InnerDimensions.Width, 1.0);
        double d = Math.Max(carton.InnerDimensions.Depth, 1.0);
        double h = Math.Max(carton.InnerDimensions.Height, 1.0);

        string balanceColor = solution.IsBalanced ? "#22c55e" : "#ef4444";
        string balanceText = solution.IsBalanced ? "BALANCED" : "IMBALANCED";

        sb.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 960 620" width="100%" height="100%" style="background:#0f172a; font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;">""");

        // Definitions & Filters
        sb.AppendLine("""
          <defs>
            <linearGradient id="headerGrad" x1="0%" y1="0%" x2="100%" y2="0%">
              <stop offset="0%" stop-color="#1e293b"/>
              <stop offset="100%" stop-color="#334155"/>
            </linearGradient>
            <filter id="shadow" x="-5%" y="-5%" width="110%" height="110%">
              <feDropShadow dx="0" dy="4" stdDeviation="4" flood-color="#000000" flood-opacity="0.3"/>
            </filter>
          </defs>
        """);

        // Header Card
        sb.AppendLine($$"""
          <rect x="20" y="20" width="920" height="70" rx="8" fill="url(#headerGrad)" stroke="#475569" stroke-width="1"/>
          <text x="40" y="50" fill="#f8fafc" font-size="18" font-weight="bold">3D Cartonization Visualizer: {{carton.Name}} ({{w}}x{{h}}x{{d}} cm)</text>
          <text x="40" y="72" fill="#94a3b8" font-size="12">Items: {{solution.PlacedItems.Count}} placed | Volume Utilization: {{solution.VolumeUtilizationPercentage.ToString("F1", Inv)}}% | Gross Weight: {{solution.TotalGrossWeightGrams.ToString("F0", Inv)}}g</text>

          <rect x="760" y="35" width="160" height="38" rx="6" fill="{{balanceColor}}" opacity="0.2"/>
          <rect x="760" y="35" width="160" height="38" rx="6" fill="none" stroke="{{balanceColor}}" stroke-width="1.5"/>
          <text x="840" y="59" fill="{{balanceColor}}" font-size="14" font-weight="bold" text-anchor="middle">{{balanceText}}</text>
        """);

        // View 1: Top-Down (XY Plane: Width x Depth)
        // Canvas region: X=40, Y=130, Width=400, Height=400
        double xyBoxX = 50;
        double xyBoxY = 150;
        double xyBoxMaxW = 400;
        double xyBoxMaxH = 380;

        double scaleXY = Math.Min(xyBoxMaxW / w, xyBoxMaxH / d);
        double svgCartonW = w * scaleXY;
        double svgCartonD = d * scaleXY;

        sb.AppendLine($$"""
          <!-- View 1: Top-Down XY View -->
          <text x="{{xyBoxX.ToString("F0", Inv)}}" y="135" fill="#38bdf8" font-size="14" font-weight="bold">Top-Down Footprint View (XY Plane)</text>
          <text x="{{(xyBoxX + svgCartonW).ToString("F0", Inv)}}" y="135" fill="#64748b" font-size="11" text-anchor="end">W: {{w}}cm x D: {{d}}cm</text>
          <rect x="{{xyBoxX.ToString("F1", Inv)}}" y="{{xyBoxY.ToString("F1", Inv)}}" width="{{svgCartonW.ToString("F1", Inv)}}" height="{{svgCartonD.ToString("F1", Inv)}}" fill="#1e293b" stroke="#0284c7" stroke-width="2" rx="4"/>
          <!-- Grid lines -->
          <line x1="{{xyBoxX.ToString("F1", Inv)}}" y1="{{(xyBoxY + (svgCartonD / 2)).ToString("F1", Inv)}}" x2="{{(xyBoxX + svgCartonW).ToString("F1", Inv)}}" y2="{{(xyBoxY + (svgCartonD / 2)).ToString("F1", Inv)}}" stroke="#334155" stroke-dasharray="4,4"/>
          <line x1="{{(xyBoxX + (svgCartonW / 2)).ToString("F1", Inv)}}" y1="{{xyBoxY.ToString("F1", Inv)}}" x2="{{(xyBoxX + (svgCartonW / 2)).ToString("F1", Inv)}}" y2="{{(xyBoxY + svgCartonD).ToString("F1", Inv)}}" stroke="#334155" stroke-dasharray="4,4"/>
        """);

        // Placed items in Top-Down view
        foreach (var item in solution.PlacedItems)
        {
            double ix = xyBoxX + (item.PositionX * scaleXY);
            double iy = xyBoxY + (item.PositionY * scaleXY);
            double iw = item.PlacedWidth * scaleXY;
            double id = item.PlacedDepth * scaleXY;

            string strokeColor = item.PositionZ <= 0.001 ? "#38bdf8" : "#fbbf24";
            string fillColor = item.PositionZ <= 0.001 ? "#0369a1" : "#b45309";

            sb.AppendLine($$"""
              <g>
                <rect x="{{ix.ToString("F1", Inv)}}" y="{{iy.ToString("F1", Inv)}}" width="{{iw.ToString("F1", Inv)}}" height="{{id.ToString("F1", Inv)}}" fill="{{fillColor}}" fill-opacity="0.8" stroke="{{strokeColor}}" stroke-width="1.5" rx="3"/>
                <text x="{{(ix + 6).ToString("F1", Inv)}}" y="{{(iy + 16).ToString("F1", Inv)}}" fill="#f8fafc" font-size="10" font-weight="bold">{{item.Sku}}</text>
                <text x="{{(ix + 6).ToString("F1", Inv)}}" y="{{(iy + 28).ToString("F1", Inv)}}" fill="#cbd5e1" font-size="9">Z:{{item.PositionZ:F0}}cm</text>
              </g>
            """);
        }

        // Center of Gravity (CoG) and Box Center on Top-Down View
        double cogSvgX = xyBoxX + (solution.CenterOfGravityX * scaleXY);
        double cogSvgY = xyBoxY + (solution.CenterOfGravityY * scaleXY);
        double geoSvgX = xyBoxX + ((w / 2.0) * scaleXY);
        double geoSvgY = xyBoxY + ((d / 2.0) * scaleXY);

        sb.AppendLine($$"""
          <!-- Geometric Center Crosshair -->
          <circle cx="{{geoSvgX.ToString("F1", Inv)}}" cy="{{geoSvgY.ToString("F1", Inv)}}" r="4" fill="none" stroke="#94a3b8" stroke-width="1.5"/>

          <!-- Center of Gravity (CoG) Bullseye -->
          <circle cx="{{cogSvgX.ToString("F1", Inv)}}" cy="{{cogSvgY.ToString("F1", Inv)}}" r="7" fill="none" stroke="#ef4444" stroke-width="2"/>
          <circle cx="{{cogSvgX.ToString("F1", Inv)}}" cy="{{cogSvgY.ToString("F1", Inv)}}" r="2" fill="#ef4444"/>
          <line x1="{{(cogSvgX - 10).ToString("F1", Inv)}}" y1="{{cogSvgY.ToString("F1", Inv)}}" x2="{{(cogSvgX + 10).ToString("F1", Inv)}}" y2="{{cogSvgY.ToString("F1", Inv)}}" stroke="#ef4444" stroke-width="1.5"/>
          <line x1="{{cogSvgX.ToString("F1", Inv)}}" y1="{{(cogSvgY - 10).ToString("F1", Inv)}}" x2="{{cogSvgX.ToString("F1", Inv)}}" y2="{{(cogSvgY + 10).ToString("F1", Inv)}}" stroke="#ef4444" stroke-width="1.5"/>
          <text x="{{(cogSvgX + 12).ToString("F1", Inv)}}" y="{{(cogSvgY - 6).ToString("F1", Inv)}}" fill="#ef4444" font-size="10" font-weight="bold">CoG ({{solution.CenterOfGravityX.ToString("F1", Inv)}}, {{solution.CenterOfGravityY.ToString("F1", Inv)}})</text>
        """);

        // View 2: Front Elevation (XZ Plane: Width x Height)
        // Canvas region: X=510, Y=150, Width=400, Height=380
        double xzBoxX = 510;
        double xzBoxY = 150;
        double xzBoxMaxW = 400;
        double xzBoxMaxH = 380;

        double scaleXZ = Math.Min(xzBoxMaxW / w, xzBoxMaxH / h);
        double svgElevW = w * scaleXZ;
        double svgElevH = h * scaleXZ;

        sb.AppendLine($$"""
          <!-- View 2: Front Elevation XZ View -->
          <text x="{{xzBoxX.ToString("F0", Inv)}}" y="135" fill="#38bdf8" font-size="14" font-weight="bold">Front Elevation View (XZ Plane)</text>
          <text x="{{(xzBoxX + svgElevW).ToString("F0", Inv)}}" y="135" fill="#64748b" font-size="11" text-anchor="end">W: {{w}}cm x H: {{h}}cm</text>
          <rect x="{{xzBoxX.ToString("F1", Inv)}}" y="{{xzBoxY.ToString("F1", Inv)}}" width="{{svgElevW.ToString("F1", Inv)}}" height="{{svgElevH.ToString("F1", Inv)}}" fill="#1e293b" stroke="#10b981" stroke-width="2" rx="4"/>
          <!-- Floor line marker -->
          <line x1="{{xzBoxX.ToString("F1", Inv)}}" y1="{{(xzBoxY + svgElevH).ToString("F1", Inv)}}" x2="{{(xzBoxX + svgElevW).ToString("F1", Inv)}}" y2="{{(xzBoxY + svgElevH).ToString("F1", Inv)}}" stroke="#22c55e" stroke-width="3"/>
          <text x="{{xzBoxX.ToString("F1", Inv)}}" y="{{(xzBoxY + svgElevH + 18).ToString("F1", Inv)}}" fill="#22c55e" font-size="10" font-weight="bold">CONTAINER FLOOR (Z=0)</text>
        """);

        // Placed items in Front Elevation view (bottom-up: SVG Y increases downwards)
        foreach (var item in solution.PlacedItems)
        {
            double ix = xzBoxX + (item.PositionX * scaleXZ);
            double ih = item.PlacedHeight * scaleXZ;
            double iy = xzBoxY + svgElevH - ((item.PositionZ + item.PlacedHeight) * scaleXZ);
            double iw = item.PlacedWidth * scaleXZ;

            string strokeColor = item.PositionZ <= 0.001 ? "#34d399" : "#a78bfa";
            string fillColor = item.PositionZ <= 0.001 ? "#065f46" : "#6d28d9";

            sb.AppendLine($$"""
              <g>
                <rect x="{{ix.ToString("F1", Inv)}}" y="{{iy.ToString("F1", Inv)}}" width="{{iw.ToString("F1", Inv)}}" height="{{ih.ToString("F1", Inv)}}" fill="{{fillColor}}" fill-opacity="0.85" stroke="{{strokeColor}}" stroke-width="1.5" rx="3"/>
                <text x="{{(ix + 6).ToString("F1", Inv)}}" y="{{(iy + 15).ToString("F1", Inv)}}" fill="#f8fafc" font-size="10" font-weight="bold">{{item.Sku}}</text>
                <text x="{{(ix + 6).ToString("F1", Inv)}}" y="{{(iy + 27).ToString("F1", Inv)}}" fill="#cbd5e1" font-size="9">H:{{item.PlacedHeight:F0}}cm</text>
              </g>
            """);
        }

        // Elevation CoG marker
        double cogElevX = xzBoxX + (solution.CenterOfGravityX * scaleXZ);
        double cogElevY = xzBoxY + svgElevH - (solution.CenterOfGravityZ * scaleXZ);

        sb.AppendLine($$"""
          <!-- CoG in Front Elevation -->
          <circle cx="{{cogElevX.ToString("F1", Inv)}}" cy="{{cogElevY.ToString("F1", Inv)}}" r="6" fill="none" stroke="#ef4444" stroke-width="2"/>
          <circle cx="{{cogElevX.ToString("F1", Inv)}}" cy="{{cogElevY.ToString("F1", Inv)}}" r="2" fill="#ef4444"/>
          <line x1="{{(cogElevX - 8).ToString("F1", Inv)}}" y1="{{cogElevY.ToString("F1", Inv)}}" x2="{{(cogElevX + 8).ToString("F1", Inv)}}" y2="{{cogElevY.ToString("F1", Inv)}}" stroke="#ef4444" stroke-width="1.5"/>
          <text x="{{(cogElevX + 10).ToString("F1", Inv)}}" y="{{(cogElevY - 4).ToString("F1", Inv)}}" fill="#ef4444" font-size="10" font-weight="bold">Z={{solution.CenterOfGravityZ.ToString("F1", Inv)}}cm</text>
        """);

        // Footer / Legend
        sb.AppendLine("""
          <!-- Legend -->
          <g transform="translate(50, 570)">
            <rect x="0" y="0" width="14" height="14" fill="#0369a1" stroke="#38bdf8" rx="2"/>
            <text x="22" y="11" fill="#94a3b8" font-size="11">Floor Layer (Z=0)</text>

            <rect x="160" y="0" width="14" height="14" fill="#b45309" stroke="#fbbf24" rx="2"/>
            <text x="182" y="11" fill="#94a3b8" font-size="11">Stacked Upper Layer</text>

            <circle cx="330" cy="7" r="5" fill="#ef4444"/>
            <text x="342" y="11" fill="#94a3b8" font-size="11">Center of Gravity</text>

            <circle cx="480" cy="7" r="4" fill="none" stroke="#94a3b8" stroke-width="1.5"/>
            <text x="492" y="11" fill="#94a3b8" font-size="11">Geometric Center</text>
          </g>
        """);

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateLayerSvg(PackingSolution solution, int layerIndex)
    {
        var sb = new StringBuilder();
        var carton = solution.CartonSpec;

        double w = Math.Max(carton.InnerDimensions.Width, 1.0);
        double d = Math.Max(carton.InnerDimensions.Depth, 1.0);

        var layerItems = solution.PlacedItems.Where(p => p.LayerIndex == layerIndex).ToList();
        double layerZ = layerItems.FirstOrDefault()?.PositionZ ?? 0.0;

        sb.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 520" width="100%" height="100%" style="background:#0f172a; font-family: monospace;">""");

        sb.AppendLine($$"""
          <text x="30" y="35" fill="#f8fafc" font-size="16" font-weight="bold">Layer {{layerIndex}} Top-Down View (Elevation Z = {{layerZ.ToString("F1", Inv)}} cm)</text>
          <text x="30" y="55" fill="#94a3b8" font-size="12">Carton: {{carton.Name}} | Items on this layer: {{layerItems.Count}}</text>
        """);

        double boxX = 30;
        double boxY = 80;
        double maxW = 540;
        double maxH = 400;

        double scale = Math.Min(maxW / w, maxH / d);
        double svgW = w * scale;
        double svgD = d * scale;

        sb.AppendLine($$"""
          <rect x="{{boxX.ToString("F1", Inv)}}" y="{{boxY.ToString("F1", Inv)}}" width="{{svgW.ToString("F1", Inv)}}" height="{{svgD.ToString("F1", Inv)}}" fill="#1e293b" stroke="#0284c7" stroke-width="2" rx="4"/>
        """);

        foreach (var item in layerItems)
        {
            double ix = boxX + (item.PositionX * scale);
            double iy = boxY + (item.PositionY * scale);
            double iw = item.PlacedWidth * scale;
            double id = item.PlacedDepth * scale;

            sb.AppendLine($$"""
              <g>
                <rect x="{{ix.ToString("F1", Inv)}}" y="{{iy.ToString("F1", Inv)}}" width="{{iw.ToString("F1", Inv)}}" height="{{id.ToString("F1", Inv)}}" fill="#0284c7" fill-opacity="0.8" stroke="#38bdf8" stroke-width="1.5" rx="3"/>
                <text x="{{(ix + 8).ToString("F1", Inv)}}" y="{{(iy + 20).ToString("F1", Inv)}}" fill="#ffffff" font-size="12" font-weight="bold">{{item.Sku}}</text>
                <text x="{{(ix + 8).ToString("F1", Inv)}}" y="{{(iy + 36).ToString("F1", Inv)}}" fill="#cbd5e1" font-size="10">Pos: ({{item.PositionX:F0}}, {{item.PositionY:F0}}) cm</text>
                <text x="{{(ix + 8).ToString("F1", Inv)}}" y="{{(iy + 50).ToString("F1", Inv)}}" fill="#cbd5e1" font-size="10">Size: {{item.PlacedWidth:F0}}x{{item.PlacedDepth:F0}} cm</text>
              </g>
            """);
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    /// <inheritdoc />
    public PackingVisualMetadata ExtractVisualMetadata(PackingSolution solution)
    {
        var carton = solution.CartonSpec;
        var visualItems = new List<VisualItemMetadata>(solution.PlacedItems.Count);

        foreach (var p in solution.PlacedItems)
        {
            string colorHex = p.PositionZ <= 0.001
                ? "#0284c7" // Blue for base floor
                : (p.LayerIndex == 1 ? "#10b981" : "#f59e0b"); // Green/amber for higher layers

            visualItems.Add(new VisualItemMetadata(
                ItemId: p.ItemId,
                Sku: p.Sku,
                X: p.PositionX,
                Y: p.PositionY,
                Z: p.PositionZ,
                Width: p.PlacedWidth,
                Height: p.PlacedHeight,
                Depth: p.PlacedDepth,
                FragilityTier: FragilityTier.Standard,
                ColorHex: colorHex));
        }

        return new PackingVisualMetadata(
            CartonName: carton.Name,
            CartonWidth: carton.InnerDimensions.Width,
            CartonHeight: carton.InnerDimensions.Height,
            CartonDepth: carton.InnerDimensions.Depth,
            VolumeUtilizationPercentage: solution.VolumeUtilizationPercentage,
            IsBalanced: solution.IsBalanced,
            Items: visualItems);
    }
}
