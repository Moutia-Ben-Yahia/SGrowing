using FluentAssertions;
using SGrowing.Api.Modules.AI.Memory;
using Xunit;

namespace SGrowing.Tests.Unit;

public class AgentMemoryServiceTests
{
    private readonly AgentMemoryService _service;

    public AgentMemoryServiceTests()
    {
        _service = new AgentMemoryService();
    }

    [Fact]
    public void SeedTenantDefaults_PopulatesCuratedDefaultFacts()
    {
        // Act
        var memories = _service.GetAllMemories(AgentMemoryService.DefaultTenantId);

        // Assert
        memories.Should().HaveCount(4);

        // 1. Brand Voice
        var brand = memories.FirstOrDefault(m => m.Category == AgentMemoryCategory.BrandMemory);
        brand.Should().NotBeNull();
        brand!.Key.Should().Be("Brand Voice Guidelines");
        brand.Value.Should().Contain("Direct, culturally resonant, respectful in Arabic & English");
        brand.Confidence.Should().Be(MemoryConfidenceLevel.Verified);
        brand.SourceAgent.Should().Be("BrandToneDirectorAgent");
        brand.Tags.Should().Contain(new[] { "brand", "voice", "tone", "arabic", "english" });

        // 2. Business Rules
        var biz = memories.FirstOrDefault(m => m.Category == AgentMemoryCategory.BusinessMemory);
        biz.Should().NotBeNull();
        biz!.Value.Should().Contain("Saudi 15% VAT, 24h COD delivery in Riyadh, 3-day return policy");
        biz.Confidence.Should().Be(MemoryConfidenceLevel.Verified);

        // 3. Campaign Memory
        var camp = memories.FirstOrDefault(m => m.Category == AgentMemoryCategory.CampaignMemory);
        camp.Should().NotBeNull();
        camp!.Value.Should().Contain("Ramadan discount max 25%, ROAS target 3.8x");
        camp.Confidence.Should().Be(MemoryConfidenceLevel.High);

        // 4. Customer Context
        var cust = memories.FirstOrDefault(m => m.Category == AgentMemoryCategory.CustomerContext);
        cust.Should().NotBeNull();
        cust!.Value.Should().Contain("VIP tier requires dedicated WhatsApp support");
        cust.Confidence.Should().Be(MemoryConfidenceLevel.Verified);
    }

    [Fact]
    public void StoreMemory_ValidRequest_PersistsFactSuccessfully()
    {
        // Arrange
        var tenantId = $"tenant_{Guid.NewGuid():N}";
        var request = new StoreMemoryRequest(
            TenantId: tenantId,
            Category: AgentMemoryCategory.OperationalFact,
            Key: "HyperPay Fallback Routing",
            Value: "If HyperPay webhook drops or latency exceeds 400ms, route checkout to Tap Payments gateway immediately.",
            Confidence: MemoryConfidenceLevel.High,
            SourceAgent: "PaymentRoutingAgent",
            TtlHours: 48,
            Tags: new List<string> { "payment", "hyperpay", "tap", "latency" },
            Metadata: new Dictionary<string, string> { ["maxLatencyMs"] = "400", ["priority"] = "critical" }
        );

        // Act
        var stored = _service.StoreMemory(request);
        var retrieved = _service.GetMemoryById(tenantId, stored.Id);

        // Assert
        stored.Should().NotBeNull();
        stored.Id.Should().StartWith("mem_");
        stored.TenantId.Should().Be(tenantId);
        stored.Key.Should().Be("HyperPay Fallback Routing");
        stored.Category.Should().Be(AgentMemoryCategory.OperationalFact);
        stored.Confidence.Should().Be(MemoryConfidenceLevel.High);
        stored.ExpiresAtUtc.Should().NotBeNull();
        stored.ExpiresAtUtc!.Value.Should().BeAfter(DateTime.UtcNow.AddHours(47));

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(stored.Id);
        retrieved.Value.Should().Be(request.Value);
        retrieved.Metadata.Should().ContainKey("maxLatencyMs");
    }

    [Fact]
    public void QueryMemory_WithSemanticKeywordScoring_RanksHighestMatchesFirst()
    {
        // Arrange
        var tenantId = $"tenant_{Guid.NewGuid():N}";
        _service.StoreMemory(new StoreMemoryRequest(
            TenantId: tenantId,
            Category: AgentMemoryCategory.BusinessMemory,
            Key: "Saudi Customs Clearance & VAT",
            Value: "All imports exceeding 1,000 SAR must include Saudi 15% VAT and SASO certificate.",
            Confidence: MemoryConfidenceLevel.Verified,
            SourceAgent: "ComplianceAgent",
            Tags: new List<string> { "saudi", "vat", "saso", "customs" }
        ));

        _service.StoreMemory(new StoreMemoryRequest(
            TenantId: tenantId,
            Category: AgentMemoryCategory.CampaignMemory,
            Key: "Riyadh Season Influencer Budget",
            Value: "Influencer tier 1 budget allocated at 150,000 SAR for Riyadh Season launch.",
            Confidence: MemoryConfidenceLevel.Medium,
            SourceAgent: "InfluencerAgent",
            Tags: new List<string> { "riyadh", "influencer", "budget" }
        ));

        // Act - Query for "Saudi VAT customs"
        var query = new QueryMemoryRequest(
            TenantId: tenantId,
            Query: "Saudi VAT customs",
            Limit: 5
        );
        var results = _service.QueryMemory(query);

        // Assert
        results.Should().NotBeEmpty();
        results[0].Entry.Key.Should().Be("Saudi Customs Clearance & VAT");
        results[0].RelevanceScore.Should().BeGreaterThan(2.0);
    }

    [Fact]
    public void QueryMemory_CategoryFiltering_RestrictsToSpecifiedCategories()
    {
        // Arrange
        var tenantId = $"tenant_{Guid.NewGuid():N}";
        _service.StoreMemory(new StoreMemoryRequest(
            TenantId: tenantId,
            Category: AgentMemoryCategory.BrandMemory,
            Key: "Arabic Dialect Guardrail",
            Value: "Always use Najdi Arabic phrasing for Riyadh region social campaigns.",
            Confidence: MemoryConfidenceLevel.Verified,
            SourceAgent: "CopywriterAgent"
        ));

        _service.StoreMemory(new StoreMemoryRequest(
            TenantId: tenantId,
            Category: AgentMemoryCategory.OperationalFact,
            Key: "Riyadh Fulfillment Center SLA",
            Value: "Same-day delivery cutoff at 14:00 AST from Sulaimaniyah hub.",
            Confidence: MemoryConfidenceLevel.Verified,
            SourceAgent: "LogisticsAgent"
        ));

        // Act - Query with Category filter restricted to BrandMemory
        var query = new QueryMemoryRequest(
            TenantId: tenantId,
            Query: "Riyadh",
            Categories: new List<AgentMemoryCategory> { AgentMemoryCategory.BrandMemory }
        );
        var results = _service.QueryMemory(query);

        // Assert
        results.Should().HaveCount(1);
        results[0].Entry.Category.Should().Be(AgentMemoryCategory.BrandMemory);
        results[0].Entry.Key.Should().Be("Arabic Dialect Guardrail");
    }

    [Fact]
    public void QueryMemory_MinConfidenceFilter_ExcludesLowerConfidenceFacts()
    {
        // Arrange
        var tenantId = $"tenant_{Guid.NewGuid():N}";
        _service.StoreMemory(new StoreMemoryRequest(
            TenantId: tenantId,
            Category: AgentMemoryCategory.BusinessMemory,
            Key: "Tentative Vendor Agreement",
            Value: "Potential 5% shipping discount under review by finance.",
            Confidence: MemoryConfidenceLevel.Low,
            SourceAgent: "ProcurementAgent"
        ));

        _service.StoreMemory(new StoreMemoryRequest(
            TenantId: tenantId,
            Category: AgentMemoryCategory.BusinessMemory,
            Key: "Mandatory Refund Window",
            Value: "Commercial Ministry stipulates 7-day refund policy for defective goods.",
            Confidence: MemoryConfidenceLevel.Verified,
            SourceAgent: "LegalAgent"
        ));

        // Act
        var query = new QueryMemoryRequest(
            TenantId: tenantId,
            Query: "discount refund",
            MinConfidence: MemoryConfidenceLevel.High
        );
        var results = _service.QueryMemory(query);

        // Assert
        results.Should().HaveCount(1);
        results[0].Entry.Key.Should().Be("Mandatory Refund Window");
        results[0].Entry.Confidence.Should().Be(MemoryConfidenceLevel.Verified);
    }

    [Fact]
    public void TtlExpiration_PruneExpired_RemovesExpiredMemoriesOnly()
    {
        // Arrange
        var tenantId = $"tenant_{Guid.NewGuid():N}";

        // Persist an already expired fact (TtlHours = -1)
        var expiredEntry = _service.StoreMemory(new StoreMemoryRequest(
            TenantId: tenantId,
            Category: AgentMemoryCategory.CampaignMemory,
            Key: "Flash Sale 1-Hour Coupon",
            Value: "Use FLASH50 for 50% discount during live stream",
            Confidence: MemoryConfidenceLevel.Medium,
            SourceAgent: "LiveStreamAgent",
            TtlHours: -1
        ));

        // Persist an active fact (TtlHours = 24)
        var activeEntry = _service.StoreMemory(new StoreMemoryRequest(
            TenantId: tenantId,
            Category: AgentMemoryCategory.OperationalFact,
            Key: "Server Maintenance Window",
            Value: "Weekly maintenance on Sundays 03:00 AST",
            Confidence: MemoryConfidenceLevel.High,
            SourceAgent: "DevOpsAgent",
            TtlHours: 24
        ));

        // Act 1: Query should not return expired fact
        var queryResults = _service.QueryMemory(new QueryMemoryRequest(tenantId, "FLASH50"));
        queryResults.Should().BeEmpty();

        // Act 2: Prune expired entries
        var prunedCount = _service.PruneExpired(tenantId);

        // Assert
        prunedCount.Should().Be(1);
        _service.GetMemoryById(tenantId, expiredEntry.Id).Should().BeNull();

        var activeRemaining = _service.GetMemoryById(tenantId, activeEntry.Id);
        activeRemaining.Should().NotBeNull();
        activeRemaining!.Id.Should().Be(activeEntry.Id);
    }

    [Fact]
    public void DeleteMemory_RemovesFactById_ReturnsTrue()
    {
        // Arrange
        var tenantId = $"tenant_{Guid.NewGuid():N}";
        var entry = _service.StoreMemory(new StoreMemoryRequest(
            TenantId: tenantId,
            Category: AgentMemoryCategory.CustomerContext,
            Key: "Custom Shipping Instruction",
            Value: "Deliver to back gate near security booth.",
            Confidence: MemoryConfidenceLevel.High,
            SourceAgent: "DispatchAgent"
        ));

        // Act
        var deleted = _service.DeleteMemory(tenantId, entry.Id);
        var deleteNonExistent = _service.DeleteMemory(tenantId, "non-existent-id");

        // Assert
        deleted.Should().BeTrue();
        deleteNonExistent.Should().BeFalse();
        _service.GetMemoryById(tenantId, entry.Id).Should().BeNull();
    }

    [Fact]
    public void FormatMemoryForPromptInjection_FormatsCleanHierarchicalPromptContext()
    {
        // Arrange
        var tenantId = AgentMemoryService.DefaultTenantId;

        // Act 1: Full prompt context
        var fullPrompt = _service.FormatMemoryForPromptInjection(tenantId);

        // Assert 1
        fullPrompt.Should().Contain("### SYSTEM AGENT MEMORY CONTEXT ###");
        fullPrompt.Should().Contain("[Brand Voice & Tone Memory]");
        fullPrompt.Should().Contain("Direct, culturally resonant, respectful in Arabic & English");
        fullPrompt.Should().Contain("[Business Rules & Operational Policy Memory]");
        fullPrompt.Should().Contain("Saudi 15% VAT, 24h COD delivery in Riyadh, 3-day return policy");
        fullPrompt.Should().Contain("[Campaign & Growth Playbook Memory]");
        fullPrompt.Should().Contain("Ramadan discount max 25%, ROAS target 3.8x");
        fullPrompt.Should().Contain("[Customer Context & Tier SLA Memory]");
        fullPrompt.Should().Contain("VIP tier requires dedicated WhatsApp support");

        // Act 2: Query-specific prompt injection
        var ramadanPrompt = _service.FormatMemoryForPromptInjection(tenantId, topicQuery: "Ramadan");
        ramadanPrompt.Should().Contain("Ramadan Campaign Guardrails");

        // Act 3: Empty tenant
        var emptyTenantPrompt = _service.FormatMemoryForPromptInjection($"empty_{Guid.NewGuid():N}");
        emptyTenantPrompt.Should().Be("[AI AGENT MEMORY CONTEXT: None available]");
    }

    [Fact]
    public void TenantIsolation_StrictlySeparatesTenantMemories()
    {
        // Arrange
        var tenantA = $"tenant_A_{Guid.NewGuid():N}";
        var tenantB = $"tenant_B_{Guid.NewGuid():N}";

        var entryA = _service.StoreMemory(new StoreMemoryRequest(
            TenantId: tenantA,
            Category: AgentMemoryCategory.BusinessMemory,
            Key: "Tenant A Secret Margin",
            Value: "Target gross margin is strictly 45% for wholesale contracts.",
            Confidence: MemoryConfidenceLevel.Verified,
            SourceAgent: "CfoAgent"
        ));

        var entryB = _service.StoreMemory(new StoreMemoryRequest(
            TenantId: tenantB,
            Category: AgentMemoryCategory.BusinessMemory,
            Key: "Tenant B Secret Margin",
            Value: "Target gross margin is strictly 22% for consumer flash sales.",
            Confidence: MemoryConfidenceLevel.Verified,
            SourceAgent: "RetailAgent"
        ));

        // Act & Assert 1: GetAllMemories is isolated
        var allA = _service.GetAllMemories(tenantA);
        var allB = _service.GetAllMemories(tenantB);

        allA.Should().ContainSingle(m => m.Id == entryA.Id);
        allA.Should().NotContain(m => m.Id == entryB.Id);

        allB.Should().ContainSingle(m => m.Id == entryB.Id);
        allB.Should().NotContain(m => m.Id == entryA.Id);

        // Act & Assert 2: Cross-tenant ID retrieval returns null
        _service.GetMemoryById(tenantB, entryA.Id).Should().BeNull();
        _service.GetMemoryById(tenantA, entryB.Id).Should().BeNull();

        // Act & Assert 3: Query is strictly isolated
        var queryResultsB = _service.QueryMemory(new QueryMemoryRequest(tenantB, "wholesale 45%"));
        queryResultsB.Should().BeEmpty();

        // Act & Assert 4: Cross-tenant delete fails
        var deleteAttempt = _service.DeleteMemory(tenantB, entryA.Id);
        deleteAttempt.Should().BeFalse();
        _service.GetMemoryById(tenantA, entryA.Id).Should().NotBeNull();
    }
}
