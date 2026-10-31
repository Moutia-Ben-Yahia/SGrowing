using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public record InterceptMessageApiRequest(
    string PhoneNumber,
    string MessageText,
    string? TenantId = null);

public record AdjustCooldownApiRequest(
    string PhoneNumberId,
    double CooldownMultiplier,
    bool? EmergencyPause = null,
    string? Reason = null);

public static class WhatsAppBroadcastPacingEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppBroadcastPacingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/channels/whatsapp/broadcast-pacing")
            .WithTags("WhatsApp Broadcast Pacing & Anti-Ban Cooldown Shaper");

        // 1. POST /simulate-campaign
        group.MapPost("/simulate-campaign", (
            [FromBody] CampaignSimulationRequest req,
            IWhatsAppBroadcastPacingService pacingService) =>
        {
            var result = pacingService.SimulateCampaign(req);
            return Results.Ok(result);
        })
        .WithName("SimulateWhatsAppBroadcastCampaign")
        .WithSummary("Simulate campaign delivery time windows, batch slicing, and safety margin recommendations");

        // 2. POST /schedule-broadcast
        group.MapPost("/schedule-broadcast", (
            [FromBody] ScheduleBroadcastRequest req,
            IWhatsAppBroadcastPacingService pacingService) =>
        {
            var result = pacingService.ScheduleBroadcast(req);
            return Results.Ok(result);
        })
        .WithName("ScheduleWhatsAppBroadcast")
        .WithSummary("Schedule a paced broadcast campaign with automatic suppression filtering and anti-ban checks");

        // 3. GET /warmup-status/{phoneNumberId}
        group.MapGet("/warmup-status/{phoneNumberId}", (
            string phoneNumberId,
            IWhatsAppBroadcastPacingService pacingService) =>
        {
            var state = pacingService.GetWarmupStatus(phoneNumberId);
            return Results.Ok(state);
        })
        .WithName("GetWhatsAppWarmupStatus")
        .WithSummary("Retrieve current warmup ramp progress, daily limits, and remaining quota for a phone number");

        // 4. POST /intercept-message
        group.MapPost("/intercept-message", (
            [FromBody] InterceptMessageApiRequest req,
            IWhatsAppBroadcastPacingService pacingService) =>
        {
            var result = pacingService.InterceptInboundMessage(req.PhoneNumber, req.MessageText, req.TenantId);
            return Results.Ok(result);
        })
        .WithName("InterceptWhatsAppInboundMessage")
        .WithSummary("Detect multi-lingual opt-out keywords and automatically update suppression ledger");

        // 5. GET /ban-risk/{phoneNumberId}
        group.MapGet("/ban-risk/{phoneNumberId}", (
            string phoneNumberId,
            IWhatsAppBroadcastPacingService pacingService) =>
        {
            var report = pacingService.GetBanRiskReport(phoneNumberId);
            return Results.Ok(report);
        })
        .WithName("GetWhatsAppBanRiskReport")
        .WithSummary("Retrieve the real-time Ban Risk Index (0-100%) and active cooldown multiplier for a phone number");

        // 6. POST /adjust-cooldown
        group.MapPost("/adjust-cooldown", (
            [FromBody] AdjustCooldownApiRequest req,
            IWhatsAppBroadcastPacingService pacingService) =>
        {
            var report = pacingService.AdjustCooldown(
                req.PhoneNumberId,
                req.CooldownMultiplier,
                req.EmergencyPause,
                req.Reason);
            return Results.Ok(report);
        })
        .WithName("AdjustWhatsAppCooldown")
        .WithSummary("Manually override the cooldown multiplier or emergency pause state for a phone number");

        // 7. GET /suppression-list
        group.MapGet("/suppression-list", (
            [FromQuery] string? search,
            [FromQuery] int? skip,
            [FromQuery] int? take,
            IWhatsAppBroadcastPacingService pacingService) =>
        {
            int s = skip ?? 0;
            int t = take ?? 50;
            var items = pacingService.GetSuppressionList(search, s, t);
            var all = pacingService.GetSuppressionList(search, 0, 100000);
            return Results.Ok(new SuppressionListResponse(items, all.Count, s, t));
        })
        .WithName("GetWhatsAppSuppressionList")
        .WithSummary("List all opted-out phone numbers blocked from receiving broadcast campaigns");

        return app;
    }
}
