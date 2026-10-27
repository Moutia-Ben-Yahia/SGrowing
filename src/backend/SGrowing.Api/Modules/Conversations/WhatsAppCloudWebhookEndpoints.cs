using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Security;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations;

public static class WhatsAppCloudWebhookEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppCloudWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations/webhooks/whatsapp")
            .WithTags("WhatsApp Cloud Webhook Ingress & OmniHub Normalization");

        // Challenge verification for Meta Developer Portal Webhook subscription
        group.MapGet("/", async (
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.challenge")] string? challenge,
            [FromQuery(Name = "hub.verify_token")] string? verifyToken,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ITenantSecretsVaultService vaultService,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

            if (!string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "Invalid hub mode" });
            }

            if (string.IsNullOrEmpty(challenge))
            {
                return Results.BadRequest(new { error = "Missing hub.challenge" });
            }

            var expectedVerifyToken = await vaultService.GetSecretAsync(effectiveTenantId, "WhatsAppVerifyToken", ct);
            if (string.IsNullOrEmpty(expectedVerifyToken))
            {
                var cred = await vaultService.GetCredentialAsync(effectiveTenantId, "WhatsAppCloud", ct);
                expectedVerifyToken = cred?.AppSecret;
            }
            if (string.IsNullOrEmpty(expectedVerifyToken))
            {
                expectedVerifyToken = config["WhatsApp:VerifyToken"] ?? config["WhatsApp:AppSecret"] ?? "sgrowing_super_secret_wa_key";
            }

            if (string.IsNullOrEmpty(verifyToken) || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(verifyToken),
                    Encoding.UTF8.GetBytes(expectedVerifyToken)))
            {
                return Results.Forbid();
            }

            return Results.Content(challenge, "text/plain");
        });

        // Inbound message receiver with HMAC-SHA256 signature verification & OmniHub normalization
        group.MapPost("/", async (
            HttpRequest request,
            [FromHeader(Name = "X-Hub-Signature-256")] string? signatureHeader,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ITenantSecretsVaultService vaultService,
            IHubContext<OmniHub, IOmniClient> hubContext,
            AutonomousAiAgentService agentService,
            SGrowingDbContext db,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);

            // Fetch app secret for HMAC-SHA256 verification
            var secret = await vaultService.GetSecretAsync(effectiveTenantId, "WhatsAppAppSecret", ct);
            if (string.IsNullOrEmpty(secret))
            {
                var cred = await vaultService.GetCredentialAsync(effectiveTenantId, "WhatsAppCloud", ct);
                secret = cred?.AppSecret;
            }
            if (string.IsNullOrEmpty(secret))
            {
                secret = config["WhatsApp:AppSecret"] ?? "sgrowing_super_secret_wa_key";
            }

            // Verify signature if provided or required
            if (!string.IsNullOrEmpty(signatureHeader))
            {
                var isValid = WebhookSignatureValidator.VerifyMetaSignature(rawBody, secret, signatureHeader);
                if (!isValid)
                {
                    return Results.Unauthorized();
                }
            }

            // Parse Meta WhatsApp Webhook payload
            try
            {
                using var jsonDoc = JsonDocument.Parse(rawBody);
                var root = jsonDoc.RootElement;

                string senderPhone = "";
                string senderName = "WhatsApp User";
                string messageText = "";
                string externalMsgId = Guid.NewGuid().ToString();

                if (root.TryGetProperty("entry", out var entryArray) && entryArray.GetArrayLength() > 0)
                {
                    var entry = entryArray[0];
                    if (entry.TryGetProperty("changes", out var changesArray) && changesArray.GetArrayLength() > 0)
                    {
                        var changeVal = changesArray[0].GetProperty("value");

                        // Extract Contact metadata
                        if (changeVal.TryGetProperty("contacts", out var contactsArray) && contactsArray.GetArrayLength() > 0)
                        {
                            var c = contactsArray[0];
                            if (c.TryGetProperty("wa_id", out var waId)) senderPhone = waId.GetString() ?? "";
                            if (c.TryGetProperty("profile", out var prof) && prof.TryGetProperty("name", out var pName))
                            {
                                senderName = pName.GetString() ?? "WhatsApp User";
                            }
                        }

                        // Extract Message
                        if (changeVal.TryGetProperty("messages", out var messagesArray) && messagesArray.GetArrayLength() > 0)
                        {
                            var msg = messagesArray[0];
                            if (msg.TryGetProperty("id", out var mId)) externalMsgId = mId.GetString() ?? externalMsgId;
                            if (string.IsNullOrEmpty(senderPhone) && msg.TryGetProperty("from", out var mFrom))
                            {
                                senderPhone = mFrom.GetString() ?? "";
                            }

                            if (msg.TryGetProperty("text", out var textObj) && textObj.TryGetProperty("body", out var body))
                            {
                                messageText = body.GetString() ?? "";
                            }
                            else if (msg.TryGetProperty("type", out var typeProp))
                            {
                                messageText = $"[{typeProp.GetString()} attachment]";
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(senderPhone))
                {
                    senderPhone = "+21690000000";
                }

                if (string.IsNullOrEmpty(messageText))
                {
                    messageText = "Hello";
                }

                // Locate or register Contact in DB
                var contact = await db.Contacts.FirstOrDefaultAsync(c => c.TenantId == effectiveTenantId && (c.Phone == senderPhone || (c.Phone != null && c.Phone.Contains(senderPhone))), ct);
                if (contact == null)
                {
                    var nameParts = senderName.Split(' ', 2);
                    contact = Contact.Create(
                        tenantId: effectiveTenantId,
                        firstName: nameParts[0],
                        lastName: nameParts.Length > 1 ? nameParts[1] : "",
                        phone: senderPhone);
                    db.Contacts.Add(contact);
                    await db.SaveChangesAsync(ct);
                }

                // Locate or create WhatsApp Inbox
                var inbox = await db.Inboxes.FirstOrDefaultAsync(i => i.TenantId == effectiveTenantId && i.ChannelType == ChannelType.WhatsAppCloud, ct);
                if (inbox == null)
                {
                    inbox = new Inbox
                    {
                        Id = Guid.NewGuid(),
                        TenantId = effectiveTenantId,
                        Name = "WhatsApp Cloud",
                        ChannelType = ChannelType.WhatsAppCloud,
                        ConfigJson = "{}",
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    db.Inboxes.Add(inbox);
                    await db.SaveChangesAsync(ct);
                }

                // Locate or create Conversation
                var conversation = await db.Conversations.FirstOrDefaultAsync(cv => cv.TenantId == effectiveTenantId && cv.ContactId == contact.Id && cv.InboxId == inbox.Id, ct);
                if (conversation == null)
                {
                    conversation = Conversation.Create(effectiveTenantId, inbox.Id, contact.Id);
                    db.Conversations.Add(conversation);
                    await db.SaveChangesAsync(ct);
                }

                // Record inbound Message
                var message = Message.Create(
                    tenantId: effectiveTenantId,
                    conversationId: conversation.Id,
                    senderType: MessageSenderType.Contact,
                    content: messageText,
                    senderId: contact.Id,
                    contentType: MessageContentType.Text,
                    mediaUrl: null,
                    externalMessageId: externalMsgId,
                    deliveryStatus: DeliveryStatus.Delivered);

                db.Messages.Add(message);
                conversation.LastMessageAtUtc = DateTime.UtcNow;
                conversation.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                // Broadcast into OmniHub SignalR for realtime agent desktops
                var messageDto = new MessageDto(
                    message.Id,
                    conversation.Id,
                    message.SenderType,
                    message.SenderId,
                    message.ContentType,
                    message.Content,
                    message.MediaUrl,
                    message.DeliveryStatus,
                    message.CreatedAtUtc);

                await hubContext.Clients.Group(OmniHub.GetConversationGroup(conversation.Id)).OnMessageReceived(messageDto);
                await hubContext.Clients.Group(OmniHub.GetTenantGroup(effectiveTenantId)).OnMessageReceived(messageDto);

                // Bot Execution Routing
                var botResult = await agentService.HandleIncomingMessageAsync(effectiveTenantId, conversation.Id, contact.Id, messageText);
                if (botResult.IsSuccess && botResult.Value != null)
                {
                    var draftDto = new AiDraftDto(
                        Guid.NewGuid(),
                        conversation.Id,
                        botResult.Value.ReplySent ?? "Thank you for reaching out, an agent will assist you shortly.",
                        0.95f,
                        botResult.Value.EscalatedToHuman ? "Escalated" : "Processed",
                        DateTime.UtcNow);

                    await hubContext.Clients.Group(OmniHub.GetConversationGroup(conversation.Id)).OnAiDraftGenerated(draftDto);
                }

                return Results.Ok(new
                {
                    status = "NormalizedAndRouted",
                    messageId = message.Id,
                    conversationId = conversation.Id,
                    contactId = contact.Id,
                    botRouted = true,
                    botProcessed = botResult.IsSuccess && botResult.Value?.Processed == true,
                    botAction = botResult.IsSuccess ? (botResult.Value?.EscalatedToHuman == true ? "EscalatedToHuman" : "AutoReplied") : null
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Failed to parse and route WhatsApp webhook: {ex.Message}" });
            }
        });

        return app;
    }
}
