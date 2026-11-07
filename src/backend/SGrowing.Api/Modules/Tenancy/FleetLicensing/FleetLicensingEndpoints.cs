using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Tenancy.FleetLicensing;

public class FleetLicensingEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenancy/fleet-licensing")
            .WithTags("Self-Hosted Enterprise Fleet & Cryptographic Licensing");

        group.MapPost("/issue", ([FromBody] IssueLicenseRequest request, [FromServices] IFleetLicensingService service) =>
        {
            try
            {
                var license = service.IssueLicense(request);
                return Results.Ok(license);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("IssueCryptographicLicense")
        .WithSummary("Issues a cryptographically signed HMAC-SHA256 enterprise license token with seat, CPU, module, and hardware constraints.");

        group.MapPost("/verify", ([FromBody] VerifyLicenseRequest request, [FromServices] IFleetLicensingService service) =>
        {
            var result = service.VerifyLicense(request.SignedToken, request.CurrentNodeFingerprint);
            return Results.Ok(result);
        })
        .WithName("VerifyLicenseToken")
        .WithSummary("Validates tamper resistance, signature authenticity, validity window, and hardware node fingerprint binding offline.");

        group.MapGet("/status", ([FromQuery] Guid? tenantId, [FromServices] IFleetLicensingService service) =>
        {
            var targetTenantId = tenantId ?? FleetLicensingService.DefaultTenantId;
            var activePayload = service.GetCurrentActiveLicense(targetTenantId);
            if (activePayload == null)
            {
                return Results.NotFound(new { error = "No active license installed for tenant.", tenantId = targetTenantId });
            }

            var canonical = FleetLicensingService.ComputeCanonicalPayload(activePayload);
            var now = DateTimeOffset.UtcNow;
            var remaining = Math.Max(0, (int)(activePayload.ExpiresAtUtc - now).TotalDays);

            return Results.Ok(new
            {
                tenantId = targetTenantId,
                organizationName = activePayload.OrganizationName,
                tier = activePayload.Tier,
                maxSeats = activePayload.MaxSeats,
                maxCpuCores = activePayload.MaxCpuCores,
                maxClusterNodes = activePayload.MaxClusterNodes,
                enabledEnterpriseModules = activePayload.EnabledEnterpriseModules,
                issuedAtUtc = activePayload.IssuedAtUtc,
                expiresAtUtc = activePayload.ExpiresAtUtc,
                daysRemaining = remaining,
                hardwareClusterFingerprint = activePayload.HardwareClusterFingerprint,
                metadata = activePayload.Metadata
            });
        })
        .WithName("GetTenantLicenseStatus")
        .WithSummary("Retrieves the currently installed cryptographic license entitlements and expiration window for the given tenant.");

        group.MapPost("/activate", ([FromBody] ActivateLicensePayloadRequest request, [FromServices] IFleetLicensingService service) =>
        {
            var result = service.SetActiveLicense(request.TenantId, request.SignedToken);
            return result.IsValid ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ActivateSignedLicenseToken")
        .WithSummary("Activates and binds a verified signed license token into the enterprise node's runtime repository.");

        group.MapPost("/airgap/generate", ([FromBody] IssueLicenseRequest request, [FromServices] IFleetLicensingService service) =>
        {
            try
            {
                var license = service.IssueLicense(request);
                return Results.Ok(new
                {
                    licenseKey = license.LicenseKey,
                    signedToken = license.SignedToken,
                    airGappedActivationCode = license.AirGappedActivationCode,
                    expiresAtUtc = license.Payload.ExpiresAtUtc
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GenerateAirGappedActivationCode")
        .WithSummary("Issues a cryptographic license and encodes it into an air-gapped activation code with SHA-256 error-checking checksums.");

        group.MapPost("/airgap/activate", ([FromBody] AirGapActivationRequest request, [FromServices] IFleetLicensingService service) =>
        {
            var response = service.ActivateAirGappedCode(request);
            return response.Success ? Results.Ok(response) : Results.BadRequest(response);
        })
        .WithName("ActivateAirGappedCode")
        .WithSummary("Decodes, verifies checksum, validates hardware binding, and activates an air-gapped license code with zero internet access.");

        group.MapPost("/heartbeat", ([FromBody] IngestHeartbeatRequest request, [FromServices] IFleetLicensingService service) =>
        {
            try
            {
                var fleetStatus = service.IngestNodeHeartbeat(request.TenantId, request.Heartbeat);
                return Results.Ok(fleetStatus);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("IngestClusterNodeHeartbeat")
        .WithSummary("Ingests real-time hardware telemetry and seat load from a cluster node, triggering multi-node quota aggregation.");

        group.MapGet("/fleet/status", ([FromQuery] Guid? tenantId, [FromServices] IFleetLicensingService service) =>
        {
            var targetTenantId = tenantId ?? FleetLicensingService.DefaultTenantId;
            var fleetStatus = service.EvaluateFleetStatus(targetTenantId);
            return Results.Ok(fleetStatus);
        })
        .WithName("GetClusterFleetStatus")
        .WithSummary("Evaluates cluster health, total allocated CPU cores, active seats, quota overages, and node availability.");

        group.MapGet("/fleet/nodes", ([FromQuery] Guid? tenantId, [FromServices] IFleetLicensingService service) =>
        {
            var targetTenantId = tenantId ?? FleetLicensingService.DefaultTenantId;
            var nodes = service.GetActiveNodes(targetTenantId);
            return Results.Ok(nodes);
        })
        .WithName("GetClusterNodes")
        .WithSummary("Returns the list of registered cluster nodes and their latest telemetry heartbeats.");
    }
}

public static class FleetLicensingEndpointExtensions
{
    public static IServiceCollection AddFleetLicensingServices(this IServiceCollection services)
    {
        services.AddSingleton<IFleetLicensingService, FleetLicensingService>();
        return services;
    }
}
