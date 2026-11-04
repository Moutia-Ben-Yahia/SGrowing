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

    [Fact]
    public void GetCampaign_ExistingAndMissing_ReturnsCampaignOrNull()
    {
        var req = new CreateCampaignRequest("tenant_get", "Sample Campaign", "tmpl_q4_flash_offer", new List<string> { "+12345" });
        var created = _engine.CreateCampaign(req);

        var retrieved = _engine.GetCampaign(created.CampaignId);
        Assert.NotNull(retrieved);
        Assert.Equal("Sample Campaign", retrieved.Name);

        var missing = _engine.GetCampaign("non_existent_camp");
        Assert.Null(missing);
    }

    [Fact]
    public void ListCampaigns_FiltersByTenantIdAndOrdersDescending()
    {
        var tenantA = "tenant_list_a";
        var tenantB = "tenant_list_b";

        _engine.CreateCampaign(new CreateCampaignRequest(tenantA, "Camp A1", "tmpl_1", new List<string>()));
        _engine.CreateCampaign(new CreateCampaignRequest(tenantA, "Camp A2", "tmpl_1", new List<string>()));
        _engine.CreateCampaign(new CreateCampaignRequest(tenantB, "Camp B1", "tmpl_1", new List<string>()));

        var listA = _engine.ListCampaigns(tenantA);
        var listB = _engine.ListCampaigns(tenantB);

        Assert.Equal(2, listA.Count);
        Assert.All(listA, c => Assert.Equal(tenantA, c.TenantId));
        Assert.Single(listB);
        Assert.Equal("Camp B1", listB[0].Name);
    }

    [Fact]
    public void RegisterOrUpdateTemplate_RegistersCustomTemplate()
    {
        var template = new WhatsAppTemplate(
            TemplateId: "tmpl_custom_survey",
            Name: "nps_post_delivery_survey",
            Category: "FEEDBACK",
            Language: "fr_FR",
            HeaderText: "Votre avis nous intéresse",
            BodyText: "Évaluez votre livraison SGrowing de 1 à 10.",
            Buttons: new List<string> { "10 - Excellent", "Autre" },
            Status: WhatsAppTemplateStatus.Pending
        );

        var saved = _engine.RegisterOrUpdateTemplate(template);
        Assert.NotNull(saved);
        Assert.Equal("tmpl_custom_survey", saved.TemplateId);

        var templates = _engine.ListTemplates("tenant_any");
        Assert.Contains(templates, t => t.TemplateId == "tmpl_custom_survey");
    }

    [Fact]
    public void CreateCampaign_NullPhoneNumbers_DefaultsTotalToZero()
    {
        var req = new CreateCampaignRequest(
            TenantId: "tenant_null_test",
            Name: "Zero Recipient Campaign",
            TemplateId: "tmpl_q4_flash_offer",
            PhoneNumbers: null!
        );

        var campaign = _engine.CreateCampaign(req);
        Assert.NotNull(campaign);
        Assert.Equal(0, campaign.TotalRecipients);
    }
}
