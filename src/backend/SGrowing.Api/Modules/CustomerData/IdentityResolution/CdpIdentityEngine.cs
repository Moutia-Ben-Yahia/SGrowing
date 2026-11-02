using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.CustomerData.IdentityResolution;

public interface ICdpIdentityEngine
{
    StitchIdentityResult StitchIdentity(StitchIdentityRequest request);
    ResolvedCustomerProfile? GetProfileGraph(string tenantId, string canonicalProfileId);
    ResolvedCustomerProfile? ResolveIdentifier(string tenantId, IdentityIdentifier identifier);
    bool IngestSession(string tenantId, VisitorSessionRecord session);
    List<VisitorSessionRecord> GetSessionsForProfile(string tenantId, string canonicalProfileId);
}

public class CdpIdentityEngine : ICdpIdentityEngine
{
    private class ProfileInternalState
    {
        public string CanonicalProfileId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public ConcurrentDictionary<string, IdentityIdentifier> Identifiers { get; } = new();
        public ConcurrentBag<IdentityLink> Links { get; } = new();
        public ConcurrentBag<string> AssociatedSessionIds { get; } = new();
        public ConcurrentDictionary<string, string> UnifiedTraits { get; } = new();
        public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastActiveUtc { get; set; } = DateTime.UtcNow;
    }

    // Key: tenantId:type:value -> CanonicalProfileId
    private readonly ConcurrentDictionary<string, string> _identifierIndex = new();

    // Key: tenantId:profileId -> ProfileInternalState
    private readonly ConcurrentDictionary<string, ProfileInternalState> _profiles = new();

    // Key: tenantId:sessionId -> VisitorSessionRecord
    private readonly ConcurrentDictionary<string, VisitorSessionRecord> _sessions = new();

    public StitchIdentityResult StitchIdentity(StitchIdentityRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.AnonymousId))
            throw new ArgumentException("AnonymousId is required.", nameof(request));
        if (request.KnownIdentifier == null || string.IsNullOrWhiteSpace(request.KnownIdentifier.Value))
            throw new ArgumentException("KnownIdentifier is required.", nameof(request));

        var normalizedKnownValue = NormalizeIdentifierValue(request.KnownIdentifier.Type, request.KnownIdentifier.Value);
        var normalizedKnown = new IdentityIdentifier(request.KnownIdentifier.Type, normalizedKnownValue);
        var anonIdentifier = new IdentityIdentifier(IdentityType.AnonymousCookie, request.AnonymousId.Trim());

        var anonKey = BuildIndexKey(request.TenantId, anonIdentifier.Type, anonIdentifier.Value);
        var knownKey = BuildIndexKey(request.TenantId, normalizedKnown.Type, normalizedKnown.Value);

        _identifierIndex.TryGetValue(anonKey, out var anonProfileId);
        _identifierIndex.TryGetValue(knownKey, out var knownProfileId);

        var now = DateTime.UtcNow;
        bool wasMerged = false;
        string canonicalId;
        ProfileInternalState targetState;

        if (anonProfileId != null && knownProfileId != null && anonProfileId != knownProfileId)
        {
            // Both exist on different profiles: merge anonProfile into knownProfile
            var knownState = GetOrCreateProfile(request.TenantId, knownProfileId);
            var anonState = GetOrCreateProfile(request.TenantId, anonProfileId);

            targetState = knownState;
            canonicalId = knownProfileId;
            wasMerged = true;

            // Merge identifiers
            foreach (var kvp in anonState.Identifiers)
            {
                targetState.Identifiers.TryAdd(kvp.Key, kvp.Value);
                var remappedKey = BuildIndexKey(request.TenantId, kvp.Value.Type, kvp.Value.Value);
                _identifierIndex[remappedKey] = canonicalId;
            }

            // Merge links
            foreach (var link in anonState.Links)
            {
                targetState.Links.Add(link);
            }

            // Merge sessions
            foreach (var sess in anonState.AssociatedSessionIds)
            {
                if (!targetState.AssociatedSessionIds.Contains(sess))
                {
                    targetState.AssociatedSessionIds.Add(sess);
                }
            }

            // Merge traits
            foreach (var trait in anonState.UnifiedTraits)
            {
                targetState.UnifiedTraits[trait.Key] = trait.Value;
            }

            if (anonState.FirstSeenUtc < targetState.FirstSeenUtc)
            {
                targetState.FirstSeenUtc = anonState.FirstSeenUtc;
            }

            _profiles.TryRemove($"{request.TenantId}:{anonProfileId}", out _);
        }
        else if (knownProfileId != null)
        {
            targetState = GetOrCreateProfile(request.TenantId, knownProfileId);
            canonicalId = knownProfileId;
        }
        else if (anonProfileId != null)
        {
            targetState = GetOrCreateProfile(request.TenantId, anonProfileId);
            canonicalId = anonProfileId;
        }
        else
        {
            canonicalId = $"prof_{Guid.NewGuid():N}";
            targetState = GetOrCreateProfile(request.TenantId, canonicalId);
        }

        // Add both identifiers to profile
        var anonIdKey = $"{anonIdentifier.Type}:{anonIdentifier.Value}";
        var knownIdKey = $"{normalizedKnown.Type}:{normalizedKnown.Value}";

        targetState.Identifiers.TryAdd(anonIdKey, anonIdentifier);
        targetState.Identifiers.TryAdd(knownIdKey, normalizedKnown);

        _identifierIndex[anonKey] = canonicalId;
        _identifierIndex[knownKey] = canonicalId;

        // Session association
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            var sid = request.SessionId.Trim();
            if (!targetState.AssociatedSessionIds.Contains(sid))
            {
                targetState.AssociatedSessionIds.Add(sid);
            }
        }

        // Additional traits
        if (request.AdditionalTraits != null)
        {
            foreach (var kvp in request.AdditionalTraits)
            {
                targetState.UnifiedTraits[kvp.Key] = kvp.Value;
            }
        }

        targetState.LastActiveUtc = now;

        var confidence = CalculateConfidence(normalizedKnown.Type);
        var linkObj = new IdentityLink(
            SourceIdentifier: anonIdentifier,
            TargetIdentifier: normalizedKnown,
            ConfidenceScore: confidence,
            MatchReason: $"Deterministic stitch of anonymous cookie {anonIdentifier.Value} to {normalizedKnown.Type} {normalizedKnown.Value}",
            LinkedAtUtc: now
        );
        targetState.Links.Add(linkObj);

        var mergedSessions = targetState.AssociatedSessionIds.ToList();

        return new StitchIdentityResult(
            Success: true,
            CanonicalProfileId: canonicalId,
            WasMerged: wasMerged,
            LinkedIdentifiersCount: targetState.Identifiers.Count,
            OverallConfidence: confidence,
            MergedSessionIds: mergedSessions,
            AuditMessage: wasMerged
                ? $"Successfully merged anonymous profile {anonProfileId} into canonical profile {canonicalId}."
                : $"Successfully stitched anonymous identity to canonical profile {canonicalId}.",
            ProcessedAtUtc: now
        );
    }

    public ResolvedCustomerProfile? GetProfileGraph(string tenantId, string canonicalProfileId)
    {
        var key = $"{tenantId}:{canonicalProfileId}";
        if (!_profiles.TryGetValue(key, out var state))
        {
            return null;
        }

        return new ResolvedCustomerProfile(
            CanonicalProfileId: state.CanonicalProfileId,
            TenantId: state.TenantId,
            Identifiers: state.Identifiers.Values.ToList(),
            Links: state.Links.ToList(),
            AssociatedSessionIds: state.AssociatedSessionIds.ToList(),
            UnifiedTraits: new Dictionary<string, string>(state.UnifiedTraits),
            FirstSeenUtc: state.FirstSeenUtc,
            LastActiveUtc: state.LastActiveUtc
        );
    }

    public ResolvedCustomerProfile? ResolveIdentifier(string tenantId, IdentityIdentifier identifier)
    {
        var normalizedValue = NormalizeIdentifierValue(identifier.Type, identifier.Value);
        var indexKey = BuildIndexKey(tenantId, identifier.Type, normalizedValue);

        if (_identifierIndex.TryGetValue(indexKey, out var profileId))
        {
            return GetProfileGraph(tenantId, profileId);
        }

        return null;
    }

    public bool IngestSession(string tenantId, VisitorSessionRecord session)
    {
        var sessionKey = $"{tenantId}:{session.SessionId}";
        _sessions[sessionKey] = session;

        // If anonymous ID already has a profile, attach session
        var anonKey = BuildIndexKey(tenantId, IdentityType.AnonymousCookie, session.AnonymousId);
        if (_identifierIndex.TryGetValue(anonKey, out var profileId))
        {
            var profileKey = $"{tenantId}:{profileId}";
            if (_profiles.TryGetValue(profileKey, out var state))
            {
                if (!state.AssociatedSessionIds.Contains(session.SessionId))
                {
                    state.AssociatedSessionIds.Add(session.SessionId);
                }
                state.LastActiveUtc = session.EndedAtUtc ?? session.StartedAtUtc;
            }
        }

        return true;
    }

    public List<VisitorSessionRecord> GetSessionsForProfile(string tenantId, string canonicalProfileId)
    {
        var profile = GetProfileGraph(tenantId, canonicalProfileId);
        if (profile == null) return new List<VisitorSessionRecord>();

        var results = new List<VisitorSessionRecord>();
        foreach (var sessionId in profile.AssociatedSessionIds)
        {
            var sessionKey = $"{tenantId}:{sessionId}";
            if (_sessions.TryGetValue(sessionKey, out var session))
            {
                results.Add(session);
            }
        }

        return results;
    }

    private ProfileInternalState GetOrCreateProfile(string tenantId, string profileId)
    {
        var key = $"{tenantId}:{profileId}";
        return _profiles.GetOrAdd(key, _ => new ProfileInternalState
        {
            CanonicalProfileId = profileId,
            TenantId = tenantId,
            FirstSeenUtc = DateTime.UtcNow,
            LastActiveUtc = DateTime.UtcNow
        });
    }

    private static string BuildIndexKey(string tenantId, IdentityType type, string value)
    {
        return $"{tenantId}:{type}:{value.Trim()}";
    }

    private static string NormalizeIdentifierValue(IdentityType type, string value)
    {
        var trimmed = value.Trim();
        return type switch
        {
            IdentityType.Email => trimmed.ToLowerInvariant(),
            IdentityType.PhoneNumber => trimmed.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", ""),
            _ => trimmed
        };
    }

    private static double CalculateConfidence(IdentityType type)
    {
        return type switch
        {
            IdentityType.CustomerId or IdentityType.CrmContactId or IdentityType.Email => 1.0,
            IdentityType.PhoneNumber => 0.95,
            IdentityType.DeviceId => 0.85,
            _ => 0.75
        };
    }
}
