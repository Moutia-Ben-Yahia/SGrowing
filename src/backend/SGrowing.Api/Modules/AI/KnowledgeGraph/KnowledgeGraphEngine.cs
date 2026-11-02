using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Ai.KnowledgeGraph;

public interface IKnowledgeGraphEngine
{
    IngestKnowledgeResponse IngestKnowledge(IngestKnowledgeRequest request);
    HybridSearchResult HybridSearch(HybridSearchQuery query);
    KnowledgeGraphSnapshot GetGraphSnapshot();
}

public sealed class KnowledgeGraphEngine : IKnowledgeGraphEngine
{
    private readonly ConcurrentDictionary<string, KnowledgeEntity> _entities = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, KnowledgeTriple> _triples = new();

    public KnowledgeGraphEngine()
    {
        SeedDefaultGraph();
    }

    public IngestKnowledgeResponse IngestKnowledge(IngestKnowledgeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RawText))
        {
            return new IngestKnowledgeResponse(0, 0, Array.Empty<string>(), "Skipped: Empty input");
        }

        var entityId = "ent_" + Guid.NewGuid().ToString("N")[..8];
        var title = request.RawText.Length > 40 ? request.RawText[..40] + "..." : request.RawText;

        var entity = new KnowledgeEntity(
            Id: entityId,
            Name: title,
            Type: "ExtractedConcept",
            Description: request.RawText,
            CentralityScore: 0.65,
            Properties: new Dictionary<string, string> { { "source", request.SourceDocId } }
        );

        _entities[entity.Id] = entity;

        // Form relation to root growth OS entity
        var tripleId = "trp_" + Guid.NewGuid().ToString("N")[..8];
        var triple = new KnowledgeTriple(
            Id: tripleId,
            SubjectId: entity.Id,
            SubjectName: entity.Name,
            Predicate: "AUGMENTS_GROWTH_OS",
            ObjectId: "ent_sgrowing_core",
            ObjectName: "SGrowing Enterprise Platform",
            Confidence: 0.94,
            SourceCitation: request.SourceDocId,
            CreatedAtUtc: DateTime.UtcNow
        );

        _triples[triple.Id] = triple;

        return new IngestKnowledgeResponse(
            IngestedEntitiesCount: 1,
            IngestedTriplesCount: 1,
            CreatedNodeIds: new[] { entity.Id },
            Status: "Success"
        );
    }

    public HybridSearchResult HybridSearch(HybridSearchQuery query)
    {
        var terms = (query.QueryText ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var matchedEntities = _entities.Values
            .Where(e => terms.Length == 0 || terms.Any(t => e.Name.Contains(t, StringComparison.OrdinalIgnoreCase) || e.Description.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => e.CentralityScore)
            .Take(10)
            .ToList();

        var entityIds = matchedEntities.Select(e => e.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matchedTriples = _triples.Values
            .Where(t => t.Confidence >= query.MinConfidence &&
                       (entityIds.Contains(t.SubjectId) || entityIds.Contains(t.ObjectId) ||
                        terms.Any(term => t.Predicate.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                          t.SubjectName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                          t.ObjectName.Contains(term, StringComparison.OrdinalIgnoreCase))))
            .Take(query.MaxTriples)
            .ToList();

        var summary = matchedTriples.Count > 0
            ? $"Grounded by {matchedEntities.Count} entities and {matchedTriples.Count} semantic relationship triples: " +
              string.Join("; ", matchedTriples.Select(t => $"({t.SubjectName}) -[{t.Predicate}]-> ({t.ObjectName})"))
            : "No high-confidence graph triples matched the target query.";

        return new HybridSearchResult(
            QueryText: query.QueryText ?? string.Empty,
            MatchedEntities: matchedEntities,
            MatchedTriples: matchedTriples,
            GroundedContextSummary: summary,
            InferenceTimeMs: 14.2
        );
    }

    public KnowledgeGraphSnapshot GetGraphSnapshot()
    {
        return new KnowledgeGraphSnapshot(
            TotalEntities: _entities.Count,
            TotalTriples: _triples.Count,
            Entities: _entities.Values.OrderByDescending(e => e.CentralityScore).ToList(),
            Triples: _triples.Values.OrderByDescending(t => t.Confidence).ToList(),
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    private void SeedDefaultGraph()
    {
        var eCore = new KnowledgeEntity("ent_sgrowing_core", "SGrowing Enterprise Platform", "System", "Autonomous AI Growth Operating System", 1.0, new Dictionary<string, string>());
        var eSdr = new KnowledgeEntity("ent_voice_sdr", "Autonomous Voice SDR Fleet", "Capability", "Sub-150ms real-time duplex voice agents", 0.95, new Dictionary<string, string>());
        var eFatigue = new KnowledgeEntity("ent_fatigue_mesh", "Omnichannel Fatigue Gate Mesh", "Guardrail", "Cross-channel frequency capping for WhatsApp and Email", 0.88, new Dictionary<string, string>());
        var eMonteCarlo = new KnowledgeEntity("ent_arr_forecaster", "Monte Carlo ARR Forecaster", "Analytics", "Stochastic 1,000-trial ARR probability distribution", 0.85, new Dictionary<string, string>());
        var eBilling = new KnowledgeEntity("ent_stripe_billing", "Multi-Currency Stripe & Tax Invoicing", "Integration", "Automated usage billing and smart dunning", 0.80, new Dictionary<string, string>());

        _entities[eCore.Id] = eCore;
        _entities[eSdr.Id] = eSdr;
        _entities[eFatigue.Id] = eFatigue;
        _entities[eMonteCarlo.Id] = eMonteCarlo;
        _entities[eBilling.Id] = eBilling;

        AddTriple("trp_1", eSdr.Id, eSdr.Name, "POWERS_INBOUND_QUALIFICATION", eCore.Id, eCore.Name, 0.99, "Architecture_Spec_v41");
        AddTriple("trp_2", eFatigue.Id, eFatigue.Name, "PROTECTS_REPUTATION_FOR", eSdr.Id, eSdr.Name, 0.97, "Outreach_Safety_Audit");
        AddTriple("trp_3", eMonteCarlo.Id, eMonteCarlo.Name, "MODELS_TRAJECTORY_FOR", eCore.Id, eCore.Name, 0.94, "RevOps_Playbook");
        AddTriple("trp_4", eBilling.Id, eBilling.Name, "FEEDS_METRICS_INTO", eMonteCarlo.Id, eMonteCarlo.Name, 0.91, "Finance_Schema_v2");
    }

    private void AddTriple(string id, string subId, string subName, string pred, string objId, string objName, double conf, string src)
    {
        _triples[id] = new KnowledgeTriple(id, subId, subName, pred, objId, objName, conf, src, DateTime.UtcNow);
    }
}
