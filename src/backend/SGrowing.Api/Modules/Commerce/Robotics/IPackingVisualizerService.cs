namespace SGrowing.Api.Modules.Commerce.Robotics;

/// <summary>
/// Service for generating SVG diagrams and 3D visual metadata for warehouse pack stations.
/// </summary>
public interface IPackingVisualizerService
{
    /// <summary>
    /// Generates a comprehensive dual-projection (Top-Down XY and Front Elevation XZ) SVG diagram.
    /// </summary>
    string GeneratePackingSvg(PackingSolution solution);

    /// <summary>
    /// Generates a 2D top-down SVG diagram for a specific packing layer.
    /// </summary>
    string GenerateLayerSvg(PackingSolution solution, int layerIndex);

    /// <summary>
    /// Extracts structured 3D coordinates and bounding box metadata for WebGL/Three.js clients.
    /// </summary>
    PackingVisualMetadata ExtractVisualMetadata(PackingSolution solution);
}
