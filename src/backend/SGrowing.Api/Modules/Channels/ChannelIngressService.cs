using SGrowing.Api.Data;
using SGrowing.Core.Channels;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.Channels;

public class ChannelIngressService
{
    private readonly SGrowingDbContext _db;
    private readonly IOutbox _outbox;

    public ChannelIngressService(SGrowingDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<Result<Message>> IngestAsync(Guid inboxId, InboundChannelMessage message, CancellationToken cancellationToken = default)
    {
        // 1. Resolve or Create Contact
        Contact? contact = null;
        if (message.ChannelType.StartsWith("whatsapp"))
        {
            contact = await _db.Contacts.FirstOrDefaultAsync(
                c => c.TenantId == message.TenantId && c.Phone == message.ExternalSenderId, cancellationToken);

            if (contact == null)
            {
                var nameParts = (message.SenderName ?? "WhatsApp User").Split(' ', 2);
                contact = Contact.Create(
                    tenantId: message.TenantId,
                    firstName: nameParts[0],
                    lastName: nameParts.Length > 1 ? nameParts[1] : "",
                    phone: message.ExternalSenderId
                );
                _db.Contacts.Add(contact);
            }
        }
        else if (message.ChannelType == "email")
        {
            contact = await _db.Contacts.FirstOrDefaultAsync(
                c => c.TenantId == message.TenantId && c.Email == message.ExternalSenderId, cancellationToken);

            if (contact == null)
            {
                var nameParts = (message.SenderName ?? "Email Contact").Split(' ', 2);
                contact = Contact.Create(
                    tenantId: message.TenantId,
                    firstName: nameParts[0],
                    lastName: nameParts.Length > 1 ? nameParts[1] : "",
                    email: message.ExternalSenderId
                );
                _db.Contacts.Add(contact);
            }
        }

        if (contact == null)
        {
            contact = Contact.Create(
                tenantId: message.TenantId,
                firstName: message.SenderName ?? "Web Visitor",
                lastName: ""
            );
            _db.Contacts.Add(contact);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // 2. Resolve or Create Conversation
        var conversation = await _db.Conversations.FirstOrDefaultAsync(
            c => c.TenantId == message.TenantId && c.InboxId == inboxId && c.ContactId == contact.Id && c.Status != ConversationStatus.Resolved,
            cancellationToken);

        if (conversation == null)
        {
            conversation = Conversation.Create(message.TenantId, inboxId, contact.Id);
            _db.Conversations.Add(conversation);
        }

        conversation.LastMessageAtUtc = DateTime.UtcNow;
        conversation.UpdatedAtUtc = DateTime.UtcNow;

        // 3. Add Message
        var msg = Message.Create(
            tenantId: message.TenantId,
            conversationId: conversation.Id,
            senderType: MessageSenderType.Contact,
            content: message.Content,
            externalMessageId: message.ExternalMessageId,
            deliveryStatus: DeliveryStatus.Delivered
        );

        _db.Messages.Add(msg);
        await _db.SaveChangesAsync(cancellationToken);

        // 4. Enqueue Outbox Event
        await _outbox.EnqueueAsync(message.TenantId, "InboundMessageReceived", new
        {
            MessageId = msg.Id,
            ConversationId = conversation.Id,
            ContactId = contact.Id,
            Content = msg.Content,
            ChannelType = message.ChannelType
        }, cancellationToken);

        return Result<Message>.Success(msg);
    }
}
