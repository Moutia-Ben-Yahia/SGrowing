using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Channels.Telegram;

public record ValidateWebAppDataApiRequest(
    string InitData,
    string? BotToken = null,
    int? MaxAgeHours = null);

public record GenerateWebAppDataApiRequest(
    Dictionary<string, string> Parameters,
    string? BotToken = null);

public static class TelegramBotEndpoints
{
    public static IEndpointRouteBuilder MapTelegramBotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/channels/telegram")
            .WithTags("Telegram Conversational Marketing & Mini-App Bot Router");

        // 1. POST /webhook
        group.MapPost("/webhook", async (
            [FromBody] TelegramUpdate update,
            HttpContext httpContext,
            ITelegramMarketingBotService botService,
            CancellationToken ct) =>
        {
            string? secretHeader = httpContext.Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();
            var result = await botService.ProcessWebhookUpdateAsync(update, secretHeader, ct);

            if (!result.Success && result.UpdateType == "SecurityRejected")
            {
                return Results.Json(result, statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(result);
        })
        .WithName("ProcessTelegramWebhookUpdate")
        .WithSummary("Process inbound Telegram Bot API 7.0+ updates (messages, callback queries, web app data payloads)");

        // 2. POST /validate-webapp-data
        group.MapPost("/validate-webapp-data", (
            [FromBody] ValidateWebAppDataApiRequest req,
            ITelegramMarketingBotService botService) =>
        {
            TimeSpan? maxAge = req.MaxAgeHours.HasValue ? TimeSpan.FromHours(req.MaxAgeHours.Value) : null;
            var result = botService.ValidateWebAppData(req.InitData, req.BotToken, maxAge);
            return Results.Ok(result);
        })
        .WithName("ValidateTelegramMiniAppData")
        .WithSummary("Cryptographically validate Telegram WebApp initData HMAC-SHA256 signature and auth_date freshness");

        // 2b. POST /generate-webapp-data (Helper for testing and sandbox)
        group.MapPost("/generate-webapp-data", (
            [FromBody] GenerateWebAppDataApiRequest req,
            ITelegramMarketingBotService botService) =>
        {
            var token = req.BotToken ?? botService.GetConfig().BotToken;
            var initData = botService.GenerateWebAppInitData(req.Parameters, token);
            return Results.Ok(new { InitData = initData });
        })
        .WithName("GenerateTelegramMiniAppData")
        .WithSummary("Generate cryptographically signed initData string for testing Mini App authentication");

        // 3. POST /send-message
        group.MapPost("/send-message", async (
            [FromBody] TelegramSendMessageRequest req,
            ITelegramMarketingBotService botService,
            CancellationToken ct) =>
        {
            var result = await botService.SendMessageAsync(req, ct);
            return Results.Ok(result);
        })
        .WithName("SendTelegramMessage")
        .WithSummary("Dispatch an interactive Telegram message with inline keyboard or URL buttons");

        // 4. POST /broadcast
        group.MapPost("/broadcast", async (
            [FromBody] TelegramBroadcastRequest req,
            ITelegramMarketingBotService botService,
            CancellationToken ct) =>
        {
            var result = await botService.BroadcastAsync(req, ct);
            return Results.Ok(result);
        })
        .WithName("DispatchTelegramBroadcast")
        .WithSummary("Queue and broadcast messages to audience segments with rate-limiting pacing and flood-wait backoff");

        // 5. GET /fsm-states
        group.MapGet("/fsm-states", (
            [FromQuery] string? tenantId,
            ITelegramMarketingBotService botService) =>
        {
            var states = botService.GetUserStates(tenantId);
            return Results.Ok(states);
        })
        .WithName("GetTelegramFsmStates")
        .WithSummary("Inspect active conversational finite state machine user sessions and captured leads");

        // 6. GET /metrics
        group.MapGet("/metrics", (
            [FromQuery] string? tenantId,
            ITelegramMarketingBotService botService) =>
        {
            var metrics = botService.GetMetrics(tenantId);
            return Results.Ok(metrics);
        })
        .WithName("GetTelegramBotMetrics")
        .WithSummary("Retrieve Telegram Bot health metrics, state distribution, and broadcast delivery performance");

        // 7. GET & PUT /config
        group.MapGet("/config", (
            [FromQuery] string? tenantId,
            ITelegramMarketingBotService botService) =>
        {
            var config = botService.GetConfig(tenantId);
            return Results.Ok(config);
        })
        .WithName("GetTelegramBotConfig")
        .WithSummary("Retrieve Telegram Bot API configuration and credentials");

        group.MapPut("/config", (
            [FromBody] TelegramBotConfig config,
            [FromQuery] string? tenantId,
            ITelegramMarketingBotService botService) =>
        {
            botService.UpdateConfig(config, tenantId);
            return Results.Ok(new { Success = true, Message = "Configuration updated successfully." });
        })
        .WithName("UpdateTelegramBotConfig")
        .WithSummary("Update Telegram Bot API tokens, webhook endpoints, and rate-limiting limits");

        // 8. GET /subscribers
        group.MapGet("/subscribers", (
            [FromQuery] string? tenantId,
            ITelegramMarketingBotService botService) =>
        {
            var subscribers = botService.GetSubscribers(tenantId);
            return Results.Ok(subscribers);
        })
        .WithName("GetTelegramSubscribers")
        .WithSummary("List all subscribed Telegram users, contact leads, and tags");

        // 9. GET /broadcast-history
        group.MapGet("/broadcast-history", (
            [FromQuery] string? tenantId,
            ITelegramMarketingBotService botService) =>
        {
            var history = botService.GetBroadcastHistory(tenantId);
            return Results.Ok(history);
        })
        .WithName("GetTelegramBroadcastHistory")
        .WithSummary("Retrieve historical broadcast campaigns, delivery counts, and flood-wait logs");

        return app;
    }
}
