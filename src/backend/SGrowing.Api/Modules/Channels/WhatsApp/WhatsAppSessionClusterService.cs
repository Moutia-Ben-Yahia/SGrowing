using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SGrowing.Core.Diagnostics;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public enum BaileysSessionState
{
    Disconnected = 0,
    PairingQrRequired = 1,
    Connected = 2,
    Reconnecting = 3,
    Banned = 4
}

public enum ClusterLoadBalanceStrategy
{
    RoundRobin = 0,
    LeastLoaded = 1,
    Random = 2
}

public record SessionStateTransitionLog(
    DateTime TimestampUtc,
    BaileysSessionState FromState,
    BaileysSessionState ToState,
    string Reason);

public class ClusterInstanceSession
{
    public Guid InstanceId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string InstanceName { get; init; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = "General"; // e.g., SalesDesk, SupportDesk, VipConcierge
    public BaileysSessionState State { get; set; } = BaileysSessionState.Disconnected;
    public DateTime? ConnectedAtUtc { get; set; }
    public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;
    public string? QrCodePayload { get; set; }
    public string? PairingCode { get; set; }
    public string? EncryptedAuthBlob { get; set; }
    public long TotalMessagesSent { get; set; }
    public long TotalMessagesReceived { get; set; }
    public int InFlightMessagesCount { get; set; }
    public int ReconnectAttempts { get; set; }
    public int PingLatencyMs { get; set; } = 25;
    public DateTime RegisteredAtUtc { get; init; } = DateTime.UtcNow;

    public static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMinutes(2);

    public bool IsHealthy => State == BaileysSessionState.Connected &&
                             (DateTime.UtcNow - LastHeartbeatUtc) <= HeartbeatTimeout;

    public List<SessionStateTransitionLog> History { get; } = [];
}

public record RegisterClusterInstanceRequest(
    string InstanceName,
    string PhoneNumber,
    string Role = "General",
    string? AuthCredentialsJson = null);

public record InstanceHealthDetail(
    string InstanceName,
    BaileysSessionState State,
    bool IsHealthy,
    int PingLatencyMs,
    string StatusMessage);

public record ClusterHealthCheckResult(
    Guid TenantId,
    int TotalInstances,
    int ConnectedCount,
    int ReconnectingCount,
    int BannedCount,
    int DisconnectedCount,
    IReadOnlyList<InstanceHealthDetail> Details,
    DateTime CheckedAtUtc);

public interface IWhatsAppSessionClusterService
{
    Task<ClusterInstanceSession> RegisterInstanceAsync(Guid tenantId, RegisterClusterInstanceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ClusterInstanceSession>> GetInstancesAsync(Guid tenantId, CancellationToken ct = default);
    Task<ClusterInstanceSession?> GetInstanceAsync(Guid tenantId, string instanceName, CancellationToken ct = default);
    Task<ClusterInstanceSession> TransitionStateAsync(Guid tenantId, string instanceName, BaileysSessionState newState, string? reason = null, CancellationToken ct = default);
    Task<ClusterInstanceSession?> SelectLoadBalancedInstanceAsync(Guid tenantId, string? targetRole = null, ClusterLoadBalanceStrategy strategy = ClusterLoadBalanceStrategy.RoundRobin, CancellationToken ct = default);
    Task<ClusterHealthCheckResult> RunHealthCheckWatchdogAsync(Guid tenantId, TimeSpan? staleThreshold = null, CancellationToken ct = default);
    string EncryptSessionPayload(string plaintext, string? customKey = null);
    string DecryptSessionPayload(string cipherText, string? customKey = null);
    Task<bool> RecordDispatchedMessageAsync(Guid tenantId, string instanceName, CancellationToken ct = default);
    Task<bool> RecordHeartbeatAsync(Guid tenantId, string instanceName, int pingLatencyMs = 25, CancellationToken ct = default);
    Task<bool> RemoveInstanceAsync(Guid tenantId, string instanceName, CancellationToken ct = default);
}

public class WhatsAppSessionClusterService : IWhatsAppSessionClusterService
{
    private readonly ConcurrentDictionary<(Guid TenantId, string InstanceName), ClusterInstanceSession> _instances = new();
    private readonly ILogger<WhatsAppSessionClusterService>? _logger;
    private long _roundRobinCounter;

    private static readonly byte[] DefaultMasterKeySalt = [
        0x53, 0x47, 0x72, 0x6F, 0x77, 0x69, 0x6E, 0x67,
        0x57, 0x68, 0x61, 0x74, 0x73, 0x41, 0x70, 0x70
    ];

    public WhatsAppSessionClusterService(ILogger<WhatsAppSessionClusterService>? logger = null)
    {
        _logger = logger;
    }

    public Task<ClusterInstanceSession> RegisterInstanceAsync(Guid tenantId, RegisterClusterInstanceRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InstanceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PhoneNumber);

        var key = (tenantId, request.InstanceName.Trim().ToLowerInvariant());
        string? encryptedBlob = null;
        if (!string.IsNullOrWhiteSpace(request.AuthCredentialsJson))
        {
            encryptedBlob = EncryptSessionPayload(request.AuthCredentialsJson);
        }

        var session = new ClusterInstanceSession
        {
            TenantId = tenantId,
            InstanceName = request.InstanceName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Role = string.IsNullOrWhiteSpace(request.Role) ? "General" : request.Role.Trim(),
            State = BaileysSessionState.Disconnected,
            EncryptedAuthBlob = encryptedBlob,
            LastHeartbeatUtc = DateTime.UtcNow
        };

        session.History.Add(new SessionStateTransitionLog(
            DateTime.UtcNow,
            BaileysSessionState.Disconnected,
            BaileysSessionState.Disconnected,
            "Instance registered in cluster"));

        _instances.AddOrUpdate(key, session, (_, existing) =>
        {
            existing.PhoneNumber = session.PhoneNumber;
            existing.Role = session.Role;
            if (encryptedBlob != null) existing.EncryptedAuthBlob = encryptedBlob;
            return existing;
        });

        _logger?.LogInformation("Registered WhatsApp cluster instance '{Name}' for tenant {TenantId} [Role: {Role}]",
            session.InstanceName, tenantId, session.Role);

        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<ClusterInstanceSession>> GetInstancesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _instances
            .Where(kvp => kvp.Key.TenantId == tenantId)
            .Select(kvp => kvp.Value)
            .OrderBy(i => i.InstanceName)
            .ToList();

        return Task.FromResult<IReadOnlyList<ClusterInstanceSession>>(list);
    }

    public Task<ClusterInstanceSession?> GetInstanceAsync(Guid tenantId, string instanceName, CancellationToken ct = default)
    {
        var key = (tenantId, instanceName.Trim().ToLowerInvariant());
        _instances.TryGetValue(key, out var instance);
        return Task.FromResult(instance);
    }

    public Task<ClusterInstanceSession> TransitionStateAsync(
        Guid tenantId,
        string instanceName,
        BaileysSessionState newState,
        string? reason = null,
        CancellationToken ct = default)
    {
        var key = (tenantId, instanceName.Trim().ToLowerInvariant());
        if (!_instances.TryGetValue(key, out var instance))
        {
            throw new KeyNotFoundException($"WhatsApp cluster instance '{instanceName}' not found for tenant {tenantId}.");
        }

        lock (instance)
        {
            var oldState = instance.State;

            // Enforce Banned terminal state rules unless explicitly overridden by admin
            if (oldState == BaileysSessionState.Banned && newState != BaileysSessionState.Banned)
            {
                var isOverride = reason?.Contains("admin_override", StringComparison.OrdinalIgnoreCase) == true;
                if (!isOverride)
                {
                    throw new InvalidOperationException(
                        $"Instance '{instanceName}' is marked Banned. State transition to {newState} requires an administrative override.");
                }
            }

            instance.State = newState;
            instance.LastHeartbeatUtc = DateTime.UtcNow;

            switch (newState)
            {
                case BaileysSessionState.Connected:
                    instance.ConnectedAtUtc ??= DateTime.UtcNow;
                    instance.ReconnectAttempts = 0;
                    instance.QrCodePayload = null;
                    instance.PairingCode = null;
                    break;

                case BaileysSessionState.PairingQrRequired:
                    instance.QrCodePayload = $"2@{Convert.ToBase64String(Guid.NewGuid().ToByteArray())},{Convert.ToBase64String(Guid.NewGuid().ToByteArray())}";
                    instance.PairingCode = $"{Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(1000, 9999)}";
                    break;

                case BaileysSessionState.Banned:
                    instance.ConnectedAtUtc = null;
                    instance.QrCodePayload = null;
                    instance.PairingCode = null;
                    _logger?.LogWarning("WhatsApp instance '{InstanceName}' marked BANNED! [Tenant: {TenantId}] Reason: {Reason}",
                        instance.InstanceName, tenantId, reason ?? "Violation detected");
                    break;

                case BaileysSessionState.Disconnected:
                    instance.ConnectedAtUtc = null;
                    break;

                case BaileysSessionState.Reconnecting:
                    instance.ReconnectAttempts++;
                    break;
            }

            instance.History.Add(new SessionStateTransitionLog(
                DateTime.UtcNow,
                oldState,
                newState,
                reason ?? $"Transitioned from {oldState} to {newState}"));
        }

        return Task.FromResult(instance);
    }

    public Task<ClusterInstanceSession?> SelectLoadBalancedInstanceAsync(
        Guid tenantId,
        string? targetRole = null,
        ClusterLoadBalanceStrategy strategy = ClusterLoadBalanceStrategy.RoundRobin,
        CancellationToken ct = default)
    {
        var candidates = _instances
            .Where(kvp => kvp.Key.TenantId == tenantId &&
                          kvp.Value.State == BaileysSessionState.Connected &&
                          kvp.Value.IsHealthy)
            .Select(kvp => kvp.Value)
            .ToList();

        if (candidates.Count == 0) return Task.FromResult<ClusterInstanceSession?>(null);

        if (!string.IsNullOrWhiteSpace(targetRole))
        {
            var roleFiltered = candidates
                .Where(c => c.Role.Equals(targetRole.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (roleFiltered.Count > 0)
            {
                candidates = roleFiltered;
            }
        }

        if (candidates.Count == 1) return Task.FromResult<ClusterInstanceSession?>(candidates[0]);

        ClusterInstanceSession selected = strategy switch
        {
            ClusterLoadBalanceStrategy.LeastLoaded =>
                candidates.OrderBy(c => c.InFlightMessagesCount).ThenBy(c => c.TotalMessagesSent).First(),

            ClusterLoadBalanceStrategy.Random =>
                candidates[Random.Shared.Next(candidates.Count)],

            _ => // RoundRobin default
                candidates[(int)(Math.Abs(Interlocked.Increment(ref _roundRobinCounter)) % candidates.Count)]
        };

        return Task.FromResult<ClusterInstanceSession?>(selected);
    }

    public Task<ClusterHealthCheckResult> RunHealthCheckWatchdogAsync(
        Guid tenantId,
        TimeSpan? staleThreshold = null,
        CancellationToken ct = default)
    {
        var threshold = staleThreshold ?? TimeSpan.FromSeconds(90);
        var now = DateTime.UtcNow;

        var tenantInstances = _instances
            .Where(kvp => kvp.Key.TenantId == tenantId)
            .Select(kvp => kvp.Value)
            .ToList();

        var details = new List<InstanceHealthDetail>();
        int connected = 0, reconnecting = 0, banned = 0, disconnected = 0;

        foreach (var instance in tenantInstances)
        {
            lock (instance)
            {
                var age = now - instance.LastHeartbeatUtc;

                if (instance.State == BaileysSessionState.Connected && age > threshold)
                {
                    instance.State = BaileysSessionState.Reconnecting;
                    instance.ReconnectAttempts++;
                    instance.History.Add(new SessionStateTransitionLog(
                        now,
                        BaileysSessionState.Connected,
                        BaileysSessionState.Reconnecting,
                        $"Watchdog detected stale heartbeat ({age.TotalSeconds:F0}s > threshold {threshold.TotalSeconds:F0}s)"));

                    if (instance.ReconnectAttempts >= 5)
                    {
                        instance.State = BaileysSessionState.Disconnected;
                        instance.History.Add(new SessionStateTransitionLog(
                            now,
                            BaileysSessionState.Reconnecting,
                            BaileysSessionState.Disconnected,
                            "Watchdog: max reconnect attempts exceeded (5/5)"));
                    }
                }

                switch (instance.State)
                {
                    case BaileysSessionState.Connected: connected++; break;
                    case BaileysSessionState.Reconnecting: reconnecting++; break;
                    case BaileysSessionState.Banned: banned++; break;
                    default: disconnected++; break;
                }

                string msg = instance.State switch
                {
                    BaileysSessionState.Connected => $"Healthy, ping {instance.PingLatencyMs}ms",
                    BaileysSessionState.Reconnecting => $"Reconnecting (attempt {instance.ReconnectAttempts}/5)",
                    BaileysSessionState.Banned => "BANNED by WhatsApp network",
                    BaileysSessionState.PairingQrRequired => "Awaiting QR scan",
                    _ => "Disconnected"
                };

                details.Add(new InstanceHealthDetail(
                    instance.InstanceName,
                    instance.State,
                    instance.IsHealthy,
                    instance.PingLatencyMs,
                    msg));
            }
        }

        var result = new ClusterHealthCheckResult(
            tenantId,
            tenantInstances.Count,
            connected,
            reconnecting,
            banned,
            disconnected,
            details,
            now);

        return Task.FromResult(result);
    }

    public string EncryptSessionPayload(string plaintext, string? customKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        byte[] keyBytes = DeriveKey(customKey);

        byte[] nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] tag = new byte[16];

        using var aesGcm = new AesGcm(keyBytes, 16);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Format: Nonce (12) + Tag (16) + Ciphertext
        byte[] combined = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, nonce.Length + tag.Length, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    public string DecryptSessionPayload(string cipherText, string? customKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);
        byte[] keyBytes = DeriveKey(customKey);

        byte[] combined = Convert.FromBase64String(cipherText);
        if (combined.Length < 28) // 12 (nonce) + 16 (tag)
        {
            throw new CryptographicException("Ciphertext payload is truncated or corrupted.");
        }

        byte[] nonce = new byte[12];
        byte[] tag = new byte[16];
        int cipherLen = combined.Length - 28;
        byte[] cipherBytes = new byte[cipherLen];

        Buffer.BlockCopy(combined, 0, nonce, 0, 12);
        Buffer.BlockCopy(combined, 12, tag, 0, 16);
        Buffer.BlockCopy(combined, 28, cipherBytes, 0, cipherLen);

        byte[] plainBytes = new byte[cipherLen];
        using var aesGcm = new AesGcm(keyBytes, 16);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    public Task<bool> RecordDispatchedMessageAsync(Guid tenantId, string instanceName, CancellationToken ct = default)
    {
        var key = (tenantId, instanceName.Trim().ToLowerInvariant());
        if (_instances.TryGetValue(key, out var instance))
        {
            lock (instance)
            {
                instance.TotalMessagesSent++;
                instance.LastHeartbeatUtc = DateTime.UtcNow;
            }
            SGrowingTelemetry.WhatsAppDispatchesCounter.Add(1,
                new KeyValuePair<string, object?>("tenant_id", tenantId.ToString()),
                new KeyValuePair<string, object?>("instance_name", instance.InstanceName));
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> RecordHeartbeatAsync(Guid tenantId, string instanceName, int pingLatencyMs = 25, CancellationToken ct = default)
    {
        var key = (tenantId, instanceName.Trim().ToLowerInvariant());
        if (_instances.TryGetValue(key, out var instance))
        {
            lock (instance)
            {
                instance.LastHeartbeatUtc = DateTime.UtcNow;
                instance.PingLatencyMs = pingLatencyMs;
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> RemoveInstanceAsync(Guid tenantId, string instanceName, CancellationToken ct = default)
    {
        var key = (tenantId, instanceName.Trim().ToLowerInvariant());
        return Task.FromResult(_instances.TryRemove(key, out _));
    }

    private static byte[] DeriveKey(string? customKey)
    {
        string secret = string.IsNullOrWhiteSpace(customKey)
            ? "SGrowing_Baileys_Cluster_ClusterSessionVault_2026_SecureKey"
            : customKey;

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret),
            DefaultMasterKeySalt,
            iterations: 100_000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);
    }
}
