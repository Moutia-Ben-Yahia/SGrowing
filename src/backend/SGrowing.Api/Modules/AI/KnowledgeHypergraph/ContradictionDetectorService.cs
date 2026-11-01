namespace SGrowing.Api.Modules.AI.KnowledgeHypergraph;

/// <summary>
/// Service analyzing the knowledge hypergraph to detect contractual, legal, and operational contradictions
/// such as conflicting exclusivity rights, disparate SLA thresholds, and superseded agreements.
/// </summary>
public sealed class ContradictionDetectorService
{
    private readonly HypergraphKnowledgeEngine _graphEngine;

    public ContradictionDetectorService(HypergraphKnowledgeEngine graphEngine)
    {
        _graphEngine = graphEngine ?? throw new ArgumentNullException(nameof(graphEngine));
    }

    /// <summary>
    /// Scans the entire hypergraph and reports detected contradictions.
    /// </summary>
    public IReadOnlyList<KnowledgeContradiction> DetectContradictions()
    {
        var contradictions = new List<KnowledgeContradiction>();
        var nodes = _graphEngine.GetAllNodes();
        var edges = _graphEngine.GetAllEdges();

        // 1. Detect Overlapping Exclusive Distribution Rights on the same product
        var productNodes = nodes.Where(n => n.EntityType == KnowledgeEntityType.ProductSku).ToList();
        foreach (var product in productNodes)
        {
            var exclusivityEdges = edges
                .Where(e => string.Equals(e.ToNodeId, product.NodeId, StringComparison.OrdinalIgnoreCase) &&
                            e.RelationType == KnowledgeRelationType.ExclusiveDistributionRights)
                .ToList();

            if (exclusivityEdges.Count > 1)
            {
                for (var i = 0; i < exclusivityEdges.Count - 1; i++)
                {
                    for (var j = i + 1; j < exclusivityEdges.Count; j++)
                    {
                        var e1 = exclusivityEdges[i];
                        var e2 = exclusivityEdges[j];

                        contradictions.Add(new KnowledgeContradiction(
                            ContradictionId: $"CON-EXCL-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
                            PrimaryNodeId: e1.FromNodeId,
                            ConflictingNodeId: e2.FromNodeId,
                            ContradictionCategory: "OverlappingExclusivity",
                            Description: $"Product '{product.Label}' has conflicting exclusive distribution rights granted to both entity '{e1.FromNodeId}' and '{e2.FromNodeId}'.",
                            Severity: ContradictionSeverity.Critical,
                            DocumentReferenceA: e1.SourceDocumentReference,
                            DocumentReferenceB: e2.SourceDocumentReference,
                            DetectedAtUtc: DateTime.UtcNow
                        ));
                    }
                }
            }
        }

        // 2. Detect Conflicting SLA Policies linked to the same Contract or Organization
        var contracts = nodes.Where(n => n.EntityType == KnowledgeEntityType.Contract).ToList();
        foreach (var contract in contracts)
        {
            var slaEdges = edges
                .Where(e => string.Equals(e.FromNodeId, contract.NodeId, StringComparison.OrdinalIgnoreCase) &&
                            e.RelationType == KnowledgeRelationType.SubjectToSla)
                .ToList();

            if (slaEdges.Count > 1)
            {
                var slaNodes = slaEdges
                    .Select(e => _graphEngine.GetNode(e.ToNodeId))
                    .Where(n => n is not null)
                    .Cast<HypergraphNode>()
                    .ToList();

                for (var i = 0; i < slaNodes.Count - 1; i++)
                {
                    for (var j = i + 1; j < slaNodes.Count; j++)
                    {
                        var s1 = slaNodes[i];
                        var s2 = slaNodes[j];

                        if (s1.Properties.TryGetValue("MaxResolutionHours", out var r1) &&
                            s2.Properties.TryGetValue("MaxResolutionHours", out var r2) &&
                            !string.Equals(r1, r2, StringComparison.OrdinalIgnoreCase))
                        {
                            contradictions.Add(new KnowledgeContradiction(
                                ContradictionId: $"CON-SLA-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
                                PrimaryNodeId: s1.NodeId,
                                ConflictingNodeId: s2.NodeId,
                                ContradictionCategory: "ConflictingSlaTerms",
                                Description: $"Contract '{contract.Label}' references multiple SLAs with conflicting resolution times: '{r1} hours' vs '{r2} hours'.",
                                Severity: ContradictionSeverity.High,
                                DocumentReferenceA: slaEdges[i].SourceDocumentReference,
                                DocumentReferenceB: slaEdges[j].SourceDocumentReference,
                                DetectedAtUtc: DateTime.UtcNow
                            ));
                        }
                    }
                }
            }
        }

        // 3. Detect Conflicting Jurisdictions on a single Contract
        foreach (var contract in contracts)
        {
            var jurisdictionEdges = edges
                .Where(e => string.Equals(e.FromNodeId, contract.NodeId, StringComparison.OrdinalIgnoreCase) &&
                            e.RelationType == KnowledgeRelationType.GovernedByJurisdiction)
                .ToList();

            if (jurisdictionEdges.Count > 1)
            {
                var distinctJurisdictions = jurisdictionEdges.Select(e => e.ToNodeId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (distinctJurisdictions.Count > 1)
                {
                    contradictions.Add(new KnowledgeContradiction(
                        ContradictionId: $"CON-JUR-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
                        PrimaryNodeId: distinctJurisdictions[0],
                        ConflictingNodeId: distinctJurisdictions[1],
                        ContradictionCategory: "ConflictingJurisdiction",
                        Description: $"Contract '{contract.Label}' contains conflicting governing law clauses pointing to multiple jurisdictions ('{distinctJurisdictions[0]}' and '{distinctJurisdictions[1]}').",
                        Severity: ContradictionSeverity.High,
                        DocumentReferenceA: jurisdictionEdges[0].SourceDocumentReference,
                        DocumentReferenceB: jurisdictionEdges[1].SourceDocumentReference,
                        DetectedAtUtc: DateTime.UtcNow
                    ));
                }
            }
        }

        return contradictions;
    }
}
