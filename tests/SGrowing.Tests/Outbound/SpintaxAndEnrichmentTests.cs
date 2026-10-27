using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.Outbound.EmailVerification;
using SGrowing.Api.Modules.Outbound.Enrichment;
using SGrowing.Api.Modules.Outbound.Spintax;
using Xunit;

namespace SGrowing.Tests.Outbound;

public class SpintaxAndEnrichmentTests
{
    private readonly SpintaxService _spintaxService = new();
    private readonly EmailVerificationService _verifier = new();

    private (SGrowingDbContext db, ContactEnrichmentPipeline pipeline) CreateEnrichmentPipeline()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new SGrowingDbContext(options);
        var leadEnrichmentService = new LeadEnrichmentService(db);
        var pipeline = new ContactEnrichmentPipeline(leadEnrichmentService, _verifier);
        return (db, pipeline);
    }

    [Fact]
    public void Spintax_NestedSyntax_ResolvesCorrectly()
    {
        // Arrange
        var template = "{Hi|Hello {there|friend}}, {FirstName}! {Hope you are having a {great|productive} week.|Hope all is well.}";
        var variables = new Dictionary<string, string> { ["FirstName"] = "Sarah" };

        // Act
        var result = _spintaxService.Parse(template, seed: 42, variables: variables);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Sarah", result);
        Assert.DoesNotContain("{", result);
        Assert.DoesNotContain("}", result);
        Assert.DoesNotContain("|", result);
    }

    [Fact]
    public void Spintax_DeterministicSeed_ProducesIdenticalVariantForProspect()
    {
        // Arrange
        var template = "{Hi|Hello|Hey|Greetings} {FirstName}, {quick question regarding|reaching out about} your pipeline.";
        var prospect = "john.doe@enterprise-corp.com";
        var variables = new Dictionary<string, string> { ["FirstName"] = "John" };

        // Act
        var run1 = _spintaxService.ParseForProspect(template, prospect, variables);
        var run2 = _spintaxService.ParseForProspect(template, prospect, variables);
        var run3 = _spintaxService.ParseForProspect(template, prospect, variables);

        // Assert
        Assert.Equal(run1, run2);
        Assert.Equal(run2, run3);
    }

    [Fact]
    public void Spintax_CalculatePermutationsCount_ComputesAccurateSpace()
    {
        // Arrange: 3 choices * 2 choices * 2 choices = 12 total permutations
        var template = "{Hi|Hello|Hey} {Mr.|Dr.} Smith, {thanks|cheers}!";

        // Act
        var count = _spintaxService.CalculatePermutationsCount(template);

        // Assert
        Assert.Equal(12, count);
    }

    [Fact]
    public async Task EmailVerifier_ValidEmail_ReturnsDeliverable()
    {
        // Arrange
        var email = "alex.founder@growth-scale.io";

        // Act
        var result = await _verifier.VerifyEmailAsync(email);

        // Assert
        Assert.True(result.SyntaxValid);
        Assert.False(result.IsDisposable);
        Assert.True(result.HasMxRecords);
        Assert.True(result.SmtpConnectionPassed);
        Assert.Equal(EmailVerificationStatus.Deliverable, result.Status);
        Assert.InRange(result.RiskScore, 0, 20);
    }

    [Fact]
    public async Task EmailVerifier_DisposableEmail_DetectsAndFlagsRisky()
    {
        // Arrange
        var email = "throwaway9921@mailinator.com";

        // Act
        var result = await _verifier.VerifyEmailAsync(email);

        // Assert
        Assert.True(result.SyntaxValid);
        Assert.True(result.IsDisposable);
        Assert.Equal(EmailVerificationStatus.Disposable, result.Status);
        Assert.True(result.RiskScore >= 90);
    }

    [Fact]
    public async Task EmailVerifier_MalformedEmail_ReturnsUndeliverable()
    {
        // Arrange
        var email = "not-an-email-address@@invalid..com";

        // Act
        var result = await _verifier.VerifyEmailAsync(email);

        // Assert
        Assert.False(result.SyntaxValid);
        Assert.Equal(EmailVerificationStatus.Undeliverable, result.Status);
        Assert.Equal(100, result.RiskScore);
    }

    [Fact]
    public async Task ContactEnrichmentPipeline_EnrichesContactData_WithDeliverabilityAndSeniority()
    {
        // Arrange
        var (_, pipeline) = CreateEnrichmentPipeline();
        var tenantId = Guid.NewGuid();
        var request = new ContactEnrichmentRequest(
            Email: "elena.rostova@fintech-ventures.com",
            FirstName: "Elena",
            LastName: "Rostova",
            JobTitle: "Chief Technology Officer & VP Engineering",
            Website: "https://fintech-ventures.com"
        );

        // Act
        var enriched = await pipeline.EnrichAndVerifyContactAsync(tenantId, request);

        // Assert
        Assert.Equal("C-Level", enriched.SeniorityLevel);
        Assert.Equal("Fintech & Payments", enriched.Industry);
        Assert.Equal(EmailVerificationStatus.Deliverable, enriched.Deliverability.Status);
        Assert.True(enriched.OverallDataConfidenceScore >= 75);
        Assert.Contains("Elena", enriched.FirstName);
        Assert.NotEmpty(enriched.TechStack);
    }

    [Fact]
    public void Spintax_UnmappedPlaceholderWithoutVariables_TerminatesWithoutHanging()
    {
        // Arrange: Template with spintax and unmapped tag {UnknownTag} with NO variables dictionary
        var template = "{Hello|Greetings} {UnknownTag}, welcome to {OurCompany}!";

        // Act - should terminate immediately, not enter an infinite loop
        var result = _spintaxService.Parse(template, seed: 10, variables: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("{UnknownTag}", result);
        Assert.Contains("{OurCompany}", result);
        Assert.True(result.StartsWith("Hello") || result.StartsWith("Greetings"));
    }

    [Fact]
    public void Spintax_HighPermutations_CalculatesAccurateCombinations()
    {
        // Arrange: 5 * 4 * 3 * 2 = 120 combinations
        var template = "{A|B|C|D|E} {1|2|3|4} {X|Y|Z} {alpha|beta}";

        // Act
        var count = _spintaxService.CalculatePermutationsCount(template);
        var variants = _spintaxService.GenerateVariants(template, count: 10);

        // Assert
        Assert.Equal(120, count);
        Assert.Equal(10, variants.Count);
    }

    [Fact]
    public async Task EmailVerifier_BatchVerify_ProcessesAllAddresses()
    {
        // Arrange
        var list = new[] { "clean@validcorp.com", "fake@mailinator.com", "malformed@@bad" };

        // Act
        var results = await _verifier.BatchVerifyEmailsAsync(list);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(EmailVerificationStatus.Deliverable, results[0].Status);
        Assert.Equal(EmailVerificationStatus.Disposable, results[1].Status);
        Assert.Equal(EmailVerificationStatus.Undeliverable, results[2].Status);
    }
}
