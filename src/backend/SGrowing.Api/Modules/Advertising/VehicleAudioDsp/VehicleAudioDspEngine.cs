using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Advertising.VehicleAudioDsp;

public interface IVehicleAudioDspEngine
{
    VehicleAudioDspOverview GetOverview();
    VehicleAdSlotBid BidInCabinAudioSlot(string vehicleMakeModel, CommuteContextType context, double speedKmh, double lat, double lon);
    VehiclePlayoutAttestation LogPlayoutAttestation(string bidId, string vehicleHardwareId, int durationSeconds, double volumeDb);
}

public sealed class VehicleAudioDspEngine : IVehicleAudioDspEngine
{
    private readonly ConcurrentDictionary<string, VehicleAdSlotBid> _bids = new();
    private readonly ConcurrentDictionary<string, VehiclePlayoutAttestation> _attestations = new();

    public VehicleAudioDspEngine()
    {
        SeedInitialState();
    }

    private void SeedInitialState()
    {
        var bid1 = new VehicleAdSlotBid(
            "BID-VEH-001",
            "Tesla Model Y (Infotainment 2026.4)",
            CommuteContextType.MorningRushCongestion,
            18.5,
            37.7749,
            -122.4194,
            "CAMP-COFFEE-DRIVETHRU",
            WinningBidCpmUsd: 26.50,
            "https://cdn.sgrowing.io/audio/peets-coffee-drive-thru-15s.aac",
            DateTime.UtcNow.AddMinutes(-8)
        );
        _bids.TryAdd(bid1.BidId, bid1);

        var att1 = new VehiclePlayoutAttestation(
            "ATT-VEH-001",
            bid1.BidId,
            "VIN-HARDWARE-99214A",
            15,
            -14.0, // EBU R128 loudness standard
            true,
            true
        );
        _attestations.TryAdd(att1.AttestationId, att1);
    }

    public VehicleAudioDspOverview GetOverview()
    {
        var bids = _bids.Values.OrderByDescending(b => b.AuctionTimestampUtc).Take(10).ToList();
        var atts = _attestations.Values.Take(10).ToList();

        return new VehicleAudioDspOverview(
            ConnectedVehiclesActiveCount: 4210,
            TotalAudioImpressionsDeliveredToday: 18920,
            AverageInCabinCpmUsd: 24.80,
            CommuteConversionLiftPct: 38.2,
            RecentBids: bids,
            RecentAttestations: atts
        );
    }

    public VehicleAdSlotBid BidInCabinAudioSlot(string vehicleMakeModel, CommuteContextType context, double speedKmh, double lat, double lon)
    {
        double cpm = context switch
        {
            CommuteContextType.DriveThruProximity => 32.00,
            CommuteContextType.MorningRushCongestion => 28.50,
            CommuteContextType.EveningCommuteReturn => 26.00,
            _ => 21.00
        };

        var bid = new VehicleAdSlotBid(
            $"BID-VEH-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            vehicleMakeModel,
            context,
            speedKmh,
            lat,
            lon,
            "CAMP-AUTOMOTIVE-PREMIUM",
            Math.Round(cpm, 2),
            "https://cdn.sgrowing.io/audio/ambient-vehicle-spot-15s.aac",
            DateTime.UtcNow
        );

        _bids.TryAdd(bid.BidId, bid);
        return bid;
    }

    public VehiclePlayoutAttestation LogPlayoutAttestation(string bidId, string vehicleHardwareId, int durationSeconds, double volumeDb)
    {
        var att = new VehiclePlayoutAttestation(
            $"ATT-VEH-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            bidId,
            vehicleHardwareId,
            durationSeconds,
            volumeDb,
            PassengerPresenceDetected: true,
            IsPlayoutVerified: true
        );

        _attestations.TryAdd(att.AttestationId, att);
        return att;
    }
}
