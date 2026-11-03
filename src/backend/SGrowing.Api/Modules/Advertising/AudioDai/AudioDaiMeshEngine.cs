using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Advertising.AudioDai;

public class AudioDaiMeshEngine
{
    private readonly ConcurrentDictionary<string, AudioAdSlot> _slots = new();
    private readonly ConcurrentBag<AudioAttributionEvent> _attributions = new();

    public AudioDaiMeshEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var s1 = new AudioAdSlot(
            "SLOT-POD-001",
            "The SaaS Growth Syndicate",
            "Ep. 214: Zero-Trust Telephony at Scale",
            "MID_ROLL",
            30,
            "OPUS",
            "AWT-98B2F3A10C",
            42.50m,
            "US-SFO",
            "SERVING"
        );

        var s2 = new AudioAdSlot(
            "SLOT-POD-002",
            "Enterprise Architecture Daily",
            "Ep. 88: High-Throughput Stream Processing",
            "PRE_ROLL",
            15,
            "AAC",
            "AWT-47C1E9D02A",
            36.00m,
            "US-NYC",
            "SERVING"
        );

        _slots[s1.SlotId] = s1;
        _slots[s2.SlotId] = s2;

        _attributions.Add(new AudioAttributionEvent(
            "ATTR-001",
            "SLOT-POD-001",
            "GROWTH2026",
            "CARPLAY",
            "DEMO_BOOKED",
            18500.00m,
            DateTime.UtcNow.AddHours(-3)
        ));

        _attributions.Add(new AudioAttributionEvent(
            "ATTR-002",
            "SLOT-POD-002",
            "ENTERPRISE30",
            "MOBILE_HEADPHONES",
            "DIRECT_SIGNUP",
            9200.00m,
            DateTime.UtcNow.AddHours(-1)
        ));
    }

    public AudioDaiOverview GetOverview()
    {
        var slotsList = _slots.Values.ToList();
        var attrList = _attributions.ToList();

        decimal totalSpend = slotsList.Count * 1450.00m;
        decimal totalAttributedRev = attrList.Sum(a => a.AttributedRevenueUsd);
        double roas = totalSpend > 0 ? (double)(totalAttributedRev / totalSpend) : 0;

        return new AudioDaiOverview(
            345000,
            96.8,
            totalSpend,
            totalAttributedRev,
            Math.Round(roas, 2),
            slotsList,
            attrList
        );
    }

    public AudioAdSlot InsertAudioSlot(InsertAudioAdRequest request)
    {
        var slotId = $"SLOT-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var watermark = $"AWT-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slotId + request.ShowName)))[..10]}";

        var slot = new AudioAdSlot(
            slotId,
            request.ShowName,
            request.EpisodeTitle,
            request.PositionType,
            request.DurationSeconds,
            "OPUS",
            watermark,
            request.CpmBidUsd,
            request.TargetDma,
            "INSERTED"
        );

        _slots[slotId] = slot;
        return slot;
    }

    public AudioAttributionEvent RecordAttribution(RecordAudioAttributionRequest request)
    {
        var attrId = $"ATTR-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var attribution = new AudioAttributionEvent(
            attrId,
            request.SlotId,
            request.PromoCode,
            request.DeviceType,
            request.ConversionChannel,
            request.AttributedRevenueUsd,
            DateTime.UtcNow
        );

        _attributions.Add(attribution);
        return attribution;
    }
}
