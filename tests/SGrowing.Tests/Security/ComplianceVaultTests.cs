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

    [Fact]
    public void RedactPii_NullOrEmptyInput_ReturnsZeroMatches()
    {
        var nullResult = _engine.RedactPii(new ScanPiiRequest(null!));
        Assert.Equal(0, nullResult.TotalPiiCount);
        Assert.Empty(nullResult.Matches);
        Assert.Equal(string.Empty, nullResult.SanitizedText);

        var emptyResult = _engine.RedactPii(new ScanPiiRequest("   "));
        Assert.Equal(0, emptyResult.TotalPiiCount);
        Assert.Empty(emptyResult.Matches);
    }

    [Fact]
    public void RedactPii_CleanInputWithoutPii_ReturnsTextUnmodified()
    {
        var text = "Standard B2B marketing cadence with high open rates and domain deliverability.";
        var result = _engine.RedactPii(new ScanPiiRequest(text));

        Assert.Equal(0, result.TotalPiiCount);
        Assert.Empty(result.Matches);
        Assert.Equal(text, result.SanitizedText);
    }

    [Fact]
    public void RecordAuditLeaf_ComputesDeterministicSha256OfPayload()
    {
        var payload = "{\"event\": \"security_rotation_trigger\"}";
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var leaf = _engine.RecordAuditLeaf("SYSTEM_EVENT", "operator_1", "tenant_1", payload);

        Assert.Equal(expectedHash, leaf.PayloadSha256);
    }

    [Fact]
    public void RedactPii_MultipleOccurrencesOfSameCategory_SanitizesAllAndIncrementsCount()
    {
        var input = "Primary email: alice@domain.org, secondary: support@backup.io; Call 415-555-1234 or 212-555-9876.";
        var result = _engine.RedactPii(new ScanPiiRequest(input));

        Assert.NotNull(result);
        Assert.Equal(4, result.TotalPiiCount);
        Assert.DoesNotContain("alice@domain.org", result.SanitizedText);
        Assert.DoesNotContain("support@backup.io", result.SanitizedText);
        Assert.DoesNotContain("415-555-1234", result.SanitizedText);
        Assert.DoesNotContain("212-555-9876", result.SanitizedText);
        Assert.Equal(2, result.Matches.Count(m => m.Category == PiiCategory.Email));
        Assert.Equal(2, result.Matches.Count(m => m.Category == PiiCategory.PhoneNumber));
    }

    [Fact]
    public void ListDataResidencyBoundaries_ContainsAsiaPacificJurisdictionWithPermittedSubprocessors()
    {
        var boundaries = _engine.ListDataResidencyBoundaries();

        var apac = Assert.Single(boundaries, b => b.RegionCode == "ap-southeast-1");
        Assert.True(apac.IsEnforced);
        Assert.Contains("Singapore Equinix SG1", apac.PrimaryStorageLocation);
        Assert.Contains("GCP Singapore", apac.PermittedSubprocessors);
    }
}
