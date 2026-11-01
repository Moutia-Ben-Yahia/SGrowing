using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.AI.KnowledgeHypergraph;
using Xunit;

namespace SGrowing.Tests.AI;

public sealed class KnowledgeHypergraphGraphRagTests
{
    private readonly HypergraphKnowledgeEngine _graphEngine = new();
    private readonly HybridVectorGraphRetriever _retriever;
    private readonly ContradictionDetectorService _contradictionDetector;

    public KnowledgeHypergraphGraphRagTests()
    {
        _retriever = new HybridVectorGraphRetriever(_graphEngine);
        _contradictionDetector = new ContradictionDetectorService(_graphEngine);
    }

    #region Graph Topological Traversal Tests

    [Fact]
    public void AddNodeAndEdge_ValidGraph_PersistsAndAllowsRetrieval()
    {
        var node1 = new HypergraphNode("N-CORP-A", "Sonatrach Holding", KnowledgeEntityType.Organization, new Dictionary<string, string> { ["Country"] = "DZ" });
        var node2 = new HypergraphNode("N-CORP-B", "Naftal Distribution", KnowledgeEntityType.Organization, new Dictionary<string, string> { ["Country"] = "DZ" });

        _graphEngine.AddNode(node1);
        _graphEngine.AddNode(node2);

        var edge = new HypergraphEdge("E-SUB-01", "N-CORP-B", "N-CORP-A", KnowledgeRelationType.SubsidiaryOf, 1.0, "Doc-Decree-1981");
        _graphEngine.AddEdge(edge);

        _graphEngine.GetNode("N-CORP-A").Should().NotBeNull();
        _graphEngine.GetAllEdges().Should().ContainSingle(e => e.EdgeId == "E-SUB-01");
    }

    [Fact]
    public void AddEdge_MissingEndpoint_ThrowsInvalidOperationException()
    {
        var node1 = new HypergraphNode("N-1", "Entity 1", KnowledgeEntityType.Organization, new Dictionary<string, string>());
        _graphEngine.AddNode(node1);

        var edge = new HypergraphEdge("E-1", "N-1", "N-NON-EXISTENT", KnowledgeRelationType.SignedContract, 1.0, "Ref");
        var act = () => _graphEngine.AddEdge(edge);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must exist in hypergraph*");
    }

    [Fact]
    public void ExtractSubgraph_1HopAnd2Hops_ExpandsNeighborsCorrectly()
    {
        // Topology: A -> B -> C -> D
        var na = new HypergraphNode("A", "Node A", KnowledgeEntityType.Organization, new Dictionary<string, string>());
        var nb = new HypergraphNode("B", "Node B", KnowledgeEntityType.Contract, new Dictionary<string, string>());
        var nc = new HypergraphNode("C", "Node C", KnowledgeEntityType.ProductSku, new Dictionary<string, string>());
        var nd = new HypergraphNode("D", "Node D", KnowledgeEntityType.LegalEntity, new Dictionary<string, string>());

        _graphEngine.AddNode(na);
        _graphEngine.AddNode(nb);
        _graphEngine.AddNode(nc);
        _graphEngine.AddNode(nd);

        _graphEngine.AddEdge(new HypergraphEdge("E1", "A", "B", KnowledgeRelationType.SignedContract, 1.0, "D1"));
        _graphEngine.AddEdge(new HypergraphEdge("E2", "B", "C", KnowledgeRelationType.ManufacturesProduct, 1.0, "D2"));
        _graphEngine.AddEdge(new HypergraphEdge("E3", "C", "D", KnowledgeRelationType.GovernedByJurisdiction, 1.0, "D3"));

        // 1 hop from A should get A and B
        var sub1 = _graphEngine.ExtractSubgraph("A", maxHops: 1);
        sub1.Nodes.Select(n => n.NodeId).Should().BeEquivalentTo(["A", "B"]);

        // 2 hops from A should get A, B, and C
        var sub2 = _graphEngine.ExtractSubgraph("A", maxHops: 2);
        sub2.Nodes.Select(n => n.NodeId).Should().BeEquivalentTo(["A", "B", "C"]);
    }

    [Fact]
    public void FindPath_ConnectedNodes_DiscoversDirectedEdgePath()
    {
        var n1 = new HypergraphNode("N1", "Supplier X", KnowledgeEntityType.Organization, new Dictionary<string, string>());
        var n2 = new HypergraphNode("N2", "Distributor Y", KnowledgeEntityType.Organization, new Dictionary<string, string>());
        var n3 = new HypergraphNode("N3", "Retailer Z", KnowledgeEntityType.Organization, new Dictionary<string, string>());

        _graphEngine.AddNode(n1);
        _graphEngine.AddNode(n2);
        _graphEngine.AddNode(n3);

        _graphEngine.AddEdge(new HypergraphEdge("E-12", "N1", "N2", KnowledgeRelationType.ExclusiveDistributionRights, 1.0, "Ref1"));
        _graphEngine.AddEdge(new HypergraphEdge("E-23", "N2", "N3", KnowledgeRelationType.SignedContract, 1.0, "Ref2"));

        var path = _graphEngine.FindPath("N1", "N3");
        path.Should().HaveCount(2);
        path[0].EdgeId.Should().Be("E-12");
        path[1].EdgeId.Should().Be("E-23");
    }

    [Fact]
    public void FindPath_DisconnectedNodes_ReturnsEmpty()
    {
        var n1 = new HypergraphNode("A", "Isolated A", KnowledgeEntityType.Organization, new Dictionary<string, string>());
        var n2 = new HypergraphNode("B", "Isolated B", KnowledgeEntityType.Organization, new Dictionary<string, string>());

        _graphEngine.AddNode(n1);
        _graphEngine.AddNode(n2);

        var path = _graphEngine.FindPath("A", "B");
        path.Should().BeEmpty();
    }

    #endregion

    #region Cosine Similarity & Vector Graph RAG Tests

    [Fact]
    public void CalculateCosineSimilarity_KnownVectors_ReturnsExpectedSimilarity()
    {
        float[] v1 = [1.0f, 0.0f, 0.0f];
        float[] v2 = [1.0f, 0.0f, 0.0f];
        float[] v3 = [0.0f, 1.0f, 0.0f];

        HybridVectorGraphRetriever.CalculateCosineSimilarity(v1, v2).Should().BeApproximately(1.0, 0.001);
        HybridVectorGraphRetriever.CalculateCosineSimilarity(v1, v3).Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void HybridVectorGraphRetriever_DenseMatchAndGraphExpansion_GeneratesEnrichedContext()
    {
        float[] targetVector = [0.9f, 0.1f, 0.0f];
        var contract = new HypergraphNode(
            NodeId: "CONTRACT-MSA-2026",
            Label: "B2B Master Supply Agreement 2026",
            EntityType: KnowledgeEntityType.Contract,
            Properties: new Dictionary<string, string> { ["PaymentTerms"] = "Net 30 Days", ["ValueDzd"] = "50000000" },
            DenseEmbedding: [0.85f, 0.15f, 0.0f]
        );

        var party = new HypergraphNode(
            NodeId: "PARTY-ORAN-DISTRIB",
            Label: "Oran Commercial Logistics EURL",
            EntityType: KnowledgeEntityType.Organization,
            Properties: new Dictionary<string, string> { ["Wilaya"] = "Oran", ["TaxId"] = "09991234567" },
            DenseEmbedding: null
        );

        _graphEngine.AddNode(contract);
        _graphEngine.AddNode(party);

        _graphEngine.AddEdge(new HypergraphEdge(
            EdgeId: "EDGE-SIGN-01",
            FromNodeId: "PARTY-ORAN-DISTRIB",
            ToNodeId: "CONTRACT-MSA-2026",
            RelationType: KnowledgeRelationType.SignedContract,
            Weight: 0.95,
            SourceDocumentReference: "Notarized Deed #8812"
        ));

        var request = new QueryGraphRagRequest(
            Query: "What are the payment terms for Oran logistics?",
            QueryEmbedding: targetVector,
            MaxVectorSeeds: 2,
            MaxGraphHops: 2,
            MinSimilarityThreshold: 0.70
        );

        var result = _retriever.Retrieve(request);

        result.TopVectorMatches.Should().ContainSingle(m => m.NodeId == "CONTRACT-MSA-2026");
        result.SynthesizedContextMarkdown.Should().Contain("B2B Master Supply Agreement 2026");
        result.SynthesizedContextMarkdown.Should().Contain("Oran Commercial Logistics EURL");
        result.SynthesizedContextMarkdown.Should().Contain("SignedContract");
        result.SynthesizedContextMarkdown.Should().Contain("Net 30 Days");
    }

    #endregion

    #region Contradiction Detection Tests

    [Fact]
    public void ContradictionDetector_OverlappingExclusiveRights_DetectsCriticalContradiction()
    {
        var product = new HypergraphNode("PROD-SOLAR-01", "Sahara Solar Panels 500W", KnowledgeEntityType.ProductSku, new Dictionary<string, string>());
        var supplierA = new HypergraphNode("SUP-A", "Algiers Green Energy SARL", KnowledgeEntityType.Organization, new Dictionary<string, string>());
        var supplierB = new HypergraphNode("SUP-B", "Oran Solar Trading SPA", KnowledgeEntityType.Organization, new Dictionary<string, string>());

        _graphEngine.AddNode(product);
        _graphEngine.AddNode(supplierA);
        _graphEngine.AddNode(supplierB);

        // Grant exclusive rights to both suppliers A and B!
        _graphEngine.AddEdge(new HypergraphEdge("E-EXCL-1", "SUP-A", "PROD-SOLAR-01", KnowledgeRelationType.ExclusiveDistributionRights, 1.0, "Doc-Contract-A"));
        _graphEngine.AddEdge(new HypergraphEdge("E-EXCL-2", "SUP-B", "PROD-SOLAR-01", KnowledgeRelationType.ExclusiveDistributionRights, 1.0, "Doc-Contract-B"));

        var contradictions = _contradictionDetector.DetectContradictions();

        contradictions.Should().ContainSingle(c => c.ContradictionCategory == "OverlappingExclusivity");
        contradictions[0].Severity.Should().Be(ContradictionSeverity.Critical);
        contradictions[0].Description.Should().Contain("Sahara Solar Panels 500W");
    }

    [Fact]
    public void ContradictionDetector_ConflictingSlaTerms_DetectsSlaContradiction()
    {
        var contract = new HypergraphNode("CONT-01", "Fleet Maintenance Master Agreement", KnowledgeEntityType.Contract, new Dictionary<string, string>());
        var sla1 = new HypergraphNode("SLA-GOLD", "Gold SLA Tier", KnowledgeEntityType.SlaPolicy, new Dictionary<string, string> { ["MaxResolutionHours"] = "4" });
        var sla2 = new HypergraphNode("SLA-SILVER", "Silver SLA Tier", KnowledgeEntityType.SlaPolicy, new Dictionary<string, string> { ["MaxResolutionHours"] = "24" });

        _graphEngine.AddNode(contract);
        _graphEngine.AddNode(sla1);
        _graphEngine.AddNode(sla2);

        _graphEngine.AddEdge(new HypergraphEdge("E-SLA-1", "CONT-01", "SLA-GOLD", KnowledgeRelationType.SubjectToSla, 1.0, "Doc-Addendum-A"));
        _graphEngine.AddEdge(new HypergraphEdge("E-SLA-2", "CONT-01", "SLA-SILVER", KnowledgeRelationType.SubjectToSla, 1.0, "Doc-Addendum-B"));

        var contradictions = _contradictionDetector.DetectContradictions();

        contradictions.Should().ContainSingle(c => c.ContradictionCategory == "ConflictingSlaTerms");
        contradictions[0].Severity.Should().Be(ContradictionSeverity.High);
    }

    [Fact]
    public void ContradictionDetector_CleanGraph_ReportsZeroContradictions()
    {
        var org = new HypergraphNode("ORG-1", "Compliant Corp", KnowledgeEntityType.Organization, new Dictionary<string, string>());
        var prod = new HypergraphNode("PROD-1", "Standard Widget", KnowledgeEntityType.ProductSku, new Dictionary<string, string>());

        _graphEngine.AddNode(org);
        _graphEngine.AddNode(prod);
        _graphEngine.AddEdge(new HypergraphEdge("E1", "ORG-1", "PROD-1", KnowledgeRelationType.ManufacturesProduct, 1.0, "Doc1"));

        var contradictions = _contradictionDetector.DetectContradictions();
        contradictions.Should().BeEmpty();
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void ServiceCollectionExtensions_AddKnowledgeHypergraphServices_RegistersAllDependencies()
    {
        var services = new ServiceCollection();
        services.AddKnowledgeHypergraphServices();

        services.Should().Contain(d => d.ServiceType == typeof(HypergraphKnowledgeEngine) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(HybridVectorGraphRetriever) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(ContradictionDetectorService) && d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
