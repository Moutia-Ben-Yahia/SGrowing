using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.CRM;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Channels;

public class DeliveryWebhookTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"DeliveryWhDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task HandleEmailDeliveryWebhook_Should_Record_Timeline_Events_For_Resend_Payload()
    {
        using var db = CreateDbContext();
        var c360 = new Customer360AttributionService(db);
        var tenantId = Guid.NewGuid();

        // Seed a contact with email
        var contact = Contact.Create(tenantId, "Yasmine", "Driss", email: "yasmine@atlas.tn");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var resendPayload = new
        {
            type = "email.delivered",
            created_at = "2026-11-03T10:00:00Z",
            data = new
            {
                email_id = "re_1234567890",
                to = new[] { "yasmine@atlas.tn" },
                subject = "Your Invoice #1042 is Ready",
                from = "billing@sgrowing.io"
            }
        };

        var httpContext = new DefaultHttpContext();
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resendPayload));
        httpContext.Request.Body = new MemoryStream(jsonBytes);
        httpContext.Request.ContentType = "application/json";

        var result = await DeliveryWebhookEndpoints.HandleEmailDeliveryWebhook(
            httpContext.Request,
            tenantId,
            null,
            c360,
            db,
            CancellationToken.None
        );

        Assert.NotNull(result);

        // Verify timeline event was added
        var events = await db.CustomerTimelineEvents
            .Where(e => e.TenantId == tenantId && e.EventType == "EmailDeliveryEvent")
            .ToListAsync();

        Assert.Single(events);
        Assert.Contains("email.delivered", events[0].Summary);
        Assert.Contains("Your Invoice #1042", events[0].Summary);
    }

    [Fact]
    public async Task HandleTwilioSmsStatusWebhook_Should_Update_Message_DeliveryStatus_And_Timeline()
    {
        using var db = CreateDbContext();
        var c360 = new Customer360AttributionService(db);
        var tenantId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        // Seed contact and message with external SID
        var contact = Contact.Create(tenantId, "Amine", "Mansour", phone: "+21620112233");
        db.Contacts.Add(contact);

        var msg = Message.Create(
            tenantId: tenantId,
            conversationId: convId,
            senderType: MessageSenderType.Agent,
            content: "Your order has been shipped!",
            externalMessageId: "SM9876543210",
            deliveryStatus: DeliveryStatus.Sent
        );
        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        var twilioPayload = new
        {
            MessageSid = "SM9876543210",
            MessageStatus = "delivered",
            To = "+21620112233",
            From = "+18005550199"
        };

        var httpContext = new DefaultHttpContext();
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(twilioPayload));
        httpContext.Request.Body = new MemoryStream(jsonBytes);
        httpContext.Request.ContentType = "application/json";

        var result = await DeliveryWebhookEndpoints.HandleTwilioSmsStatusWebhook(
            httpContext.Request,
            tenantId,
            null,
            c360,
            db,
            CancellationToken.None
        );

        Assert.NotNull(result);

        // Verify message status was updated to Delivered
        var updatedMsg = await db.Messages.FindAsync(msg.Id);
        Assert.NotNull(updatedMsg);
        Assert.Equal(DeliveryStatus.Delivered, updatedMsg.DeliveryStatus);

        // Verify timeline event
        var events = await db.CustomerTimelineEvents
            .Where(e => e.TenantId == tenantId && e.EventType == "SmsDeliveryEvent")
            .ToListAsync();

        Assert.Single(events);
        Assert.Contains("delivered", events[0].Summary);
    }

    [Fact]
    public async Task HandleWhatsAppDeliveryWebhook_Should_Update_Message_Status_And_Timeline()
    {
        using var db = CreateDbContext();
        var c360 = new Customer360AttributionService(db);
        var tenantId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var msg = Message.Create(
            tenantId: tenantId,
            conversationId: convId,
            senderType: MessageSenderType.Agent,
            content: "Exclusive VIP offer just for you",
            externalMessageId: "wamid.HBgLMjE2OTAwMDAw",
            deliveryStatus: DeliveryStatus.Sent
        );
        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        var waPayload = new
        {
            id = "wamid.HBgLMjE2OTAwMDAw",
            status = "read",
            recipient_id = "+21699887766"
        };

        var httpContext = new DefaultHttpContext();
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(waPayload));
        httpContext.Request.Body = new MemoryStream(jsonBytes);
        httpContext.Request.ContentType = "application/json";

        var result = await DeliveryWebhookEndpoints.HandleWhatsAppDeliveryWebhook(
            httpContext.Request,
            tenantId,
            null,
            c360,
            db,
            CancellationToken.None
        );

        Assert.NotNull(result);

        // Verify message status was updated to Read
        var updatedMsg = await db.Messages.FindAsync(msg.Id);
        Assert.NotNull(updatedMsg);
        Assert.Equal(DeliveryStatus.Read, updatedMsg.DeliveryStatus);

        // Verify timeline event
        var events = await db.CustomerTimelineEvents
            .Where(e => e.TenantId == tenantId && e.EventType == "WhatsAppDeliveryEvent")
            .ToListAsync();

        Assert.Single(events);
        Assert.Contains("read", events[0].Summary);
    }
}
