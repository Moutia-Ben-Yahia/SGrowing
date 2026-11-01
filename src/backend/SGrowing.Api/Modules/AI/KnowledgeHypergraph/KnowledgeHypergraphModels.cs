namespace SGrowing.Api.Modules.AI.KnowledgeHypergraph;

/// <summary>
/// Domain entity classification in enterprise knowledge graphs.
/// </summary>
public enum KnowledgeEntityType
{
    Organization = 1,
    Individual = 2,
    Contract = 3,
    LegalEntity = 4,
    ProductSku = 5,
    SlaPolicy = 6,
    Jurisdiction = 7,
    FinancialAccount = 8
}

/// <summary>
/// Typed relationship between entities in the knowledge hypergraph.
/// </summary>
public enum KnowledgeRelationType
{
    SignedContract = 1,
    SubsidiaryOf = 2,
    ManufacturesProduct = 3,
    SubjectToSla = 4,
    GovernedByJurisdiction = 5,
    GuaranteesPayment = 6,
    SupersedesAgreement = 7,
    ExclusiveDistributionRights = 8
}

/// <summary>
/// Severity level of an automated contract or policy contradiction.
/// </summary>
public enum ContradictionSeverity
{
    Warning = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Discrete entity node in the enterprise knowledge hypergraph.
/// </summary>
public sealed record HypergraphNode(
    string NodeId,
    string Label,
    KnowledgeEntityType EntityType,
    IReadOnlyDictionary<string, string> Properties,
    float[]? DenseEmbedding = null,
    DateTime? CreatedAtUtc = null
);

/// <summary>
/// Directed, weighted, timestamped relationship edge in the hypergraph.
/// </summary>
public sealed record HypergraphEdge(
    string EdgeId,
    string FromNodeId,
    string ToNodeId,
    KnowledgeRelationType RelationType,
    double Weight,
    string SourceDocumentReference,
    DateTime? ValidFromUtc = null,
    DateTime? ValidToUtc = null
);

/// <summary>
/// Subgraph extracted via k-hop topological traversal around seed nodes.
/// </summary>
public sealed record ExtractedSubgraph(
    string SeedNodeId,
    IReadOnlyList<HypergraphNode> Nodes,
    IReadOnlyList<HypergraphEdge> Edges,
    int DepthHops,
    double CentralityScore
);

/// <summary>
/// Dense vector cosine match candidate.
/// </summary>
public sealed record VectorMatchResult(
    string NodeId,
    string Label,
    KnowledgeEntityType EntityType,
    double CosineSimilarity
);

/// <summary>
/// Fused hybrid Graph RAG retrieval output combining vector search and subgraph contextualization.
/// </summary>
public sealed record HybridGraphRagResult(
    string Query,
    IReadOnlyList<VectorMatchResult> TopVectorMatches,
    IReadOnlyList<ExtractedSubgraph> ExpandedSubgraphs,
    string SynthesizedContextMarkdown,
    DateTime RetrievedAtUtc
);

/// <summary>
/// Contradiction detected across multiple enterprise contracts or policies.
/// </summary>
public sealed record KnowledgeContradiction(
    string ContradictionId,
    string PrimaryNodeId,
    string ConflictingNodeId,
    string ContradictionCategory,
    string Description,
    ContradictionSeverity Severity,
    string DocumentReferenceA,
    string DocumentReferenceB,
    DateTime DetectedAtUtc
);

/// <summary>
/// Request to perform hybrid Graph RAG query.
/// </summary>
public sealed record QueryGraphRagRequest(
    string Query,
    float[] QueryEmbedding,
    int MaxVectorSeeds = 3,
    int MaxGraphHops = 2,
    double MinSimilarityThreshold = 0.50
);
