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
}
