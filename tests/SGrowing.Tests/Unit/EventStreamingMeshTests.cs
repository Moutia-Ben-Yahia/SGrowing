using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SGrowing.Api.Common.Streaming;
using Xunit;

namespace SGrowing.Tests.Unit;

public class EventStreamingMeshTests
{
    private record OrderPlacedEvent(string OrderId, decimal Amount, string CustomerId);
    private record IncompatibleEvent(int Number);

    [Fact]
    public void ComputePartition_IsStrictlyDeterministicForKey()
    {
        var mesh = new EventStreamingMeshService();
        var key = "tenant_algeria_16";

        var part1 = mesh.ComputePartition("commerce.orders", key);
        var part2 = mesh.ComputePartition("commerce.orders", key);
        var part3 = mesh.ComputePartition("commerce.orders", key);

        part1.Should().Be(part2);
        part2.Should().Be(part3);
        part1.Should().BeInRange(0, mesh.GetPartitionCount("commerce.orders") - 1);
    }

    [Fact]
    public async Task PublishEventAsync_AssignsMonotonicOffsetsInFifoOrder()
    {
        var mesh = new EventStreamingMeshService();
        var topic = "analytics.clickstream";
        var partitionKey = "user_sess_999";
        var partitionId = mesh.ComputePartition(topic, partitionKey);

        var pub1 = await mesh.PublishEventAsync(topic, partitionKey, new OrderPlacedEvent("ORD-1", 100m, "C-1"));
        var pub2 = await mesh.PublishEventAsync(topic, partitionKey, new OrderPlacedEvent("ORD-2", 200m, "C-1"));
        var pub3 = await mesh.PublishEventAsync(topic, partitionKey, new OrderPlacedEvent("ORD-3", 300m, "C-1"));

        pub1.PartitionId.Should().Be(partitionId);
        pub2.PartitionId.Should().Be(partitionId);
        pub3.PartitionId.Should().Be(partitionId);

        pub1.Offset.Should().Be(0);
        pub2.Offset.Should().Be(1);
        pub3.Offset.Should().Be(2);

        var batch = await mesh.ConsumePartitionBatchAsync<OrderPlacedEvent>(topic, partitionId, fromOffset: 0, maxBatchSize: 10);
        batch.Should().HaveCount(3);
        batch[0].Payload.OrderId.Should().Be("ORD-1");
        batch[1].Payload.OrderId.Should().Be("ORD-2");
        batch[2].Payload.OrderId.Should().Be("ORD-3");
    }

    [Fact]
    public async Task ConsumerOffsets_SupportsMultipleIndependentConsumerGroups()
    {
        var mesh = new EventStreamingMeshService();
        var topic = "attribution.touchpoints";
        var partitionId = 0;

        await mesh.CommitConsumerOffsetAsync("group-cdp-stitching", topic, partitionId, offset: 42);
        await mesh.CommitConsumerOffsetAsync("group-shapley-calculator", topic, partitionId, offset: 15);

        var offset1 = await mesh.GetConsumerOffsetAsync("group-cdp-stitching", topic, partitionId);
        var offset2 = await mesh.GetConsumerOffsetAsync("group-shapley-calculator", topic, partitionId);

        offset1.Should().Be(42);
        offset2.Should().Be(15);
    }

    [Fact]
    public async Task ConsumePartitionBatch_QuarantinesPoisonMessageIntoDeadLetterQueue()
    {
        var mesh = new EventStreamingMeshService();
        var topic = "crm.interactions";
        var partitionKey = "contact_44";
        var partitionId = mesh.ComputePartition(topic, partitionKey);

        // Publish valid string
        await mesh.PublishEventAsync(topic, partitionKey, "Valid string payload");

        // Attempt consuming as incompatible record structure expecting JSON object
        var result = await mesh.ConsumePartitionBatchAsync<OrderPlacedEvent>(topic, partitionId, fromOffset: 0);

        // Deserialization failure should quarantine to DLQ without crashing
        result.Should().BeEmpty();

        var dlq = await mesh.GetDeadLetterMessagesAsync(topic);
        dlq.Should().ContainSingle();
        dlq[0].OriginalTopic.Should().Be(topic);
        dlq[0].PartitionKey.Should().Be(partitionKey);
        dlq[0].FailureReason.Should().NotBeNullOrWhiteSpace();
    }
}
