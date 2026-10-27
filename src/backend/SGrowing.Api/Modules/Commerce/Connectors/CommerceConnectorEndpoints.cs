using System.IO;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.Connectors;

public record ConfigureConnectorRequest(
    Guid TenantId,
    string Platform,
    string StoreDomain,
    string WebhookSecret,
    string AccessToken,
    bool IsActive = true
);

public record TriggerSyncRequest(
    Guid TenantId,
    string? Platform = null
);

public record CommerceConnectorsStatusResponse(
    ConnectorStatusDto Shopify,
    ConnectorStatusDto WooCommerce,
    DateTime TimestampUtc
);

public static class CommerceConnectorEndpoints
{
    public static IEndpointRouteBuilder MapCommerceConnectorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/connectors")
            .WithTags("E-Commerce Connectors (Shopify & WooCommerce)");

        // 1. Shopify Webhook Ingestion
        group.MapPost("/shopify/webhook", async (
            HttpContext context,
            [FromQuery] Guid? tenantId,
            IShopifyConnectorService shopifyService,
            SGrowingDbContext db,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId 
                ?? (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tidHeader) && Guid.TryParse(tidHeader, out var parsedTid) ? parsedTid : Guid.Empty);

            if (effectiveTenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Valid tenant ID is required via query string (?tenantId=) or X-Tenant-Id header." });
            }

            var hmacHeader = context.Request.Headers["X-Shopify-Hmac-Sha256"].ToString();
            var topic = context.Request.Headers["X-Shopify-Topic"].ToString();
            if (string.IsNullOrEmpty(topic)) topic = "orders/create";

            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var rawBody = await reader.ReadToEndAsync(ct);

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Results.BadRequest(new { error = "Request body cannot be empty" });
            }

            // Look up registered webhook connection for tenant
            var connection = await db.StorefrontConnections
                .FirstOrDefaultAsync(s => s.TenantId == effectiveTenantId && s.Platform == StorefrontPlatform.Shopify, ct);

            if (connection == null || !connection.IsActive)
            {
                return Results.NotFound(new { error = "Shopify storefront connection not found or inactive for tenant" });
            }

            // If a secret is configured, require and verify HMAC signature strictly
            var secret = connection.WebhookSecret;
            if (!string.IsNullOrEmpty(secret))
            {
                if (string.IsNullOrEmpty(hmacHeader) || !shopifyService.VerifyWebhookSignature(rawBody, secret, hmacHeader))
                {
                    return Results.Unauthorized();
                }
            }

            var result = await shopifyService.ProcessOrderWebhookAsync(effectiveTenantId, topic, rawBody, ct);
            return Results.Ok(result);
        })
        .WithName("IngestShopifyWebhook")
        .WithSummary("Ingest real-time orders from Shopify webhooks and map into CRM Deals & COD Shipments");

        // 2. WooCommerce Webhook Ingestion
        group.MapPost("/woocommerce/webhook", async (
            HttpContext context,
            [FromQuery] Guid? tenantId,
            IWooCommerceConnectorService wooService,
            SGrowingDbContext db,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId 
                ?? (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tidHeader) && Guid.TryParse(tidHeader, out var parsedTid) ? parsedTid : Guid.Empty);

            if (effectiveTenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Valid tenant ID is required via query string (?tenantId=) or X-Tenant-Id header." });
            }

            var signatureHeader = context.Request.Headers["X-WC-Webhook-Signature"].ToString();
            var eventName = context.Request.Headers["X-WC-Webhook-Event"].ToString();
            if (string.IsNullOrEmpty(eventName))
            {
                eventName = context.Request.Headers["X-WC-Webhook-Topic"].ToString();
            }
            if (string.IsNullOrEmpty(eventName)) eventName = "order.created";

            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var rawBody = await reader.ReadToEndAsync(ct);

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Results.BadRequest(new { error = "Request body cannot be empty" });
            }

            // Look up registered webhook connection for tenant
            var connection = await db.StorefrontConnections
                .FirstOrDefaultAsync(s => s.TenantId == effectiveTenantId && s.Platform == StorefrontPlatform.WooCommerce, ct);

            if (connection == null || !connection.IsActive)
            {
                return Results.NotFound(new { error = "WooCommerce storefront connection not found or inactive for tenant" });
            }

            var secret = connection.WebhookSecret;
            if (!string.IsNullOrEmpty(secret))
            {
                if (string.IsNullOrEmpty(signatureHeader) || !wooService.VerifyWebhookSignature(rawBody, secret, signatureHeader))
                {
                    return Results.Unauthorized();
                }
            }

            var result = await wooService.ProcessOrderWebhookAsync(effectiveTenantId, eventName, rawBody, ct);
            return Results.Ok(result);
        })
        .WithName("IngestWooCommerceWebhook")
        .WithSummary("Ingest real-time orders from WooCommerce webhooks and map into CRM Deals & COD Shipments");

        // 3. Connectors Status
        group.MapGet("/status", async (
            [FromQuery] Guid? tenantId,
            IShopifyConnectorService shopifyService,
            IWooCommerceConnectorService wooService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? Guid.Empty;
            var shopifyStatus = await shopifyService.GetConnectorStatusAsync(effectiveTenantId, ct);
            var wooStatus = await wooService.GetConnectorStatusAsync(effectiveTenantId, ct);

            return Results.Ok(new CommerceConnectorsStatusResponse(
                Shopify: shopifyStatus,
                WooCommerce: wooStatus,
                TimestampUtc: DateTime.UtcNow
            ));
        })
        .WithName("GetCommerceConnectorsStatus")
        .WithSummary("Retrieve live synchronization status and metrics for Shopify and WooCommerce");

        // 4. Configure Connector
        group.MapPost("/configure", async (
            [FromBody] ConfigureConnectorRequest req,
            SGrowingDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.StoreDomain))
            {
                return Results.BadRequest(new { error = "Store domain is required" });
            }

            var platform = req.Platform.Equals("WooCommerce", StringComparison.OrdinalIgnoreCase)
                ? StorefrontPlatform.WooCommerce
                : StorefrontPlatform.Shopify;

            var connection = await db.StorefrontConnections
                .FirstOrDefaultAsync(s => s.TenantId == req.TenantId && s.Platform == platform, ct);

            if (connection == null)
            {
                connection = StorefrontConnection.Create(
                    tenantId: req.TenantId,
                    platform: platform,
                    storeDomain: req.StoreDomain,
                    webhookSecret: req.WebhookSecret ?? string.Empty,
                    accessToken: req.AccessToken ?? string.Empty
                );
                connection.IsActive = req.IsActive;
                await db.StorefrontConnections.AddAsync(connection, ct);
            }
            else
            {
                connection.StoreDomain = req.StoreDomain.Trim().ToLowerInvariant();
                connection.WebhookSecret = req.WebhookSecret ?? connection.WebhookSecret;
                connection.AccessToken = req.AccessToken ?? connection.AccessToken;
                connection.IsActive = req.IsActive;
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                success = true,
                platform = platform.ToString(),
                storeDomain = connection.StoreDomain,
                isActive = connection.IsActive
            });
        })
        .WithName("ConfigureCommerceConnector")
        .WithSummary("Configure store domain, API token, and webhook secret for Shopify or WooCommerce");

        // 5. Trigger Catalog Sync
        group.MapPost("/sync", async (
            [FromBody] TriggerSyncRequest req,
            IShopifyConnectorService shopifyService,
            IWooCommerceConnectorService wooService,
            CancellationToken ct) =>
        {
            var results = new List<CatalogSyncResult>();

            if (string.IsNullOrEmpty(req.Platform) || req.Platform.Equals("all", StringComparison.OrdinalIgnoreCase) || req.Platform.Equals("shopify", StringComparison.OrdinalIgnoreCase))
            {
                var sfyRes = await shopifyService.TriggerCatalogSyncAsync(req.TenantId, ct);
                results.Add(sfyRes);
            }

            if (string.IsNullOrEmpty(req.Platform) || req.Platform.Equals("all", StringComparison.OrdinalIgnoreCase) || req.Platform.Equals("woocommerce", StringComparison.OrdinalIgnoreCase))
            {
                var wcRes = await wooService.TriggerCatalogSyncAsync(req.TenantId, ct);
                results.Add(wcRes);
            }

            return Results.Ok(new
            {
                success = true,
                results
            });
        })
        .WithName("TriggerCommerceCatalogSync")
        .WithSummary("Trigger on-demand catalog and inventory synchronization from connected storefronts");

        return app;
    }
}
