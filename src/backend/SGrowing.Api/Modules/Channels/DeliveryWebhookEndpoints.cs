using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels;

public static class DeliveryWebhookEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks")
            .WithTags("Inbound Channel Delivery Webhooks (Feedback Loop)");

        // 1. Resend / Generic Email Delivery Webhook
        group.MapPost("/email/delivery", HandleEmailDeliveryWebhook)
            .AllowAnonymous()
            .WithSummary("Ingest Resend/SendGrid/Generic email delivery, bounce, open, click events into CRM contact timeline");

        group.MapPost("/resend", HandleEmailDeliveryWebhook)
            .AllowAnonymous()
            .WithSummary("Alias for Resend webhook ingestion");

        // 2. Twilio SMS Status Callback Webhook
        group.MapPost("/twilio/status", HandleTwilioSmsStatusWebhook)
            .AllowAnonymous()
            .WithSummary("Ingest Twilio/Telnyx SMS delivery receipts and callbacks into CRM contact timeline");

        group.MapPost("/sms/delivery", HandleTwilioSmsStatusWebhook)
            .AllowAnonymous()
            .WithSummary("Alias for SMS delivery status callbacks");

        // 3. WhatsApp Direct Delivery Receipt Webhook
        group.MapPost("/whatsapp/delivery", HandleWhatsAppDeliveryWebhook)
            .AllowAnonymous()
            .WithSummary("Ingest WhatsApp delivery/read receipts into CRM contact timeline");

        return app;
    }

    public static async Task<IResult> HandleEmailDeliveryWebhook(
        HttpRequest request,
        [FromQuery] Guid? tenantId,
        [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
        ICustomer360AttributionService c360Service,
        SGrowingDbContext db,
        CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var rawJson = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(rawJson))
            return Results.BadRequest(new { error = "Empty webhook payload" });

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var events = new List<JsonElement>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray()) events.Add(item);
            }
            else
            {
                events.Add(root);
            }

            int processed = 0;
            foreach (var evt in events)
            {
                string eventType = "email.delivered";
                if (evt.TryGetProperty("type", out var typeProp))
                    eventType = typeProp.GetString() ?? eventType;
                else if (evt.TryGetProperty("event", out var eventProp))
                    eventType = eventProp.GetString() ?? eventType;

                string recipientEmail = "";
                string subject = "";
                string emailId = "";

                if (evt.TryGetProperty("data", out var dataObj))
                {
                    if (dataObj.TryGetProperty("email_id", out var eid)) emailId = eid.GetString() ?? "";
                    if (dataObj.TryGetProperty("subject", out var subj)) subject = subj.GetString() ?? "";
                    if (dataObj.TryGetProperty("to", out var toProp))
                    {
                        recipientEmail = toProp.ValueKind == JsonValueKind.Array && toProp.GetArrayLength() > 0
                            ? toProp[0].GetString() ?? ""
                            : toProp.GetString() ?? "";
                    }
                }
                else
                {
                    if (evt.TryGetProperty("email", out var emProp)) recipientEmail = emProp.GetString() ?? "";
                    if (evt.TryGetProperty("subject", out var sProp)) subject = sProp.GetString() ?? "";
                    if (evt.TryGetProperty("id", out var idProp)) emailId = idProp.GetString() ?? "";
                }

                if (string.IsNullOrWhiteSpace(recipientEmail))
                    continue;

                var effectiveTenantId = tenantId ?? headerTenantId;
                if (!effectiveTenantId.HasValue || effectiveTenantId.Value == Guid.Empty)
                {
                    var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Email == recipientEmail, ct);
                    effectiveTenantId = contact?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
                }

                await c360Service.RecordEventAsync(new RecordCustomerEventRequest(
                    TenantId: effectiveTenantId.Value,
                    Email: recipientEmail,
                    EventType: "EmailDeliveryEvent",
                    Summary: $"Email event [{eventType}] for '{recipientEmail}': {(string.IsNullOrWhiteSpace(subject) ? emailId : subject)}",
                    PayloadJson: evt.GetRawText(),
                    OccurredAtUtc: DateTime.UtcNow
                ), ct);

                processed++;
            }

            return Results.Ok(new { status = "Success", processedCount = processed });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to parse email delivery webhook: {ex.Message}" });
        }
    }

    public static async Task<IResult> HandleTwilioSmsStatusWebhook(
        HttpRequest request,
        [FromQuery] Guid? tenantId,
        [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
        ICustomer360AttributionService c360Service,
        SGrowingDbContext db,
        CancellationToken ct)
    {
        string? messageSid = null;
        string? messageStatus = null;
        string? to = null;
        string? from = null;
        string rawPayload = "";

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(ct);
            messageSid = form["MessageSid"].FirstOrDefault() ?? form["SmsSid"].FirstOrDefault();
            messageStatus = form["MessageStatus"].FirstOrDefault() ?? form["SmsStatus"].FirstOrDefault();
            to = form["To"].FirstOrDefault();
            from = form["From"].FirstOrDefault();
            rawPayload = JsonSerializer.Serialize(form.ToDictionary(k => k.Key, v => v.Value.ToString()));
        }
        else
        {
            using var reader = new StreamReader(request.Body);
            rawPayload = await reader.ReadToEndAsync(ct);
            if (!string.IsNullOrWhiteSpace(rawPayload))
            {
                try
                {
                    using var doc = JsonDocument.Parse(rawPayload);
                    var r = doc.RootElement;
                    if (r.TryGetProperty("MessageSid", out var ms)) messageSid = ms.GetString();
                    if (r.TryGetProperty("MessageStatus", out var st)) messageStatus = st.GetString();
                    if (r.TryGetProperty("To", out var t)) to = t.GetString();
                    if (r.TryGetProperty("From", out var f)) from = f.GetString();
                }
                catch { }
            }
        }

        if (string.IsNullOrWhiteSpace(to) && string.IsNullOrWhiteSpace(messageSid))
            return Results.BadRequest(new { error = "Missing To or MessageSid parameter" });

        var effectiveTenantId = tenantId ?? headerTenantId;
        if (!effectiveTenantId.HasValue || effectiveTenantId.Value == Guid.Empty)
        {
            var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Phone == to || (c.Phone != null && to != null && c.Phone.Contains(to)), ct);
            effectiveTenantId = contact?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
        }

        if (!string.IsNullOrWhiteSpace(messageSid))
        {
            var msg = await db.Messages.FirstOrDefaultAsync(m => m.ExternalMessageId == messageSid, ct);
            if (msg != null)
            {
                msg.DeliveryStatus = (messageStatus?.ToLowerInvariant()) switch
                {
                    "delivered" => DeliveryStatus.Delivered,
                    "failed" or "undelivered" => DeliveryStatus.Failed,
                    "read" => DeliveryStatus.Read,
                    _ => DeliveryStatus.Sent
                };
                await db.SaveChangesAsync(ct);
            }
        }

        await c360Service.RecordEventAsync(new RecordCustomerEventRequest(
            TenantId: effectiveTenantId.Value,
            Phone: to,
            EventType: "SmsDeliveryEvent",
            Summary: $"SMS [{messageStatus ?? "StatusUpdate"}] to '{to}' (Sid: {messageSid})",
            PayloadJson: rawPayload,
            OccurredAtUtc: DateTime.UtcNow
        ), ct);

        return Results.Ok(new { status = "Success", messageSid, deliveryStatus = messageStatus });
    }

    public static async Task<IResult> HandleWhatsAppDeliveryWebhook(
        HttpRequest request,
        [FromQuery] Guid? tenantId,
        [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
        ICustomer360AttributionService c360Service,
        SGrowingDbContext db,
        CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var rawJson = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(rawJson))
            return Results.BadRequest(new { error = "Empty webhook payload" });

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            string statusVal = "delivered";
            string recipientId = "";
            string msgId = "";

            if (root.TryGetProperty("status", out var sProp)) statusVal = sProp.GetString() ?? statusVal;
            if (root.TryGetProperty("recipient_id", out var rProp)) recipientId = rProp.GetString() ?? "";
            if (root.TryGetProperty("id", out var idProp)) msgId = idProp.GetString() ?? "";

            if (string.IsNullOrWhiteSpace(recipientId))
                return Results.BadRequest(new { error = "Missing recipient_id" });

            var effectiveTenantId = tenantId ?? headerTenantId;
            if (!effectiveTenantId.HasValue || effectiveTenantId.Value == Guid.Empty)
            {
                var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Phone == recipientId || (c.Phone != null && c.Phone.Contains(recipientId)), ct);
                effectiveTenantId = contact?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            }

            if (!string.IsNullOrWhiteSpace(msgId))
            {
                var msg = await db.Messages.FirstOrDefaultAsync(m => m.ExternalMessageId == msgId, ct);
                if (msg != null)
                {
                    msg.DeliveryStatus = (statusVal.ToLowerInvariant()) switch
                    {
                        "delivered" => DeliveryStatus.Delivered,
                        "read" => DeliveryStatus.Read,
                        "failed" => DeliveryStatus.Failed,
                        _ => DeliveryStatus.Sent
                    };
                    await db.SaveChangesAsync(ct);
                }
            }

            await c360Service.RecordEventAsync(new RecordCustomerEventRequest(
                TenantId: effectiveTenantId.Value,
                Phone: recipientId,
                EventType: "WhatsAppDeliveryEvent",
                Summary: $"WhatsApp delivery [{statusVal}] to '{recipientId}' (MsgId: {msgId})",
                PayloadJson: rawJson,
                OccurredAtUtc: DateTime.UtcNow
            ), ct);

            return Results.Ok(new { status = "Success", messageId = msgId, deliveryStatus = statusVal });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to parse WhatsApp delivery webhook: {ex.Message}" });
        }
    }
}
