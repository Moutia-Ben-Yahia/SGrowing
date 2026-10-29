using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Conversations.PresenceAndCsat;

public record RecordCsatRequest(
    Guid ConversationId,
    Guid CustomerContactId,
    string CustomerName,
    int Score,
    string? Channel = "WhatsApp",
    string? FeedbackText = ""
);

public static class CsatSurveyEndpoints
{
    public static IEndpointRouteBuilder MapCsatSurveyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conversations/csat")
            .WithTags("ConversationCsat");

        group.MapPost("/responses", async (
            Guid tenantId,
            RecordCsatRequest req,
            ICsatSurveyService service) =>
        {
            var result = await service.RecordCsatResponseAsync(
                tenantId,
                req.ConversationId,
                req.CustomerContactId,
                req.CustomerName,
                req.Score,
                req.Channel ?? "WhatsApp",
                req.FeedbackText ?? "");

            return Results.Ok(result);
        });

        group.MapGet("/{conversationId:guid}", async (
            Guid tenantId,
            Guid conversationId,
            ICsatSurveyService service) =>
        {
            var result = await service.GetConversationCsatAsync(tenantId, conversationId);
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapGet("/metrics", async (
            Guid tenantId,
            ICsatSurveyService service) =>
        {
            var metrics = await service.GetTenantCsatMetricsAsync(tenantId);
            return Results.Ok(metrics);
        });

        return app;
    }
}
