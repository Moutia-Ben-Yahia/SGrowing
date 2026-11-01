using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.ConfidentialEnclave;

/// <summary>
/// Mutually attested confidential mesh service orchestrating encrypted tunnels
/// between verified micro-VM enclaves with AES-256-GCM authenticated messaging.
/// </summary>
public sealed class MicroVmConfidentialMeshService
{
    private sealed class MeshChannelState
    {
        public required ConfidentialMeshChannel Channel { get; set; }
        public required byte[] MeshSessionKey { get; init; }
    }

    private readonly ConfidentialEnclaveAttestationEngine _attestationEngine;
    private readonly ConcurrentDictionary<string, MeshChannelState> _activeChannels = new(StringComparer.OrdinalIgnoreCase);

    public MicroVmConfidentialMeshService(ConfidentialEnclaveAttestationEngine attestationEngine)
    {
        _attestationEngine = attestationEngine ?? throw new ArgumentNullException(nameof(attestationEngine));
    }

    /// <summary>
    /// Establishes a mutually attested confidential mesh channel between two micro-VMs.
    /// </summary>
    public ConfidentialMeshChannel EstablishChannel(EstablishMeshChannelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Verify Source Enclave Attestation Report
        var sourceResult = _attestationEngine.VerifyReport(request.SourceReport, request.SharedChallengeNonceHex);
        if (!sourceResult.Passed)
        {
            throw new InvalidOperationException($"Source enclave attestation failed: {sourceResult.FailureReason}");
        }

        // 2. Verify Target Enclave Attestation Report
        var targetResult = _attestationEngine.VerifyReport(request.TargetReport, request.SharedChallengeNonceHex);
        if (!targetResult.Passed)
        {
            throw new InvalidOperationException($"Target enclave attestation failed: {targetResult.FailureReason}");
        }

        // 3. Derive symmetric mesh session key via HKDF
        var ikm = Encoding.UTF8.GetBytes($"{sourceResult.EnclaveSessionToken}:{targetResult.EnclaveSessionToken}");
        var salt = Encoding.UTF8.GetBytes(request.SharedChallengeNonceHex);
        var info = Encoding.UTF8.GetBytes("SGrowing.ConfidentialMicroVmMesh.TlsChannel.2026");

        var sessionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 32, salt, info);

        var channelId = $"MSH-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var channel = new ConfidentialMeshChannel(
            ChannelId: channelId,
            SourceEnclaveId: request.SourceEnclaveId,
            TargetEnclaveId: request.TargetEnclaveId,
            MutualAttestationVerified: true,
            EstablishedAtUtc: DateTime.UtcNow,
            MessagesTransferred: 0,
            IsActive: true
        );

        _activeChannels[channelId] = new MeshChannelState
        {
            Channel = channel,
            MeshSessionKey = sessionKey
        };

        return channel;
    }

    /// <summary>
    /// Encrypts message payload across the confidential mesh channel using authenticated AES-256-GCM.
    /// </summary>
    public byte[] EncryptMeshMessage(string channelId, byte[] plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentNullException.ThrowIfNull(plaintext);

        if (!_activeChannels.TryGetValue(channelId, out var state) || !state.Channel.IsActive)
        {
            throw new InvalidOperationException($"Mesh channel '{channelId}' is not active or does not exist.");
        }

        var nonce = new byte[12]; // 96-bit GCM nonce
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];   // 128-bit authentication tag

        using (var aesGcm = new AesGcm(state.MeshSessionKey, 16))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // Update channel message count
        state.Channel = state.Channel with { MessagesTransferred = state.Channel.MessagesTransferred + 1 };

        // Output format: [12-byte Nonce] + [16-byte Tag] + [Ciphertext]
        var combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length + tag.Length, ciphertext.Length);

        return combined;
    }

    /// <summary>
    /// Decrypts and authenticates a message payload received across the mesh channel.
    /// </summary>
    public byte[] DecryptMeshMessage(string channelId, byte[] encryptedPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentNullException.ThrowIfNull(encryptedPayload);

        if (!_activeChannels.TryGetValue(channelId, out var state) || !state.Channel.IsActive)
        {
            throw new InvalidOperationException($"Mesh channel '{channelId}' is not active or does not exist.");
        }

        if (encryptedPayload.Length < 28) // 12 (nonce) + 16 (tag)
        {
            throw new ArgumentException("Encrypted payload is too short.", nameof(encryptedPayload));
        }

        var nonce = new byte[12];
        var tag = new byte[16];
        var ciphertextLength = encryptedPayload.Length - 28;
        var ciphertext = new byte[ciphertextLength];

        Buffer.BlockCopy(encryptedPayload, 0, nonce, 0, 12);
        Buffer.BlockCopy(encryptedPayload, 12, tag, 0, 16);
        Buffer.BlockCopy(encryptedPayload, 28, ciphertext, 0, ciphertextLength);

        var plaintext = new byte[ciphertextLength];
        using (var aesGcm = new AesGcm(state.MeshSessionKey, 16))
        {
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return plaintext;
    }

    /// <summary>
    /// Terminates a confidential mesh channel and wipes session keys.
    /// </summary>
    public bool TerminateChannel(string channelId)
    {
        if (_activeChannels.TryRemove(channelId, out var state))
        {
            CryptographicOperations.ZeroMemory(state.MeshSessionKey);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Retrieves active channel details.
    /// </summary>
    public ConfidentialMeshChannel? GetChannel(string channelId)
    {
        return _activeChannels.TryGetValue(channelId, out var state) ? state.Channel : null;
    }
}
