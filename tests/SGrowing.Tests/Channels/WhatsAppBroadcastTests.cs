using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.Channels;

public class WhatsAppBroadcastTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public async Task DispatchBroadcast_WithApprovedTemplate_EnqueuesMessagesToOutbox()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new BroadcastDispatcherService(db, outbox);
        var tenantId = Guid.NewGuid();

        var template = WhatsAppTemplate.Create(
            tenantId,
            "black_friday_vip",
            WhatsAppTemplateCategory.Marketing,
            "Hello {{1}}, our Black Friday 50% discount is now live exclusively for VIP members!"
        );
        db.WhatsAppTemplates.Add(template);

        var c1 = Contact.Create(tenantId, "Peter", "Parker", "peter@dailybugle.com", "12125550100");
        c1.Tags.Add("vip");
        var c2 = Contact.Create(tenantId, "Miles", "Morales", "miles@brooklyn.edu", "12125550200");
        c2.Tags.Add("vip");
        var c3 = Contact.Create(tenantId, "Norman", "Osborn", "norman@oscorp.com", "12125550300"); // Not VIP
        db.Contacts.AddRange(c1, c2, c3);

        var campaign = BroadcastCampaign.Create(tenantId, "Black Friday VIP Campaign", template.Id, "vip");
        db.BroadcastCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        // Act
        var res = await service.DispatchBroadcastAsync(tenantId, campaign.Id);

        // Assert
        res.IsSuccess.Should().BeTrue();
        res.Value.RecipientsMatched.Should().Be(2);
        res.Value.SentCount.Should().Be(2);

        var updatedCampaign = await db.BroadcastCampaigns.FindAsync(campaign.Id);
        updatedCampaign!.Status.Should().Be(BroadcastStatus.Completed);

        var outboxMessages = await db.OutboxMessages.Where(m => m.EventType == "WhatsApp.Template.Send").ToListAsync();
        outboxMessages.Should().HaveCount(2);
        outboxMessages.Any(m => m.Payload.Contains("Peter")).Should().BeTrue();
        outboxMessages.Any(m => m.Payload.Contains("Miles")).Should().BeTrue();
    }

    [Fact]
    public async Task DispatchBroadcast_ReturnsFailure_WhenCampaignDoesNotExist()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new BroadcastDispatcherService(db, outbox);
        var tenantId = Guid.NewGuid();

        var res = await service.DispatchBroadcastAsync(tenantId, Guid.NewGuid());

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Be("Broadcast campaign not found.");
    }

    [Fact]
    public async Task DispatchBroadcast_ReturnsFailure_WhenTemplateDoesNotExist()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new BroadcastDispatcherService(db, outbox);
        var tenantId = Guid.NewGuid();

        var campaign = BroadcastCampaign.Create(tenantId, "Missing Template Campaign", Guid.NewGuid(), "target_tag");
        db.BroadcastCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var res = await service.DispatchBroadcastAsync(tenantId, campaign.Id);

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Be("Template not found.");
    }

    [Fact]
    public async Task DispatchBroadcast_ReturnsFailure_WhenTemplateIsNotApproved()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new BroadcastDispatcherService(db, outbox);
        var tenantId = Guid.NewGuid();

        var template = WhatsAppTemplate.Create(
            tenantId,
            "pending_template",
            WhatsAppTemplateCategory.Marketing,
            "Hello {{1}}"
        );
        template.Status = WhatsAppTemplateStatus.Pending; // Not Approved
        db.WhatsAppTemplates.Add(template);

        var campaign = BroadcastCampaign.Create(tenantId, "Unapproved Template Campaign", template.Id, "target_tag");
        db.BroadcastCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var res = await service.DispatchBroadcastAsync(tenantId, campaign.Id);

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Contain("Template is not approved by Meta");
    }

    [Fact]
    public async Task DispatchBroadcast_ExcludesContactsWithMissingOrEmptyPhoneNumber()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new BroadcastDispatcherService(db, outbox);
        var tenantId = Guid.NewGuid();

        var template = WhatsAppTemplate.Create(
            tenantId,
            "promo_template",
            WhatsAppTemplateCategory.Marketing,
            "Special promo for {{1}} {{2}}"
        );
        db.WhatsAppTemplates.Add(template);

        var contactWithPhone = Contact.Create(tenantId, "Valid", "Phone", "valid@phone.com", "+21698765432");
        contactWithPhone.Tags.Add("promo");

        var contactWithoutPhone = Contact.Create(tenantId, "No", "Phone", "no@phone.com", null);
        contactWithoutPhone.Tags.Add("promo");

        var contactEmptyPhone = Contact.Create(tenantId, "Empty", "Phone", "empty@phone.com", "");
        contactEmptyPhone.Tags.Add("promo");

        db.Contacts.AddRange(contactWithPhone, contactWithoutPhone, contactEmptyPhone);

        var campaign = BroadcastCampaign.Create(tenantId, "Promo Campaign", template.Id, "promo");
        db.BroadcastCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var res = await service.DispatchBroadcastAsync(tenantId, campaign.Id);

        res.IsSuccess.Should().BeTrue();
        res.Value.RecipientsMatched.Should().Be(1);
        res.Value.SentCount.Should().Be(1);
    }

    [Fact]
    public async Task DispatchBroadcast_ZeroMatchingContacts_ReturnsSuccessWithZeroSent()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new BroadcastDispatcherService(db, outbox);
        var tenantId = Guid.NewGuid();

        var template = WhatsAppTemplate.Create(
            tenantId,
            "zero_audience_template",
            WhatsAppTemplateCategory.Marketing,
            "Hello {{1}}"
        );
        db.WhatsAppTemplates.Add(template);

        var campaign = BroadcastCampaign.Create(tenantId, "Empty Audience Campaign", template.Id, "nonexistent_tag");
        db.BroadcastCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var res = await service.DispatchBroadcastAsync(tenantId, campaign.Id);

        res.IsSuccess.Should().BeTrue();
        res.Value.RecipientsMatched.Should().Be(0);
        res.Value.SentCount.Should().Be(0);

        var updated = await db.BroadcastCampaigns.FindAsync(campaign.Id);
        updated!.Status.Should().Be(BroadcastStatus.Completed);
        updated.SentCount.Should().Be(0);
    }

    [Fact]
    public async Task DispatchBroadcast_TenantIsolation_ExcludesContactsFromOtherTenants()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new BroadcastDispatcherService(db, outbox);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var templateA = WhatsAppTemplate.Create(tenantA, "template_a", WhatsAppTemplateCategory.Marketing, "Hello {{1}}");
        db.WhatsAppTemplates.Add(templateA);

        var contactA = Contact.Create(tenantA, "Contact", "A", "a@corp.com", "+12025550101");
        contactA.Tags.Add("target");

        // Contact B has same tag but belongs to Tenant B
        var contactB = Contact.Create(tenantB, "Contact", "B", "b@corp.com", "+12025550102");
        contactB.Tags.Add("target");

        db.Contacts.AddRange(contactA, contactB);

        var campaignA = BroadcastCampaign.Create(tenantA, "Campaign A", templateA.Id, "target");
        db.BroadcastCampaigns.Add(campaignA);
        await db.SaveChangesAsync();

        var res = await service.DispatchBroadcastAsync(tenantA, campaignA.Id);

        res.IsSuccess.Should().BeTrue();
        res.Value.RecipientsMatched.Should().Be(1);
        res.Value.SentCount.Should().Be(1);

        var outboxMessages = await db.OutboxMessages.Where(m => m.TenantId == tenantA).ToListAsync();
        outboxMessages.Should().ContainSingle();
        outboxMessages[0].Payload.Should().Contain("12025550101");
        outboxMessages[0].Payload.Should().NotContain("12025550102");
    }

    [Fact]
    public async Task DispatchBroadcast_RendersFirstAndLastNamesInTemplate()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new BroadcastDispatcherService(db, outbox);
        var tenantId = Guid.NewGuid();

        var template = WhatsAppTemplate.Create(
            tenantId,
            "personalized_greeting",
            WhatsAppTemplateCategory.Utility,
            "Dear {{1}} {{2}}, your invoice is ready."
        );
        db.WhatsAppTemplates.Add(template);

        var contact = Contact.Create(tenantId, "Tony", "Stark", "tony@stark.com", "+12025550199");
        contact.Tags.Add("invoicing");
        db.Contacts.Add(contact);

        var campaign = BroadcastCampaign.Create(tenantId, "Billing Notice", template.Id, "invoicing");
        db.BroadcastCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var res = await service.DispatchBroadcastAsync(tenantId, campaign.Id);

        res.IsSuccess.Should().BeTrue();
        var msg = await db.OutboxMessages.FirstOrDefaultAsync(m => m.TenantId == tenantId);
        msg.Should().NotBeNull();
        msg!.Payload.Should().Contain("Dear Tony Stark, your invoice is ready.");
    }
}
