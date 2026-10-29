using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public static class WhatsAppPairingEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IServiceCollection AddWhatsAppPairingServices(this IServiceCollection services)
    {
        services.AddScoped<IWhatsAppPairingProtocolService, WhatsAppPairingProtocolService>();
        return services;
    }

    public static IEndpointRouteBuilder MapWhatsAppPairingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/channels/whatsapp")
            .WithTags("WhatsApp Pairing Protocol & Interactive COD Confirmation");

        // 1. Request 8-digit Pairing Code
        group.MapPost("/pairing/request", async (
            [FromBody] WhatsAppPairingRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppPairingProtocolService service,
            CancellationToken ct) =>
        {
            try
            {
                var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
                var result = await service.RequestPairingCodeAsync(
                    effectiveTenantId,
                    req.PhoneNumber,
                    req.InstanceName ?? "default",
                    ct);

                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RequestWhatsAppPairingCode")
        .WithSummary("Request an 8-character human-readable pairing code with 10-minute TTL for phone connection");

        // 2. Verify / Simulate Companion Handshake
        group.MapPost("/pairing/verify", async (
            [FromBody] WhatsAppVerifyPairingRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppPairingProtocolService service,
            CancellationToken ct) =>
        {
            try
            {
                var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
                var result = await service.SimulatePairingHandshakeAsync(
                    effectiveTenantId,
                    req.PairingCode,
                    ct);

                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("VerifyWhatsAppPairingHandshake")
        .WithSummary("Simulate WhatsApp companion device Noise_XX handshake and persist credentials");

        // 3. Get Pairing Connection Status
        group.MapGet("/pairing/status", async (
            [FromQuery] string? instanceName,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppPairingProtocolService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var status = await service.GetPairingStatusAsync(
                effectiveTenantId,
                instanceName ?? "default",
                ct);

            return Results.Ok(status);
        })
        .WithName("GetWhatsAppPairingStatus")
        .WithSummary("Retrieve current companion device pairing status, active code countdown, and connection health");

        // 4. Dispatch Interactive COD Confirmation Buttons
        group.MapPost("/interactive/dispatch-order", async (
            [FromBody] DispatchInteractiveCodRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppPairingProtocolService service,
            CancellationToken ct) =>
        {
            try
            {
                var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
                var result = await service.DispatchInteractiveCodConfirmationAsync(
                    effectiveTenantId,
                    req.OrderId,
                    req.CustomerPhone,
                    req.TotalAmount,
                    req.Currency ?? "TND",
                    ct);

                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("DispatchInteractiveCodOrderConfirmation")
        .WithSummary("Dispatch interactive 3-button message for order verification (Confirm / Edit Address / Cancel)");

        // 5. Ingest Interactive Button Webhook Callbacks
        group.MapPost("/interactive/webhook", async (
            [FromBody] InteractiveWebhookRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppPairingProtocolService service,
            CancellationToken ct) =>
        {
            try
            {
                var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
                var result = await service.HandleInteractiveButtonResponseAsync(
                    effectiveTenantId,
                    req.OrderId,
                    req.ButtonId,
                    req.UpdatedText,
                    ct);

                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("IngestWhatsAppInteractiveButtonWebhook")
        .WithSummary("Process customer button taps (btn_confirm, btn_edit_address, btn_cancel) and trigger inventory/WMS flows");

        // 6. Timeout Sweep Fallback to Call Center Dialer
        group.MapPost("/interactive/timeout-sweep", async (
            [FromBody] TimeoutSweepRequest? req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppPairingProtocolService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var minutes = req?.TimeoutMinutes ?? 30;
            var timeoutThreshold = TimeSpan.FromMinutes(minutes);

            var result = await service.CheckConfirmationTimeoutAndFallbackAsync(
                effectiveTenantId,
                timeoutThreshold,
                ct);

            return Results.Ok(result);
        })
        .WithName("SweepWhatsAppInteractiveTimeouts")
        .WithSummary("Sweep orders awaiting confirmation > threshold and automatically escalate to outbound call center dialer queue");

        return app;
    }
}
