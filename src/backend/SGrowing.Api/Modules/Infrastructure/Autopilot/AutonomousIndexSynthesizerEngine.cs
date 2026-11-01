using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Infrastructure.Autopilot;

/// <summary>
/// Interface for the Autonomous Database Index Synthesizer Engine.
/// Analyzes slow query execution plans and synthesizes non-blocking PostgreSQL index recommendations.
/// </summary>
public interface IAutonomousIndexSynthesizerEngine
{
    /// <summary>
    /// Analyzes a batch of slow query metrics and stores valid index recommendations.
    /// </summary>
    IReadOnlyList<IndexRecommendation> AnalyzeSlowQueries(IEnumerable<SlowQueryMetric> queries);

    /// <summary>
    /// Evaluates a single query metric and synthesizes an index recommendation if criteria are met.
    /// </summary>
    IndexRecommendation? SynthesizeIndex(SlowQueryMetric query);

    /// <summary>
    /// Retrieves all currently active index recommendations.
    /// </summary>
    IReadOnlyList<IndexRecommendation> GetActiveRecommendations();

    /// <summary>
    /// Dismisses an active recommendation by ID.
    /// </summary>
    bool DismissRecommendation(string recommendationId);

    /// <summary>
    /// Clears all active recommendations (used in maintenance or testing).
    /// </summary>
    void ClearRecommendations();
}

/// <summary>
/// Production-grade implementation of <see cref="IAutonomousIndexSynthesizerEngine"/>.
/// Thread-safe query plan &amp; missing index analyzer using native <see cref="System.Threading.Lock"/>.
/// </summary>
public sealed class AutonomousIndexSynthesizerEngine : IAutonomousIndexSynthesizerEngine
{
    private readonly System.Threading.Lock _lock = new();
    private readonly Dictionary<string, IndexRecommendation> _recommendations = new(StringComparer.OrdinalIgnoreCase);

    // Heuristics for index type selection
    private static readonly string[] TimestampIndicators = ["createdat", "createddate", "timestamp", "eventtime", "insertedat", "loggedat", "utc", "time", "date"];
    private static readonly string[] GinJsonIndicators = ["metadata", "tags", "payload", "properties", "attributes", "data", "json", "labels"];

    /// <inheritdoc />
    public IReadOnlyList<IndexRecommendation> AnalyzeSlowQueries(IEnumerable<SlowQueryMetric> queries)
    {
        ArgumentNullException.ThrowIfNull(queries);

        List<IndexRecommendation> generated = [];

        foreach (var query in queries)
        {
            var recommendation = SynthesizeIndex(query);
            if (recommendation is not null)
            {
                generated.Add(recommendation);
            }
        }

        return generated;
    }

    /// <inheritdoc />
    public IndexRecommendation? SynthesizeIndex(SlowQueryMetric query)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Evaluation criteria: expensive sequential scans on large tables
        // RowsExamined > 5,000, MeanTime > 50ms, ScanType == SequentialScan
        if (query.ScanType != QueryScanType.SequentialScan)
        {
            return null;
        }

        if (query.RowsExaminedPerExecution <= 5000)
        {
            return null;
        }

        if (query.MeanExecutionTimeMs <= 50.0)
        {
            return null;
        }

        if (query.PredicateColumns.Count == 0 || string.IsNullOrWhiteSpace(query.TargetTable))
        {
            return null;
        }

        // Determine optimal index structure
        var indexType = DetermineOptimalIndexType(query);

        // Generate clean identifier names and DDL
        var cleanTable = SanitizeIdentifier(query.TargetTable);
        var cleanCols = string.Join("_", query.PredicateColumns.Select(SanitizeIdentifier));
        var indexName = $"idx_{cleanTable}_{cleanCols}";
        var columnsExpression = string.Join(", ", query.PredicateColumns);

        var typeStr = indexType switch
        {
            IndexType.BTree => "btree",
            IndexType.Brin => "brin",
            IndexType.Gin => "gin",
            IndexType.Hash => "hash",
            _ => "btree"
        };

        var ddl = $"CREATE INDEX CONCURRENTLY IF NOT EXISTS {indexName} ON {query.TargetTable} USING {typeStr} ({columnsExpression});";

        // Compute estimated cost reduction percentage [40% - 95%]
        var costReduction = CalculateCostReduction(query, indexType);

        // Compute bloat impact score [0.05 - 1.0]
        var bloatScore = CalculateBloatImpact(indexType, query.PredicateColumns.Count);

        var justification = $"Sequential scan on '{query.TargetTable}' examined {query.RowsExaminedPerExecution:N0} rows " +
            $"with mean time {query.MeanExecutionTimeMs:F1}ms. Synthesized {indexType} index on ({columnsExpression}) " +
            $"to reduce I/O cost by estimated {costReduction:F1}%.";

        var recommendationId = $"rec-{cleanTable}-{cleanCols}-{indexType.ToString().ToLowerInvariant()}";

        var recommendation = new IndexRecommendation(
            RecommendationId: recommendationId,
            TargetTable: query.TargetTable,
            Columns: query.PredicateColumns,
            IndexType: indexType,
            IsConcurrent: true,
            CreateDdl: ddl,
            EstimatedCostReductionPct: costReduction,
            BloatImpactScore: bloatScore,
            Justification: justification);

        lock (_lock)
        {
            _recommendations[recommendationId] = recommendation;
        }

        return recommendation;
    }

    /// <inheritdoc />
    public IReadOnlyList<IndexRecommendation> GetActiveRecommendations()
    {
        lock (_lock)
        {
            return [.. _recommendations.Values];
        }
    }

    /// <inheritdoc />
    public bool DismissRecommendation(string recommendationId)
    {
        if (string.IsNullOrWhiteSpace(recommendationId))
        {
            return false;
        }

        lock (_lock)
        {
            return _recommendations.Remove(recommendationId);
        }
    }

    /// <inheritdoc />
    public void ClearRecommendations()
    {
        lock (_lock)
        {
            _recommendations.Clear();
        }
    }

    private static IndexType DetermineOptimalIndexType(SlowQueryMetric query)
    {
        // 1. Check for JSONB / array fields -> GIN
        foreach (var col in query.PredicateColumns)
        {
            var normalized = col.Replace("_", "").ToLowerInvariant();
            if (GinJsonIndicators.Any(indicator => normalized.Contains(indicator, StringComparison.OrdinalIgnoreCase)) ||
                normalized.Contains("->>") || normalized.Contains("@>") || normalized.Contains("[]"))
            {
                return IndexType.Gin;
            }
        }

        // 2. Check for monotonic timestamp / ID columns on large tables -> BRIN
        // BRIN is optimal for physical order-correlated columns on large tables
        var isSingleTimeColumn = query.PredicateColumns.Count == 1 &&
            TimestampIndicators.Any(ind => query.PredicateColumns[0].Replace("_", "").ToLowerInvariant().Contains(ind, StringComparison.OrdinalIgnoreCase));

        var isLargeLogOrTimeseries = query.RowsExaminedPerExecution >= 20_000 &&
            query.PredicateColumns.Any(col => TimestampIndicators.Any(ind => col.Replace("_", "").ToLowerInvariant().Contains(ind, StringComparison.OrdinalIgnoreCase)));

        if (isSingleTimeColumn || isLargeLogOrTimeseries)
        {
            // If it's a composite multi-column predicate where time is combined with high-cardinality filters (like TenantId),
            // composite B-Tree is preferred for point/range queries unless table is predominantly append-only time log
            if (query.PredicateColumns.Count == 1 || query.TargetTable.Contains("log", StringComparison.OrdinalIgnoreCase) || query.TargetTable.Contains("event", StringComparison.OrdinalIgnoreCase))
            {
                return IndexType.Brin;
            }
        }

        // 3. Composite B-Tree for multi-column equality and range predicates (or single column relational filters)
        return IndexType.BTree;
    }

    private static double CalculateCostReduction(SlowQueryMetric query, IndexType indexType)
    {
        double reduction = 50.0;

        if (query.RowsExaminedPerExecution > 100_000) reduction += 20.0;
        else if (query.RowsExaminedPerExecution > 20_000) reduction += 12.0;
        else if (query.RowsExaminedPerExecution > 10_000) reduction += 6.0;

        if (query.MeanExecutionTimeMs > 500.0) reduction += 15.0;
        else if (query.MeanExecutionTimeMs > 200.0) reduction += 10.0;
        else if (query.MeanExecutionTimeMs > 100.0) reduction += 5.0;

        if (indexType is IndexType.BTree && query.PredicateColumns.Count > 1)
        {
            reduction += 5.0;
        }

        if (indexType is IndexType.Brin)
        {
            reduction += 4.0;
        }

        return Math.Clamp(Math.Round(reduction, 1), 40.0, 95.0);
    }

    private static double CalculateBloatImpact(IndexType indexType, int columnCount)
    {
        return indexType switch
        {
            IndexType.Brin => 0.05, // Minimal physical page allocation
            IndexType.BTree => Math.Clamp(Math.Round(0.20 + (0.05 * Math.Max(0, columnCount - 1)), 2), 0.20, 0.45),
            IndexType.Gin => 0.55,  // Inverted index postings lists require more storage
            IndexType.Hash => 0.25,
            _ => 0.30
        };
    }

    private static string SanitizeIdentifier(string input)
    {
        // Strip out ASC, DESC, NULLS FIRST/LAST, parentheses, and special characters
        var cleaned = Regex.Replace(input, @"\b(ASC|DESC|NULLS\s+(FIRST|LAST))\b", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"[^a-zA-Z0-9_]", "");
        return cleaned.ToLowerInvariant().Trim('_');
    }
}
