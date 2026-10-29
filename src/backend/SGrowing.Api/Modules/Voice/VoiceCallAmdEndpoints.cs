using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Voice;

public record ClassifyAmdRequest(
    string DestinationPhone,
    int? AudioDurationMs = 2100,
    string? ForcedClassification = null,
    Guid? CallSessionId = null
);

public record ConfigureVoicemailTemplateRequest(
    string TemplateName,
    string AudioUrl,
    string? TextPrompt = "",
    bool? UseTts = false
);

public static class VoiceCallAmdEndpoints
{
    public static IEndpointRouteBuilder MapVoiceCallAmdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/voice/amd")
            .WithTags("VoiceAmd");

        group.MapPost("/classify", async (
            Guid tenantId,
            ClassifyAmdRequest req,
            IVoiceCallAmdService service) =>
        {
            var result = await service.ClassifyAudioStreamAmdAsync(
                tenantId,
                req.DestinationPhone,
                req.AudioDurationMs ?? 2100,
                req.ForcedClassification,
                req.CallSessionId);

            return Results.Ok(result);
        });

        group.MapPost("/templates", async (
            Guid tenantId,
            ConfigureVoicemailTemplateRequest req,
            IVoiceCallAmdService service) =>
        {
            var template = await service.ConfigureVoicemailTemplateAsync(
                tenantId,
                req.TemplateName,
                req.AudioUrl,
                req.TextPrompt ?? "",
                req.UseTts ?? false);

            return Results.Ok(template);
        });

        group.MapGet("/records", async (
            Guid tenantId,
            int? limit,
            IVoiceCallAmdService service) =>
        {
            var records = await service.GetAmdRecordsAsync(tenantId, limit ?? 50);
            return Results.Ok(records);
        });

        return app;
    }
}
