using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Infrastructure.SecretRotationMesh;

public sealed class SecretRotationMeshEngine
{
    private readonly ConcurrentDictionary<string, ManagedSecretProfile> _secrets = new();
    private readonly ConcurrentDictionary<string, EphemeralCertRecord> _certs = new();
    private readonly ConcurrentBag<SecretRotationReceipt> _rotationHistory = new();

    public SecretRotationMeshEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var s1 = new ManagedSecretProfile(
            "SEC-DB-PG-MASTER",
            "PostgreSQL 17 Aurora Multi-Region Cluster Superuser Key",
            "PostgreSQL Replica",
            30,
            DateTime.UtcNow.AddDays(-12),
            DateTime.UtcNow.AddDays(18),
            "SYNCED_AWS_GCP_AZURE_HSM"
        );

        var s2 = new ManagedSecretProfile(
            "SEC-API-STRIPE-LIVE",
            "Stripe Global Payment Gateway Restricted API Key",
            "Stripe Webhook Key",
            60,
            DateTime.UtcNow.AddDays(-20),
            DateTime.UtcNow.AddDays(40),
            "SYNCED_AWS_GCP_AZURE_HSM"
        );

        _secrets[s1.SecretId] = s1;
        _secrets[s2.SecretId] = s2;

        var c1 = new EphemeralCertRecord(
            "0x7F998811AA22",
            "CN=edge-proxy-fra-01.mesh.sgrowing.io",
            "EDGE-POP-FRA-01",
            15,
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddMinutes(10),
            "CERT_ACTIVE_VALID"
        );

        _certs[c1.CertSerialHex] = c1;
    }

    public SecretRotationOverview GetOverview()
    {
        var secretsList = _secrets.Values.ToList();
        var certsList = _certs.Values.ToList();

        return new SecretRotationOverview(
            secretsList.Count,
            certsList.Count,
            128,
            100.0,
            secretsList,
            certsList
        );
    }

    public EphemeralCertResult IssueEphemeralCert(IssueEphemeralCertRequest request)
    {
        var serial = $"0x{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var expires = DateTime.UtcNow.AddMinutes(request.ValidDurationMinutes);

        var cert = new EphemeralCertRecord(
            serial,
            request.SubjectDistinguishedName,
            request.TargetEdgeNode,
            request.ValidDurationMinutes,
            DateTime.UtcNow,
            expires,
            "CERT_ACTIVE_VALID"
        );

        _certs[serial] = cert;

        return new EphemeralCertResult(
            serial,
            request.SubjectDistinguishedName,
            "-----BEGIN CERTIFICATE-----\nMIIBkzCCATygAwIBAgIQ...==\n-----END CERTIFICATE-----",
            expires,
            $"SOV-SIG-{Guid.NewGuid():N}"[..16].ToUpperInvariant()
        );
    }

    public SecretRotationReceipt RotateSecret(RotateSecretRequest request)
    {
        var secret = _secrets.TryGetValue(request.SecretId, out var s) ? s : _secrets.Values.First();

        var updated = secret with
        {
            LastRotatedAt = DateTime.UtcNow,
            NextScheduledRotation = DateTime.UtcNow.AddDays(secret.RotationCadenceDays)
        };
        _secrets[secret.SecretId] = updated;

        var receipt = new SecretRotationReceipt(
            $"ROT-EXEC-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            secret.SecretId,
            $"v_{Guid.NewGuid():N}"[..8],
            $"v_{Guid.NewGuid():N}"[..8],
            "ZERO_DOWNTIME_ROTATION_SUCCESSFUL",
            $"0x{Guid.NewGuid():N}"[..16],
            DateTime.UtcNow
        );

        _rotationHistory.Add(receipt);
        return receipt;
    }
}
