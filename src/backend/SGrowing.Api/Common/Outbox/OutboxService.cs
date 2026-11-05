using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Common.Outbox;

public class OutboxService : IOutboxService
{
    private readonly SGrowingDbContext? _dbContext;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<OutboxService>? _logger;
    private readonly ConcurrentDictionary<Guid, OutboxMessageEntity> _inMemoryMessages = new();
    private readonly object _lockObj = new();

    public OutboxService()
        : this((IServiceScopeFactory?)null, null)
    {
    }

    public OutboxService(SGrowingDbContext? dbContext, ILogger<OutboxService>? logger = null)
        : this(dbContext, null, logger)
    {
    }

    public OutboxService(IServiceScopeFactory? scopeFactory, ILogger<OutboxService>? logger = null)
        : this(null, scopeFactory, logger)
    {
    }

    public OutboxService(
        SGrowingDbContext? dbContext,
        IServiceScopeFactory? scopeFactory,
        ILogger<OutboxService>? logger = null)
    {
        _dbContext = dbContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<OutboxMessageEntity> EnqueueAsync<T>(
        Guid tenantId,
        string eventType,
        T payload,
        string? correlationId = null,
        string? destinationTopic = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType cannot be empty.", nameof(eventType));

        var payloadJson = payload is string str ? str : JsonSerializer.Serialize(payload);
        var now = DateTime.UtcNow;

        var message = new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType,
            PayloadJson = payloadJson,
            DestinationTopic = destinationTopic,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0,
            MaxRetries = 5,
            LastError = null,
            ProcessedAtUtc = null,
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            CorrelationId = correlationId
        };

        if (_dbContext != null)
        {
            await _dbContext.TransactionalOutboxMessages.AddAsync(message, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
            if (db != null)
            {
                await db.TransactionalOutboxMessages.AddAsync(message, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        _inMemoryMessages[message.Id] = message;
        return message;
    }

    public async Task<List<OutboxMessageEntity>> GetPendingMessagesAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        if (_dbContext != null)
        {
            var messages = await _dbContext.TransactionalOutboxMessages
                .IgnoreQueryFilters()
                .Where(m => (m.Status == OutboxMessageStatus.Pending || m.Status == OutboxMessageStatus.Processing)
                            && m.NextAttemptAtUtc <= now
                            && m.RetryCount < m.MaxRetries)
                .OrderBy(m => m.CreatedAtUtc)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var msg in messages)
            {
                msg.Status = OutboxMessageStatus.Processing;
                msg.UpdatedAtUtc = now;
                _inMemoryMessages[msg.Id] = msg;
            }

            if (messages.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return messages;
        }

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
            if (db != null)
            {
                var messages = await db.TransactionalOutboxMessages
                    .IgnoreQueryFilters()
                    .Where(m => (m.Status == OutboxMessageStatus.Pending || m.Status == OutboxMessageStatus.Processing)
                                && m.NextAttemptAtUtc <= now
                                && m.RetryCount < m.MaxRetries)
                    .OrderBy(m => m.CreatedAtUtc)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                foreach (var msg in messages)
                {
                    msg.Status = OutboxMessageStatus.Processing;
                    msg.UpdatedAtUtc = now;
                    _inMemoryMessages[msg.Id] = msg;
                }

                if (messages.Count > 0)
                {
                    await db.SaveChangesAsync(cancellationToken);
                }

                return messages;
            }
        }

        lock (_lockObj)
        {
            var messages = _inMemoryMessages.Values
                .Where(m => (m.Status == OutboxMessageStatus.Pending || m.Status == OutboxMessageStatus.Processing)
                            && m.NextAttemptAtUtc <= now
                            && m.RetryCount < m.MaxRetries)
                .OrderBy(m => m.CreatedAtUtc)
                .Take(batchSize)
                .ToList();

            foreach (var msg in messages)
            {
                msg.Status = OutboxMessageStatus.Processing;
                msg.UpdatedAtUtc = now;
            }

            return messages;
        }
    }

    public async Task MarkAsPublishedAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        if (_dbContext != null)
        {
            var msg = await _dbContext.TransactionalOutboxMessages
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

            if (msg != null)
            {
                msg.Status = OutboxMessageStatus.Published;
                msg.ProcessedAtUtc = now;
                msg.UpdatedAtUtc = now;
                await _dbContext.SaveChangesAsync(cancellationToken);
                _inMemoryMessages[msg.Id] = msg;
                return;
            }
        }

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
            if (db != null)
            {
                var msg = await db.TransactionalOutboxMessages
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

                if (msg != null)
                {
                    msg.Status = OutboxMessageStatus.Published;
                    msg.ProcessedAtUtc = now;
                    msg.UpdatedAtUtc = now;
                    await db.SaveChangesAsync(cancellationToken);
                    _inMemoryMessages[msg.Id] = msg;
                    return;
                }
            }
        }

        if (_inMemoryMessages.TryGetValue(messageId, out var inMemMsg))
        {
            inMemMsg.Status = OutboxMessageStatus.Published;
            inMemMsg.ProcessedAtUtc = now;
            inMemMsg.UpdatedAtUtc = now;
        }
    }

    public async Task MarkAsFailedAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        Action<OutboxMessageEntity> updateAction = msg =>
        {
            msg.RetryCount++;
            msg.LastError = error;
            msg.UpdatedAtUtc = now;

            var delaySeconds = Math.Pow(2, msg.RetryCount) * 5;
            msg.NextAttemptAtUtc = now.AddSeconds(delaySeconds);

            if (msg.RetryCount >= msg.MaxRetries)
            {
                msg.Status = OutboxMessageStatus.Failed;
            }
            else
            {
                msg.Status = OutboxMessageStatus.Pending;
            }
        };

        if (_dbContext != null)
        {
            var msg = await _dbContext.TransactionalOutboxMessages
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

            if (msg != null)
            {
                updateAction(msg);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _inMemoryMessages[msg.Id] = msg;
                return;
            }
        }

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
            if (db != null)
            {
                var msg = await db.TransactionalOutboxMessages
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

                if (msg != null)
                {
                    updateAction(msg);
                    await db.SaveChangesAsync(cancellationToken);
                    _inMemoryMessages[msg.Id] = msg;
                    return;
                }
            }
        }

        if (_inMemoryMessages.TryGetValue(messageId, out var inMemMsg))
        {
            updateAction(inMemMsg);
        }
    }

    public async Task<int> PurgeProcessedMessagesAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.UtcNow.Subtract(olderThan);

        if (_dbContext != null)
        {
            var toPurge = await _dbContext.TransactionalOutboxMessages
                .IgnoreQueryFilters()
                .Where(m => (m.Status == OutboxMessageStatus.Published || m.Status == OutboxMessageStatus.Failed)
                            && ((m.ProcessedAtUtc != null && m.ProcessedAtUtc <= threshold) || (m.ProcessedAtUtc == null && m.CreatedAtUtc <= threshold)))
                .ToListAsync(cancellationToken);

            if (toPurge.Count > 0)
            {
                _dbContext.TransactionalOutboxMessages.RemoveRange(toPurge);
                await _dbContext.SaveChangesAsync(cancellationToken);
                foreach (var p in toPurge)
                {
                    _inMemoryMessages.TryRemove(p.Id, out _);
                }
            }

            return toPurge.Count;
        }

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
            if (db != null)
            {
                var toPurge = await db.TransactionalOutboxMessages
                    .IgnoreQueryFilters()
                    .Where(m => (m.Status == OutboxMessageStatus.Published || m.Status == OutboxMessageStatus.Failed)
                                && ((m.ProcessedAtUtc != null && m.ProcessedAtUtc <= threshold) || (m.ProcessedAtUtc == null && m.CreatedAtUtc <= threshold)))
                    .ToListAsync(cancellationToken);

                if (toPurge.Count > 0)
                {
                    db.TransactionalOutboxMessages.RemoveRange(toPurge);
                    await db.SaveChangesAsync(cancellationToken);
                    foreach (var p in toPurge)
                    {
                        _inMemoryMessages.TryRemove(p.Id, out _);
                    }
                }

                return toPurge.Count;
            }
        }

        lock (_lockObj)
        {
            var keysToRemove = _inMemoryMessages.Values
                .Where(m => (m.Status == OutboxMessageStatus.Published || m.Status == OutboxMessageStatus.Failed)
                            && ((m.ProcessedAtUtc != null && m.ProcessedAtUtc <= threshold) || (m.ProcessedAtUtc == null && m.CreatedAtUtc <= threshold)))
                .Select(m => m.Id)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _inMemoryMessages.TryRemove(key, out _);
            }

            return keysToRemove.Count;
        }
    }
}
