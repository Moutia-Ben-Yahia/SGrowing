using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Ai.KnowledgeGraph;

public sealed record KnowledgeEntity(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("centralityScore")] double CentralityScore,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, string> Properties
);

public sealed record KnowledgeTriple(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("subjectId")] string SubjectId,
    [property: JsonPropertyName("subjectName")] string SubjectName,
    [property: JsonPropertyName("predicate")] string Predicate,
    [property: JsonPropertyName("objectId")] string ObjectId,
    [property: JsonPropertyName("objectName")] string ObjectName,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("sourceCitation")] string SourceCitation,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc
);

public sealed record HybridSearchQuery(
    [property: JsonPropertyName("queryText")] string QueryText,
    [property: JsonPropertyName("maxTriples")] int MaxTriples = 10,
    [property: JsonPropertyName("minConfidence")] double MinConfidence = 0.70,
    [property: JsonPropertyName("entityTypes")] IReadOnlyList<string>? EntityTypes = null,
    [property: JsonPropertyName("neighborhoodDepth")] int NeighborhoodDepth = 1
);

public sealed record HybridSearchResult(
    [property: JsonPropertyName("queryText")] string QueryText,
    [property: JsonPropertyName("matchedEntities")] IReadOnlyList<KnowledgeEntity> MatchedEntities,
    [property: JsonPropertyName("matchedTriples")] IReadOnlyList<KnowledgeTriple> MatchedTriples,
    [property: JsonPropertyName("groundedContextSummary")] string GroundedContextSummary,
    [property: JsonPropertyName("inferenceTimeMs")] double InferenceTimeMs
);

public sealed record IngestKnowledgeRequest(
    [property: JsonPropertyName("rawText")] string RawText,
    [property: JsonPropertyName("sourceDocId")] string SourceDocId,
    [property: JsonPropertyName("extractOntology")] bool ExtractOntology = true
);

public sealed record IngestKnowledgeResponse(
    [property: JsonPropertyName("ingestedEntitiesCount")] int IngestedEntitiesCount,
    [property: JsonPropertyName("ingestedTriplesCount")] int IngestedTriplesCount,
    [property: JsonPropertyName("createdNodeIds")] IReadOnlyList<string> CreatedNodeIds,
    [property: JsonPropertyName("status")] string Status
);

public sealed record KnowledgeGraphSnapshot(
    [property: JsonPropertyName("totalEntities")] int TotalEntities,
    [property: JsonPropertyName("totalTriples")] int TotalTriples,
    [property: JsonPropertyName("entities")] IReadOnlyList<KnowledgeEntity> Entities,
    [property: JsonPropertyName("triples")] IReadOnlyList<KnowledgeTriple> Triples,
    [property: JsonPropertyName("generatedAtUtc")] DateTime GeneratedAtUtc
);
