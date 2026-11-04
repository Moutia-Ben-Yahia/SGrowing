using FluentAssertions;
using SGrowing.Api.Modules.Search.Universal;
using Xunit;

namespace SGrowing.Tests.Search;

public class UniversalSearchEngineServiceTests
{
    private readonly UniversalSearchEngineService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task SearchAsync_ShouldReturnBM25RankedResults_WithFieldWeighting()
    {
        var doc1 = new SearchDocument(
            Guid.NewGuid(),
            _tenantId,
            SearchableEntityType.KnowledgeArticle,
            "Refund Policy and Chargeback Resolution",
            "Details on how customer refunds are processed and chargeback mediation is handled.",
            new List<string> { "finance", "refunds" }
        );

        var doc2 = new SearchDocument(
            Guid.NewGuid(),
            _tenantId,
            SearchableEntityType.CrmDeal,
            "Acme Enterprise Contract Renewal",
            "Annual subscription renewal deal with no refund options included.",
            new List<string> { "sales", "enterprise" }
        );

        await _service.IndexDocumentsBatchAsync(new List<SearchDocument> { doc1, doc2 });

        var query = new UniversalSearchQuery(_tenantId, "refund policy");
        var response = await _service.SearchAsync(query);

        response.Should().NotBeNull();
        response.TotalMatches.Should().BeGreaterThanOrEqualTo(1);
        response.Results.First().DocumentId.Should().Be(doc1.DocumentId); // Title match gets 3x weighting
        response.Results.First().SnippetHighlight.Should().Contain("<mark>");
        response.Facets.Should().ContainKey(SearchableEntityType.KnowledgeArticle);
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByEntityType_WhenSpecified()
    {
        var customerDoc = new SearchDocument(
            Guid.NewGuid(),
            _tenantId,
            SearchableEntityType.Customer,
            "John Doe Logistics",
            "Active high-volume freight customer based in Hamburg.",
            new List<string> { "vip", "freight" }
        );

        var ticketDoc = new SearchDocument(
            Guid.NewGuid(),
            _tenantId,
            SearchableEntityType.TicketMessage,
            "Ticket #9812 - Freight delay query",
            "John Doe called regarding an urgent freight delay.",
            new List<string> { "urgent", "freight" }
        );

        await _service.IndexDocumentsBatchAsync(new List<SearchDocument> { customerDoc, ticketDoc });

        // Filter only Customer entity
        var query = new UniversalSearchQuery(
            _tenantId,
            "John Doe",
            EntityFilter: new List<SearchableEntityType> { SearchableEntityType.Customer }
        );

        var response = await _service.SearchAsync(query);

        response.Results.Should().ContainSingle();
        response.Results[0].EntityType.Should().Be(SearchableEntityType.Customer);
    }

    [Fact]
    public async Task SearchAsync_ShouldSupportFuzzyMatching_ForTypoTolerance()
    {
        var doc = new SearchDocument(
            Guid.NewGuid(),
            _tenantId,
            SearchableEntityType.OrderFulfillment,
            "Warehouse Split Shipment Dispatch",
            "Urgent delivery container arriving via expedited courier.",
            new List<string> { "fulfillment" }
        );

        await _service.IndexDocumentAsync(doc);

        // Typo: "shipmnt" instead of "shipment"
        var query = new UniversalSearchQuery(_tenantId, "shipmnt", EnableFuzzyMatching: true);
        var response = await _service.SearchAsync(query);

        response.TotalMatches.Should().Be(1);
        response.Results.First().DocumentId.Should().Be(doc.DocumentId);
    }

    [Fact]
    public async Task SearchAsync_ShouldEnforceTenantIsolation()
    {
        var otherTenantId = Guid.NewGuid();

        var doc1 = new SearchDocument(Guid.NewGuid(), _tenantId, SearchableEntityType.KnowledgeArticle, "Secret Roadmap", "Tenant 1 details");
        var doc2 = new SearchDocument(Guid.NewGuid(), otherTenantId, SearchableEntityType.KnowledgeArticle, "Secret Roadmap", "Tenant 2 details");

        await _service.IndexDocumentsBatchAsync(new List<SearchDocument> { doc1, doc2 });

        var query = new UniversalSearchQuery(_tenantId, "Secret Roadmap");
        var response = await _service.SearchAsync(query);

        response.Results.Should().ContainSingle();
        response.Results[0].DocumentId.Should().Be(doc1.DocumentId);
    }

    [Fact]
    public async Task GetDocumentCountAsync_ReturnsAccurateCountForTenant()
    {
        var tenantId = Guid.NewGuid();
        var emptyCount = await _service.GetDocumentCountAsync(tenantId);
        emptyCount.Should().Be(0);

        var doc1 = new SearchDocument(Guid.NewGuid(), tenantId, SearchableEntityType.KnowledgeArticle, "Doc One", "Content one");
        var doc2 = new SearchDocument(Guid.NewGuid(), tenantId, SearchableEntityType.CrmDeal, "Doc Two", "Content two");
        await _service.IndexDocumentsBatchAsync(new List<SearchDocument> { doc1, doc2 });

        var afterCount = await _service.GetDocumentCountAsync(tenantId);
        afterCount.Should().Be(2);
    }

    [Fact]
    public async Task SearchAsync_EmptyOrWhitespaceQuery_ReturnsZeroMatches()
    {
        var doc = new SearchDocument(Guid.NewGuid(), _tenantId, SearchableEntityType.Customer, "Alpha Customer", "Active user");
        await _service.IndexDocumentAsync(doc);

        var emptyResponse = await _service.SearchAsync(new UniversalSearchQuery(_tenantId, ""));
        var wsResponse = await _service.SearchAsync(new UniversalSearchQuery(_tenantId, "   "));

        emptyResponse.TotalMatches.Should().Be(0);
        emptyResponse.Results.Should().BeEmpty();
        wsResponse.TotalMatches.Should().Be(0);
        wsResponse.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_UnknownTenant_ReturnsZeroMatches()
    {
        var unknownTenantId = Guid.NewGuid();
        var response = await _service.SearchAsync(new UniversalSearchQuery(unknownTenantId, "anything"));

        response.TotalMatches.Should().Be(0);
        response.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_NullQuery_ThrowsArgumentNullException()
    {
        var act = async () => await _service.SearchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task IndexDocumentAsync_NullDocument_ThrowsArgumentNullException()
    {
        var act = async () => await _service.IndexDocumentAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SearchAsync_NonFuzzyMatching_RequiresExactMatch()
    {
        var tenantId = Guid.NewGuid();
        var doc = new SearchDocument(Guid.NewGuid(), tenantId, SearchableEntityType.OrderFulfillment, "Expedited Shipping Container", "Tracking container number 4581");
        await _service.IndexDocumentAsync(doc);

        // Typo search with fuzzy disabled -> 0 matches
        var noFuzzyResponse = await _service.SearchAsync(new UniversalSearchQuery(tenantId, "shippng", EnableFuzzyMatching: false));
        noFuzzyResponse.TotalMatches.Should().Be(0);

        // Exact match with fuzzy disabled -> 1 match
        var exactResponse = await _service.SearchAsync(new UniversalSearchQuery(tenantId, "shipping", EnableFuzzyMatching: false));
        exactResponse.TotalMatches.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_LimitEnforcement_ReturnsAtMostLimitResults()
    {
        var tenantId = Guid.NewGuid();
        var docs = Enumerable.Range(1, 10).Select(i => new SearchDocument(
            Guid.NewGuid(),
            tenantId,
            SearchableEntityType.KnowledgeArticle,
            $"Knowledge Base Article #{i}",
            $"Standard operating procedure article #{i} for customer support"
        )).ToList();

        await _service.IndexDocumentsBatchAsync(docs);

        var query = new UniversalSearchQuery(tenantId, "standard operating", Limit: 3);
        var response = await _service.SearchAsync(query);

        response.TotalMatches.Should().Be(10);
        response.Results.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchAsync_Facets_AggregatesCountsAcrossMultipleEntityTypes()
    {
        var tenantId = Guid.NewGuid();
        var doc1 = new SearchDocument(Guid.NewGuid(), tenantId, SearchableEntityType.Customer, "Acme Corp Customer Profile", "Leading enterprise client");
        var doc2 = new SearchDocument(Guid.NewGuid(), tenantId, SearchableEntityType.CrmDeal, "Acme Corp Annual Renewal", "High priority deal");
        var doc3 = new SearchDocument(Guid.NewGuid(), tenantId, SearchableEntityType.TicketMessage, "Acme Corp Urgent Ticket", "Billing question from Acme");

        await _service.IndexDocumentsBatchAsync(new List<SearchDocument> { doc1, doc2, doc3 });

        var query = new UniversalSearchQuery(tenantId, "Acme Corp");
        var response = await _service.SearchAsync(query);

        response.Facets.Should().ContainKey(SearchableEntityType.Customer);
        response.Facets.Should().ContainKey(SearchableEntityType.CrmDeal);
        response.Facets.Should().ContainKey(SearchableEntityType.TicketMessage);
        response.Facets[SearchableEntityType.Customer].Should().Be(1);
        response.Facets[SearchableEntityType.CrmDeal].Should().Be(1);
        response.Facets[SearchableEntityType.TicketMessage].Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_TagsWeighting_ScoresHigherThanBodyOnlyMatch()
    {
        var tenantId = Guid.NewGuid();
        var docWithTag = new SearchDocument(
            Guid.NewGuid(),
            tenantId,
            SearchableEntityType.KnowledgeArticle,
            "General Maintenance Overview",
            "Routine server health checklist.",
            new List<string> { "cybersecurity" }
        );

        var docWithBodyOnly = new SearchDocument(
            Guid.NewGuid(),
            tenantId,
            SearchableEntityType.KnowledgeArticle,
            "General Maintenance Overview",
            "Routine cybersecurity server health checklist.",
            new List<string> { "routine" }
        );

        await _service.IndexDocumentsBatchAsync(new List<SearchDocument> { docWithTag, docWithBodyOnly });

        var query = new UniversalSearchQuery(tenantId, "cybersecurity");
        var response = await _service.SearchAsync(query);

        response.Results.Should().HaveCount(2);
        // Tag weight (2.0) is higher than Body weight (1.0), so docWithTag should rank first
        response.Results[0].DocumentId.Should().Be(docWithTag.DocumentId);
        response.Results[0].RelevanceScore.Should().BeGreaterThan(response.Results[1].RelevanceScore);
    }

    [Fact]
    public async Task IndexDocumentsBatchAsync_NullList_ThrowsArgumentNullException()
    {
        var act = async () => await _service.IndexDocumentsBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SearchAsync_UrlRouteAndAuthor_ArePreservedInResults()
    {
        var tenantId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);
        var doc = new SearchDocument(
            Guid.NewGuid(),
            tenantId,
            SearchableEntityType.ContractDocument,
            "Master Services Agreement 2026",
            "Executed agreement terms and conditions.",
            new List<string> { "legal", "msa" },
            UrlRoute: "/contracts/msa-2026",
            AuthorOrOwner: "legal-counsel@sgrowing.io",
            CreatedAtUtc: createdAt
        );

        await _service.IndexDocumentAsync(doc);

        var query = new UniversalSearchQuery(tenantId, "Master Services Agreement");
        var response = await _service.SearchAsync(query);

        response.Results.Should().ContainSingle();
        var item = response.Results.First();
        item.DocumentId.Should().Be(doc.DocumentId);
        item.UrlRoute.Should().Be("/contracts/msa-2026");
        item.AuthorOrOwner.Should().Be("legal-counsel@sgrowing.io");
        item.CreatedAtUtc.Should().Be(createdAt);
    }

    [Fact]
    public async Task SearchAsync_SnippetHighlight_ContainsMarkTagsAroundMatchingTerm()
    {
        var tenantId = Guid.NewGuid();
        var doc = new SearchDocument(
            Guid.NewGuid(),
            tenantId,
            SearchableEntityType.KnowledgeArticle,
            "Warehouse Protocol",
            "The quick brown fox jumps over the lazy dog and reviews the warehouse distribution status."
        );

        await _service.IndexDocumentAsync(doc);

        var query = new UniversalSearchQuery(tenantId, "distribution");
        var response = await _service.SearchAsync(query);

        response.Results.Should().ContainSingle();
        var item = response.Results.First();
        item.SnippetHighlight.Should().Contain("<mark>");
        item.SnippetHighlight.Should().Contain("</mark>");
        item.SnippetHighlight.Should().ContainEquivalentOf("distribution");
    }
}
