using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.SpatialAdBidding;

public sealed record SpatialAdOverview(
    int ActiveSpatialPlacementsCount,
    int DailyBidsEvaluatedCount,
    double BidWinRatePct,
    double AverageSpatialLatencyMs,
    IReadOnlyList<SpatialPlacementZone> PlacementZones,
    IReadOnlyList<SpatialBidRecord> RecentWinningBids
);

public sealed record SpatialPlacementZone(
    string ZoneId,
    string VirtualWorldEngine, // Unreal Engine 5, Unity, WebXR
    string PlacementType, // 3D Billboard, Dynamic Hologram, Vehicle Livery
    string TextureResolution,
    double LightingOcclusionTolerancePct,
    decimal FloorCpmUsd
);

public sealed record SpatialBidRecord(
    string BidId,
    string ZoneId,
    string AdvertiserName,
    decimal WinningBidCpmUsd,
    double CameraDistanceUnits,
    double GazeDwellTimeSeconds,
    string RenderingStatus,
    DateTime Timestamp
);

public sealed record SpatialBidEvaluationRequest(
    string ZoneId,
    string VirtualEngine,
    double CameraX,
    double CameraY,
    double CameraZ,
    double FrustumAngleDegrees,
    double AmbientLightLumens,
    double OcclusionFactor
);

public sealed record SpatialBidEvaluationResult(
    string EvaluationId,
    string ZoneId,
    bool ShouldBid,
    decimal OfferedBidCpmUsd,
    string SelectedTextureMapUrl,
    string ShaderProfile,
    long EvaluationLatencyMs
);

public sealed record VerifySpatialViewRequest(
    string BidId,
    string ZoneId,
    double ContinuousGazeDwellSeconds,
    double ViewingAngleDeviationDeg
);

public sealed record SpatialViewAttestation(
    string AttestationId,
    string BidId,
    bool ViewabilityVerified,
    double AttestedGazeSeconds,
    string CryptographicGazeProof,
    DateTime Timestamp
);
