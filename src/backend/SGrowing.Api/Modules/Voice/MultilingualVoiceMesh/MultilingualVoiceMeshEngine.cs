using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.MultilingualVoiceMesh;

public sealed class MultilingualVoiceMeshEngine
{
    private readonly ConcurrentDictionary<string, LiveTranslationSession> _sessions = new();
    private readonly ConcurrentDictionary<string, VoiceCloneProfile> _profiles = new();
    private readonly ConcurrentBag<VoiceCloneReceipt> _receiptsHistory = new();

    public MultilingualVoiceMeshEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var p1 = new VoiceCloneProfile(
            "VCL-ALEX-01",
            "Alexandre Dubois (VP Sales EMEA)",
            "0x99AABBCC881122",
            124.0,
            0.982,
            DateTime.UtcNow.AddDays(-10)
        );

        var p2 = new VoiceCloneProfile(
            "VCL-YUKI-02",
            "Yuki Tanaka (Head of Partnerships APAC)",
            "0x77665544332211",
            195.0,
            0.978,
            DateTime.UtcNow.AddDays(-5)
        );

        _profiles[p1.ProfileId] = p1;
        _profiles[p2.ProfileId] = p2;

        var s1 = new LiveTranslationSession(
            "TRN-SES-401",
            "fr-FR",
            "en-US",
            p1.SpeakerName,
            168.0,
            450,
            "STREAMING_ACTIVE"
        );

        var s2 = new LiveTranslationSession(
            "TRN-SES-402",
            "ja-JP",
            "en-US",
            p2.SpeakerName,
            185.0,
            320,
            "STREAMING_ACTIVE"
        );

        _sessions[s1.SessionId] = s1;
        _sessions[s2.SessionId] = s2;
    }

    public MultilingualVoiceOverview GetOverview()
    {
        var sessionList = _sessions.Values.ToList();
        var profileList = _profiles.Values.ToList();

        return new MultilingualVoiceOverview(
            sessionList.Count,
            42,
            176.5,
            0.98,
            sessionList,
            profileList
        );
    }

    public TranslatedAudioChunkResult TranslateAudioChunk(TranslateAudioChunkRequest request)
    {
        var session = _sessions.TryGetValue(request.SessionId, out var s) ? s : _sessions.Values.First();

        var updated = session with { ChunksTranslated = session.ChunksTranslated + 1 };
        _sessions[session.SessionId] = updated;

        return new TranslatedAudioChunkResult(
            session.SessionId,
            "T1BVUzQ4a1RyYW5zbGF0ZWRDaHVuazk5OA==",
            "Nous pouvons déployer l'architecture souveraine sur votre cloud.",
            "We can deploy the sovereign architecture onto your dedicated cloud.",
            165,
            98.2
        );
    }

    public VoiceCloneReceipt RegisterVoiceClone(RegisterVoiceCloneRequest request)
    {
        var profileId = $"VCL-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var digest = $"0x{Guid.NewGuid():N}"[..16];

        var profile = new VoiceCloneProfile(
            profileId,
            request.SpeakerName,
            digest,
            140.0,
            0.985,
            DateTime.UtcNow
        );

        _profiles[profileId] = profile;

        var receipt = new VoiceCloneReceipt(
            profileId,
            request.SpeakerName,
            "TIMBRE_EMBEDDING_REGISTERED",
            digest,
            DateTime.UtcNow
        );

        _receiptsHistory.Add(receipt);
        return receipt;
    }
}
