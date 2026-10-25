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
        tenantContext.SetTenant(Guid.NewGuid());
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
}
