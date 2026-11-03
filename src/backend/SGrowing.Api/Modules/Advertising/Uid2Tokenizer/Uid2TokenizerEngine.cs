using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Advertising.Uid2Tokenizer;

public interface IUid2TokenizerEngine
{
    Uid2TokenizerOverview GetOverview();
    GenerateTokenResult GenerateToken(GenerateTokenCommand command);
    StitchIdentityResult StitchIdentity(StitchIdentityCommand command);
}

public class Uid2TokenizerEngine : IUid2TokenizerEngine
{
    private readonly List<FirstPartyIdentityNode> _identities =
    [
        new("GRAPH-ID-01", "8f4a9b2c8e11a9f029381...", "99a0b12e847c2...", "FP-COOKIE-9901", "GAID-8812-401", 14, DateTime.UtcNow.AddMinutes(-15), true),
        new("GRAPH-ID-02", "3b2c1a9e8841029410f92...", "", "FP-COOKIE-8814", "IDFA-7714-209", 8, DateTime.UtcNow.AddHours(-2), true),
        new("GRAPH-ID-03", "110294f881249b01293ac...", "44b912c019284...", "FP-COOKIE-7721", "", 22, DateTime.UtcNow.AddMinutes(-5), true)
    ];

    private readonly List<Uid2Token> _tokens =
    [
        new("UID2-TOKEN-AD-88419A", "REFRESH-TK-110", DateTime.UtcNow.AddHours(-4), DateTime.UtcNow.AddDays(30), "8f4a9b2c8e11a9f029381...", "SALT-BUCKET-2026-Q1", "Active"),
        new("UID2-TOKEN-AD-33129B", "REFRESH-TK-111", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddDays(30), "3b2c1a9e8841029410f92...", "SALT-BUCKET-2026-Q1", "Active"),
        new("UID2-TOKEN-AD-99410C", "REFRESH-TK-112", DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddDays(30), "110294f881249b01293ac...", "SALT-BUCKET-2026-Q1", "Active")
    ];

    public Uid2TokenizerOverview GetOverview()
    {
        var totalIdentities = _identities.Count;
        var activeTokens = _tokens.Count(t => t.TokenStatus == "Active");
        var complianceRate = 99.8;
        var dailyStitched = 8450;

        return new Uid2TokenizerOverview(
            totalIdentities,
            activeTokens,
            complianceRate,
            dailyStitched,
            _identities,
            _tokens
        );
    }

    public GenerateTokenResult GenerateToken(GenerateTokenCommand command)
    {
        var normalized = command.RawEmail.Trim().ToLowerInvariant();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized + command.SaltBucketKey));
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var adToken = $"UID2-TOKEN-AD-{Guid.NewGuid().ToString("N")[..16].ToUpperInvariant()}";
        var refreshToken = $"UID2-REFRESH-{Guid.NewGuid().ToString("N")[..16].ToUpperInvariant()}";
        var expires = DateTime.UtcNow.AddDays(30);

        return new GenerateTokenResult(
            adToken,
            refreshToken,
            expires,
            hashHex,
            true // EUID compliant
        );
    }

    public StitchIdentityResult StitchIdentity(StitchIdentityCommand command)
    {
        var graphId = $"GRAPH-ID-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var unifiedProfileId = $"PROFILE-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var adToken = $"UID2-TOKEN-AD-{Guid.NewGuid().ToString("N")[..16].ToUpperInvariant()}";

        return new StitchIdentityResult(
            graphId,
            unifiedProfileId,
            6, // Merged sessions count
            adToken,
            $"RECEIPT-STITCH-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}"
        );
    }
}
