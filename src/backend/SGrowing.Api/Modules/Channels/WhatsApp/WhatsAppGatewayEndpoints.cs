using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public static class WhatsAppGatewayEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapWhatsAppGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/channels/whatsapp/gateway")
            .WithTags("Compiled WhatsApp Multi-Device Gateway");

        group.MapGet("/instances", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppGatewayService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var instances = await service.GetInstancesAsync(effectiveTenantId, ct);
            return Results.Ok(instances);
        })
        .WithName("GetWhatsAppInstances")
        .WithSummary("List active and paired WhatsApp gateway instances");

        group.MapPost("/instances", async (
            [FromBody] CreateWhatsAppInstanceRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppGatewayService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var instance = await service.CreateInstanceAsync(effectiveTenantId, req, ct);
            return Results.Ok(instance);
        })
        .WithName("CreateWhatsAppInstance")
        .WithSummary("Create a new isolated WhatsApp gateway session instance");

        group.MapPost("/instances/{name}/pair-code", async (
            string name,
            [FromBody] RequestPairingCodeRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppGatewayService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.GeneratePairingCodeAsync(effectiveTenantId, name, req, ct);
            if (result == null) return Results.NotFound(new { error = $"Instance '{name}' not found" });
            return Results.Ok(result);
        })
        .WithName("GenerateWhatsAppPairingCode")
        .WithSummary("Generate 8-digit Noise/Signal protocol pairing code for phone number link");

        group.MapPost("/instances/{name}/pair-qr", async (
            string name,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppGatewayService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.GenerateQrCodeAsync(effectiveTenantId, name, ct);
            if (result == null) return Results.NotFound(new { error = $"Instance '{name}' not found" });
            return Results.Ok(result);
        })
        .WithName("GenerateWhatsAppQrCode")
        .WithSummary("Generate Base64 QR code token for scanning with WhatsApp mobile app");

        group.MapPost("/instances/{name}/connect", async (
            string name,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppGatewayService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var instance = await service.ConnectInstanceAsync(effectiveTenantId, name, ct);
            if (instance == null) return Results.NotFound(new { error = $"Instance '{name}' not found" });
            return Results.Ok(instance);
        })
        .WithName("ConnectWhatsAppInstance")
        .WithSummary("Confirm device pairing and establish WebSocket socket session");

        group.MapPost("/instances/{name}/disconnect", async (
            string name,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppGatewayService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var instance = await service.DisconnectInstanceAsync(effectiveTenantId, name, ct);
            if (instance == null) return Results.NotFound(new { error = $"Instance '{name}' not found" });
            return Results.Ok(instance);
        })
        .WithName("DisconnectWhatsAppInstance")
        .WithSummary("Disconnect session and terminate WebSocket connection");

        group.MapPost("/instances/{name}/dispatch", async (
            string name,
            [FromBody] DispatchWhatsAppMessageRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppGatewayService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.DispatchMessageAsync(effectiveTenantId, name, req, ct);
            if (result == null) return Results.NotFound(new { error = $"Instance '{name}' not found" });
            return Results.Ok(result);
        })
        .WithName("DispatchWhatsAppGatewayMessage")
        .WithSummary("High-speed outbound message dispatch via compiled Go gateway");

        return app;
    }
}
