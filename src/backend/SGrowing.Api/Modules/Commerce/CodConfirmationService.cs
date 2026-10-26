using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface ICodConfirmationService
{
    Task<OrderCollisionLockResult> AcquireLockAsync(Guid tenantId, Guid orderId, string agentId, string agentName, int leaseMinutes = 3);
    Task<bool> ReleaseLockAsync(Guid tenantId, Guid orderId, string agentId);
    Task<CodRiskEvaluationResult> EvaluateRiskAsync(Guid tenantId, CodOrder order);
    Task<CodOrder> RecordCallOutcomeAsync(Guid tenantId, Guid orderId, string agentId, string agentName, CallOutcome outcome, string? notes, DateTime? callbackUtc);
    Task<string> GenerateInteractiveWhatsAppPayloadAsync(Guid tenantId, Guid orderId);
    Task<CodOrder> HandleCustomerInteractiveResponseAsync(Guid tenantId, Guid orderId, string action);
}

public record OrderCollisionLockResult(bool LockAcquired, string CurrentHolderAgentName, DateTime? ExpiresAtUtc, string Message);
public record CodRiskEvaluationResult(FraudRiskLevel RiskLevel, int Score, List<string> Flags, bool IsAllowedToShip);

public class CodConfirmationService : ICodConfirmationService
{
    private readonly SGrowingDbContext _dbContext;

    public CodConfirmationService(SGrowingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OrderCollisionLockResult> AcquireLockAsync(Guid tenantId, Guid orderId, string agentId, string agentName, int leaseMinutes = 3)
    {
        var now = DateTime.UtcNow;

        // Clean up expired locks for this order
        var existingLocks = await _dbContext.OrderCollisionLocks
            .Where(l => l.TenantId == tenantId && l.OrderId == orderId && l.IsActive)
            .ToListAsync();

        foreach (var l in existingLocks)
        {
            if (l.ExpiresAtUtc <= now)
            {
                l.IsActive = false;
            }
        }

        var activeLock = existingLocks.FirstOrDefault(l => l.IsActive && l.ExpiresAtUtc > now);

        if (activeLock != null)
        {
            if (activeLock.AgentId == agentId)
            {
                // Renew existing lease
                activeLock.ExpiresAtUtc = now.AddMinutes(leaseMinutes);
                await _dbContext.SaveChangesAsync();
                return new OrderCollisionLockResult(true, agentName, activeLock.ExpiresAtUtc, "Lock lease renewed.");
            }

            return new OrderCollisionLockResult(false, activeLock.AgentName, activeLock.ExpiresAtUtc, $"Collision prevented: Order is locked by {activeLock.AgentName}.");
        }

        var newLock = new OrderCollisionLock
        {
            TenantId = tenantId,
            OrderId = orderId,
            AgentId = agentId,
            AgentName = agentName,
            LockedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(leaseMinutes),
            IsActive = true
        };

        _dbContext.OrderCollisionLocks.Add(newLock);
        await _dbContext.SaveChangesAsync();

        return new OrderCollisionLockResult(true, agentName, newLock.ExpiresAtUtc, "Order lock acquired.");
    }

    public async Task<bool> ReleaseLockAsync(Guid tenantId, Guid orderId, string agentId)
    {
        var locks = await _dbContext.OrderCollisionLocks
            .Where(l => l.TenantId == tenantId && l.OrderId == orderId && l.AgentId == agentId && l.IsActive)
            .ToListAsync();

        if (!locks.Any())
            return false;

        foreach (var l in locks)
        {
            l.IsActive = false;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<CodRiskEvaluationResult> EvaluateRiskAsync(Guid tenantId, CodOrder order)
    {
        var flags = new List<string>();
        int score = 0;

        // 1. Phone number format checks
        var cleanPhone = Regex.Replace(order.PhoneNumber, @"\s+", "");
        bool isValidFormat = order.CountryCode switch
        {
            "TN" => Regex.IsMatch(cleanPhone, @"^(\+216)?[24579]\d{7}$"),
            "EG" => Regex.IsMatch(cleanPhone, @"^(\+20)?01[0125]\d{8}$"),
            "DZ" => Regex.IsMatch(cleanPhone, @"^(\+213)?[567]\d{8}$"),
            "MA" => Regex.IsMatch(cleanPhone, @"^(\+212)?[567]\d{8}$"),
            _ => cleanPhone.Length >= 8
        };

        if (!isValidFormat)
        {
            flags.Add("INVALID_PHONE_FORMAT");
            score += 35;
        }

        // 2. Blacklist check
        var isBlacklisted = await _dbContext.BlacklistEntries
            .AnyAsync(b => b.TenantId == tenantId && b.Type == "Phone" && b.Value == cleanPhone);

        if (isBlacklisted)
        {
            flags.Add("BLACKLISTED_PHONE_NUMBER");
            score += 60;
        }

        // 3. Duplicate order velocity (orders in last 15 minutes with same phone)
        var fifteenMinutesAgo = DateTime.UtcNow.AddMinutes(-15);
        var recentDuplicates = await _dbContext.CodOrders
            .CountAsync(o => o.TenantId == tenantId && o.Id != order.Id && o.PhoneNumber == order.PhoneNumber && o.CreatedAtUtc >= fifteenMinutesAgo);

        if (recentDuplicates > 0)
        {
            flags.Add($"DUPLICATE_ORDER_VELOCITY_{recentDuplicates}");
            score += 25 * recentDuplicates;
        }

        // 4. Address check
        if (string.IsNullOrWhiteSpace(order.GovernorateOrWilaya) || string.IsNullOrWhiteSpace(order.DelegationOrCommune))
        {
            flags.Add("INCOMPLETE_GEOGRAPHIC_CASCADE");
            score += 20;
        }

        var riskLevel = score switch
        {
            >= 70 => FraudRiskLevel.Fraudulent,
            >= 45 => FraudRiskLevel.High,
            >= 20 => FraudRiskLevel.Medium,
            _ => FraudRiskLevel.Low
        };

        order.RiskScore = Math.Min(100, score);
        order.RiskLevel = riskLevel;
        order.FraudFlagsJson = JsonSerializer.Serialize(flags);

        if (riskLevel == FraudRiskLevel.Fraudulent)
        {
            order.Status = CodOrderStatus.FakeDetected;
        }

        await _dbContext.SaveChangesAsync();

        return new CodRiskEvaluationResult(riskLevel, order.RiskScore, flags, riskLevel != FraudRiskLevel.Fraudulent);
    }

    public async Task<CodOrder> RecordCallOutcomeAsync(Guid tenantId, Guid orderId, string agentId, string agentName, CallOutcome outcome, string? notes, DateTime? callbackUtc)
    {
        var order = await _dbContext.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var record = new CallOutcomeRecord
        {
            TenantId = tenantId,
            OrderId = orderId,
            AgentId = agentId,
            AgentName = agentName,
            Outcome = outcome,
            Notes = notes,
            ScheduledCallbackUtc = callbackUtc,
            RecordedAtUtc = DateTime.UtcNow
        };
        _dbContext.CallOutcomeRecords.Add(record);

        switch (outcome)
        {
            case CallOutcome.Confirmed:
                order.Status = CodOrderStatus.Confirmed;
                order.ConfirmationChannel = ConfirmationChannel.PhoneCall;
                order.ConfirmedAtUtc = DateTime.UtcNow;
                break;
            case CallOutcome.CancelledByCustomer:
                order.Status = CodOrderStatus.Cancelled;
                break;
            case CallOutcome.AddressIncomplete:
            case CallOutcome.CallbackRequested:
            case CallOutcome.NoAnswer:
            case CallOutcome.Busy:
                order.Status = CodOrderStatus.Postponed;
                break;
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            order.Notes = string.IsNullOrWhiteSpace(order.Notes) ? notes : $"{order.Notes} | {notes}";
        }

        // Release any active locks
        await ReleaseLockAsync(tenantId, orderId, agentId);

        await _dbContext.SaveChangesAsync();
        return order;
    }

    public async Task<string> GenerateInteractiveWhatsAppPayloadAsync(Guid tenantId, Guid orderId)
    {
        var order = await _dbContext.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = order.PhoneNumber,
            type = "interactive",
            interactive = new
            {
                type = "button",
                header = new { type = "text", text = $"Order Confirmation #{order.OrderNumber}" },
                body = new { text = $"Hello {order.CustomerName}! Thank you for your order ({order.TotalAmount} {order.CodCurrency}) to {order.GovernorateOrWilaya}, {order.DelegationOrCommune}. Please confirm your Cash-on-Delivery order:" },
                footer = new { text = "SGrowing Automated Commerce" },
                action = new
                {
                    buttons = new[]
                    {
                        new { type = "reply", reply = new { id = $"CONFIRM_{order.Id}", title = "✅ Confirm Order" } },
                        new { type = "reply", reply = new { id = $"CANCEL_{order.Id}", title = "❌ Cancel Order" } }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    public async Task<CodOrder> HandleCustomerInteractiveResponseAsync(Guid tenantId, Guid orderId, string action)
    {
        var order = await _dbContext.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        if (action.Contains("CONFIRM", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = CodOrderStatus.Confirmed;
            order.ConfirmationChannel = ConfirmationChannel.WhatsAppInteractive;
            order.ConfirmedAtUtc = DateTime.UtcNow;
            order.Notes = (order.Notes ?? "") + " | Auto-confirmed by customer via WhatsApp 1-Click.";
        }
        else if (action.Contains("CANCEL", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = CodOrderStatus.Cancelled;
            order.Notes = (order.Notes ?? "") + " | Cancelled by customer via WhatsApp 1-Click.";
        }

        await _dbContext.SaveChangesAsync();
        return order;
    }
}
