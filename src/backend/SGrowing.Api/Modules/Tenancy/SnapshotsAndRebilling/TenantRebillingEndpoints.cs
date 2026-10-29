using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;

public record SetRebillingConfigRequest(
    Guid SubAccountTenantId,
    decimal SmsMarkupMultiplier,
    decimal VoiceMarkupMultiplier,
    decimal AiTokenMarkupMultiplier
);

public record CalculateRebillRequest(
    Guid SubAccountTenantId,
    string ServiceType,
    decimal WholesaleCost
);

public static class TenantRebillingEndpoints
{
    public static IEndpointRouteBuilder MapTenantRebillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants/rebilling")
            .WithTags("TenantRebilling");

        group.MapPost("/config", async (
            Guid agencyTenantId,
            SetRebillingConfigRequest req,
            ITenantRebillingService service) =>
        {
            var config = await service.SetRebillingConfigAsync(
                agencyTenantId,
                req.SubAccountTenantId,
                req.SmsMarkupMultiplier,
                req.VoiceMarkupMultiplier,
                req.AiTokenMarkupMultiplier);

            return Results.Ok(config);
        });

        group.MapGet("/config/{subAccountId:guid}", async (
            Guid agencyTenantId,
            Guid subAccountId,
            ITenantRebillingService service) =>
        {
            var config = await service.GetRebillingConfigAsync(agencyTenantId, subAccountId);
            return config != null ? Results.Ok(config) : Results.NotFound();
        });

        group.MapPost("/calculate", async (
            Guid agencyTenantId,
            CalculateRebillRequest req,
            ITenantRebillingService service) =>
        {
            var result = await service.CalculateRebilledCostAsync(
                agencyTenantId,
                req.SubAccountTenantId,
                req.ServiceType,
                req.WholesaleCost);

            return Results.Ok(result);
        });

        return app;
    }
}
