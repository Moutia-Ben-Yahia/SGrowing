using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Advertising.DoohBidding;

public class DoohBiddingMeshEngine
{
    private readonly ConcurrentDictionary<string, DoohBillboardPlacement> _placements = new();
    private readonly ConcurrentBag<MobileFootTrafficAttribution> _attributions = new();

    public DoohBiddingMeshEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var p1 = new DoohBillboardPlacement(
            "DOOH-NYC-001",
            "Times Square Mega-Tower Spectacular",
            "NYC_TIMES_SQUARE",
            "8K_SURROUND",
            15,
            42000.0,
            2.85,
            68.50m,
            "SERVING"
        );

        var p2 = new DoohBillboardPlacement(
            "DOOH-SFO-002",
            "SFO International Terminal 2 Executive Concourse",
            "SFO_AIRPORT_T2",
            "4K_UHD",
            10,
            12500.0,
            1.95,
            45.00m,
            "SERVING"
        );

        _placements[p1.PlacementId] = p1;
        _placements[p2.PlacementId] = p2;

        _attributions.Add(new MobileFootTrafficAttribution(
            "ATTR-FT-001",
            "DOOH-NYC-001",
            "MAID-GRP-89B10",
            "Manhattan Flagship Store (5th Ave)",
            1420,
            24.5,
            DateTime.UtcNow.AddHours(-6)
        ));

        _attributions.Add(new MobileFootTrafficAttribution(
            "ATTR-FT-002",
            "DOOH-SFO-002",
            "MAID-GRP-44A90",
            "San Francisco Experience Center (Market St)",
            680,
            18.2,
            DateTime.UtcNow.AddHours(-2)
        ));
    }

    public DoohBiddingOverview GetOverview()
    {
        var placementsList = _placements.Values.ToList();
        var attrList = _attributions.ToList();

        double avgMultiplier = placementsList.Count > 0
            ? Math.Round(placementsList.Average(p => p.DynamicAudienceMultiplier), 2)
            : 1.0;

        int totalVisits = attrList.Sum(a => a.AttributedStoreVisits);
        decimal totalSpend = placementsList.Sum(p => p.ClearingCpmUsd * 250.0m);

        return new DoohBiddingOverview(
            placementsList.Count,
            avgMultiplier,
            totalVisits,
            totalSpend,
            placementsList,
            attrList
        );
    }

    public DoohBillboardPlacement SubmitBid(SubmitDoohBidRequest request)
    {
        var placementId = $"DOOH-{request.GeoLocationDma[..3]}-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        double multiplier = request.GeoLocationDma.Contains("TIMES_SQUARE") ? 3.10 : 2.10;
        decimal clearingCpm = Math.Min(request.MaxBidCpmUsd, request.MaxBidCpmUsd * 0.92m);

        var placement = new DoohBillboardPlacement(
            placementId,
            request.VenueName,
            request.GeoLocationDma,
            request.ScreenResolution,
            request.LoopDurationSeconds,
            25000.0,
            multiplier,
            clearingCpm,
            "BID_WON"
        );

        _placements[placementId] = placement;
        return placement;
    }

    public MobileFootTrafficAttribution RecordFootTraffic(RecordFootTrafficRequest request)
    {
        var attrId = $"ATTR-FT-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var attribution = new MobileFootTrafficAttribution(
            attrId,
            request.PlacementId,
            $"MAID-GRP-{Guid.NewGuid():N}"[..14].ToUpperInvariant(),
            request.PhysicalStoreLocation,
            request.AttributedStoreVisits,
            request.LiftOverBaselinePct,
            DateTime.UtcNow
        );

        _attributions.Add(attribution);
        return attribution;
    }
}
