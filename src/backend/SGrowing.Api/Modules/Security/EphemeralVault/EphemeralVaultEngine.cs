using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.EphemeralVault;

public interface IEphemeralVaultEngine
{
    EphemeralToken IssueToken(RequestEphemeralTokenRequest request);
    List<EphemeralToken> GetActiveTokens();
    RevocationResult RevokeToken(string tokenId, string revokedBy);
    int EmergencyRevokeAll(string authorizedBy);
}

public class EphemeralVaultEngine : IEphemeralVaultEngine
{
    private readonly ConcurrentDictionary<string, EphemeralToken> _vault = new(StringComparer.OrdinalIgnoreCase);

    public EphemeralVaultEngine()
    {
        // Seed active ephemeral tokens
        IssueToken(new RequestEphemeralTokenRequest(
            "usr_sec_lead",
            AccessScope.CustomerPiiAccess,
            "GDPR subject data erasure verification",
            15
        ));

        IssueToken(new RequestEphemeralTokenRequest(
            "usr_sre_operator",
            AccessScope.ProdDeploy,
            "Emergency hotfix Canary ring 0 promotion",
            30
        ));
    }

    public EphemeralToken IssueToken(RequestEphemeralTokenRequest request)
    {
        var duration = Math.Clamp(request.DurationMinutes, 5, 60);
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(duration);
        var tokenId = $"eph_tok_{DateTime.UtcNow.Ticks % 1000000}";

        var rawSecret = Guid.NewGuid().ToString("N");
        var prefix = $"{rawSecret[..8]}...";

        var sigBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{tokenId}:{request.RequestedByUserId}:{request.Scope}:{expires:O}"));
        var signature = Convert.ToHexString(sigBytes)[..24];

        var token = new EphemeralToken(
            tokenId,
            request.RequestedByUserId,
            request.Scope,
            request.JustificationReason,
            now,
            expires,
            TokenStatus.ActiveValid,
            prefix,
            signature
        );

        _vault[tokenId] = token;
        return token;
    }

    public List<EphemeralToken> GetActiveTokens()
    {
        var now = DateTime.UtcNow;
        return _vault.Values
            .Select(t => (t.Status == TokenStatus.ActiveValid && t.ExpiresAtUtc < now)
                ? t with { Status = TokenStatus.ExpiredTtl }
                : t)
            .OrderByDescending(t => t.IssuedAtUtc)
            .ToList();
    }

    public RevocationResult RevokeToken(string tokenId, string revokedBy)
    {
        if (!_vault.TryGetValue(tokenId, out var token))
        {
            return new RevocationResult(false, tokenId, revokedBy, DateTime.UtcNow, "Token not found.");
        }

        var revoked = token with { Status = TokenStatus.RevokedEmergency };
        _vault[tokenId] = revoked;

        return new RevocationResult(true, tokenId, revokedBy, DateTime.UtcNow, "Ephemeral token revoked immediately across mesh.");
    }

    public int EmergencyRevokeAll(string authorizedBy)
    {
        int count = 0;
        foreach (var key in _vault.Keys)
        {
            if (_vault.TryGetValue(key, out var token) && token.Status == TokenStatus.ActiveValid)
            {
                _vault[key] = token with { Status = TokenStatus.RevokedEmergency };
                count++;
            }
        }

        return count;
    }
}
