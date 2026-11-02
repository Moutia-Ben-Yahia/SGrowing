using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Channels.WhatsAppBroadcast;

public interface IWhatsAppBroadcastEngine
{
    BroadcastCampaign CreateCampaign(CreateCampaignRequest request);
    BroadcastCampaign? GetCampaign(string campaignId);
    List<BroadcastCampaign> ListCampaigns(string tenantId);
    BroadcastCampaign TriggerBroadcast(string campaignId);
    List<WhatsAppTemplate> ListTemplates(string tenantId);
    WhatsAppTemplate RegisterOrUpdateTemplate(WhatsAppTemplate template);
}

public class WhatsAppBroadcastEngine : IWhatsAppBroadcastEngine
{
    private readonly ConcurrentDictionary<string, BroadcastCampaign> _campaigns = new();
    private readonly ConcurrentDictionary<string, WhatsAppTemplate> _templates = new();

    public WhatsAppBroadcastEngine()
    {
        SeedDefaultTemplates();
    }

    private void SeedDefaultTemplates()
    {
        var t1 = new WhatsAppTemplate(
            TemplateId: "tmpl_q4_flash_offer",
            Name: "q4_enterprise_flash_discount",
            Category: "MARKETING",
            Language: "en_US",
            HeaderText: "⚡ Exclusive Growth OS Enterprise Upgrade",
            BodyText: "Hi {{1}}, upgrade your team's autonomous growth fleet before Friday to unlock 25% ARR credit and dedicated SDR agents.",
            Buttons: new List<string> { "Claim Offer", "Book Demo", "Unsubscribe" },
            Status: WhatsAppTemplateStatus.Approved
        );

        var t2 = new WhatsAppTemplate(
            TemplateId: "tmpl_order_delivery_otp",
            Name: "order_dispatch_verification",
            Category: "UTILITY",
            Language: "en_US",
            HeaderText: "📦 Your SGrowing Shipment is In Transit",
            BodyText: "Hello {{1}}, order #{{2}} has been dispatched. Track your delivery or confirm receiving PIN {{3}}.",
            Buttons: new List<string> { "Track Package", "Contact Courier" },
            Status: WhatsAppTemplateStatus.Approved
        );

        _templates[t1.TemplateId] = t1;
        _templates[t2.TemplateId] = t2;
    }

    public BroadcastCampaign CreateCampaign(CreateCampaignRequest request)
    {
        var campaignId = "wabc_" + Guid.NewGuid().ToString("N")[..8];
        var total = request.PhoneNumbers?.Count ?? 0;

        var campaign = new BroadcastCampaign(
            CampaignId: campaignId,
            TenantId: request.TenantId,
            Name: request.Name,
            TemplateId: request.TemplateId,
            TotalRecipients: total,
            SentCount: 0,
            DeliveredCount: 0,
            ReadCount: 0,
            FailedCount: 0,
            Status: BroadcastCampaignStatus.Draft,
            CreatedAtUtc: DateTime.UtcNow,
            StartedAtUtc: null,
            CompletedAtUtc: null
        );

        _campaigns[campaignId] = campaign;
        return campaign;
    }

    public BroadcastCampaign? GetCampaign(string campaignId)
    {
        _campaigns.TryGetValue(campaignId, out var campaign);
        return campaign;
    }

    public List<BroadcastCampaign> ListCampaigns(string tenantId)
    {
        return _campaigns.Values
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToList();
    }

    public BroadcastCampaign TriggerBroadcast(string campaignId)
    {
        if (!_campaigns.TryGetValue(campaignId, out var campaign))
        {
            throw new KeyNotFoundException($"WhatsApp Broadcast Campaign '{campaignId}' not found.");
        }

        var sent = campaign.TotalRecipients;
        var delivered = (int)Math.Round(sent * 0.98);
        var read = (int)Math.Round(delivered * 0.82);
        var failed = sent - delivered;

        var completed = campaign with
        {
            SentCount = sent,
            DeliveredCount = delivered,
            ReadCount = read,
            FailedCount = failed,
            Status = BroadcastCampaignStatus.Completed,
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow
        };

        _campaigns[campaignId] = completed;
        return completed;
    }

    public List<WhatsAppTemplate> ListTemplates(string tenantId)
    {
        return _templates.Values.OrderBy(t => t.Name).ToList();
    }

    public WhatsAppTemplate RegisterOrUpdateTemplate(WhatsAppTemplate template)
    {
        _templates[template.TemplateId] = template;
        return template;
    }
}
