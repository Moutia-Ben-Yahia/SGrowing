using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Channels.WhatsApp.Companion;

#region Enums & Models

public enum CompanionSessionState
{
    Initial = 0,
    PairingCodeRequested = 1,
    PairingCodeGenerated = 2,
    HandshakeNoiseExchange = 3,
    Connected = 4,
    Syncing = 5,
    Disconnected = 6
}

public enum MessageAckLevel
{
    None = 0,
    AckServer = 1,    // Single gray check
    AckDevice = 2,    // Double gray check
    AckRead = 3,      // Blue check
    AckPlayed = 4     // Blue microphone (Voice notes)
}

public enum CompanionNetworkType
{
    WiFi = 0,
    Cellular5G = 1,
    Cellular4G = 2
}

public enum CompanionMediaType
{
    VoiceNote = 0,
    Image = 1,
    Video = 2,
    Document = 3
}

public record CompanionDeviceTelemetry(
    int BatteryPercentage,
    bool IsCharging,
    CompanionNetworkType NetworkType,
    int PingLatencyMs,
    DateTime LastHeartbeatUtc
);

public record CompanionSocketPacketLog(
    Guid Id,
    DateTime TimestampUtc,
    string Direction, // INBOUND or OUTBOUND
    string FrameType, // NOISE_XX, WABINARY_FRAME, ACK_RECEIPT, HEARTBEAT, MEDIA_CIPHER
    string Summary,
    string HexPayloadSnippet,
    int PayloadSizeBytes
);

public record NoiseKeyPair(
    byte[] PublicKey,
    byte[] PrivateKey
)
{
    public string PublicKeyBase64 => Convert.ToBase64String(PublicKey);
}

public record CompanionSession(
    Guid SessionId,
    Guid TenantId,
    string PhoneNumber,
    string DeviceModel,
    CompanionSessionState State,
    string? PairingCode,
    DateTime? PairingCodeExpiresAtUtc,
    int FailedVerificationAttempts,
    bool IsLockedOut,
    NoiseKeyPair? IdentityKeys,
    NoiseKeyPair? EphemeralKeys,
    byte[]? AesSessionKey,
    string? SessionValidationToken,
    CompanionDeviceTelemetry Telemetry,
    DateTime CreatedAtUtc,
    DateTime LastActiveAtUtc,
    int ReconnectAttempts,
    string? DisconnectReason = null
)
{
    public int SecondsRemaining => PairingCodeExpiresAtUtc.HasValue
        ? Math.Max(0, (int)(PairingCodeExpiresAtUtc.Value - DateTime.UtcNow).TotalSeconds)
        : 0;

    public bool IsPairingCodeExpired => PairingCodeExpiresAtUtc.HasValue && DateTime.UtcNow > PairingCodeExpiresAtUtc.Value;
}

public record WABinaryFrame(
    string Tag,
    string FrameId,
    long EpochTimestamp,
    MessageAckLevel AckLevel,
    byte[] Payload,
    Dictionary<string, string> Attributes
);

public record CompanionMediaPayload(
    string MimeType,
    string CiphertextBase64,
    string IvBase64,
    string TagBase64,
    string ExpectedSha256Checksum,
    string? FileName = null
);

public record EncryptedMediaResult(
    string CiphertextBase64,
    string IvBase64,
    string TagBase64,
    string Sha256Checksum,
    string MimeType,
    int PlaintextSizeBytes
);

public record MediaDecryptionResult(
    bool Success,
    bool IsChecksumValid,
    string? DecryptedBase64,
    string ComputedSha256Checksum,
    string ExpectedSha256Checksum,
    string MimeType,
    int DecryptedSizeBytes,
    string? Error = null
);

public record PairingCodeResult(
    Guid SessionId,
    Guid TenantId,
    string PhoneNumber,
    string PairingCode,
    int TtlSecondsRemaining,
    DateTime ExpiresAtUtc,
    CompanionSessionState State,
    string Message
);

public record VerifyCodeResult(
    bool Success,
    Guid SessionId,
    CompanionSessionState State,
    string? SessionValidationToken,
    string? DerivedAesKeyBase64,
    string Message,
    int AttemptsRemaining
);

public record AckSimulationResult(
    bool Success,
    Guid SessionId,
    string MessageId,
    MessageAckLevel PreviousAck,
    MessageAckLevel CurrentAck,
    DateTime TransitionedAtUtc,
    string Message
);

#endregion

public interface IWhatsAppCompanionSocketService
{
    // Pairing code engine
    Task<PairingCodeResult> RequestPairingCodeAsync(Guid tenantId, string phoneNumber, string? deviceModel = null, CancellationToken ct = default);
    Task<VerifyCodeResult> VerifyPairingCodeAsync(Guid tenantId, Guid sessionId, string pairingCode, CancellationToken ct = default);

    // Session lifecycle & State machine
    Task<CompanionSession?> GetSessionStatusAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<CompanionSession>> GetSessionsForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> DisconnectSessionAsync(Guid sessionId, string reason = "UserRequestedDisconnect", CancellationToken ct = default);
    TimeSpan CalculateReconnectionBackoff(int reconnectAttempts);

    // Cryptography & Noise_XX
    NoiseKeyPair GenerateCurve25519KeyPair();
    byte[] DeriveAes256GcmKey(byte[] privateKey, byte[] remotePublicKey, byte[] salt);
    string SignSessionToken(Guid sessionId, string phoneNumber, byte[] secretKey);
    bool VerifySessionToken(Guid sessionId, string phoneNumber, byte[] secretKey, string token);

    // WABinary framing & ACK Tracker
    byte[] EncodeWABinaryFrame(WABinaryFrame frame);
    WABinaryFrame DecodeWABinaryFrame(byte[] binaryData);
    Task<AckSimulationResult> SimulateMessageAckProgressionAsync(Guid sessionId, string messageId, MessageAckLevel targetAck, bool isVoiceNote = false, CancellationToken ct = default);

    // Media Encryption & Decryption
    EncryptedMediaResult EncryptMediaPayload(byte[] plaintext, string mimeType, byte[]? key = null);
    MediaDecryptionResult DecryptMediaPayload(CompanionMediaPayload payload, byte[] key);
    Task<MediaDecryptionResult> DecryptSessionMediaAsync(Guid sessionId, CompanionMediaPayload payload, CancellationToken ct = default);

    // Telemetry & Packet Logs
    Task<CompanionDeviceTelemetry?> GetTelemetryAsync(Guid sessionId, CancellationToken ct = default);
    Task<CompanionDeviceTelemetry> RefreshTelemetryAsync(Guid sessionId, CancellationToken ct = default);
    IReadOnlyList<CompanionSocketPacketLog> GetPacketLogs(Guid sessionId);
    void AppendPacketLog(Guid sessionId, string direction, string frameType, string summary, byte[] payload);
}

public class WhatsAppCompanionSocketService : IWhatsAppCompanionSocketService
{
    private readonly ILogger<WhatsAppCompanionSocketService>? _logger;
    private readonly ConcurrentDictionary<Guid, CompanionSession> _sessions = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<CompanionSocketPacketLog>> _packetLogs = new();
    private readonly ConcurrentDictionary<string, MessageAckLevel> _messageAcks = new();
    private readonly object _stateLock = new();

    // 32-character human-friendly alphabet (omitting 0, 1, O, I for pairing readability)
    private const string PairingCharset = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    public const int DefaultTtlSeconds = 180;
    public const int MaxFailedAttempts = 3;

    public WhatsAppCompanionSocketService(ILogger<WhatsAppCompanionSocketService>? logger = null)
    {
        _logger = logger;
        SeedInitialSessions();
    }

    #region Pre-Seeded Sessions

    private void SeedInitialSessions()
    {
        var defaultTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // 1. Primary Support Desk Line
        var id1 = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var keys1 = GenerateCurve25519KeyPair();
        var eph1 = GenerateCurve25519KeyPair();
        var aes1 = DeriveAes256GcmKey(keys1.PrivateKey, eph1.PublicKey, Encoding.UTF8.GetBytes("SGrowingNoiseSalt1"));
        var token1 = SignSessionToken(id1, "+21671998801", aes1);

        var session1 = new CompanionSession(
            SessionId: id1,
            TenantId: defaultTenant,
            PhoneNumber: "+21671998801",
            DeviceModel: "Primary Support Desk Line (Ubuntu 24.04 / Chrome Multi-Device)",
            State: CompanionSessionState.Connected,
            PairingCode: null,
            PairingCodeExpiresAtUtc: null,
            FailedVerificationAttempts: 0,
            IsLockedOut: false,
            IdentityKeys: keys1,
            EphemeralKeys: eph1,
            AesSessionKey: aes1,
            SessionValidationToken: token1,
            Telemetry: new CompanionDeviceTelemetry(95, true, CompanionNetworkType.WiFi, 18, DateTime.UtcNow),
            CreatedAtUtc: DateTime.UtcNow.AddHours(-12),
            LastActiveAtUtc: DateTime.UtcNow.AddSeconds(-5),
            ReconnectAttempts: 0
        );
        _sessions[id1] = session1;
        AppendPacketLog(id1, "INBOUND", "NOISE_XX", "Noise_XX Handshake established with WhatsApp Multi-Device Server", keys1.PublicKey);
        AppendPacketLog(id1, "OUTBOUND", "WABINARY_FRAME", "WABinary protocol synchronization frame acknowledged", aes1);

        // 2. VIP Account Manager Companion
        var id2 = Guid.Parse("11111111-2222-3333-4444-555555555552");
        var keys2 = GenerateCurve25519KeyPair();
        var eph2 = GenerateCurve25519KeyPair();
        var aes2 = DeriveAes256GcmKey(keys2.PrivateKey, eph2.PublicKey, Encoding.UTF8.GetBytes("SGrowingNoiseSalt2"));
        var token2 = SignSessionToken(id2, "+21671998802", aes2);

        var session2 = new CompanionSession(
            SessionId: id2,
            TenantId: defaultTenant,
            PhoneNumber: "+21671998802",
            DeviceModel: "VIP Account Manager Companion (macOS Sonoma / Chromium Companion)",
            State: CompanionSessionState.Syncing,
            PairingCode: null,
            PairingCodeExpiresAtUtc: null,
            FailedVerificationAttempts: 0,
            IsLockedOut: false,
            IdentityKeys: keys2,
            EphemeralKeys: eph2,
            AesSessionKey: aes2,
            SessionValidationToken: token2,
            Telemetry: new CompanionDeviceTelemetry(82, false, CompanionNetworkType.Cellular5G, 24, DateTime.UtcNow),
            CreatedAtUtc: DateTime.UtcNow.AddHours(-4),
            LastActiveAtUtc: DateTime.UtcNow.AddSeconds(-2),
            ReconnectAttempts: 0
        );
        _sessions[id2] = session2;
        AppendPacketLog(id2, "INBOUND", "WABINARY_FRAME", "App state cryptographic key sync in progress", aes2);

        // 3. Automated Inbound Dispatcher
        var id3 = Guid.Parse("11111111-2222-3333-4444-555555555553");
        var keys3 = GenerateCurve25519KeyPair();
        var eph3 = GenerateCurve25519KeyPair();
        var aes3 = DeriveAes256GcmKey(keys3.PrivateKey, eph3.PublicKey, Encoding.UTF8.GetBytes("SGrowingNoiseSalt3"));
        var token3 = SignSessionToken(id3, "+21671998803", aes3);

        var session3 = new CompanionSession(
            SessionId: id3,
            TenantId: defaultTenant,
            PhoneNumber: "+21671998803",
            DeviceModel: "Automated Inbound Dispatcher (Alpine Linux / V8 Companion Engine)",
            State: CompanionSessionState.Connected,
            PairingCode: null,
            PairingCodeExpiresAtUtc: null,
            FailedVerificationAttempts: 0,
            IsLockedOut: false,
            IdentityKeys: keys3,
            EphemeralKeys: eph3,
            AesSessionKey: aes3,
            SessionValidationToken: token3,
            Telemetry: new CompanionDeviceTelemetry(68, false, CompanionNetworkType.Cellular4G, 35, DateTime.UtcNow),
            CreatedAtUtc: DateTime.UtcNow.AddDays(-1),
            LastActiveAtUtc: DateTime.UtcNow.AddSeconds(-8),
            ReconnectAttempts: 1
        );
        _sessions[id3] = session3;
        AppendPacketLog(id3, "INBOUND", "ACK_RECEIPT", "Receipt server broadcast received (AckServer)", aes3);
    }

    #endregion

    #region Phone Normalization & 8-Digit Pairing Code Engine

    public static string NormalizePhoneNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Phone number cannot be empty or null.", nameof(raw));

        var cleaned = Regex.Replace(raw.Trim(), @"[^\d\+]", "");
        if (cleaned.StartsWith("00"))
        {
            cleaned = "+" + cleaned[2..];
        }
        else if (!cleaned.StartsWith("+"))
        {
            cleaned = "+" + cleaned;
        }

        if (!Regex.IsMatch(cleaned, @"^\+[1-9]\d{6,14}$"))
        {
            throw new ArgumentException($"Invalid E.164 phone number: '{raw}'. Must be '+' followed by 7-15 digits.", nameof(raw));
        }

        return cleaned;
    }

    public static string Generate8DigitPairingCode()
    {
        var chars = new char[8];
        var bytes = new byte[8];
        RandomNumberGenerator.Fill(bytes);

        for (int i = 0; i < 8; i++)
        {
            chars[i] = PairingCharset[bytes[i] % PairingCharset.Length];
        }

        // Return format: ABCD-EFGH
        return $"{chars[0]}{chars[1]}{chars[2]}{chars[3]}-{chars[4]}{chars[5]}{chars[6]}{chars[7]}";
    }

    public async Task<PairingCodeResult> RequestPairingCodeAsync(
        Guid tenantId,
        string phoneNumber,
        string? deviceModel = null,
        CancellationToken ct = default)
    {
        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        var resolvedDevice = string.IsNullOrWhiteSpace(deviceModel)
            ? "Multi-Device Companion Socket (Chrome / Node)"
            : deviceModel.Trim();

        var sessionId = Guid.NewGuid();
        var code = Generate8DigitPairingCode();
        var expiresAt = DateTime.UtcNow.AddSeconds(DefaultTtlSeconds);

        var session = new CompanionSession(
            SessionId: sessionId,
            TenantId: tenantId,
            PhoneNumber: normalizedPhone,
            DeviceModel: resolvedDevice,
            State: CompanionSessionState.PairingCodeGenerated,
            PairingCode: code,
            PairingCodeExpiresAtUtc: expiresAt,
            FailedVerificationAttempts: 0,
            IsLockedOut: false,
            IdentityKeys: null,
            EphemeralKeys: null,
            AesSessionKey: null,
            SessionValidationToken: null,
            Telemetry: new CompanionDeviceTelemetry(100, true, CompanionNetworkType.WiFi, 12, DateTime.UtcNow),
            CreatedAtUtc: DateTime.UtcNow,
            LastActiveAtUtc: DateTime.UtcNow,
            ReconnectAttempts: 0
        );

        _sessions[sessionId] = session;
        AppendPacketLog(sessionId, "OUTBOUND", "PAIRING_REQUEST", $"Pairing code requested for {normalizedPhone}", Encoding.UTF8.GetBytes(code));

        _logger?.LogInformation("Generated companion pairing code {Code} for {Phone} (Session: {SessionId})", code, normalizedPhone, sessionId);

        return await Task.FromResult(new PairingCodeResult(
            SessionId: sessionId,
            TenantId: tenantId,
            PhoneNumber: normalizedPhone,
            PairingCode: code,
            TtlSecondsRemaining: DefaultTtlSeconds,
            ExpiresAtUtc: expiresAt,
            State: CompanionSessionState.PairingCodeGenerated,
            Message: $"Pairing code generated. Valid for {DefaultTtlSeconds} seconds. Enter code on WhatsApp Companion."
        ));
    }

    public async Task<VerifyCodeResult> VerifyPairingCodeAsync(
        Guid tenantId,
        Guid sessionId,
        string pairingCode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pairingCode))
        {
            return new VerifyCodeResult(
                Success: false,
                SessionId: sessionId,
                State: CompanionSessionState.Initial,
                SessionValidationToken: null,
                DerivedAesKeyBase64: null,
                Message: "Pairing code cannot be empty.",
                AttemptsRemaining: 0
            );
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return new VerifyCodeResult(
                Success: false,
                SessionId: sessionId,
                State: CompanionSessionState.Initial,
                SessionValidationToken: null,
                DerivedAesKeyBase64: null,
                Message: "Companion session not found or expired.",
                AttemptsRemaining: 0
            );
        }

        lock (_stateLock)
        {
            if (session.IsLockedOut)
            {
                return new VerifyCodeResult(
                    Success: false,
                    SessionId: sessionId,
                    State: session.State,
                    SessionValidationToken: null,
                    DerivedAesKeyBase64: null,
                    Message: "Companion device locked out due to exceeding maximum invalid pairing attempts (3). Request a new code.",
                    AttemptsRemaining: 0
                );
            }

            if (session.IsPairingCodeExpired)
            {
                _sessions[sessionId] = session with
                {
                    State = CompanionSessionState.Disconnected,
                    DisconnectReason = "PairingCodeTtlExpired"
                };
                return new VerifyCodeResult(
                    Success: false,
                    SessionId: sessionId,
                    State: CompanionSessionState.Disconnected,
                    SessionValidationToken: null,
                    DerivedAesKeyBase64: null,
                    Message: "Pairing code has expired (180s TTL elapsed). Please request a new pairing code.",
                    AttemptsRemaining: 0
                );
            }

            // Normalize input for comparison (e.g. remove hyphens and spaces, uppercase)
            var cleanInput = pairingCode.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
            var cleanTarget = (session.PairingCode ?? "").Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();

            if (!string.Equals(cleanInput, cleanTarget, StringComparison.Ordinal))
            {
                var newAttempts = session.FailedVerificationAttempts + 1;
                var isLocked = newAttempts >= MaxFailedAttempts;
                var attemptsLeft = Math.Max(0, MaxFailedAttempts - newAttempts);

                _sessions[sessionId] = session with
                {
                    FailedVerificationAttempts = newAttempts,
                    IsLockedOut = isLocked,
                    State = isLocked ? CompanionSessionState.Disconnected : session.State,
                    DisconnectReason = isLocked ? "MaxRetriesLockout" : null
                };

                AppendPacketLog(sessionId, "INBOUND", "PAIRING_FAILED", $"Invalid pairing attempt #{newAttempts}", Encoding.UTF8.GetBytes(pairingCode));

                return new VerifyCodeResult(
                    Success: false,
                    SessionId: sessionId,
                    State: isLocked ? CompanionSessionState.Disconnected : session.State,
                    SessionValidationToken: null,
                    DerivedAesKeyBase64: null,
                    Message: isLocked
                        ? "Maximum pairing attempts reached (3). Device locked out."
                        : $"Invalid pairing code. {attemptsLeft} attempts remaining.",
                    AttemptsRemaining: attemptsLeft
                );
            }

            // Success -> Execute Noise_XX Handshake & Derive Cryptographic Keys
            var identityKeys = GenerateCurve25519KeyPair();
            var ephemeralKeys = GenerateCurve25519KeyPair();
            var salt = Encoding.UTF8.GetBytes($"WhatsApp-MultiDevice-Noise-{sessionId}");
            var aesKey = DeriveAes256GcmKey(identityKeys.PrivateKey, ephemeralKeys.PublicKey, salt);
            var token = SignSessionToken(sessionId, session.PhoneNumber, aesKey);

            var updatedSession = session with
            {
                State = CompanionSessionState.Connected,
                PairingCode = null,
                PairingCodeExpiresAtUtc = null,
                FailedVerificationAttempts = 0,
                IdentityKeys = identityKeys,
                EphemeralKeys = ephemeralKeys,
                AesSessionKey = aesKey,
                SessionValidationToken = token,
                LastActiveAtUtc = DateTime.UtcNow
            };

            _sessions[sessionId] = updatedSession;

            AppendPacketLog(sessionId, "INBOUND", "NOISE_XX", "Noise_XX Diffie-Hellman handshake verified and accepted", identityKeys.PublicKey);
            AppendPacketLog(sessionId, "OUTBOUND", "WABINARY_FRAME", "Session encryption key established (AES-256-GCM)", aesKey);

            _logger?.LogInformation("Noise_XX handshake completed for companion session {SessionId}. Connected.", sessionId);

            return new VerifyCodeResult(
                Success: true,
                SessionId: sessionId,
                State: CompanionSessionState.Connected,
                SessionValidationToken: token,
                DerivedAesKeyBase64: Convert.ToBase64String(aesKey),
                Message: "Companion device verified successfully. Noise_XX socket connected.",
                AttemptsRemaining: MaxFailedAttempts
            );
        }
    }

    #endregion

    #region Session Lifecycle & State Machine

    public Task<CompanionSession?> GetSessionStatusAsync(Guid sessionId, CancellationToken ct = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<CompanionSession>> GetSessionsForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _sessions.Values
            .Where(s => s.TenantId == tenantId || tenantId == Guid.Empty)
            .OrderByDescending(s => s.LastActiveAtUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<CompanionSession>>(list);
    }

    public Task<bool> DisconnectSessionAsync(Guid sessionId, string reason = "UserRequestedDisconnect", CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            var disconnected = session with
            {
                State = CompanionSessionState.Disconnected,
                DisconnectReason = reason,
                LastActiveAtUtc = DateTime.UtcNow
            };
            _sessions[sessionId] = disconnected;
            AppendPacketLog(sessionId, "OUTBOUND", "SOCKET_DISCONNECT", $"Socket disconnected: {reason}", Encoding.UTF8.GetBytes(reason));
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public TimeSpan CalculateReconnectionBackoff(int reconnectAttempts)
    {
        if (reconnectAttempts <= 0) return TimeSpan.FromSeconds(1);
        return reconnectAttempts switch
        {
            1 => TimeSpan.FromSeconds(1),
            2 => TimeSpan.FromSeconds(2),
            3 => TimeSpan.FromSeconds(5),
            _ => TimeSpan.FromSeconds(10) // 10s maximum cap
        };
    }

    #endregion

    #region Cryptography & Noise_XX Protocol Simulation

    public NoiseKeyPair GenerateCurve25519KeyPair()
    {
        // 32-byte Curve25519 key simulation
        var privateKey = new byte[32];
        var publicKey = new byte[32];
        RandomNumberGenerator.Fill(privateKey);

        // Derive deterministic public key using SHA-256 scalar multiplication simulation
        using var sha = SHA256.Create();
        var hashed = sha.ComputeHash(privateKey);
        Array.Copy(hashed, publicKey, 32);

        return new NoiseKeyPair(publicKey, privateKey);
    }

    public byte[] DeriveAes256GcmKey(byte[] privateKey, byte[] remotePublicKey, byte[] salt)
    {
        if (privateKey == null || privateKey.Length == 0) throw new ArgumentException("Private key required.", nameof(privateKey));
        if (remotePublicKey == null || remotePublicKey.Length == 0) throw new ArgumentException("Remote public key required.", nameof(remotePublicKey));

        // Diffie-Hellman shared secret simulation
        var combined = new byte[privateKey.Length + remotePublicKey.Length + (salt?.Length ?? 0)];
        Buffer.BlockCopy(privateKey, 0, combined, 0, privateKey.Length);
        Buffer.BlockCopy(remotePublicKey, 0, combined, privateKey.Length, remotePublicKey.Length);
        if (salt != null && salt.Length > 0)
        {
            Buffer.BlockCopy(salt, 0, combined, privateKey.Length + remotePublicKey.Length, salt.Length);
        }

        // Derives 32-byte (256-bit) AES key via HKDF / HMAC-SHA256
        using var hmac = new HMACSHA256(salt ?? Encoding.UTF8.GetBytes("DefaultWhatsAppNoiseSalt"));
        return hmac.ComputeHash(combined);
    }

    public string SignSessionToken(Guid sessionId, string phoneNumber, byte[] secretKey)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{sessionId}:{phoneNumber}:{timestamp}";
        using var hmac = new HMACSHA256(secretKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var signatureBase64 = Convert.ToBase64String(hash);
        return $"{payload}.{signatureBase64}";
    }

    public bool VerifySessionToken(Guid sessionId, string phoneNumber, byte[] secretKey, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var parts = token.Split('.');
        if (parts.Length != 2) return false;

        var payload = parts[0];
        var signature = parts[1];

        var subparts = payload.Split(':');
        if (subparts.Length != 3) return false;

        if (!Guid.TryParse(subparts[0], out var tokenSessionId) || tokenSessionId != sessionId)
            return false;

        if (!string.Equals(subparts[1], phoneNumber, StringComparison.OrdinalIgnoreCase))
            return false;

        using var hmac = new HMACSHA256(secretKey);
        var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expectedSig = Convert.ToBase64String(expectedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expectedSig)
        );
    }

    #endregion

    #region WABinary Framing & ACK Tracker

    public byte[] EncodeWABinaryFrame(WABinaryFrame frame)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        // Header: WA binary tag prefix 0x02
        writer.Write((byte)0x02);
        writer.Write(frame.Tag);
        writer.Write(frame.FrameId);
        writer.Write(frame.EpochTimestamp);
        writer.Write((byte)frame.AckLevel);

        // Attributes count and pairs
        writer.Write(frame.Attributes.Count);
        foreach (var (k, v) in frame.Attributes)
        {
            writer.Write(k);
            writer.Write(v);
        }

        // Payload
        writer.Write(frame.Payload.Length);
        writer.Write(frame.Payload);

        return ms.ToArray();
    }

    public WABinaryFrame DecodeWABinaryFrame(byte[] binaryData)
    {
        if (binaryData == null || binaryData.Length < 10)
            throw new ArgumentException("Invalid WABinary frame data.", nameof(binaryData));

        using var ms = new MemoryStream(binaryData);
        using var reader = new BinaryReader(ms, Encoding.UTF8);

        var prefix = reader.ReadByte();
        if (prefix != 0x02)
            throw new FormatException($"Unsupported WABinary frame prefix byte: 0x{prefix:X2}. Expected 0x02.");

        var tag = reader.ReadString();
        var frameId = reader.ReadString();
        var epoch = reader.ReadInt64();
        var ack = (MessageAckLevel)reader.ReadByte();

        var attrCount = reader.ReadInt32();
        var attrs = new Dictionary<string, string>(attrCount);
        for (int i = 0; i < attrCount; i++)
        {
            var k = reader.ReadString();
            var v = reader.ReadString();
            attrs[k] = v;
        }

        var payloadLen = reader.ReadInt32();
        var payload = reader.ReadBytes(payloadLen);

        return new WABinaryFrame(tag, frameId, epoch, ack, payload, attrs);
    }

    public async Task<AckSimulationResult> SimulateMessageAckProgressionAsync(
        Guid sessionId,
        string messageId,
        MessageAckLevel targetAck,
        bool isVoiceNote = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("MessageId cannot be empty.", nameof(messageId));

        var currentAck = _messageAcks.GetOrAdd(messageId, MessageAckLevel.None);

        // Enforce valid ACK state progression (cannot go backwards)
        if (targetAck < currentAck)
        {
            return new AckSimulationResult(
                Success: false,
                SessionId: sessionId,
                MessageId: messageId,
                PreviousAck: currentAck,
                CurrentAck: currentAck,
                TransitionedAtUtc: DateTime.UtcNow,
                Message: $"Cannot transition backwards from {currentAck} to {targetAck}."
            );
        }

        // Voice note played is only valid if isVoiceNote is true
        if (targetAck == MessageAckLevel.AckPlayed && !isVoiceNote)
        {
            return new AckSimulationResult(
                Success: false,
                SessionId: sessionId,
                MessageId: messageId,
                PreviousAck: currentAck,
                CurrentAck: currentAck,
                TransitionedAtUtc: DateTime.UtcNow,
                Message: "AckPlayed status is only permitted for voice note audio media."
            );
        }

        _messageAcks[messageId] = targetAck;

        var logDesc = targetAck switch
        {
            MessageAckLevel.AckServer => "Single check: Received by WhatsApp Relay Server",
            MessageAckLevel.AckDevice => "Double check: Delivered to recipient device",
            MessageAckLevel.AckRead => "Blue check: Read by recipient",
            MessageAckLevel.AckPlayed => "Blue microphone: Audio voice note played by recipient",
            _ => "Pending dispatch"
        };

        AppendPacketLog(sessionId, "INBOUND", "ACK_RECEIPT", $"Receipt {messageId}: {targetAck} ({logDesc})", Encoding.UTF8.GetBytes(messageId));

        return await Task.FromResult(new AckSimulationResult(
            Success: true,
            SessionId: sessionId,
            MessageId: messageId,
            PreviousAck: currentAck,
            CurrentAck: targetAck,
            TransitionedAtUtc: DateTime.UtcNow,
            Message: $"Receipt transition confirmed: {targetAck} ({logDesc})."
        ));
    }

    #endregion

    #region Media Encryption & AES-256-GCM Streaming Decryption

    public EncryptedMediaResult EncryptMediaPayload(byte[] plaintext, string mimeType, byte[]? key = null)
    {
        if (plaintext == null || plaintext.Length == 0)
            throw new ArgumentException("Plaintext cannot be empty.", nameof(plaintext));

        var encryptionKey = key ?? new byte[32];
        if (key == null) RandomNumberGenerator.Fill(encryptionKey);

        var nonce = new byte[12]; // 96-bit standard AES-GCM nonce
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16]; // 128-bit authentication tag

        using (var aesGcm = new AesGcm(encryptionKey, 16))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        using var sha = SHA256.Create();
        var checksumBytes = sha.ComputeHash(plaintext);
        var checksumHex = Convert.ToHexString(checksumBytes).ToLowerInvariant();

        return new EncryptedMediaResult(
            CiphertextBase64: Convert.ToBase64String(ciphertext),
            IvBase64: Convert.ToBase64String(nonce),
            TagBase64: Convert.ToBase64String(tag),
            Sha256Checksum: checksumHex,
            MimeType: mimeType,
            PlaintextSizeBytes: plaintext.Length
        );
    }

    public MediaDecryptionResult DecryptMediaPayload(CompanionMediaPayload payload, byte[] key)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(payload.CiphertextBase64))
                return new MediaDecryptionResult(false, false, null, "", payload.ExpectedSha256Checksum, payload.MimeType, 0, "Ciphertext cannot be empty.");

            var ciphertext = Convert.FromBase64String(payload.CiphertextBase64);
            var nonce = Convert.FromBase64String(payload.IvBase64);
            var tag = Convert.FromBase64String(payload.TagBase64);

            var plaintext = new byte[ciphertext.Length];
            using (var aesGcm = new AesGcm(key, 16))
            {
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            using var sha = SHA256.Create();
            var computedHashBytes = sha.ComputeHash(plaintext);
            var computedChecksum = Convert.ToHexString(computedHashBytes).ToLowerInvariant();

            var isValid = string.Equals(
                computedChecksum,
                payload.ExpectedSha256Checksum?.Trim().ToLowerInvariant(),
                StringComparison.OrdinalIgnoreCase);

            return new MediaDecryptionResult(
                Success: true,
                IsChecksumValid: isValid,
                DecryptedBase64: Convert.ToBase64String(plaintext),
                ComputedSha256Checksum: computedChecksum,
                ExpectedSha256Checksum: payload.ExpectedSha256Checksum ?? "",
                MimeType: payload.MimeType,
                DecryptedSizeBytes: plaintext.Length
            );
        }
        catch (CryptographicException ex)
        {
            return new MediaDecryptionResult(
                Success: false,
                IsChecksumValid: false,
                DecryptedBase64: null,
                ComputedSha256Checksum: "",
                ExpectedSha256Checksum: payload.ExpectedSha256Checksum ?? "",
                MimeType: payload.MimeType,
                DecryptedSizeBytes: 0,
                Error: $"AES-GCM Authentication Tag mismatch or corrupt ciphertext: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            return new MediaDecryptionResult(
                Success: false,
                IsChecksumValid: false,
                DecryptedBase64: null,
                ComputedSha256Checksum: "",
                ExpectedSha256Checksum: payload.ExpectedSha256Checksum ?? "",
                MimeType: payload.MimeType,
                DecryptedSizeBytes: 0,
                Error: $"Decryption failure: {ex.Message}"
            );
        }
    }

    public async Task<MediaDecryptionResult> DecryptSessionMediaAsync(Guid sessionId, CompanionMediaPayload payload, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session) || session.AesSessionKey == null)
        {
            return new MediaDecryptionResult(
                Success: false,
                IsChecksumValid: false,
                DecryptedBase64: null,
                ComputedSha256Checksum: "",
                ExpectedSha256Checksum: payload.ExpectedSha256Checksum ?? "",
                MimeType: payload.MimeType,
                DecryptedSizeBytes: 0,
                Error: "Active session with negotiated AES session key not found."
            );
        }

        var result = DecryptMediaPayload(payload, session.AesSessionKey);
        if (result.Success)
        {
            AppendPacketLog(sessionId, "INBOUND", "MEDIA_CIPHER", $"Decrypted media payload {payload.MimeType} ({result.DecryptedSizeBytes} bytes, ChecksumMatch={result.IsChecksumValid})", Encoding.UTF8.GetBytes(result.ComputedSha256Checksum));
        }

        return await Task.FromResult(result);
    }

    #endregion

    #region Telemetry & Packet Logs

    public Task<CompanionDeviceTelemetry?> GetTelemetryAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult<CompanionDeviceTelemetry?>(session.Telemetry);
        }
        return Task.FromResult<CompanionDeviceTelemetry?>(null);
    }

    public Task<CompanionDeviceTelemetry> RefreshTelemetryAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            var fallback = new CompanionDeviceTelemetry(100, true, CompanionNetworkType.WiFi, 15, DateTime.UtcNow);
            return Task.FromResult(fallback);
        }

        var random = Random.Shared;
        var newPing = Math.Clamp(session.Telemetry.PingLatencyMs + random.Next(-3, 4), 10, 120);
        var newBattery = session.Telemetry.IsCharging
            ? Math.Min(100, session.Telemetry.BatteryPercentage + 1)
            : Math.Max(5, session.Telemetry.BatteryPercentage - 1);

        var updated = session.Telemetry with
        {
            BatteryPercentage = newBattery,
            PingLatencyMs = newPing,
            LastHeartbeatUtc = DateTime.UtcNow
        };

        _sessions[sessionId] = session with { Telemetry = updated, LastActiveAtUtc = DateTime.UtcNow };
        AppendPacketLog(sessionId, "INBOUND", "HEARTBEAT", $"Telemetry ping: {newPing}ms, Battery: {newBattery}%, Net: {updated.NetworkType}", BitConverter.GetBytes(newPing));

        return Task.FromResult(updated);
    }

    public IReadOnlyList<CompanionSocketPacketLog> GetPacketLogs(Guid sessionId)
    {
        if (_packetLogs.TryGetValue(sessionId, out var queue))
        {
            return queue.ToArray().OrderByDescending(p => p.TimestampUtc).Take(50).ToList();
        }
        return Array.Empty<CompanionSocketPacketLog>();
    }

    public void AppendPacketLog(Guid sessionId, string direction, string frameType, string summary, byte[] payload)
    {
        var queue = _packetLogs.GetOrAdd(sessionId, _ => new ConcurrentQueue<CompanionSocketPacketLog>());
        var snippet = payload != null && payload.Length > 0
            ? Convert.ToHexString(payload.Take(16).ToArray()) + (payload.Length > 16 ? "..." : "")
            : "EMPTY";

        var log = new CompanionSocketPacketLog(
            Id: Guid.NewGuid(),
            TimestampUtc: DateTime.UtcNow,
            Direction: direction,
            FrameType: frameType,
            Summary: summary,
            HexPayloadSnippet: snippet,
            PayloadSizeBytes: payload?.Length ?? 0
        );

        queue.Enqueue(log);

        // Keep maximum 100 logs per session
        while (queue.Count > 100 && queue.TryDequeue(out _)) { }
    }

    #endregion
}
