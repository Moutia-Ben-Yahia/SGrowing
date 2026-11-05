using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Common.Outbox;

public interface IOutboxService
{
    Task<OutboxMessageEntity> EnqueueAsync<T>(
        Guid tenantId,
        string eventType,
        T payload,
        string? correlationId = null,
        string? destinationTopic = null,
        CancellationToken cancellationToken = default);

    Task<List<OutboxMessageEntity>> GetPendingMessagesAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default);

    Task MarkAsPublishedAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task MarkAsFailedAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken = default);

    Task<int> PurgeProcessedMessagesAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default);
}
