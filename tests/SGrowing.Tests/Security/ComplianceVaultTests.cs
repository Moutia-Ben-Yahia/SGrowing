using SGrowing.Api.Modules.Security.ComplianceVault;

namespace SGrowing.Tests.Security;

public class ComplianceVaultTests
{
    private readonly IComplianceVaultEngine _engine = new ComplianceVaultEngine();

    [Fact]
    public void RedactPii_MasksEmailsPhonesAndCreditCardsWithSyntheticTokens()
    {
        var input = "Please contact John Doe at john.doe@enterprise.com or call 415-555-0199 regarding card 4111-2222-3333-4444 and SSN 123-45-6789.";
        var result = _engine.RedactPii(new ScanPiiRequest(input));

        Assert.NotNull(result);
        Assert.True(result.TotalPiiCount >= 3);
        Assert.DoesNotContain("john.doe@enterprise.com", result.SanitizedText);
        Assert.DoesNotContain("4111-2222-3333-4444", result.SanitizedText);
        Assert.DoesNotContain("123-45-6789", result.SanitizedText);
        Assert.Contains("[REDACTED_EMAIL_", result.SanitizedText);
        Assert.Contains("[REDACTED_SSN_", result.SanitizedText);
    }

    [Fact]
    public void RecordAuditLeaf_UpdatesMerkleTreeAndMaintainsTamperProofRoot()
    {
        var leaf = _engine.RecordAuditLeaf("SECRET_VAULT_ACCESSED", "auditor_bob", "org_acme", "{\"key\": \"DB_MASTER_KEY\"}");

        Assert.NotNull(leaf);
        Assert.NotEmpty(leaf.MerkleHash);
        Assert.NotEmpty(leaf.PayloadSha256);

        var verify = _engine.VerifyMerkleRoot();
        Assert.NotNull(verify);
        Assert.True(verify.IsTamperProof);
        Assert.True(verify.TotalLeaves >= 4);
        Assert.NotEmpty(verify.RootHash);
    }

    [Fact]
    public void ListDataResidencyBoundaries_ReturnsEnforcedRegions()
    {
        var boundaries = _engine.ListDataResidencyBoundaries();

        Assert.NotEmpty(boundaries);
        Assert.Contains(boundaries, b => b.RegionCode == "eu-central-1" && b.IsEnforced);
        Assert.Contains(boundaries, b => b.RegionCode == "us-east-1" && b.IsEnforced);
    }
}
