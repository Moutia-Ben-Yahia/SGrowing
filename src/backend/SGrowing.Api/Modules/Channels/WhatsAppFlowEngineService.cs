using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels;

public interface IWhatsAppFlowEngineService
{
    Task<FlowDefinition> CreateFlowDefinitionAsync(Guid tenantId, FlowDefinition flow);
    (string Language, decimal SentimentScore) AnalyzeMessageText(string text);
    Task<FlowTurnResult> ExecuteFlowTurnAsync(
        Guid tenantId,
        Guid flowId,
        string customerPhone,
        string incomingMessage);
    Task<List<FlowDefinition>> ListFlowsAsync(Guid tenantId);
}

public class WhatsAppFlowEngineService : IWhatsAppFlowEngineService
{
    private readonly SGrowingDbContext _db;

    public WhatsAppFlowEngineService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<FlowDefinition> CreateFlowDefinitionAsync(Guid tenantId, FlowDefinition flow)
    {
        flow.TenantId = tenantId;
        _db.FlowDefinitions.Add(flow);
        await _db.SaveChangesAsync();
        return flow;
    }

    public (string Language, decimal SentimentScore) AnalyzeMessageText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return ("Darija", 0.5m);

        var lower = text.ToLowerInvariant();

        // 1. Sentiment analysis
        decimal sentiment = 0.5m;
        string[] negativeWords = { "arnaque", "voleur", "scam", "sarra9a", "retard", "nul", "furious", "hate", "ghachitou" };
        string[] positiveWords = { "merci", "top", "ya3tikom saha", "parfait", "excellent", "behi barcha", "chokran" };

        if (negativeWords.Any(w => lower.Contains(w)))
        {
            sentiment = 0.15m;
        }
        else if (positiveWords.Any(w => lower.Contains(w)))
        {
            sentiment = 0.9m;
        }

        // 2. Language detection
        string language = "Darija";
        if (Regex.IsMatch(text, @"\p{IsArabic}"))
        {
            language = "Arabic";
        }
        else if (lower.Contains("bonjour") || lower.Contains("merci") || lower.Contains("commande") || lower.Contains("livraison"))
        {
            language = "French";
        }
        else if (lower.Contains("hello") || lower.Contains("order") || lower.Contains("track") || lower.Contains("support"))
        {
            language = "English";
        }
        else if (lower.Contains("3aslema") || lower.Contains("behi") || lower.Contains("chnawa") || lower.Contains("nheb") || lower.Contains("win"))
        {
            language = "Darija";
        }

        return (language, sentiment);
    }

    public async Task<FlowTurnResult> ExecuteFlowTurnAsync(
        Guid tenantId,
        Guid flowId,
        string customerPhone,
        string incomingMessage)
    {
        var cleanPhone = customerPhone.Trim();
        var (language, sentiment) = AnalyzeMessageText(incomingMessage);

        var session = await _db.FlowExecutionSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.FlowDefinitionId == flowId && s.CustomerPhone == cleanPhone);

        if (session == null)
        {
            session = new FlowExecutionSession
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FlowDefinitionId = flowId,
                CustomerPhone = cleanPhone,
                CurrentNodeKey = "start",
                DetectedLanguage = language,
                SentimentScore = sentiment,
                StartedAtUtc = DateTime.UtcNow,
                LastInteractionAtUtc = DateTime.UtcNow
            };
            _db.FlowExecutionSessions.Add(session);
        }
        else
        {
            session.DetectedLanguage = language;
            session.SentimentScore = sentiment;
            session.LastInteractionAtUtc = DateTime.UtcNow;
        }

        // Sentiment Guard: Auto-escalate angry / frustrated customer immediately
        if (sentiment < 0.25m)
        {
            session.IsEscalatedToHuman = true;
            session.CurrentNodeKey = "human_queue";
            await _db.SaveChangesAsync();

            string escalationMsg = language == "French"
                ? "Nous sommes désolés pour ce désagrément. Un responsable du service client prend immédiatement en charge votre dossier."
                : "سامحنا على القلق. قمنا بتحويل محادثتك فوراً إلى مسؤول خدمة الحرفاء للمتابعة المباشرة.";

            return new FlowTurnResult(
                "human_queue",
                escalationMsg,
                new List<string> { "Parler à un agent" },
                IsEscalated: true,
                language,
                ExecutedAction: "EscalatedToHumanAgent"
            );
        }

        var lowerInput = incomingMessage.Trim().ToLowerInvariant();

        // State Machine Step Evaluation
        if (session.CurrentNodeKey == "start" || lowerInput == "start" || lowerInput == "menu" || lowerInput == "3aslema" || lowerInput == "bonjour")
        {
            session.CurrentNodeKey = "main_menu";
            await _db.SaveChangesAsync();

            string greeting = language == "French"
                ? "Bienvenue chez SGrowing ! Comment pouvons-nous vous aider aujourd'hui ?"
                : "عسلامة ومرحبا بيك في SGrowing ! كيفاش نجمو نعاونوك اليوم ؟";

            var buttons = language == "French"
                ? new List<string> { "1. Confirmer commande", "2. Suivre mon colis", "3. Échange de taille", "4. Parler à un agent" }
                : new List<string> { "1. تأكيد الطلب", "2. تتبع الطرد", "3. تبديل المقاس", "4. التحدث مع عون" };

            return new FlowTurnResult("main_menu", greeting, buttons, false, language);
        }

        if (lowerInput.Contains("confirmer") || lowerInput.Contains("تأكيد") || lowerInput.Contains("1"))
        {
            session.CurrentNodeKey = "order_confirmed";
            // Look up latest pending order and confirm it
            var order = await _db.CodOrders
                .Where(o => o.TenantId == tenantId && o.PhoneNumber == cleanPhone && o.Status == CodOrderStatus.PendingConfirmation)
                .OrderByDescending(o => o.CreatedAtUtc)
                .FirstOrDefaultAsync();

            string action = "NoPendingOrderFound";
            string responseText = "Aucune commande en attente trouvée.";

            if (order != null)
            {
                order.Status = CodOrderStatus.Confirmed;
                order.ConfirmedAtUtc = DateTime.UtcNow;
                action = "OrderConfirmedInWms";

                responseText = language == "French"
                    ? $"Parfait ! Votre commande {order.OrderNumber} ({order.TotalAmount} TND) est confirmée et préparée pour expédition rapide."
                    : $"يعطيك الصحة ! تم تأكيد طلبيتك {order.OrderNumber} بمبلغ {order.TotalAmount} د.ت وجاري تجهيزها للتسليم السريع.";
            }

            await _db.SaveChangesAsync();
            return new FlowTurnResult("order_confirmed", responseText, new List<string> { "Suivre colis", "Menu" }, false, language, action);
        }

        if (lowerInput.Contains("suivre") || lowerInput.Contains("تتبع") || lowerInput.Contains("2"))
        {
            session.CurrentNodeKey = "tracking_info";
            var order = await _db.CodOrders
                .Where(o => o.TenantId == tenantId && o.PhoneNumber == cleanPhone)
                .OrderByDescending(o => o.CreatedAtUtc)
                .FirstOrDefaultAsync();

            string responseText = order != null
                ? (language == "French"
                    ? $"Votre colis {order.OrderNumber} est actuellement en statut: {order.Status}."
                    : $"طردك رقم {order.OrderNumber} حالياً في وضع: {order.Status}.")
                : "Aucune commande associée à ce numéro.";

            await _db.SaveChangesAsync();
            return new FlowTurnResult("tracking_info", responseText, new List<string> { "Menu" }, false, language);
        }

        if (lowerInput.Contains("échange") || lowerInput.Contains("تبديل") || lowerInput.Contains("3"))
        {
            session.CurrentNodeKey = "exchange_flow";
            await _db.SaveChangesAsync();

            string responseText = language == "French"
                ? "Pour un échange direct à domicile, veuillez nous indiquer votre nouvelle taille souhaitée."
                : "لتغيير المقاس مجاناً عند التسليم، الرجاء إرسال المقاس الجديد المطلوب.";

            return new FlowTurnResult("exchange_flow", responseText, new List<string> { "Taille 42", "Taille 43", "Taille 44" }, false, language, "DropAndSwapInitiated");
        }

        // Default fallback
        session.CurrentNodeKey = "main_menu";
        await _db.SaveChangesAsync();
        return new FlowTurnResult("main_menu", "Veuillez choisir une option dans le menu ci-dessous.", new List<string> { "Confirmer", "Suivre colis", "Menu" }, false, language);
    }

    public async Task<List<FlowDefinition>> ListFlowsAsync(Guid tenantId)
    {
        return await _db.FlowDefinitions
            .Where(f => f.TenantId == tenantId && f.IsActive)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }
}
