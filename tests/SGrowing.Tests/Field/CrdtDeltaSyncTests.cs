using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Field.Sync;
using System.Text.Json;
using Xunit;

namespace SGrowing.Tests.Field;

public class CrdtDeltaSyncTests
{
    private readonly CrdtStateReplicatorService _replicator = new();

    // =========================================================================
    // 1. VectorClock & Causality Tests
    // =========================================================================

    [Fact]
    public void VectorClock_InitialState_IsEmptyOrMatchesProvidedDictionary()
    {
        var emptyClock = new VectorClock();
        emptyClock.Entries.Should().BeEmpty();
        emptyClock["node-1"].Should().Be(0L);

        var seededClock = new VectorClock(new Dictionary<string, long>
        {
            ["tablet-1"] = 5L,
            ["phone-2"] = 12L
        });
        seededClock["tablet-1"].Should().Be(5L);
        seededClock["phone-2"].Should().Be(12L);
        seededClock["unknown"].Should().Be(0L);
    }

    [Fact]
    public void VectorClock_Increment_IncreasesNodeCounter()
    {
        var clock = new VectorClock();
        clock.Increment("node-a");
        clock["node-a"].Should().Be(1L);

        clock.Increment("node-a");
        clock["node-a"].Should().Be(2L);
    }

    [Fact]
    public void VectorClock_Increment_MultipleNodes_TracksCountersIndependently()
    {
        var clock = new VectorClock();
        clock.Increment("device-alpha");
        clock.Increment("device-beta");
        clock.Increment("device-alpha");

        clock["device-alpha"].Should().Be(2L);
        clock["device-beta"].Should().Be(1L);
    }

    [Fact]
    public void VectorClock_WithIncrement_ReturnsNewInstanceWithoutMutatingOriginal()
    {
        var original = new VectorClock(new Dictionary<string, long> { ["node-1"] = 3L });
        var updated = original.WithIncrement("node-1");

        original["node-1"].Should().Be(3L);
        updated["node-1"].Should().Be(4L);
        updated.Should().NotBeSameAs(original);
    }

    [Fact]
    public void VectorClock_CompareTo_IdenticalClocks_ReturnsEqual()
    {
        var c1 = new VectorClock(new Dictionary<string, long> { ["A"] = 2, ["B"] = 4 });
        var c2 = new VectorClock(new Dictionary<string, long> { ["A"] = 2, ["B"] = 4 });

        c1.CompareTo(c2).Should().Be(CausalityRelation.Equal);
        (c1 == c2).Should().BeTrue();
    }

    [Fact]
    public void VectorClock_CompareTo_Dominates_WhenAllGreaterOrEqualAndAtLeastOneGreater()
    {
        var c1 = new VectorClock(new Dictionary<string, long> { ["A"] = 3, ["B"] = 4 });
        var c2 = new VectorClock(new Dictionary<string, long> { ["A"] = 2, ["B"] = 4 });

        c1.CompareTo(c2).Should().Be(CausalityRelation.Dominates);
        c1.Dominates(c2).Should().BeTrue();
        c2.IsDominatedBy(c1).Should().BeTrue();
    }

    [Fact]
    public void VectorClock_CompareTo_Dominated_WhenAllLesserOrEqualAndAtLeastOneLesser()
    {
        var c1 = new VectorClock(new Dictionary<string, long> { ["A"] = 1, ["B"] = 2 });
        var c2 = new VectorClock(new Dictionary<string, long> { ["A"] = 1, ["B"] = 5 });

        c1.CompareTo(c2).Should().Be(CausalityRelation.Dominated);
        c1.IsDominatedBy(c2).Should().BeTrue();
    }

    [Fact]
    public void VectorClock_CompareTo_Concurrent_WhenClocksHaveDivergentBranches()
    {
        var c1 = new VectorClock(new Dictionary<string, long> { ["A"] = 3, ["B"] = 1 });
        var c2 = new VectorClock(new Dictionary<string, long> { ["A"] = 1, ["B"] = 3 });

        c1.CompareTo(c2).Should().Be(CausalityRelation.Concurrent);
        c1.IsConcurrentWith(c2).Should().BeTrue();
    }

    [Fact]
    public void VectorClock_CompareTo_DisjointNodeSets_ReturnsConcurrent()
    {
        var c1 = new VectorClock(new Dictionary<string, long> { ["DeviceX"] = 1 });
        var c2 = new VectorClock(new Dictionary<string, long> { ["DeviceY"] = 1 });

        c1.CompareTo(c2).Should().Be(CausalityRelation.Concurrent);
    }

    [Fact]
    public void VectorClock_Merge_TakesMaximalCounterPerNode()
    {
        var c1 = new VectorClock(new Dictionary<string, long> { ["A"] = 4, ["B"] = 2, ["C"] = 1 });
        var c2 = new VectorClock(new Dictionary<string, long> { ["A"] = 2, ["B"] = 5, ["D"] = 7 });

        var merged = c1.Merge(c2);

        merged["A"].Should().Be(4L);
        merged["B"].Should().Be(5L);
        merged["C"].Should().Be(1L);
        merged["D"].Should().Be(7L);
    }

    [Fact]
    public void VectorClock_Merge_Commutativity_MergeAWithBEqualsMergeBWithA()
    {
        var a = new VectorClock(new Dictionary<string, long> { ["X"] = 10, ["Y"] = 3 });
        var b = new VectorClock(new Dictionary<string, long> { ["X"] = 5, ["Y"] = 8, ["Z"] = 2 });

        var ab = a.Merge(b);
        var ba = b.Merge(a);

        ab.Should().Be(ba);
        ab.CompareTo(ba).Should().Be(CausalityRelation.Equal);
    }

    [Fact]
    public void VectorClock_Merge_Associativity_ThreeWayMergeEvaluatesIdentically()
    {
        var a = new VectorClock(new Dictionary<string, long> { ["Node1"] = 5 });
        var b = new VectorClock(new Dictionary<string, long> { ["Node2"] = 8 });
        var c = new VectorClock(new Dictionary<string, long> { ["Node1"] = 2, ["Node3"] = 4 });

        var mergeLeft = a.Merge(b).Merge(c);
        var mergeRight = a.Merge(b.Merge(c));

        mergeLeft.Should().Be(mergeRight);
    }

    [Fact]
    public void VectorClock_Merge_Idempotency_MergeWithSelfReturnsEquivalentClock()
    {
        var a = new VectorClock(new Dictionary<string, long> { ["N1"] = 7, ["N2"] = 12 });
        var selfMerged = a.Merge(a);

        selfMerged.Should().Be(a);
    }

    [Fact]
    public void VectorClock_MergeInPlace_MutatesClockWithMaximalCounters()
    {
        var c1 = new VectorClock(new Dictionary<string, long> { ["A"] = 2, ["B"] = 5 });
        var c2 = new VectorClock(new Dictionary<string, long> { ["A"] = 4, ["C"] = 1 });

        c1.MergeInPlace(c2);

        c1["A"].Should().Be(4L);
        c1["B"].Should().Be(5L);
        c1["C"].Should().Be(1L);
    }

    [Fact]
    public void VectorClock_Serialization_ToJsonAndFromJson_RoundTripsAccurately()
    {
        var clock = new VectorClock(new Dictionary<string, long>
        {
            ["scanner-01"] = 42,
            ["cloud-central"] = 100
        });

        var json = clock.ToJson();
        var deserialized = VectorClock.FromJson(json);

        deserialized.Should().Be(clock);
        deserialized["scanner-01"].Should().Be(42);
        deserialized["cloud-central"].Should().Be(100);
    }

    [Fact]
    public void VectorClock_Parse_FromString_ReconstructsClock()
    {
        var parsed = VectorClock.Parse("tablet-a:10,tablet-b:25");
        parsed["tablet-a"].Should().Be(10L);
        parsed["tablet-b"].Should().Be(25L);

        var jsonParsed = VectorClock.Parse("{\"nodeX\": 99}");
        jsonParsed["nodeX"].Should().Be(99L);
    }

    [Fact]
    public void VectorClock_CausalityRelation_Aliases_HaveIdenticalEnumValues()
    {
        ((int)CausalityRelation.Dominates).Should().Be((int)CausalityRelation.HappensAfter);
        ((int)CausalityRelation.Dominated).Should().Be((int)CausalityRelation.HappensBefore);
        ((int)CausalityRelation.Concurrent).Should().Be((int)CausalityRelation.Conflict);
        ((int)CausalityRelation.Equal).Should().Be(0);
    }

    // =========================================================================
    // 2. LwwRegister Tests
    // =========================================================================

    [Fact]
    public void LwwRegister_Assign_UpdatesValueWhenTimestampIsNewer()
    {
        var t1 = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var t2 = t1.AddMinutes(5);

        var reg = new LwwRegister<string>("v1", t1, "node-1");
        reg.Assign("v2", t2, "node-1");

        reg.Value.Should().Be("v2");
        reg.Timestamp.Should().Be(t2);
        reg.NodeId.Should().Be("node-1");
    }

    [Fact]
    public void LwwRegister_Assign_IgnoresWriteWhenTimestampIsOlder()
    {
        var t1 = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var tOlder = t1.AddMinutes(-5);

        var reg = new LwwRegister<string>("v1", t1, "node-1");
        reg.Assign("staleValue", tOlder, "node-1");

        reg.Value.Should().Be("v1");
        reg.Timestamp.Should().Be(t1);
    }

    [Fact]
    public void LwwRegister_Merge_NewerTimestampWins()
    {
        var t1 = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var t2 = t1.AddSeconds(30);

        var regA = new LwwRegister<int>(100, t1, "nodeA");
        var regB = new LwwRegister<int>(200, t2, "nodeB");

        var merged = regA.Merge(regB);
        merged.Value.Should().Be(200);
        merged.Timestamp.Should().Be(t2);
        merged.NodeId.Should().Be("nodeB");
    }

    [Fact]
    public void LwwRegister_Merge_EqualTimestamps_TieBreaksByNodeIdOrdinalDescending()
    {
        var ts = new DateTimeOffset(2026, 11, 1, 14, 0, 0, TimeSpan.Zero);

        var regLowerNode = new LwwRegister<string>("alphaVal", ts, "client-001");
        var regHigherNode = new LwwRegister<string>("betaVal", ts, "client-002");

        // "client-002" > "client-001" ordinally
        var merged1 = regLowerNode.Merge(regHigherNode);
        var merged2 = regHigherNode.Merge(regLowerNode);

        merged1.Value.Should().Be("betaVal");
        merged1.NodeId.Should().Be("client-002");
        merged2.Value.Should().Be("betaVal");
        merged2.NodeId.Should().Be("client-002");
    }

    [Fact]
    public void LwwRegister_Merge_Commutativity_MergeAWithBEqualsMergeBWithA()
    {
        var t1 = new DateTimeOffset(2026, 11, 1, 8, 30, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 11, 1, 8, 35, 0, TimeSpan.Zero);

        var a = new LwwRegister<string>("StopPending", t1, "device-1");
        var b = new LwwRegister<string>("StopDelivered", t2, "device-2");

        var ab = a.Merge(b);
        var ba = b.Merge(a);

        ab.Should().Be(ba);
    }

    [Fact]
    public void LwwRegister_Merge_Associativity_HoldsAcrossThreeReplicas()
    {
        var baseTime = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

        var a = new LwwRegister<int>(1, baseTime.AddMinutes(1), "node-1");
        var b = new LwwRegister<int>(2, baseTime.AddMinutes(2), "node-2");
        var c = new LwwRegister<int>(3, baseTime.AddMinutes(3), "node-3");

        var left = a.Merge(b).Merge(c);
        var right = a.Merge(b.Merge(c));

        left.Should().Be(right);
        left.Value.Should().Be(3);
    }

    [Fact]
    public void LwwRegister_Merge_Idempotency_MergeWithSelfLeavesStateUnchanged()
    {
        var ts = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var reg = new LwwRegister<string>("ActiveStatus", ts, "node-x");

        var merged = reg.Merge(reg);
        merged.Should().Be(reg);
    }

    // =========================================================================
    // 3. AwOrSet (Add-Wins Observed-Remove Set) Tests
    // =========================================================================

    [Fact]
    public void AwOrSet_Add_ContainsElementAndIncrementsCount()
    {
        var set = new AwOrSet<string>();
        set.Add("STOP-1001", "tag-1");

        set.Contains("STOP-1001").Should().BeTrue();
        set.Count.Should().Be(1);
        set.Elements.Should().Contain("STOP-1001");
    }

    [Fact]
    public void AwOrSet_Remove_RemovesObservedElement()
    {
        var set = new AwOrSet<string>();
        set.Add("STOP-1001", "tag-1");
        set.Remove("STOP-1001");

        set.Contains("STOP-1001").Should().BeFalse();
        set.Count.Should().Be(0);
        set.Elements.Should().BeEmpty();
    }

    [Fact]
    public void AwOrSet_AddWins_ConcurrentAddAndRemove_AddWins()
    {
        // Replica A and Replica B both observe STOP-A with tag-1
        var replicaA = new AwOrSet<string>();
        replicaA.Add("STOP-A", "tag-1");

        var replicaB = new AwOrSet<string>();
        replicaB.Add("STOP-A", "tag-1");

        // Replica A removes STOP-A (observing only tag-1)
        replicaA.Remove("STOP-A");

        // Replica B concurrently re-adds STOP-A with a new unobserved tag-2
        replicaB.Add("STOP-A", "tag-2");

        // Merge Replica A and Replica B
        var mergedA = replicaA.Merge(replicaB);
        var mergedB = replicaB.Merge(replicaA);

        // Add-Wins: tag-2 is unobserved by replica A's removal, so STOP-A survives!
        mergedA.Contains("STOP-A").Should().BeTrue();
        mergedB.Contains("STOP-A").Should().BeTrue();
        mergedA.Elements.Should().Contain("STOP-A");
        mergedA.Equals(mergedB).Should().BeTrue();
    }

    [Fact]
    public void AwOrSet_RemoveThenAdd_ReAddedElementIsPresentWithNewTag()
    {
        var set = new AwOrSet<string>();
        set.Add("STOP-99", "initial-tag");
        set.Remove("STOP-99");
        set.Contains("STOP-99").Should().BeFalse();

        set.Add("STOP-99", "readded-tag");
        set.Contains("STOP-99").Should().BeTrue();
        set.Count.Should().Be(1);
    }

    [Fact]
    public void AwOrSet_Merge_CombinesDisjointElementsFromBothReplicas()
    {
        var setA = new AwOrSet<string>();
        setA.Add("STOP-1", "tag-a1");
        setA.Add("STOP-2", "tag-a2");

        var setB = new AwOrSet<string>();
        setB.Add("STOP-3", "tag-b3");

        var merged = setA.Merge(setB);
        merged.Count.Should().Be(3);
        merged.Elements.Should().BeEquivalentTo(["STOP-1", "STOP-2", "STOP-3"]);
    }

    [Fact]
    public void AwOrSet_Merge_Commutativity_MergeAWithBEqualsMergeBWithA()
    {
        var setA = new AwOrSet<string>();
        setA.Add("STOP-1", "tag-1");
        setA.Add("STOP-2", "tag-2");

        var setB = new AwOrSet<string>();
        setB.Add("STOP-2", "tag-2-alt");
        setB.Add("STOP-3", "tag-3");

        var ab = setA.Merge(setB);
        var ba = setB.Merge(setA);

        ab.Equals(ba).Should().BeTrue();
        ab.Elements.Should().BeEquivalentTo(ba.Elements);
    }

    [Fact]
    public void AwOrSet_Merge_Associativity_ThreeWayMergeMatches()
    {
        var a = new AwOrSet<string>();
        a.Add("A", "tag-a");

        var b = new AwOrSet<string>();
        b.Add("B", "tag-b");

        var c = new AwOrSet<string>();
        c.Add("C", "tag-c");

        var left = a.Merge(b).Merge(c);
        var right = a.Merge(b.Merge(c));

        left.Equals(right).Should().BeTrue();
        left.Elements.Should().BeEquivalentTo(["A", "B", "C"]);
    }

    [Fact]
    public void AwOrSet_Merge_Idempotency_MergeWithSelfPreservesElements()
    {
        var set = new AwOrSet<string>();
        set.Add("ITEM-1", "tag-1");
        set.Add("ITEM-2", "tag-2");

        var merged = set.Merge(set);
        merged.Equals(set).Should().BeTrue();
        merged.Elements.Should().BeEquivalentTo(["ITEM-1", "ITEM-2"]);
    }

    // =========================================================================
    // 4. Replicator, Delta Sync, Resume Token, POD & Invariant Tests
    // =========================================================================

    [Fact]
    public void Replicator_InitialState_HasServerClock()
    {
        _replicator.ServerClock.Should().NotBeNull();
        _replicator.ServerClock["server-replicator"].Should().BeGreaterThan(0L);
    }

    [Fact]
    public async Task Replicator_UpsertManifest_AndGetSnapshot_ReturnsStoredManifest()
    {
        var manifestId = Guid.NewGuid();
        var manifest = new FieldManifestRecord
        {
            ManifestId = manifestId,
            DriverId = "driver-42",
            Status = ManifestStatus.Assigned
        };

        var upserted = await _replicator.UpsertManifestAsync(manifest);
        upserted.ManifestId.Should().Be(manifestId);

        var retrieved = await _replicator.GetManifestSnapshotAsync(manifestId);
        retrieved.Should().NotBeNull();
        retrieved!.DriverId.Should().Be("driver-42");
        retrieved.Status.Should().Be(ManifestStatus.Assigned);
    }

    [Fact]
    public async Task Replicator_GetDriverManifestsSnapshot_FiltersByDriverId()
    {
        var driverA = "driver-alpha";
        var driverB = "driver-beta";

        var m1 = new FieldManifestRecord { ManifestId = Guid.NewGuid(), DriverId = driverA, Status = ManifestStatus.Draft };
        var m2 = new FieldManifestRecord { ManifestId = Guid.NewGuid(), DriverId = driverA, Status = ManifestStatus.EnRoute };
        var m3 = new FieldManifestRecord { ManifestId = Guid.NewGuid(), DriverId = driverB, Status = ManifestStatus.Draft };

        await _replicator.UpsertManifestAsync(m1);
        await _replicator.UpsertManifestAsync(m2);
        await _replicator.UpsertManifestAsync(m3);

        var driverAManifests = await _replicator.GetDriverManifestsSnapshotAsync(driverA);
        driverAManifests.Should().HaveCount(2);
        driverAManifests.Should().Contain(m => m.ManifestId == m1.ManifestId);
        driverAManifests.Should().Contain(m => m.ManifestId == m2.ManifestId);
    }

    [Fact]
    public async Task Replicator_PushPullDelta_AppliesClientDeltasAndUpdatesServerClock()
    {
        var manifestId = Guid.NewGuid();
        var stopId = Guid.NewGuid();
        var clientClock = new VectorClock(new Dictionary<string, long> { ["device-scanner-01"] = 1 });

        var stop = new DeliveryStopRecord(
            StopId: stopId,
            TrackingNumber: "TRK-987654",
            RecipientName: "John Doe",
            Address: "123 Market St, Springfield",
            CodAmount: 49.99m,
            Status: StopStatus.Pending
        );

        var deltas = new List<ManifestDeltaItem>
        {
            new(
                DeltaId: Guid.NewGuid(),
                ManifestId: manifestId,
                DeltaType: ManifestDeltaType.StopAdded,
                Timestamp: DateTimeOffset.UtcNow,
                NodeId: "device-scanner-01",
                VectorClock: clientClock,
                StopId: stopId,
                StopData: stop
            )
        };

        var request = new SyncBatchRequest("device-scanner-01", clientClock, deltas);
        var response = await _replicator.ReplicateAsync(request);

        response.AppliedCount.Should().Be(1);
        response.ServerClock["device-scanner-01"].Should().Be(1L);

        var snapshot = await _replicator.GetManifestSnapshotAsync(manifestId);
        snapshot.Should().NotBeNull();
        snapshot!.Stops.Elements.Should().ContainSingle(s => s.StopId == stopId && s.TrackingNumber == "TRK-987654");
    }

    [Fact]
    public async Task Replicator_PushPullDelta_Idempotent_DuplicateDeltaIgnored()
    {
        var manifestId = Guid.NewGuid();
        var deltaId = Guid.NewGuid();
        var clientClock = new VectorClock(new Dictionary<string, long> { ["device-x"] = 1 });

        var delta = new ManifestDeltaItem(
            DeltaId: deltaId,
            ManifestId: manifestId,
            DeltaType: ManifestDeltaType.ManifestStatusUpdated,
            Timestamp: DateTimeOffset.UtcNow,
            NodeId: "device-x",
            VectorClock: clientClock,
            NewStatus: ManifestStatus.EnRoute
        );

        var req1 = new SyncBatchRequest("device-x", clientClock, [delta]);
        var res1 = await _replicator.ReplicateAsync(req1);
        res1.AppliedCount.Should().Be(1);

        // Sending the exact same delta ID again
        var req2 = new SyncBatchRequest("device-x", clientClock, [delta]);
        var res2 = await _replicator.ReplicateAsync(req2);
        res2.AppliedCount.Should().Be(0);
    }

    [Fact]
    public async Task Replicator_PushPullDelta_StreamsServerDeltasToClient()
    {
        var manifestId = Guid.NewGuid();

        // 1. Client A creates a delta
        var clientAClock = new VectorClock(new Dictionary<string, long> { ["client-a"] = 1 });
        var deltaFromA = new ManifestDeltaItem(
            DeltaId: Guid.NewGuid(),
            ManifestId: manifestId,
            DeltaType: ManifestDeltaType.ManifestStatusUpdated,
            Timestamp: DateTimeOffset.UtcNow,
            NodeId: "client-a",
            VectorClock: clientAClock,
            NewStatus: ManifestStatus.EnRoute
        );
        await _replicator.ReplicateAsync(new SyncBatchRequest("client-a", clientAClock, [deltaFromA]));

        // 2. Client B syncs with empty clock
        var clientBClock = new VectorClock();
        var resB = await _replicator.ReplicateAsync(new SyncBatchRequest("client-b", clientBClock, []));

        resB.ServerDeltas.Should().ContainSingle(d => d.DeltaId == deltaFromA.DeltaId);
        resB.ServerDeltas[0].NewStatus.Should().Be(ManifestStatus.EnRoute);
    }

    [Fact]
    public async Task Replicator_ResumeToken_TracksUnacknowledgedSequenceRange_AndResumesPagination()
    {
        var localReplicator = new CrdtStateReplicatorService();
        var manifestId = Guid.NewGuid();

        // Add 5 distinct deltas from node-origin
        for (int i = 0; i < 5; i++)
        {
            var clock = new VectorClock(new Dictionary<string, long> { ["node-origin"] = i + 1 });
            var delta = new ManifestDeltaItem(
                DeltaId: Guid.NewGuid(),
                ManifestId: manifestId,
                DeltaType: ManifestDeltaType.StopAdded,
                Timestamp: DateTimeOffset.UtcNow.AddMinutes(i),
                NodeId: "node-origin",
                VectorClock: clock,
                Details: $"Delta-{i}"
            );
            await localReplicator.ReplicateAsync(new SyncBatchRequest("node-origin", clock, [delta]));
        }

        // Client C syncs with BatchSize = 2
        var clientCClock = new VectorClock();
        var batch1 = await localReplicator.ReplicateAsync(new SyncBatchRequest("client-c", clientCClock, [], BatchSize: 2));

        batch1.ServerDeltas.Should().HaveCount(2);
        batch1.HasMore.Should().BeTrue();
        batch1.ResumeToken.Should().NotBeNullOrWhiteSpace();

        // Check unacknowledged ranges
        var unack = localReplicator.GetUnacknowledgedRanges("client-c");
        unack.Should().NotBeEmpty();

        // Client C resumes with batch1.ResumeToken
        var batch2 = await localReplicator.ReplicateAsync(new SyncBatchRequest("client-c", clientCClock, [], ResumeToken: batch1.ResumeToken, BatchSize: 2));

        batch2.ServerDeltas.Should().HaveCount(2);
        batch2.HasMore.Should().BeTrue();
        batch2.ResumeToken.Should().NotBeNullOrWhiteSpace();

        // Client C requests final batch
        var batch3 = await localReplicator.ReplicateAsync(new SyncBatchRequest("client-c", clientCClock, [], ResumeToken: batch2.ResumeToken, BatchSize: 2));
        batch3.ServerDeltas.Should().HaveCount(1);
        batch3.HasMore.Should().BeFalse();
        batch3.ResumeToken.Should().BeNull();
    }

    [Fact]
    public async Task Replicator_ProofOfDelivery_IngestsSignatureGeocodeAndPhotoHash()
    {
        var manifestId = Guid.NewGuid();
        var stopId = Guid.NewGuid();

        var initialStop = new DeliveryStopRecord(
            StopId: stopId,
            TrackingNumber: "TRK-POD-001",
            RecipientName: "Alice Smith",
            Address: "456 Elm St",
            CodAmount: 0m,
            Status: StopStatus.Pending
        );

        var manifest = new FieldManifestRecord
        {
            ManifestId = manifestId,
            DriverId = "driver-77",
            Status = ManifestStatus.EnRoute
        }.WithStop(initialStop);

        await _replicator.UpsertManifestAsync(manifest);

        var pod = new ProofOfDeliveryData(
            SignaturePath: "/signatures/2026/11/01/sig_stop001.png",
            Geocode: "37.7749,-122.4194",
            PhotoSha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            DeliveredAt: DateTimeOffset.UtcNow,
            SignerName: "Alice Smith",
            Notes: "Delivered to front porch"
        );

        var updated = await _replicator.SubmitProofOfDeliveryAsync(manifestId, stopId, pod, "driver-phone-77");

        updated.ProofOfDeliveryData.Should().NotBeNull();
        updated.ProofOfDeliveryData!.SignaturePath.Should().Be("/signatures/2026/11/01/sig_stop001.png");
        updated.ProofOfDeliveryData.Geocode.Should().Be("37.7749,-122.4194");
        updated.ProofOfDeliveryData.PhotoSha256.Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

        var stop = updated.Stops.Elements.FirstOrDefault(s => s.StopId == stopId);
        stop.Should().NotBeNull();
        stop!.Status.Should().Be(StopStatus.Delivered);
        stop.Pod.Should().Be(pod);
        updated.Status.Should().Be(ManifestStatus.Completed);
    }

    [Fact]
    public async Task Replicator_ProofOfDelivery_WhenAllStopsDelivered_AdvancesManifestToCompleted()
    {
        var manifestId = Guid.NewGuid();
        var s1 = new DeliveryStopRecord(Guid.NewGuid(), "TRK-1", "User 1", "Addr 1", 0m);
        var s2 = new DeliveryStopRecord(Guid.NewGuid(), "TRK-2", "User 2", "Addr 2", 0m);

        var manifest = new FieldManifestRecord
        {
            ManifestId = manifestId,
            DriverId = "driver-1",
            Status = ManifestStatus.EnRoute
        }.WithStop(s1).WithStop(s2);

        await _replicator.UpsertManifestAsync(manifest);

        var pod1 = new ProofOfDeliveryData(SignaturePath: "sig1.png", DeliveredAt: DateTimeOffset.UtcNow);
        var pod2 = new ProofOfDeliveryData(SignaturePath: "sig2.png", DeliveredAt: DateTimeOffset.UtcNow);

        var stateAfter1 = await _replicator.SubmitProofOfDeliveryAsync(manifestId, s1.StopId, pod1, "node");
        stateAfter1.Status.Should().Be(ManifestStatus.EnRoute);

        var stateAfter2 = await _replicator.SubmitProofOfDeliveryAsync(manifestId, s2.StopId, pod2, "node");
        stateAfter2.Status.Should().Be(ManifestStatus.Completed);
    }

    [Fact]
    public void Replicator_ConflictConvergence_TwoOfflineDevicesMergeToIdenticalState()
    {
        var manifestId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        // Device A was offline, delivered Stop 1
        var stop1 = new DeliveryStopRecord(Guid.NewGuid(), "TRK-A", "Customer A", "100 Ave", 10m, StopStatus.Delivered);
        var replicaA = new FieldManifestRecord(
            manifestId: manifestId,
            driverId: "driver-1",
            status: ManifestStatus.EnRoute,
            vectorClock: new VectorClock(new Dictionary<string, long> { ["device-a"] = 3 }),
            stops: new AwOrSet<DeliveryStopRecord>().Add(stop1, "tag-a"),
            proofOfDeliveryData: null,
            lastModifiedUtc: baseTime.AddMinutes(5),
            lastModifiedByNodeId: "device-a"
        );

        // Device B was offline, added Stop 2
        var stop2 = new DeliveryStopRecord(Guid.NewGuid(), "TRK-B", "Customer B", "200 Ave", 25m, StopStatus.Pending);
        var replicaB = new FieldManifestRecord(
            manifestId: manifestId,
            driverId: "driver-1",
            status: ManifestStatus.EnRoute,
            vectorClock: new VectorClock(new Dictionary<string, long> { ["device-b"] = 4 }),
            stops: new AwOrSet<DeliveryStopRecord>().Add(stop2, "tag-b"),
            proofOfDeliveryData: null,
            lastModifiedUtc: baseTime.AddMinutes(8),
            lastModifiedByNodeId: "device-b"
        );

        var mergedAB = _replicator.MergeManifests(replicaA, replicaB);
        var mergedBA = _replicator.MergeManifests(replicaB, replicaA);

        // Convergence invariant
        mergedAB.Should().Be(mergedBA);
        mergedAB.Stops.Count.Should().Be(2);
        mergedAB.Stops.Elements.Select(s => s.TrackingNumber).Should().BeEquivalentTo(["TRK-A", "TRK-B"]);
        mergedAB.VectorClock["device-a"].Should().Be(3L);
        mergedAB.VectorClock["device-b"].Should().Be(4L);
    }

    [Fact]
    public void Replicator_VerifyInvariants_EnforcesCommutativityAssociativityIdempotency()
    {
        var manifestId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

        var stop1 = new DeliveryStopRecord(Guid.NewGuid(), "T-1", "Client 1", "Address 1", 0m);
        var stop2 = new DeliveryStopRecord(Guid.NewGuid(), "T-2", "Client 2", "Address 2", 15m);
        var stop3 = new DeliveryStopRecord(Guid.NewGuid(), "T-3", "Client 3", "Address 3", 30m);

        var manifestA = new FieldManifestRecord(
            manifestId, "driver-1", ManifestStatus.Draft,
            new VectorClock(new Dictionary<string, long> { ["node-A"] = 1 }),
            new AwOrSet<DeliveryStopRecord>().Add(stop1, "tag-1"),
            null, baseTime.AddMinutes(1), "node-A");

        var manifestB = new FieldManifestRecord(
            manifestId, "driver-1", ManifestStatus.Assigned,
            new VectorClock(new Dictionary<string, long> { ["node-B"] = 2 }),
            new AwOrSet<DeliveryStopRecord>().Add(stop2, "tag-2"),
            null, baseTime.AddMinutes(2), "node-B");

        var manifestC = new FieldManifestRecord(
            manifestId, "driver-1", ManifestStatus.EnRoute,
            new VectorClock(new Dictionary<string, long> { ["node-C"] = 3 }),
            new AwOrSet<DeliveryStopRecord>().Add(stop3, "tag-3"),
            null, baseTime.AddMinutes(3), "node-C");

        bool invariantsHold = _replicator.VerifyInvariants(manifestA, manifestB, manifestC);
        invariantsHold.Should().BeTrue();
    }

    [Fact]
    public void Replicator_HealthCheck_ReturnsHealthyStatusAndMetrics()
    {
        var health = _replicator.GetHealth();
        health.Status.Should().Be("Healthy");
        health.EngineVersion.Should().Be("1.0.0-crdt");
        health.ServerTimestampUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CrdtDeltaSyncEndpoints_AddCrdtDeltaSyncServices_RegistersInterfacesWithoutBuildProvider()
    {
        var services = new ServiceCollection();
        services.AddCrdtDeltaSyncServices();

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(ICrdtStateReplicatorService) &&
            sd.ImplementationType == typeof(CrdtStateReplicatorService) &&
            sd.Lifetime == ServiceLifetime.Singleton);
    }
}
