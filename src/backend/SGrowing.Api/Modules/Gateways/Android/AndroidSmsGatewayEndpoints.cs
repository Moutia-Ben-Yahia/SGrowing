using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Gateways.Android;

public record RegisterDeviceRequest(
    string DeviceName,
    string DeviceIdentifier,
    string FcmToken,
    string? Sim1 = "Ooredoo",
    string? Sim2 = "Orange",
    int? DailyQuota = 500
);

public record DeviceHeartbeatRequest(
    string DeviceIdentifier,
    int BatteryPercent
);

public record SendAndroidSmsRequest(
    string RecipientPhone,
    string MessageBody,
    int? PreferredSimSlot = null
);

public static class AndroidSmsGatewayEndpoints
{
    public static IEndpointRouteBuilder MapAndroidSmsGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/gateways/android-sms")
            .WithTags("AndroidSmsGateway");

        group.MapPost("/devices/register", async (
            Guid tenantId,
            RegisterDeviceRequest req,
            IAndroidSmsGatewayService service) =>
        {
            var device = await service.RegisterDeviceAsync(
                tenantId,
                req.DeviceName,
                req.DeviceIdentifier,
                req.FcmToken,
                req.Sim1 ?? "Ooredoo",
                req.Sim2 ?? "Orange",
                req.DailyQuota ?? 500);

            return Results.Ok(device);
        });

        group.MapPost("/devices/heartbeat", async (
            Guid tenantId,
            DeviceHeartbeatRequest req,
            IAndroidSmsGatewayService service) =>
        {
            var ok = await service.RecordHeartbeatAsync(tenantId, req.DeviceIdentifier, req.BatteryPercent);
            return Results.Ok(new { Success = ok });
        });

        group.MapPost("/send", async (
            Guid tenantId,
            SendAndroidSmsRequest req,
            IAndroidSmsGatewayService service) =>
        {
            var result = await service.DispatchSmsAsync(
                tenantId,
                req.RecipientPhone,
                req.MessageBody,
                req.PreferredSimSlot);

            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/devices", async (
            Guid tenantId,
            IAndroidSmsGatewayService service) =>
        {
            var devices = await service.ListDevicesAsync(tenantId);
            return Results.Ok(devices);
        });

        group.MapGet("/history", async (
            Guid tenantId,
            int? limit,
            IAndroidSmsGatewayService service) =>
        {
            var history = await service.GetDispatchHistoryAsync(tenantId, limit ?? 50);
            return Results.Ok(history);
        });

        return app;
    }
}
