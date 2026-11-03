using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.AI.DocumentGrounding;
using Xunit;

namespace SGrowing.Tests.AI;

public sealed class DocumentGroundingTests
{
    private readonly DocumentGroundingCitationEngine _engine = new();

    private static IReadOnlyList<DocumentChunk> CreateContractCorpus()
    {
        return
        [
            new(
                ChunkId: "CHUNK-01",
                DocumentName: "Master_Service_Agreement_v2.pdf",
                PageNumber: 14,
                SectionTitle: "Section 8.2: Service Level Agreement (SLA) & Uptime",
                VerbatimContent: "The Vendor guarantees a minimum monthly platform uptime of 99.95%, excluding scheduled maintenance windows announced at least 72 hours in advance. Failure to meet this uptime SLA entitles Customer to a 15% billing credit applied against the next invoice cycle."
            ),
            new(
                ChunkId: "CHUNK-02",
                DocumentName: "Master_Service_Agreement_v2.pdf",
                PageNumber: 21,
                SectionTitle: "Section 14.1: Governing Law & Jurisdiction",
                VerbatimContent: "This Agreement shall be governed by and construed in accordance with the laws of the Commercial Court of Algiers, Algeria. Any dispute arising under this Agreement that cannot be amicably settled within thirty days shall be subject to binding arbitration."
            )
        ];
    }

    [Fact]
    public void VerifyAnswerGrounding_FullyGroundedAnswer_ReturnsHighGroundingScoreAndCitations()
    {
        // Arrange: Candidate answer grounded directly in Section 8.2
        var question = "What is the guaranteed uptime SLA and penalty credit?";
        var answer = "The Vendor guarantees a minimum monthly platform uptime of 99.95%, excluding scheduled maintenance announced 72 hours in advance. Failure to meet the uptime SLA entitles Customer to a 15% billing credit applied against the next invoice cycle.";

        var request = new AuditAnswerGroundingRequest(
            Question: question,
            CandidateAnswer: answer,
            RetrievedCorpusChunks: CreateContractCorpus()
        );

        // Act
        var result = _engine.VerifyAnswerGrounding(request);

        // Assert
        result.Should().NotBeNull();
        result.OverallGroundingScorePercent.Should().BeGreaterThanOrEqualTo(85.0);
        result.HallucinationRiskDetected.Should().BeFalse();
        result.TotalSentences.Should().Be(2);
        result.GroundedSentencesCount.Should().Be(2);
        result.SentenceCitations[0].CitationReference.Should().Contain("Master_Service_Agreement_v2.pdf");
        result.SentenceCitations[0].CitationReference.Should().Contain("Section 8.2");
    }

    [Fact]
    public void VerifyAnswerGrounding_HallucinatedAnswer_FlagsHallucinationRiskDetected()
    {
        // Arrange: Answer completely hallucinated with fictitious numbers and jurisdictions
        var question = "What is the penalty for SLA breach?";
        var answer = "The vendor provides a 50% refund under the jurisdiction of the Singapore International Arbitration Centre with 99.999% uptime.";

        var request = new AuditAnswerGroundingRequest(
            Question: question,
            CandidateAnswer: answer,
            RetrievedCorpusChunks: CreateContractCorpus()
        );

        // Act
        var result = _engine.VerifyAnswerGrounding(request);

        // Assert
        result.HallucinationRiskDetected.Should().BeTrue();
        result.OverallGroundingScorePercent.Should().BeLessThan(50.0);
        result.SentenceCitations[0].IsGrounded.Should().BeFalse();
    }

    [Fact]
    public void VerifyAnswerGrounding_MixedSentences_IdentifiesUngroundedSentencesCorrectly()
    {
        // Arrange: Sentence 1 is authentic; Sentence 2 is fabricated
        var question = "What is the governing law and payment terms?";
        var answer = "This Agreement shall be governed by and construed in accordance with the laws of the Commercial Court of Algiers. Customers must pay invoices strictly within 3 days via cryptocurrency or cash courier.";

        var request = new AuditAnswerGroundingRequest(
            Question: question,
            CandidateAnswer: answer,
            RetrievedCorpusChunks: CreateContractCorpus()
        );

        // Act
        var result = _engine.VerifyAnswerGrounding(request);

        // Assert
        result.TotalSentences.Should().Be(2);
        result.GroundedSentencesCount.Should().Be(1);
        result.SentenceCitations[0].IsGrounded.Should().BeTrue();
        result.SentenceCitations[1].IsGrounded.Should().BeFalse();
    }

    [Fact]
    public void AddDocumentGroundingServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDocumentGroundingServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(DocumentGroundingCitationEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void VerifyAnswerGrounding_NullRequest_ThrowsArgumentNullException()
    {
        var act = () => _engine.VerifyAnswerGrounding(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void VerifyAnswerGrounding_EmptyOrWhitespaceAnswer_ReturnsZeroGroundingScore()
    {
        var request = new AuditAnswerGroundingRequest("What is SLA?", "   ", CreateContractCorpus());
        var result = _engine.VerifyAnswerGrounding(request);

        result.Should().NotBeNull();
        result.TotalSentences.Should().Be(0);
        result.OverallGroundingScorePercent.Should().Be(0.0);
        result.HallucinationRiskDetected.Should().BeTrue();
    }

    [Fact]
    public void VerifyAnswerGrounding_EmptyCorpusChunks_ReturnsUngroundedResults()
    {
        var request = new AuditAnswerGroundingRequest(
            "What is SLA?",
            "Guaranteed uptime is 99.95%.",
            new List<DocumentChunk>());

        var result = _engine.VerifyAnswerGrounding(request);

        result.Should().NotBeNull();
        result.TotalSentences.Should().Be(1);
        result.GroundedSentencesCount.Should().Be(0);
        result.SentenceCitations[0].IsGrounded.Should().BeFalse();
        result.SentenceCitations[0].CitationReference.Should().BeNull();
        result.HallucinationRiskDetected.Should().BeTrue();
    }
}
