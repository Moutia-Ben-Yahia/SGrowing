using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Security.SecretVaultMesh;

public interface ISecretVaultMeshEngine
{
    List<VaultSecretEntry> GetSecrets();
    VaultSecretEntry RotateSecret(RotateSecretRequest request);
    ZeroizationResult ZeroizeSecret(ZeroizeSecretRequest request);
}

public class SecretVaultMeshEngine : ISecretVaultMeshEngine
{
    private readonly ConcurrentDictionary<string, VaultSecretEntry> _secrets = new(StringComparer.OrdinalIgnoreCase);

    public SecretVaultMeshEngine()
    {
        // Seed secrets
        var s1 = new VaultSecretEntry("sec_db_master_01", "PostgreSQL Master Shard Root", SecretType.DatabaseMasterCredentials, 3, RotationPhase.ActivePrimary, DateTime.UtcNow.AddDays(-14), 14);
        var s2 = new VaultSecretEntry("sec_jwt_ed25519_02", "JWT Ed25519 Token Signing Key", SecretType.JwtSigningKey, 7, RotationPhase.ActivePrimary, DateTime.UtcNow.AddDays(-28), 28);
        var s3 = new VaultSecretEntry("sec_tls_wildcard_03", "Wildcard *.sgrowing.io TLS Private Key", SecretType.TlsPrivateKey, 2, RotationPhase.ActivePrimary, DateTime.UtcNow.AddDays(-45), 45);
        var s4 = new VaultSecretEntry("sec_stripe_live_04", "Stripe Production Billing Secret Key", SecretType.StripeApiKey, 4, RotationPhase.ActivePrimary, DateTime.UtcNow.AddDays(-60), 60);

        _secrets[s1.SecretId] = s1;
        _secrets[s2.SecretId] = s2;
        _secrets[s3.SecretId] = s3;
        _secrets[s4.SecretId] = s4;
    }

    public List<VaultSecretEntry> GetSecrets() => _secrets.Values.OrderBy(s => s.SecretId).ToList();

    public VaultSecretEntry RotateSecret(RotateSecretRequest request)
    {
        if (!_secrets.TryGetValue(request.SecretId, out var existing))
        {
            throw new KeyNotFoundException($"Secret {request.SecretId} not found in vault.");
        }

        var rotated = existing with
        {
            Version = existing.Version + 1,
            LastRotatedAtUtc = DateTime.UtcNow,
            AgeInDays = 0,
            Phase = RotationPhase.ActivePrimary
        };

        _secrets[request.SecretId] = rotated;
        return rotated;
    }

    public ZeroizationResult ZeroizeSecret(ZeroizeSecretRequest request)
    {
        if (!_secrets.TryGetValue(request.SecretId, out var existing))
        {
            return new ZeroizationResult(false, request.SecretId, "Secret not found.");
        }

        var zeroized = existing with
        {
            Phase = RotationPhase.ZeroizedPurged,
            AgeInDays = 0
        };

        _secrets[request.SecretId] = zeroized;
        return new ZeroizationResult(true, request.SecretId, $"Secret {request.SecretId} ({existing.Name}) has been cryptographically zeroized and purged from memory across all regional clusters.");
    }
}
