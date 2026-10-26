using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Voice;

public interface IAutomatedVoiceDialerService
{
    Task<OutboundCallAttempt> QueueOrderForVoiceConfirmationAsync(Guid tenantId, Guid orderId, Guid? campaignId = null);
    string GenerateIvrScript(CodOrder order, string languageCode = "ar-TN");
    Task<OutboundCallAttempt> ProcessIvrCustomerResponseAsync(Guid tenantId, Guid attemptId, string? speechText, string? dtmfDigit);
    IvrSpokenIntent ClassifyCustomerIntent(string? speechText, string? dtmfDigit);
}

public class AutomatedVoiceDialerService : IAutomatedVoiceDialerService
{
    private readonly SGrowingDbContext _db;

    public AutomatedVoiceDialerService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<OutboundCallAttempt> QueueOrderForVoiceConfirmationAsync(Guid tenantId, Guid orderId, Guid? campaignId = null)
    {
        var order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId);
        if (order == null)
            throw new InvalidOperationException($"Order with ID {orderId} not found.");

        var existingAttempts = await _db.OutboundCallAttempts
            .Where(a => a.TenantId == tenantId && a.OrderId == orderId)
            .CountAsync();

        var attemptNumber = existingAttempts + 1;
        var attempt = OutboundCallAttempt.Create(tenantId, orderId, order.PhoneNumber, order.CustomerName, campaignId, attemptNumber);
        
        _db.OutboundCallAttempts.Add(attempt);
        await _db.SaveChangesAsync();

        return attempt;
    }

    public string GenerateIvrScript(CodOrder order, string languageCode = "ar-TN")
    {
        var lang = (languageCode ?? "ar-TN").ToLowerInvariant();

        if (lang.StartsWith("fr"))
        {
            return $"Bonjour {order.CustomerName}. Nous vous contactons pour confirmer votre commande N° {order.OrderNumber} d'un montant de {order.TotalAmount:F2} {order.CodCurrency} pour livraison à {order.GovernorateOrWilaya}. Pour confirmer, dites OUI ou tapez 1. Pour reporter, dites REPORTER ou tapez 2. Pour annuler, dites ANNULER ou tapez 3.";
        }
        else if (lang.StartsWith("en"))
        {
            return $"Hello {order.CustomerName}. Calling to confirm your order #{order.OrderNumber} for {order.TotalAmount:F2} {order.CodCurrency} delivering to {order.GovernorateOrWilaya}. Say CONFIRM or press 1 to confirm. Say POSTPONE or press 2. Say CANCEL or press 3.";
        }
        else
        {
            // Tunisian / Maghrebi Arabic
            return $"عسلامة {order.CustomerName}، نكلمو فيك من المتجر لتأكيد طلبيتك رقم {order.OrderNumber} بمبلغ {order.TotalAmount:F2} {order.CodCurrency} والتوصيل إلى {order.GovernorateOrWilaya}. لتأكيد الطلب قل نعم أو إضغط 1. لتأجيل الطلب قل تأجيل أو إضغط 2. للإلغاء قل إلغاء أو إضغط 3.";
        }
    }

    public IvrSpokenIntent ClassifyCustomerIntent(string? speechText, string? dtmfDigit)
    {
        // 1. Check DTMF keypress first
        if (!string.IsNullOrWhiteSpace(dtmfDigit))
        {
            var digit = dtmfDigit.Trim();
            if (digit == "1") return IvrSpokenIntent.Confirmed;
            if (digit == "2") return IvrSpokenIntent.Postponed;
            if (digit == "3") return IvrSpokenIntent.Cancelled;
        }

        if (string.IsNullOrWhiteSpace(speechText))
            return IvrSpokenIntent.None;

        var text = speechText.Trim().ToLowerInvariant();

        // 2. Tunisian, Arabic, French & English Confirmation patterns
        var confirmPattern = @"(نعم|نأكد|اي|أي|موافق|تأكيد|صحيح|oui|confirme|confirmer|d'accord|daccord|ok|yes|confirm|c'est bon)";
        if (Regex.IsMatch(text, confirmPattern, RegexOptions.IgnoreCase))
        {
            return IvrSpokenIntent.Confirmed;
        }

        // 3. Postpone patterns
        var postponePattern = @"(تأجيل|غدوة|مبعد|أجل|لاحقا|بعدين|postposer|reporter|plus tard|rappeler|demain|postpone|later)";
        if (Regex.IsMatch(text, postponePattern, RegexOptions.IgnoreCase))
        {
            return IvrSpokenIntent.Postponed;
        }

        // 4. Cancellation patterns
        var cancelPattern = @"(إلغاء|بطلت|لا|مانحبش|فسخ|annuler|annulation|non|pas d'accord|cancel|stop)";
        if (Regex.IsMatch(text, cancelPattern, RegexOptions.IgnoreCase))
        {
            return IvrSpokenIntent.Cancelled;
        }

        return IvrSpokenIntent.Unrecognized;
    }

    public async Task<OutboundCallAttempt> ProcessIvrCustomerResponseAsync(
        Guid tenantId,
        Guid attemptId,
        string? speechText,
        string? dtmfDigit)
    {
        var attempt = await _db.OutboundCallAttempts.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == attemptId);
        if (attempt == null)
            throw new InvalidOperationException($"Call attempt {attemptId} not found.");

        var order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == attempt.OrderId);
        if (order == null)
            throw new InvalidOperationException($"Order {attempt.OrderId} not found.");

        var intent = ClassifyCustomerIntent(speechText, dtmfDigit);
        attempt.SpeechTranscript = speechText;
        attempt.DtmfDigit = dtmfDigit;
        attempt.SpokenIntent = intent;
        attempt.CompletedAtUtc = DateTime.UtcNow;

        switch (intent)
        {
            case IvrSpokenIntent.Confirmed:
                attempt.State = IvrCallState.Confirmed;
                order.Status = CodOrderStatus.Confirmed;
                order.ConfirmationChannel = ConfirmationChannel.PhoneCall;
                order.ConfirmedAtUtc = DateTime.UtcNow;
                order.Notes = (order.Notes ?? "") + $" [Auto-Confirmed via Voice AI Call #{attempt.AttemptNumber}]";

                // Synchronize Defensive WMS: Promote stock from Reserved to Committed
                await PromoteReservedStockToCommittedAsync(tenantId, order);
                break;

            case IvrSpokenIntent.Cancelled:
                attempt.State = IvrCallState.Cancelled;
                order.Status = CodOrderStatus.Cancelled;
                order.Notes = (order.Notes ?? "") + $" [Cancelled via Voice AI Call #{attempt.AttemptNumber}: {speechText ?? dtmfDigit}]";

                // Synchronize Defensive WMS: Release reserved stock back to Physical/Available
                await ReleaseReservedStockAsync(tenantId, order);
                break;

            case IvrSpokenIntent.Postponed:
                attempt.State = IvrCallState.Postponed;
                order.Status = CodOrderStatus.Postponed;
                order.Notes = (order.Notes ?? "") + $" [Postponed via Voice AI Call #{attempt.AttemptNumber}]";
                break;

            default:
                // Unrecognized or No Answer
                if (attempt.AttemptNumber < 3)
                {
                    attempt.State = IvrCallState.NoAnswer;
                    attempt.ScheduledRetryUtc = DateTime.UtcNow.AddMinutes(60 * attempt.AttemptNumber);
                    order.Notes = (order.Notes ?? "") + $" [Voice Call #{attempt.AttemptNumber} unanswered. Retry at {attempt.ScheduledRetryUtc:u}]";
                }
                else
                {
                    attempt.State = IvrCallState.Failed;
                    order.Notes = (order.Notes ?? "") + $" [Voice Call max attempts reached ({attempt.AttemptNumber})]. Fallback to WhatsApp.";
                }
                break;
        }

        await _db.SaveChangesAsync();
        return attempt;
    }

    private async Task PromoteReservedStockToCommittedAsync(Guid tenantId, CodOrder order)
    {
        var inventories = await _db.WarehouseInventories.Where(w => w.TenantId == tenantId).ToListAsync();
        foreach (var inv in inventories)
        {
            if (inv.ReservedStock > 0)
            {
                inv.ReservedStock = Math.Max(0, inv.ReservedStock - 1);
                inv.CommittedStock += 1;
                inv.LastUpdatedUtc = DateTime.UtcNow;
            }
        }
    }

    private async Task ReleaseReservedStockAsync(Guid tenantId, CodOrder order)
    {
        var inventories = await _db.WarehouseInventories.Where(w => w.TenantId == tenantId).ToListAsync();
        foreach (var inv in inventories)
        {
            if (inv.ReservedStock > 0)
            {
                inv.ReservedStock = Math.Max(0, inv.ReservedStock - 1);
                inv.LastUpdatedUtc = DateTime.UtcNow;
            }
        }
    }
}
