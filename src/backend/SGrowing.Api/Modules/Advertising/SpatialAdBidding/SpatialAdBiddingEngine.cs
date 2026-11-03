using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Advertising.SpatialAdBidding;

public sealed class SpatialAdBiddingEngine
{
    private readonly ConcurrentDictionary<string, SpatialPlacementZone> _zones = new();
    private readonly ConcurrentDictionary<string, SpatialBidRecord> _bids = new();
    private readonly ConcurrentBag<SpatialViewAttestation> _attestationsHistory = new();

    public SpatialAdBiddingEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var z1 = new SpatialPlacementZone(
            "ZONE-UE5-CYBERPUNK-01",
            "Unreal Engine 5 (Nanite/Lumen)",
            "Dynamic Cyberpunk Neon Hologram",
            "4096x2048 PBR Albedo/Normal/Emissive",
            15.0,
            12.50m
        );

        var z2 = new SpatialPlacementZone(
            "ZONE-UNITY-STADIUM-02",
            "Unity 6 (URP)",
            "Esports Virtual Stadium Perimeter LED Ribbon",
            "8192x1024 HDR Uncompressed",
            10.0,
            18.00m
        );

        _zones[z1.ZoneId] = z1;
        _zones[z2.ZoneId] = z2;

        var b1 = new SpatialBidRecord(
            "BID-SPAT-881",
            z1.ZoneId,
            "Apex Cybernetics",
            15.80m,
            24.5,
            3.8,
            "PROJECTION_PBR_APPLIED",
            DateTime.UtcNow.AddMinutes(-10)
        );

        var b2 = new SpatialBidRecord(
            "BID-SPAT-882",
            z2.ZoneId,
            "Quantum Cloud Gaming",
            21.20m,
            12.0,
            5.2,
            "PROJECTION_PBR_APPLIED",
            DateTime.UtcNow.AddMinutes(-5)
        );

        _bids[b1.BidId] = b1;
        _bids[b2.BidId] = b2;
    }

    public SpatialAdOverview GetOverview()
    {
        var zonesList = _zones.Values.ToList();
        var bidsList = _bids.Values.ToList();

        return new SpatialAdOverview(
            zonesList.Count,
            184000,
            64.2,
            18.5,
            zonesList,
            bidsList
        );
    }

    public SpatialBidEvaluationResult EvaluateBid(SpatialBidEvaluationRequest request)
    {
        var zone = _zones.TryGetValue(request.ZoneId, out var z) ? z : _zones.Values.First();

        var shouldBid = request.OcclusionFactor <= (zone.LightingOcclusionTolerancePct / 100.0) &&
                        request.FrustumAngleDegrees < 65.0;

        var bidCpm = shouldBid ? Math.Round(zone.FloorCpmUsd * 1.25m, 2) : 0m;

        return new SpatialBidEvaluationResult(
            $"EVAL-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            zone.ZoneId,
            shouldBid,
            bidCpm,
            "https://cdn.sgrowing.io/spatial/textures/apex-holo-pbr.ktx2",
            "SHADER_PBR_LUMEN_ADAPTIVE_V2",
            16
        );
    }

    public SpatialViewAttestation VerifySpatialView(VerifySpatialViewRequest request)
    {
        var verified = request.ContinuousGazeDwellSeconds >= 2.0 && request.ViewingAngleDeviationDeg <= 45.0;

        var attestation = new SpatialViewAttestation(
            $"ATT-GAZE-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            request.BidId,
            verified,
            request.ContinuousGazeDwellSeconds,
            $"0x{Guid.NewGuid():N}"[..16],
            DateTime.UtcNow
        );

        _attestationsHistory.Add(attestation);
        return attestation;
    }
}
