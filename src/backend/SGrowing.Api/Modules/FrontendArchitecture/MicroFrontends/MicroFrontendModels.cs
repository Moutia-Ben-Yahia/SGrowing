using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.FrontendArchitecture.MicroFrontends;

/// <summary>
/// States for the remote micro-app circuit breaker.
/// </summary>
public enum CircuitState
{
    Closed,
    HalfOpen,
    Open
}

/// <summary>
/// Progressive hydration strategies for Edge SSR and Module Federation.
/// </summary>
public enum HydrationStrategy
{
    Immediate,
    OnVisible,
    OnIdle
}

/// <summary>
/// Operational health status of a federated remote micro-app.
/// </summary>
public enum RemoteHealthStatus
{
    Healthy,
    Degraded,
    CircuitOpen
}

/// <summary>
/// Metadata describing a dynamic Module Federation remote entry.
/// </summary>
public sealed record RemoteModuleMetadata(
    string ModuleId,
    string RemoteEntryUrl,
    string ExposedComponent,
    string Version,
    RemoteHealthStatus HealthStatus,
    double LatencyMs,
    DateTimeOffset LastPingUtc,
    string? FallbackComponentUrl = null,
    IReadOnlyList<string>? Dependencies = null);

/// <summary>
/// Metrics and telemetry for a remote micro-app circuit breaker instance.
/// </summary>
public sealed record CircuitBreakerMetrics(
    string ModuleId,
    CircuitState State,
    int ConsecutiveFailures,
    int FailureThreshold,
    int ResetTimeoutSeconds,
    DateTimeOffset? LastFailureUtc,
    DateTimeOffset LastStateChangeUtc,
    int TotalTripCount = 0,
    int TotalSuccessProbes = 0);

/// <summary>
/// Cryptographically verified cross-app event for federated sub-application messaging.
/// </summary>
public sealed record CrossAppEvent(
    Guid EventId,
    string SourceAppId,
    string TargetAppId,
    string EventType,
    string PayloadJson,
    string Nonce,
    DateTimeOffset TimestampUtc,
    string Signature);

/// <summary>
/// Edge SSR hydration manifest defining pre-rendered markup, critical CSS, and progressive hydration rules.
/// </summary>
public sealed record EdgeSsrManifest(
    string RoutePath,
    string PreRenderedHtml,
    string CriticalCss,
    IReadOnlyList<string> IntersectingComponents,
    HydrationStrategy Strategy,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyDictionary<string, HydrationStrategy>? ComponentHydrationMap = null,
    string? HostBundleUrl = null);

/// <summary>
/// Unified Module Federation manifest delivered to the Angular host shell.
/// </summary>
public sealed record FederationManifestResponse(
    string HostVersion,
    IReadOnlyList<RemoteModuleMetadata> Remotes,
    IReadOnlyDictionary<string, string> SharedDependencies);

/// <summary>
/// Degraded fallback view rendered when a remote micro-app's circuit breaker is open.
/// </summary>
public sealed record FallbackViewRepresentation(
    string ModuleId,
    string FallbackHtml,
    string FallbackComponentUrl,
    bool IsFallbackActive,
    string Reason,
    DateTimeOffset EmittedAtUtc,
    int RetryAfterSeconds);

/// <summary>
/// Result of publishing an event across federated micro-apps.
/// </summary>
public sealed record EventPublishResult(
    bool IsSuccess,
    Guid EventId,
    int DeliveredCount,
    string? ErrorCode = null,
    string? ErrorMessage = null);

/// <summary>
/// Live metrics for the federated cross-app event broker.
/// </summary>
public sealed record EventBusMetrics(
    long TotalEventsPublished,
    long TotalEventsDelivered,
    long TotalReplayAttacksBlocked,
    long TotalTamperedSignaturesBlocked,
    long TotalExpiredTimestampsBlocked,
    int ActiveSubscriptionsCount);

/// <summary>
/// Request payload to register or update a federated remote module.
/// </summary>
public sealed record RegisterRemoteRequest(
    string ModuleId,
    string RemoteEntryUrl,
    string ExposedComponent,
    string Version,
    string? FallbackComponentUrl = null);

/// <summary>
/// Request payload to force-trip a remote circuit breaker.
/// </summary>
public sealed record TripCircuitRequest(
    string ModuleId,
    string? Reason = null);

/// <summary>
/// Request payload to reset a remote circuit breaker.
/// </summary>
public sealed record ResetCircuitRequest(
    string ModuleId);

/// <summary>
/// Request payload to publish a cryptographically signed cross-app event.
/// </summary>
public sealed record PublishCrossAppEventRequest(
    string SourceAppId,
    string TargetAppId,
    string EventType,
    string PayloadJson,
    string? Nonce = null,
    DateTimeOffset? TimestampUtc = null,
    string? Signature = null);

/// <summary>
/// Engine overall health report.
/// </summary>
public sealed record EngineHealthReport(
    string HostVersion,
    string Status,
    int ActiveRemotesCount,
    int OpenCircuitsCount,
    int HalfOpenCircuitsCount,
    EventBusMetrics EventBus,
    DateTimeOffset TimestampUtc);
