using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.VoiceDialect;

public sealed class VoiceDialectEngine
{
    private readonly ConcurrentDictionary<string, RegionalDialectProfileRecord> _profiles = new();
    private readonly ConcurrentBag<AdaptedPhraseRecord> _adaptations = new();

    public VoiceDialectEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var d1 = new RegionalDialectProfileRecord(
            "en-US-TEXAS",
            "US Southern & Texas Enterprise",
            "Warm drawl, unhurried cadence, cordial respectful address",
            -0.8,
            0.94,
            new[] { "Sounds great -> Much obliged, we can sure get that handled for y'all", "Let's review the agreement -> Let's take a look under the hood together" }
        );

        var d2 = new RegionalDialectProfileRecord(
            "en-GB-SCOTTISH",
            "UK Scotland & Northern Enterprise",
            "Crisp rhotic cadence, direct concise phrasing, high transparency",
            0.5,
            1.02,
            new[] { "Understood -> Right then, fair enough", "We can finalize today -> Let's get this sorted straight away" }
        );

        _profiles[d1.DialectCode] = d1;
        _profiles[d2.DialectCode] = d2;

        var a1 = new AdaptedPhraseRecord(
            "ADP-001",
            d1.DialectCode,
            "We are ready to proceed with the contract.",
            "We're ready to get this across the finish line for y'all, much obliged.",
            0.982,
            DateTime.UtcNow.AddMinutes(-30)
        );

        _adaptations.Add(a1);
    }

    public VoiceDialectOverview GetOverview()
    {
        var profList = _profiles.Values.ToList();
        var adaptList = _adaptations.ToList();
        var avgScore = adaptList.Count > 0 ? adaptList.Average(a => a.NaturalnessScore) : 0.98;

        return new VoiceDialectOverview(
            profList.Count,
            adaptList.Count,
            avgScore,
            profList,
            adaptList
        );
    }

    public AdaptedPhraseRecord AdaptPhrase(AdaptPhraseRequest request)
    {
        var adaptId = $"ADP-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var profile = _profiles.GetValueOrDefault(request.DialectCode);

        var adapted = request.DialectCode switch
        {
            "en-US-TEXAS" => $"Much obliged, {request.StandardPhrase.ToLowerInvariant()} right on schedule for y'all.",
            "en-GB-SCOTTISH" => $"Right then, {request.StandardPhrase.ToLowerInvariant()} sorted straight away.",
            _ => $"Certainly, {request.StandardPhrase}"
        };

        var record = new AdaptedPhraseRecord(
            adaptId,
            request.DialectCode,
            request.StandardPhrase,
            adapted,
            0.985,
            DateTime.UtcNow
        );

        _adaptations.Add(record);
        return record;
    }

    public RegionalDialectProfileRecord RegisterProfile(RegisterDialectProfileRequest request)
    {
        var record = new RegionalDialectProfileRecord(
            request.DialectCode,
            request.RegionName,
            request.LinguisticCadenceDescription,
            request.PitchAdjustmentSemiTones,
            request.SpeechRateMultiplier,
            request.LocalIdiomMappings
        );

        _profiles[request.DialectCode] = record;
        return record;
    }
}
