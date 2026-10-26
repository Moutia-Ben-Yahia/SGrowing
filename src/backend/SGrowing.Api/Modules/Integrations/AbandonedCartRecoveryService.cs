using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Integrations;

public class CartRecoveryDispatchResult
{
    public Guid CartId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string FormattedMessage { get; set; } = string.Empty;
    public string RecoveryUrl { get; set; } = string.Empty;
}

public class AbandonedCartRecoveryService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<AbandonedCartRecoveryService>? _logger;

    public AbandonedCartRecoveryService(SGrowingDbContext db, ILogger<AbandonedCartRecoveryService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<CartRecoveryDispatchResult>> TriggerRecoveryScanAsync(
        Guid tenantId,
        TimeSpan? abandonmentAge = null,
        CancellationToken cancellationToken = default)
    {
        var age = abandonmentAge ?? TimeSpan.FromMinutes(60);
        var cutoff = DateTime.UtcNow - age;

        var eligibleCarts = await _db.AbandonedCartRecords
            .Where(c => c.TenantId == tenantId &&
                        c.Status == AbandonedCartStatus.Detected &&
                        c.DetectedAtUtc <= cutoff)
            .Take(25)
            .ToListAsync(cancellationToken);

        var dispatched = new List<CartRecoveryDispatchResult>();

        foreach (var cart in eligibleCarts)
        {
            cart.MarkRecoveryTriggered();

            var message = $"Hi! We noticed you left some great items in your cart (${cart.CartTotal:N2}). Use code SAVE10 for 10% off when you complete your order here: {cart.RecoveryUrl}";

            var outboxMsg = OutboxMessage.Create(
                tenantId,
                "AbandonedCartRecoveryDispatchedEvent",
                $"{{\"cartId\":\"{cart.Id}\",\"email\":\"{cart.CustomerEmail}\",\"total\":{cart.CartTotal}}}"
            );
            await _db.OutboxMessages.AddAsync(outboxMsg, cancellationToken);

            dispatched.Add(new CartRecoveryDispatchResult
            {
                CartId = cart.Id,
                CustomerEmail = cart.CustomerEmail,
                FormattedMessage = message,
                RecoveryUrl = cart.RecoveryUrl
            });

            _logger?.LogInformation("Dispatched recovery message for cart {CartId} to {Email}", cart.Id, cart.CustomerEmail);
        }

        if (dispatched.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return dispatched;
    }
}
