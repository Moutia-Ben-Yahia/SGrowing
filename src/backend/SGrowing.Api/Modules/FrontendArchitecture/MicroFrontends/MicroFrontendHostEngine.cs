using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace SGrowing.Api.Modules.FrontendArchitecture.MicroFrontends;

/// <summary>
/// Service contract for the Micro-Frontend Host Engine managing Module Federation remotes and shared singletons.
/// </summary>
public interface IMicroFrontendHostEngine
{
    FederationManifestResponse GetFederationManifest(string? hostVersion = null);
    bool RegisterRemote(RemoteModuleMetadata metadata);
    bool UnregisterRemote(string moduleId);
    RemoteModuleMetadata? GetRemote(string moduleId);
    IReadOnlyList<RemoteModuleMetadata> GetAllRemotes();
    bool UpdateRemoteHealth(string moduleId, RemoteHealthStatus status, double latencyMs);
    bool ResolveSemVerMatch(string requestedVersionRange, string availableVersion);
    RemoteModuleMetadata? ResolveCompatibleRemote(string moduleId, string semverConstraint);
    IReadOnlyDictionary<string, string> GetSharedDependencies();
}

/// <summary>
/// Thread-safe in-memory host engine for dynamic Module Federation remotes and semver negotiation.
/// </summary>
public sealed class MicroFrontendHostEngine : IMicroFrontendHostEngine
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, RemoteModuleMetadata> _remotes = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _sharedDependencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["@angular/core"] = "^22.0.0",
        ["@angular/common"] = "^22.0.0",
        ["rxjs"] = "^7.8.1",
        ["@microsoft/signalr"] = "^10.0.0",
        ["@sgrowing/core-ui"] = "^22.0.0"
    };

    public const string DefaultHostVersion = "22.0.0";

    public MicroFrontendHostEngine()
    {
        // Seed standard enterprise cockpit remotes
        var now = DateTimeOffset.UtcNow;
        var seedRemotes = new[]
        {
            new RemoteModuleMetadata(
                ModuleId: "@sgrowing/cockpit-commerce",
                RemoteEntryUrl: "https://cdn.sgrowing.io/mfe/commerce/22.4.0/remoteEntry.js",
                ExposedComponent: "CommerceModule",
                Version: "22.4.0",
                HealthStatus: RemoteHealthStatus.Healthy,
                LatencyMs: 12.5,
                LastPingUtc: now,
                FallbackComponentUrl: "https://cdn.sgrowing.io/mfe/commerce/fallbacks/commerce-resilient.js",
                Dependencies: ["@angular/core", "rxjs"]),
            new RemoteModuleMetadata(
                ModuleId: "@sgrowing/cockpit-fintech",
                RemoteEntryUrl: "https://cdn.sgrowing.io/mfe/fintech/22.2.1/remoteEntry.js",
                ExposedComponent: "FintechModule",
                Version: "22.2.1",
                HealthStatus: RemoteHealthStatus.Healthy,
                LatencyMs: 18.0,
                LastPingUtc: now,
                FallbackComponentUrl: "https://cdn.sgrowing.io/mfe/fintech/fallbacks/fintech-resilient.js",
                Dependencies: ["@angular/core", "rxjs"]),
            new RemoteModuleMetadata(
                ModuleId: "@sgrowing/cockpit-agents",
                RemoteEntryUrl: "https://cdn.sgrowing.io/mfe/agents/22.5.0/remoteEntry.js",
                ExposedComponent: "AgentsModule",
                Version: "22.5.0",
                HealthStatus: RemoteHealthStatus.Healthy,
                LatencyMs: 24.2,
                LastPingUtc: now,
                FallbackComponentUrl: "https://cdn.sgrowing.io/mfe/agents/fallbacks/agents-resilient.js",
                Dependencies: ["@angular/core", "rxjs", "@microsoft/signalr"]),
            new RemoteModuleMetadata(
                ModuleId: "@sgrowing/cockpit-voice",
                RemoteEntryUrl: "https://cdn.sgrowing.io/mfe/voice/22.1.0/remoteEntry.js",
                ExposedComponent: "VoiceModule",
                Version: "22.1.0",
                HealthStatus: RemoteHealthStatus.Healthy,
                LatencyMs: 15.7,
                LastPingUtc: now,
                FallbackComponentUrl: "https://cdn.sgrowing.io/mfe/voice/fallbacks/voice-resilient.js",
                Dependencies: ["@angular/core", "rxjs", "@microsoft/signalr"])
        };

        foreach (var remote in seedRemotes)
        {
            _remotes[remote.ModuleId] = remote;
        }
    }

    public FederationManifestResponse GetFederationManifest(string? hostVersion = null)
    {
        lock (_lock)
        {
            var remotes = _remotes.Values.ToList();
            var shared = new Dictionary<string, string>(_sharedDependencies, StringComparer.OrdinalIgnoreCase);
            return new FederationManifestResponse(
                HostVersion: hostVersion ?? DefaultHostVersion,
                Remotes: remotes,
                SharedDependencies: shared);
        }
    }

    public bool RegisterRemote(RemoteModuleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (string.IsNullOrWhiteSpace(metadata.ModuleId))
        {
            throw new ArgumentException("ModuleId cannot be null or whitespace.", nameof(metadata));
        }

        lock (_lock)
        {
            _remotes[metadata.ModuleId] = metadata;
            return true;
        }
    }

    public bool UnregisterRemote(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) return false;

        lock (_lock)
        {
            return _remotes.Remove(moduleId);
        }
    }

    public RemoteModuleMetadata? GetRemote(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) return null;

        lock (_lock)
        {
            return _remotes.TryGetValue(moduleId, out var metadata) ? metadata : null;
        }
    }

    public IReadOnlyList<RemoteModuleMetadata> GetAllRemotes()
    {
        lock (_lock)
        {
            return _remotes.Values.ToList();
        }
    }

    public bool UpdateRemoteHealth(string moduleId, RemoteHealthStatus status, double latencyMs)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) return false;

        lock (_lock)
        {
            if (!_remotes.TryGetValue(moduleId, out var current))
            {
                return false;
            }

            var updated = current with
            {
                HealthStatus = status,
                LatencyMs = Math.Max(0, latencyMs),
                LastPingUtc = DateTimeOffset.UtcNow
            };
            _remotes[moduleId] = updated;
            return true;
        }
    }

    public IReadOnlyDictionary<string, string> GetSharedDependencies()
    {
        lock (_lock)
        {
            return new Dictionary<string, string>(_sharedDependencies, StringComparer.OrdinalIgnoreCase);
        }
    }

    public RemoteModuleMetadata? ResolveCompatibleRemote(string moduleId, string semverConstraint)
    {
        var remote = GetRemote(moduleId);
        if (remote is null) return null;

        return ResolveSemVerMatch(semverConstraint, remote.Version) ? remote : null;
    }

    /// <summary>
    /// Resolves npm-compatible SemVer ranges (^22.0.0, ~22.1.0, exact, or wildcard).
    /// </summary>
    public bool ResolveSemVerMatch(string requestedVersionRange, string availableVersion)
    {
        if (string.IsNullOrWhiteSpace(requestedVersionRange) || requestedVersionRange == "*" || requestedVersionRange.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(availableVersion))
        {
            return false;
        }

        if (!TryParseSemVer(availableVersion, out var available))
        {
            return false;
        }

        var trimmed = requestedVersionRange.Trim();

        // Caret range ^X.Y.Z
        if (trimmed.StartsWith('^'))
        {
            if (!TryParseSemVer(trimmed[1..], out var requested)) return false;

            if (requested.Major > 0)
            {
                // Major must match; minor/patch must be >= requested
                if (available.Major != requested.Major) return false;
                if (available.Minor < requested.Minor) return false;
                if (available.Minor == requested.Minor && available.Patch < requested.Patch) return false;
                return true;
            }
            if (requested.Minor > 0)
            {
                // 0.Y.Z: minor must match, patch >= requested
                if (available.Major != 0 || available.Minor != requested.Minor) return false;
                return available.Patch >= requested.Patch;
            }
            // 0.0.Z: exact patch match
            return available.Major == 0 && available.Minor == 0 && available.Patch == requested.Patch;
        }

        // Tilde range ~X.Y.Z
        if (trimmed.StartsWith('~'))
        {
            if (!TryParseSemVer(trimmed[1..], out var requested)) return false;
            if (available.Major != requested.Major || available.Minor != requested.Minor) return false;
            return available.Patch >= requested.Patch;
        }

        // Greater than or equal >=X.Y.Z
        if (trimmed.StartsWith(">="))
        {
            if (!TryParseSemVer(trimmed[2..], out var requested)) return false;
            return CompareSemVer(available, requested) >= 0;
        }

        // Equality =X.Y.Z or X.Y.Z
        var cleanExact = trimmed.TrimStart('=');
        if (TryParseSemVer(cleanExact, out var exact))
        {
            return available.Major == exact.Major &&
                   available.Minor == exact.Minor &&
                   available.Patch == exact.Patch;
        }

        return false;
    }

    private static bool TryParseSemVer(string raw, out (int Major, int Minor, int Patch) version)
    {
        version = (0, 0, 0);
        var match = Regex.Match(raw.Trim(), @"^v?(\d+)(?:\.(\d+))?(?:\.(\d+))?");
        if (!match.Success) return false;

        var major = int.Parse(match.Groups[1].Value);
        var minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        var patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

        version = (major, minor, patch);
        return true;
    }

    private static int CompareSemVer((int Major, int Minor, int Patch) a, (int Major, int Minor, int Patch) b)
    {
        if (a.Major != b.Major) return a.Major.CompareTo(b.Major);
        if (a.Minor != b.Minor) return a.Minor.CompareTo(b.Minor);
        return a.Patch.CompareTo(b.Patch);
    }
}
