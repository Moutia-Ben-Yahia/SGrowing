using SGrowing.Api.Modules.Channels.WhatsAppBroadcast;

namespace SGrowing.Tests.Channels;

public class WhatsAppHighVolumeBroadcastMeshTests
{
    private readonly WhatsAppBroadcastEngine _engine = new();

    [Fact]
    public void ListTemplates_ReturnsSeededApprovedTemplates()
    {
        var templates = _engine.ListTemplates("tenant_wa_test");

        Assert.NotEmpty(templates);
        Assert.Contains(templates, t => t.TemplateId == "tmpl_q4_flash_offer");
        Assert.All(templates, t => Assert.Equal(WhatsAppTemplateStatus.Approved, t.Status));
    }

    [Fact]
    public void CreateCampaign_InitializesWithDraftStatus()
    {
        var req = new CreateCampaignRequest(
            TenantId: "tenant_wa_test",
            Name: "Black Friday VIP Early Access",
            TemplateId: "tmpl_q4_flash_offer",
            PhoneNumbers: new List<string> { "+15550101010", "+15550102020", "+15550103030" }
        );

        var campaign = _engine.CreateCampaign(req);

        Assert.NotNull(campaign);
        Assert.StartsWith("wabc_", campaign.CampaignId);
        Assert.Equal(3, campaign.TotalRecipients);
        Assert.Equal(BroadcastCampaignStatus.Draft, campaign.Status);
    }

    [Fact]
    public void TriggerBroadcast_ExecutesHighDeliveryRate()
    {
        var req = new CreateCampaignRequest(
            TenantId: "tenant_wa_test",
            Name: "Product Launch Alert",
            TemplateId: "tmpl_q4_flash_offer",
            PhoneNumbers: Enumerable.Range(1, 100).Select(i => $"+1555000{i:D4}").ToList()
        );

        var campaign = _engine.CreateCampaign(req);
        var completed = _engine.TriggerBroadcast(campaign.CampaignId);

        Assert.Equal(BroadcastCampaignStatus.Completed, completed.Status);
        Assert.Equal(100, completed.SentCount);
        Assert.True(completed.DeliveredCount >= 95);
        Assert.True(completed.ReadCount >= 70);
    }

    [Fact]
    public void TriggerBroadcast_WhenCampaignMissing_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() => _engine.TriggerBroadcast("non_existent_wabc"));
    }
}
