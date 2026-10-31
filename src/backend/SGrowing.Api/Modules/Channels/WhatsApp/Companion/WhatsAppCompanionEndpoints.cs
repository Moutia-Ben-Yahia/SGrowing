using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Channels.WhatsApp.Companion;

#region DTOs

public record RequestPairingCodeDto(
    Guid? TenantId,
    string PhoneNumber,
    string? DeviceModel = null
);

public record VerifyPairingCodeDto(
    Guid? TenantId,
    Guid SessionId,
    string PairingCode
);

public record SimulateAckDto(
    Guid SessionId,
    string MessageId,
    MessageAckLevel TargetAck,
    bool IsVoiceNote = false
);

public record DecryptMediaDto(
    Guid SessionId,
    string MimeType,
    string CiphertextBase64,
    string IvBase64,
    string TagBase64,
    string ExpectedSha256Checksum,
    string? FileName = null
);

public record EncryptMediaTestDto(
    string Plaintext,
    string MimeType
);

#endregion

public static class WhatsAppCompanionEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IServiceCollection AddWhatsAppCompanionServices(this IServiceCollection services)
    {
        services.AddSingleton<IWhatsAppCompanionSocketService, WhatsAppCompanionSocketService>();
        return services;
    }

    public static IEndpointRouteBuilder MapWhatsAppCompanionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/channels/whatsapp/companion")
            .WithTags("WhatsApp Companion Socket & Pairing Bridge");

        // 1. Request 8-digit Pairing Code
        group.MapPost("/pairing-code", async (
            [FromBody] RequestPairingCodeDto req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppCompanionSocketService service,
            CancellationToken ct) =>
        {
            try
            {
                var effectiveTenantId = ResolveTenantId(req.TenantId ?? tenantId, headerTenantId);
                var result = await service.RequestPairingCodeAsync(effectiveTenantId, req.PhoneNumber, req.DeviceModel, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RequestWhatsAppCompanionPairingCode")
        .WithSummary("Generate RFC-compliant 8-character pairing code formatted as ABCD-EFGH with 180s TTL countdown.");

        // 2. Verify Pairing Code & Complete Noise_XX Handshake
        group.MapPost("/verify-code", async (
            [FromBody] VerifyPairingCodeDto req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppCompanionSocketService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(req.TenantId ?? tenantId, headerTenantId);
            var result = await service.VerifyPairingCodeAsync(effectiveTenantId, req.SessionId, req.PairingCode, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("VerifyWhatsAppCompanionPairingCode")
        .WithSummary("Verify pairing code, check retry threshold, and finalize Noise_XX Diffie-Hellman handshake.");

        // 3. Get Session Live Status & State Machine
        group.MapGet("/status/{sessionId:guid}", async (
            Guid sessionId,
            IWhatsAppCompanionSocketService service,
            CancellationToken ct) =>
        {
            var session = await service.GetSessionStatusAsync(sessionId, ct);
            if (session == null)
            {
                return Results.NotFound(new { error = $"Companion session {sessionId} not found." });
            }

            var packetLogs = service.GetPacketLogs(sessionId);
            return Results.Ok(new
            {
                session,
                packetLogs
            });
        })
        .WithName("GetWhatsAppCompanionStatus")
        .WithSummary("Retrieve live state machine phase, keys, and socket packet inspector logs.");

        // 4. Terminate Companion Socket Session
        group.MapPost("/disconnect/{sessionId:guid}", async (
            Guid sessionId,
            [FromQuery] string? reason,
            IWhatsAppCompanionSocketService service,
            CancellationToken ct) =>
        {
            var success = await service.DisconnectSessionAsync(sessionId, reason ?? "UserRequestedDisconnect", ct);
            return success
                ? Results.Ok(new { success = true, sessionId, message = "Companion session disconnected successfully." })
                : Results.NotFound(new { success = false, error = $"Companion session {sessionId} not found." });
        })
        .WithName("DisconnectWhatsAppCompanion")
        .WithSummary("Terminate active companion socket session and move state machine to Disconnected.");

        // 5. List Companion Sessions for Tenant
        group.MapGet("/sessions", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppCompanionSocketService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var sessions = await service.GetSessionsForTenantAsync(effectiveTenantId, ct);
            return Results.Ok(new
            {
                tenantId = effectiveTenantId,
                count = sessions.Count,
                sessions
            });
        })
        .WithName("ListWhatsAppCompanionSessions")
        .WithSummary("List all active and registered companion devices and phone lines for the tenant.");

        // 6. Simulate Receipt Acknowledgement Progression
        group.MapPost("/simulate-ack", async (
            [FromBody] SimulateAckDto req,
            IWhatsAppCompanionSocketService service,
            CancellationToken ct) =>
        {
            var result = await service.SimulateMessageAckProgressionAsync(
                req.SessionId,
                req.MessageId,
                req.TargetAck,
                req.IsVoiceNote,
                ct);

            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("SimulateWhatsAppMessageAck")
        .WithSummary("Simulate WABinary receipt acknowledgement (AckServer -> AckDevice -> AckRead -> AckPlayed).");

        // 7. Decrypt Binary Media Payload & Validate SHA-256 Checksum
        group.MapPost("/decrypt-media", async (
            [FromBody] DecryptMediaDto req,
            IWhatsAppCompanionSocketService service,
            CancellationToken ct) =>
        {
            var payload = new CompanionMediaPayload(
                req.MimeType,
                req.CiphertextBase64,
                req.IvBase64,
                req.TagBase64,
                req.ExpectedSha256Checksum,
                req.FileName
            );

            var result = await service.DecryptSessionMediaAsync(req.SessionId, payload, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("DecryptWhatsAppCompanionMedia")
        .WithSummary("Stream-decrypt AES-256-GCM encrypted media binary payload and verify SHA-256 file checksum.");

        // 8. Get Live Telemetry
        group.MapGet("/telemetry/{sessionId:guid}", async (
            Guid sessionId,
            [FromQuery] bool? refresh,
            IWhatsAppCompanionSocketService service,
            CancellationToken ct) =>
        {
            if (refresh == true)
            {
                var refreshed = await service.RefreshTelemetryAsync(sessionId, ct);
                return Results.Ok(refreshed);
            }

            var telemetry = await service.GetTelemetryAsync(sessionId, ct);
            return telemetry != null
                ? Results.Ok(telemetry)
                : Results.NotFound(new { error = $"Telemetry for session {sessionId} not found." });
        })
        .WithName("GetWhatsAppCompanionTelemetry")
        .WithSummary("Get real-time device battery percentage, charging state, network type, and ping latency.");

        // 9. Helper: Encrypt Media Test Vector
        group.MapPost("/encrypt-test-vector", (
            [FromBody] EncryptMediaTestDto req,
            IWhatsAppCompanionSocketService service) =>
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(req.Plaintext);
            var result = service.EncryptMediaPayload(bytes, req.MimeType);
            return Results.Ok(result);
        })
        .WithName("EncryptWhatsAppTestVector")
        .WithSummary("Developer helper: generate an AES-256-GCM encrypted payload test vector with valid SHA-256.");

        return app;
    }
}
