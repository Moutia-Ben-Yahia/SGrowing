using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Commerce.Connectors.Marketplace;

/// <summary>
/// Exposes Minimal API endpoints for connector marketplace discovery, installation, and order transformation.
/// </summary>
public class ConnectorMarketplaceEndpoints : IEndpointModule
{
    public ConnectorMarketplaceEndpoints()
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/connectors/marketplace")
            .WithTags("Connector Marketplace & Manifest SDK");

        // 1. GET /api/v1/connectors/marketplace
        group.MapGet("", (IConnectorMarketplaceService service) =>
        {
            var manifests = service.GetAvailableManifests();
            return Results.Ok(manifests);
        })
        .WithName("GetAvailableManifests")
        .WithSummary("Retrieve all available e-commerce platform manifests in the marketplace");

        // 2. GET /api/v1/connectors/marketplace/installed
        group.MapGet("/installed", (
            [FromQuery] Guid? tenantId,
            HttpContext context,
            IConnectorMarketplaceService service) =>
        {
            var effectiveTenantId = tenantId 
                ?? (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tidHeader) && Guid.TryParse(tidHeader, out var parsedTid) ? parsedTid : Guid.Empty);

            var installed = service.GetInstalledConnectors(effectiveTenantId);
            return Results.Ok(installed);
        })
        .WithName("GetInstalledConnectors")
        .WithSummary("List all installed connectors for the requesting tenant");

        // 3. GET /api/v1/connectors/marketplace/{connectorId}
        group.MapGet("/{connectorId}", (string connectorId, IConnectorMarketplaceService service) =>
        {
            var manifest = service.GetManifest(connectorId);
            return manifest is not null 
                ? Results.Ok(manifest) 
                : Results.NotFound(new { error = $"Connector manifest '{connectorId}' was not found." });
        })
        .WithName("GetConnectorManifest")
        .WithSummary("Retrieve connector manifest specifications and webhook capabilities by connector ID");

        // 4. POST /api/v1/connectors/marketplace/install
        group.MapPost("/install", (
            [FromQuery] Guid? tenantId,
            HttpContext context,
            [FromBody] ConnectorInstallRequest request,
            IConnectorMarketplaceService service) =>
        {
            var effectiveTenantId = tenantId 
                ?? (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tidHeader) && Guid.TryParse(tidHeader, out var parsedTid) ? parsedTid : Guid.NewGuid());

            var installed = service.InstallConnector(effectiveTenantId, request);
            return Results.Ok(installed);
        })
        .WithName("InstallConnector")
        .WithSummary("Install and configure a marketplace connector with credentials and custom field mappings");

        // 5. POST /api/v1/connectors/marketplace/{connectorId}/transform-order
        group.MapPost("/{connectorId}/transform-order", async (
            string connectorId,
            HttpContext context,
            IConnectorMarketplaceService service) =>
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var rawBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Results.BadRequest(new TransformPayloadResult(false, "Order", null, "Request body cannot be empty."));
            }

            string payloadToTransform = rawBody;
            Dictionary<string, string>? customMappings = null;

            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("rawJsonPayload", out var rawProp) && rawProp.ValueKind == JsonValueKind.String)
                    {
                        payloadToTransform = rawProp.GetString() ?? rawBody;
                        if (doc.RootElement.TryGetProperty("fieldMappings", out var mapProp) && mapProp.ValueKind == JsonValueKind.Object)
                        {
                            customMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(mapProp.GetRawText());
                        }
                    }
                }
            }
            catch
            {
                // Raw payload isn't wrapper JSON, process as direct raw body
            }

            var result = service.TransformOrderPayload(connectorId, payloadToTransform, customMappings);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("TransformOrderPayload")
        .WithSummary("Transform external e-commerce provider order payload into canonical SGrowing format");
    }
}
