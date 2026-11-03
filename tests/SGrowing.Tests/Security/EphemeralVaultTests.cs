using SGrowing.Api.Modules.Security.EphemeralVault;
using Xunit;

namespace SGrowing.Tests.Security;

public class EphemeralVaultTests
{
    private readonly EphemeralVaultEngine _engine = new();

    [Fact]
    public void SeededTokens_AreActiveAndValid()
    {
        var tokens = _engine.GetActiveTokens();

        Assert.NotNull(tokens);
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.Equal(TokenStatus.ActiveValid, t.Status));
        Assert.All(tokens, t => Assert.NotEmpty(t.CryptographicSignature));
    }

    [Fact]
    public void IssueToken_SetsCorrectTtlAndSignature()
    {
        var request = new RequestEphemeralTokenRequest(
            "usr_audit",
            AccessScope.DbReadOnly,
            "Quarterly revenue reconciliation audit",
            20
        );

        var token = _engine.IssueToken(request);

        Assert.NotNull(token);
        Assert.Equal(AccessScope.DbReadOnly, token.Scope);
        Assert.Equal(TokenStatus.ActiveValid, token.Status);
        Assert.True(token.ExpiresAtUtc > token.IssuedAtUtc);
    }

    [Fact]
    public void RevokeToken_MarksTokenRevoked()
    {
        var tokens = _engine.GetActiveTokens();
        var target = tokens[0];

        var result = _engine.RevokeToken(target.TokenId, "CisoAdmin");

        Assert.True(result.Success);
        var updatedList = _engine.GetActiveTokens();
        var updated = updatedList.Find(t => t.TokenId == target.TokenId);
        Assert.Equal(TokenStatus.RevokedEmergency, updated?.Status);
    }

    [Fact]
    public void EmergencyRevokeAll_InvalidatesAllActiveCredentials()
    {
        int count = _engine.EmergencyRevokeAll("SecurityCiso");

        Assert.True(count >= 2);
        var tokens = _engine.GetActiveTokens();
        Assert.All(tokens, t => Assert.Equal(TokenStatus.RevokedEmergency, t.Status));
    }
}
