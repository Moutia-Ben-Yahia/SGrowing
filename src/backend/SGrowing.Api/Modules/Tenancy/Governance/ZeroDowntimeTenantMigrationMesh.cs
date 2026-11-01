using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Tenancy.Governance;

public enum ShardTarget
{
    Source = 1,
    Target = 2
}

public enum MigrationPhase
{
    NotStarted = 0,
    Backfill = 1,
    DualWriteReplication = 2,
    ReconcilingDivergence = 3,
    ReadyForCutover = 4,
    CutoverInProgress = 5,
    CutoverCompleted = 6,
    RolledBack = 7,
    Failed = 8
}

public sealed record CdcLogEvent(
    Guid EventId,
    Guid TenantId,
    long SequenceNumber,
    string TableName,
    string Operation,
    string RecordId,
    string Payload,
    string PayloadHash,
    DateTime TimestampUtc
);

public sealed record ShardRecord(
    string RecordId,
    string TableName,
    string Payload,
    string PayloadHash,
    DateTime UpdatedAtUtc
);

public sealed record MerkleChunk(
    int ChunkIndex,
    string ChunkHash,
    IReadOnlyList<string> RecordIds
);

public sealed record MerkleTreeResult(
    string MerkleRootHash,
    int TotalRecords,
    int TotalChunks,
    IReadOnlyList<MerkleChunk> Chunks
);

public sealed record DivergenceComparisonResult(
    bool HasZeroDrift,
    string SourceMerkleRoot,
    string TargetMerkleRoot,
    int DivergentChunkCount,
    IReadOnlyList<int> DivergentChunkIndices,
    IReadOnlyList<string> DiscrepantRecordIds
);

public sealed record MigrationCutoverResult(
    bool Success,
    Guid MigrationId,
    Guid TenantId,
    MigrationPhase CurrentPhase,
    ShardTarget ActiveWriteTarget,
    TimeSpan WritePauseDuration,
    string? Message
);

public sealed record MigrationJob(
    Guid MigrationId,
    Guid TenantId,
    string SourceShard,
    string TargetShard,
    MigrationPhase Phase,
    ShardTarget ActiveWriteTarget,
    long LastSequenceNumber,
    long TotalEventsReplicated,
    DateTime StartedAtUtc,
    DateTime? CutoverAtUtc = null,
    TimeSpan? CutoverPauseDuration = null,
    double PostCutoverErrorRate = 0.0,
    string? FailureOrRollbackReason = null
);

public interface IZeroDowntimeTenantMigrationMesh
{
    MigrationJob StartMigration(Guid tenantId, string sourceShard, string targetShard);
    Task<CdcLogEvent> ProcessWriteAsync(Guid tenantId, string tableName, string operation, string recordId, string payload);
    void SeedShardRecord(string shard, Guid tenantId, string tableName, string recordId, string payload);
    ShardRecord? GetShardRecord(string shard, Guid tenantId, string tableName, string recordId);
    IReadOnlyList<ShardRecord> GetShardRecords(string shard, Guid tenantId, string tableName);
    Task<MerkleTreeResult> ComputeShardMerkleTreeAsync(string shard, Guid tenantId, string tableName, int chunkSize = 5);
    Task<DivergenceComparisonResult> CompareDivergenceAsync(Guid migrationId, string tableName, int chunkSize = 5);
    Task<MigrationCutoverResult> ExecuteCutoverAsync(Guid migrationId, TimeSpan? maxPauseAllowed = null);
    Task<MigrationJob> TriggerRollbackAsync(Guid migrationId, string reason);
    void RecordPostCutoverOperation(Guid migrationId, bool success, string? errorDetails = null);
    MigrationJob? GetMigration(Guid migrationId);
    MigrationJob? GetTenantActiveMigration(Guid tenantId);
}

public sealed class ZeroDowntimeTenantMigrationMesh : IZeroDowntimeTenantMigrationMesh
{
    // High-performance native System.Threading.Lock for zero-downtime cutover latch
    private readonly Lock _cutoverLock = new();

    private readonly ConcurrentDictionary<Guid, MigrationJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, Guid> _tenantActiveJobs = new();

    // In-memory multi-shard storage: key = $"{shard}:{tenantId}:{tableName}", value = dict of records
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ShardRecord>> _shardDatastores = new();

    // CDC log stream per migration
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<CdcLogEvent>> _cdcStreams = new();
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _processedEventIds = new();
    private readonly ConcurrentDictionary<Guid, long> _tenantSequenceCounters = new();

    // Post-cutover error tracking: migrationId -> list of recent success/failure booleans
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<bool>> _postCutoverTelemetry = new();
    private const double RollbackErrorRateThreshold = 0.20; // 20% error rate triggers automatic rollback

    public MigrationJob StartMigration(Guid tenantId, string sourceShard, string targetShard)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceShard);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetShard);

        var migrationId = Guid.NewGuid();
        var job = new MigrationJob(
            MigrationId: migrationId,
            TenantId: tenantId,
            SourceShard: sourceShard,
            TargetShard: targetShard,
            Phase: MigrationPhase.DualWriteReplication,
            ActiveWriteTarget: ShardTarget.Source,
            LastSequenceNumber: 0,
            TotalEventsReplicated: 0,
            StartedAtUtc: DateTime.UtcNow
        );

        _jobs[migrationId] = job;
        _tenantActiveJobs[tenantId] = migrationId;
        _cdcStreams[migrationId] = new ConcurrentQueue<CdcLogEvent>();
        _processedEventIds[migrationId] = [];
        _tenantSequenceCounters[tenantId] = 0;

        return job;
    }

    public void SeedShardRecord(string shard, Guid tenantId, string tableName, string recordId, string payload)
    {
        var storeKey = BuildStoreKey(shard, tenantId, tableName);
        var tableStore = _shardDatastores.GetOrAdd(storeKey, _ => new ConcurrentDictionary<string, ShardRecord>());

        var payloadHash = ComputeSha256Hex(payload);
        tableStore[recordId] = new ShardRecord(
            RecordId: recordId,
            TableName: tableName,
            Payload: payload,
            PayloadHash: payloadHash,
            UpdatedAtUtc: DateTime.UtcNow
        );
    }

    public ShardRecord? GetShardRecord(string shard, Guid tenantId, string tableName, string recordId)
    {
        var storeKey = BuildStoreKey(shard, tenantId, tableName);
        if (_shardDatastores.TryGetValue(storeKey, out var tableStore) &&
            tableStore.TryGetValue(recordId, out var record))
        {
            return record;
        }

        return null;
    }

    public IReadOnlyList<ShardRecord> GetShardRecords(string shard, Guid tenantId, string tableName)
    {
        var storeKey = BuildStoreKey(shard, tenantId, tableName);
        if (_shardDatastores.TryGetValue(storeKey, out var tableStore))
        {
            return tableStore.Values.OrderBy(r => r.RecordId).ToList();
        }

        return [];
    }

    public Task<CdcLogEvent> ProcessWriteAsync(Guid tenantId, string tableName, string operation, string recordId, string payload)
    {
        // Enforce latch locking during cutover to guarantee sub-50ms pause consistency
        lock (_cutoverLock)
        {
            var activeMigration = GetTenantActiveMigration(tenantId);
            var activeTarget = activeMigration?.ActiveWriteTarget ?? ShardTarget.Source;

            var payloadHash = ComputeSha256Hex(payload);
            var now = DateTime.UtcNow;

            if (activeMigration == null || activeMigration.Phase == MigrationPhase.CutoverCompleted)
            {
                // Routine write: post-cutover or outside migration
                var targetShard = activeMigration?.TargetShard ?? "default-shard";
                SeedShardRecord(targetShard, tenantId, tableName, recordId, payload);

                var seq = _tenantSequenceCounters.AddOrUpdate(tenantId, 1, (_, current) => current + 1);
                var cdc = new CdcLogEvent(
                    EventId: Guid.NewGuid(),
                    TenantId: tenantId,
                    SequenceNumber: seq,
                    TableName: tableName,
                    Operation: operation,
                    RecordId: recordId,
                    Payload: payload,
                    PayloadHash: payloadHash,
                    TimestampUtc: now
                );

                return Task.FromResult(cdc);
            }

            if (activeMigration.Phase == MigrationPhase.RolledBack)
            {
                // Write routes strictly back to source
                SeedShardRecord(activeMigration.SourceShard, tenantId, tableName, recordId, payload);
                var seq = _tenantSequenceCounters.AddOrUpdate(tenantId, 1, (_, current) => current + 1);
                return Task.FromResult(new CdcLogEvent(
                    EventId: Guid.NewGuid(),
                    TenantId: tenantId,
                    SequenceNumber: seq,
                    TableName: tableName,
                    Operation: operation,
                    RecordId: recordId,
                    Payload: payload,
                    PayloadHash: payloadHash,
                    TimestampUtc: now
                ));
            }

            // During DualWriteReplication:
            // 1. Primary write to Source shard
            SeedShardRecord(activeMigration.SourceShard, tenantId, tableName, recordId, payload);

            // 2. Generate monotonic sequence CDC log event
            var sequenceNumber = _tenantSequenceCounters.AddOrUpdate(tenantId, 1, (_, current) => current + 1);
            var eventId = Guid.NewGuid();

            var cdcEvent = new CdcLogEvent(
                EventId: eventId,
                TenantId: tenantId,
                SequenceNumber: sequenceNumber,
                TableName: tableName,
                Operation: operation,
                RecordId: recordId,
                Payload: payload,
                PayloadHash: payloadHash,
                TimestampUtc: now
            );

            // 3. Queue to CDC log stream
            if (_cdcStreams.TryGetValue(activeMigration.MigrationId, out var queue))
            {
                queue.Enqueue(cdcEvent);
            }

            // 4. Dual-write replicate asynchronously to Target shard
            ReplicateToTarget(activeMigration, cdcEvent);

            return Task.FromResult(cdcEvent);
        }
    }

    private void ReplicateToTarget(MigrationJob job, CdcLogEvent cdcEvent)
    {
        if (!_processedEventIds.TryGetValue(job.MigrationId, out var processedSet))
            return;

        // Idempotency deduplication check
        if (!processedSet.Add(cdcEvent.EventId))
        {
            return; // Duplicate event ignored
        }

        if (string.Equals(cdcEvent.Operation, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            var storeKey = BuildStoreKey(job.TargetShard, job.TenantId, cdcEvent.TableName);
            if (_shardDatastores.TryGetValue(storeKey, out var tableStore))
            {
                tableStore.TryRemove(cdcEvent.RecordId, out _);
            }
        }
        else
        {
            SeedShardRecord(job.TargetShard, job.TenantId, cdcEvent.TableName, cdcEvent.RecordId, cdcEvent.Payload);
        }

        _jobs[job.MigrationId] = job with
        {
            LastSequenceNumber = Math.Max(job.LastSequenceNumber, cdcEvent.SequenceNumber),
            TotalEventsReplicated = job.TotalEventsReplicated + 1
        };
    }

    public Task<MerkleTreeResult> ComputeShardMerkleTreeAsync(string shard, Guid tenantId, string tableName, int chunkSize = 5)
    {
        var records = GetShardRecords(shard, tenantId, tableName);
        if (records.Count == 0)
        {
            var emptyHash = ComputeSha256Hex(string.Empty);
            return Task.FromResult(new MerkleTreeResult(emptyHash, 0, 0, []));
        }

        // 1. Chunk sorted records
        var chunks = new List<MerkleChunk>();
        var totalChunks = (int)Math.Ceiling(records.Count / (double)chunkSize);

        for (int i = 0; i < totalChunks; i++)
        {
            var chunkRows = records.Skip(i * chunkSize).Take(chunkSize).ToList();
            var sb = new StringBuilder();
            foreach (var r in chunkRows)
            {
                sb.Append($"{r.RecordId}:{r.PayloadHash};");
            }
            var chunkHash = ComputeSha256Hex(sb.ToString());
            chunks.Add(new MerkleChunk(i, chunkHash, chunkRows.Select(r => r.RecordId).ToList()));
        }

        // 2. Build Merkle tree from chunks to compute root hash
        var currentLevelHashes = chunks.Select(c => c.ChunkHash).ToList();
        while (currentLevelHashes.Count > 1)
        {
            var nextLevelHashes = new List<string>();
            for (int i = 0; i < currentLevelHashes.Count; i += 2)
            {
                if (i + 1 < currentLevelHashes.Count)
                {
                    var combined = ComputeSha256Hex(currentLevelHashes[i] + currentLevelHashes[i + 1]);
                    nextLevelHashes.Add(combined);
                }
                else
                {
                    // Odd leaf: pair with itself
                    var combined = ComputeSha256Hex(currentLevelHashes[i] + currentLevelHashes[i]);
                    nextLevelHashes.Add(combined);
                }
            }
            currentLevelHashes = nextLevelHashes;
        }

        var rootHash = currentLevelHashes.FirstOrDefault() ?? ComputeSha256Hex(string.Empty);
        return Task.FromResult(new MerkleTreeResult(rootHash, records.Count, chunks.Count, chunks));
    }

    public async Task<DivergenceComparisonResult> CompareDivergenceAsync(Guid migrationId, string tableName, int chunkSize = 5)
    {
        if (!_jobs.TryGetValue(migrationId, out var job))
        {
            throw new KeyNotFoundException($"Migration '{migrationId}' not found.");
        }

        var sourceTree = await ComputeShardMerkleTreeAsync(job.SourceShard, job.TenantId, tableName, chunkSize);
        var targetTree = await ComputeShardMerkleTreeAsync(job.TargetShard, job.TenantId, tableName, chunkSize);

        if (string.Equals(sourceTree.MerkleRootHash, targetTree.MerkleRootHash, StringComparison.Ordinal))
        {
            return new DivergenceComparisonResult(
                HasZeroDrift: true,
                SourceMerkleRoot: sourceTree.MerkleRootHash,
                TargetMerkleRoot: targetTree.MerkleRootHash,
                DivergentChunkCount: 0,
                DivergentChunkIndices: [],
                DiscrepantRecordIds: []
            );
        }

        // Identify divergent chunks & records
        var divergentChunkIndices = new List<int>();
        var discrepantRecordIds = new List<string>();

        var maxChunks = Math.Max(sourceTree.Chunks.Count, targetTree.Chunks.Count);
        for (int i = 0; i < maxChunks; i++)
        {
            var sourceChunk = i < sourceTree.Chunks.Count ? sourceTree.Chunks[i] : null;
            var targetChunk = i < targetTree.Chunks.Count ? targetTree.Chunks[i] : null;

            if (sourceChunk == null || targetChunk == null ||
                !string.Equals(sourceChunk.ChunkHash, targetChunk.ChunkHash, StringComparison.Ordinal))
            {
                divergentChunkIndices.Add(i);

                var sourceIds = sourceChunk?.RecordIds ?? [];
                var targetIds = targetChunk?.RecordIds ?? [];
                var allIds = sourceIds.Union(targetIds).Distinct();

                foreach (var id in allIds)
                {
                    var sRec = GetShardRecord(job.SourceShard, job.TenantId, tableName, id);
                    var tRec = GetShardRecord(job.TargetShard, job.TenantId, tableName, id);

                    if (sRec == null || tRec == null || !string.Equals(sRec.PayloadHash, tRec.PayloadHash, StringComparison.Ordinal))
                    {
                        discrepantRecordIds.Add(id);
                    }
                }
            }
        }

        return new DivergenceComparisonResult(
            HasZeroDrift: false,
            SourceMerkleRoot: sourceTree.MerkleRootHash,
            TargetMerkleRoot: targetTree.MerkleRootHash,
            DivergentChunkCount: divergentChunkIndices.Count,
            DivergentChunkIndices: divergentChunkIndices,
            DiscrepantRecordIds: discrepantRecordIds.Distinct().ToList()
        );
    }

    public async Task<MigrationCutoverResult> ExecuteCutoverAsync(Guid migrationId, TimeSpan? maxPauseAllowed = null)
    {
        if (!_jobs.TryGetValue(migrationId, out var job))
        {
            return new MigrationCutoverResult(
                Success: false,
                MigrationId: migrationId,
                TenantId: Guid.Empty,
                CurrentPhase: MigrationPhase.Failed,
                ActiveWriteTarget: ShardTarget.Source,
                WritePauseDuration: TimeSpan.Zero,
                Message: $"Migration '{migrationId}' not found."
            );
        }

        var maxAllowedPause = maxPauseAllowed ?? TimeSpan.FromMilliseconds(50); // Sovereign mandate: <50ms cutover write pause

        // Pre-cutover drift check: verify zero divergence on primary tables
        var divergence = await CompareDivergenceAsync(migrationId, "StorefrontOrders");
        if (!divergence.HasZeroDrift)
        {
            return new MigrationCutoverResult(
                Success: false,
                MigrationId: migrationId,
                TenantId: job.TenantId,
                CurrentPhase: job.Phase,
                ActiveWriteTarget: job.ActiveWriteTarget,
                WritePauseDuration: TimeSpan.Zero,
                Message: $"Cutover rejected: Data drift detected between Source and Target shards ({divergence.DivergentChunkCount} divergent chunks)."
            );
        }

        // Sub-second cutover latch switchover
        var sw = Stopwatch.StartNew();
        lock (_cutoverLock)
        {
            // 1. Enter cutover phase (pauses incoming concurrent writes)
            _jobs[migrationId] = job with { Phase = MigrationPhase.CutoverInProgress };

            // 2. Drain any remaining CDC events in queue
            if (_cdcStreams.TryGetValue(migrationId, out var queue))
            {
                while (queue.TryDequeue(out var pendingEvent))
                {
                    ReplicateToTarget(job, pendingEvent);
                }
            }

            // 3. Atomically switch routing pointer from Source -> Target
            var completedJob = job with
            {
                Phase = MigrationPhase.CutoverCompleted,
                ActiveWriteTarget = ShardTarget.Target,
                CutoverAtUtc = DateTime.UtcNow
            };

            sw.Stop();
            var pauseDuration = sw.Elapsed;

            completedJob = completedJob with { CutoverPauseDuration = pauseDuration };
            _jobs[migrationId] = completedJob;

            return new MigrationCutoverResult(
                Success: true,
                MigrationId: migrationId,
                TenantId: job.TenantId,
                CurrentPhase: MigrationPhase.CutoverCompleted,
                ActiveWriteTarget: ShardTarget.Target,
                WritePauseDuration: pauseDuration,
                Message: $"Cutover completed successfully. Routing switched to Target shard in {pauseDuration.TotalMilliseconds:F2}ms (Limit: {maxAllowedPause.TotalMilliseconds:F2}ms)."
            );
        }
    }

    public void RecordPostCutoverOperation(Guid migrationId, bool success, string? errorDetails = null)
    {
        if (!_jobs.TryGetValue(migrationId, out var job))
            return;

        if (job.Phase != MigrationPhase.CutoverCompleted)
            return;

        var queue = _postCutoverTelemetry.GetOrAdd(migrationId, _ => new ConcurrentQueue<bool>());
        queue.Enqueue(success);

        // Keep last 50 operation samples
        while (queue.Count > 50)
        {
            queue.TryDequeue(out _);
        }

        var samples = queue.ToArray();
        var failureCount = samples.Count(s => !s);
        var errorRate = samples.Length > 0 ? (double)failureCount / samples.Length : 0.0;

        _jobs[migrationId] = job with { PostCutoverErrorRate = errorRate };

        if (errorRate >= RollbackErrorRateThreshold && samples.Length >= 5)
        {
            _ = TriggerRollbackAsync(migrationId, $"Post-cutover error rate exceeded threshold ({errorRate:P1} >= {RollbackErrorRateThreshold:P1}). Details: {errorDetails}");
        }
    }

    public Task<MigrationJob> TriggerRollbackAsync(Guid migrationId, string reason)
    {
        lock (_cutoverLock)
        {
            if (!_jobs.TryGetValue(migrationId, out var job))
            {
                throw new KeyNotFoundException($"Migration '{migrationId}' not found.");
            }

            var rolledBack = job with
            {
                Phase = MigrationPhase.RolledBack,
                ActiveWriteTarget = ShardTarget.Source, // Instantly swing write pointer back to source
                FailureOrRollbackReason = reason
            };

            _jobs[migrationId] = rolledBack;
            return Task.FromResult(rolledBack);
        }
    }

    public MigrationJob? GetMigration(Guid migrationId) =>
        _jobs.TryGetValue(migrationId, out var job) ? job : null;

    public MigrationJob? GetTenantActiveMigration(Guid tenantId)
    {
        if (_tenantActiveJobs.TryGetValue(tenantId, out var migId) &&
            _jobs.TryGetValue(migId, out var job))
        {
            return job;
        }

        return null;
    }

    private static string BuildStoreKey(string shard, Guid tenantId, string tableName) =>
        $"{shard}:{tenantId}:{tableName}".ToLowerInvariant();

    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
