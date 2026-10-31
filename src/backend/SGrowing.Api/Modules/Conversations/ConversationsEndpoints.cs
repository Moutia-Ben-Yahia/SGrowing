using Microsoft.AspNetCore.Mvc;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Core.Channels;

namespace SGrowing.Api.Modules.Conversations;

public static class ConversationsEndpoints
{
    public static IEndpointRouteBuilder MapConversationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/conversations/{id:guid}/messages", async (Guid id, [FromQuery] Guid tenantId, GetConversationMessagesHandler handler) =>
        {
            var res = await handler.HandleAsync(tenantId, id);
            return res.IsSuccess ? Results.Ok(res.Value) : Results.NotFound(new { error = res.Error });
        });

        app.MapPost("/api/conversations/{id:guid}/messages", async (Guid id, [FromBody] SendMessageRequest req, SendMessageHandler handler) =>
        {
            var res = await handler.HandleAsync(req with { ConversationId = id });
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        app.MapPost("/api/conversations/{id:guid}/status", async (Guid id, [FromBody] ChangeStatusRequest req, ChangeStatusHandler handler) =>
        {
            var res = await handler.HandleAsync(req with { ConversationId = id });
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        // AI Draft Approval
        app.MapPost("/api/ai/drafts/{id:guid}/approve", async (Guid id, [FromBody] ApproveAiDraftRequest req, ApproveAiDraftHandler handler) =>
        {
            var res = await handler.HandleAsync(req with { DraftId = id });
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        // Webhook Ingress (with Reply Sentiment Handoff hook)
        app.MapPost("/api/webhooks/{channel}", async (
            string channel,
            [FromQuery] Guid tenantId,
            [FromQuery] Guid inboxId,
            HttpRequest request,
            ChannelIngressService ingressService,
            ProspectReplyHandoffService handoffService,
            WhatsAppCloudAdapter waCloud,
            EvolutionApiAdapter evoWa,
            EmailAdapter email,
            TelegramAdapter telegram,
            DiscordAdapter discord,
            MetaGraphAdapter meta,
            XTwitterAdapter xTwitter,
            SmsAdapter sms) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();

            InboundChannelMessage? parsed = channel.ToLower() switch
            {
                "whatsapp_cloud" => waCloud.ParseInboundWebhook(tenantId, body),
                "evolution_api" => evoWa.ParseInboundWebhook(tenantId, body),
                "email" => email.ParseInboundWebhook(tenantId, body),
                "telegram" => telegram.ParseInboundWebhook(tenantId, body),
                "discord" => discord.ParseInboundWebhook(tenantId, body),
                "meta_graph" => meta.ParseInboundWebhook(tenantId, body),
                "instagram" => meta.ParseInboundWebhook(tenantId, body),
                "facebook" => meta.ParseInboundWebhook(tenantId, body),
                "threads" => meta.ParseInboundWebhook(tenantId, body),
                "x_twitter" => xTwitter.ParseInboundWebhook(tenantId, body),
                "sms" => sms.ParseInboundWebhook(tenantId, body),
                _ => null
            };

            if (parsed == null)
                return Results.BadRequest(new { error = $"Could not parse webhook for channel: {channel}" });

            var res = await ingressService.IngestAsync(inboxId, parsed);
            if (!res.IsSuccess)
                return Results.BadRequest(new { error = res.Error });

            // Evaluate prospect reply sentiment for outbound handoff
            await handoffService.HandleReplyAsync(tenantId, res.Value.SenderId ?? Guid.Empty, res.Value.ConversationId, parsed.Content);

            return Results.Ok(new { status = "Ingested", messageId = res.Value.Id });
        });

        return app;
    }
}
