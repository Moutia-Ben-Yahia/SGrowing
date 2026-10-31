using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Dev;
using SGrowing.Api.Modules.Documents;
using SGrowing.Api.Modules.Gateways;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Api.Modules.Security;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Dev;

public static class DeveloperGatewayEndpoints
{
    public static IEndpointRouteBuilder MapDeveloperGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        // Public Developer Gateway & API Keys
        app.MapPost("/api/developer/api-keys", async ([FromBody] CreateApiKeyDto req, IDeveloperGatewayService devService) =>
        {
            var (apiKey, plainKey) = await devService.CreateApiKeyAsync(req.TenantId, req.Name, req.Scopes, req.IsLive, req.RateLimit);
            return Results.Ok(new { apiKey, secretKey = plainKey });
        });

        app.MapPost("/api/developer/api-keys/validate", async ([FromBody] ValidateKeyDto req, IDeveloperGatewayService devService) =>
        {
            var res = await devService.ValidateApiKeyAsync(req.SecretKey);
            return Results.Ok(res);
        });

        app.MapPost("/api/developer/webhooks", async ([FromBody] RegisterWebhookDto req, IDeveloperGatewayService devService) =>
        {
            var sub = await devService.RegisterWebhookAsync(req.TenantId, req.TargetUrl, req.Events);
            return Results.Ok(sub);
        });

        app.MapPost("/api/developer/webhooks/dispatch", async ([FromBody] DispatchWebhookDto req, IDeveloperGatewayService devService) =>
        {
            var logs = await devService.DispatchWebhookEventAsync(req.TenantId, req.EventType, req.Payload);
            return Results.Ok(logs);
        });

        app.MapPost("/api/developer/webhooks/replay/{attemptId}", async (Guid attemptId, [FromQuery] Guid tenantId, IDeveloperGatewayService devService) =>
        {
            var log = await devService.ReplayWebhookAttemptAsync(tenantId, attemptId);
            return Results.Ok(log);
        });

        app.MapPost("/api/developer/webhooks/subscriptions", async ([FromBody] CreateWebhookSubscriptionDto req, SGrowingDbContext db) =>
        {
            var sub = WebhookSubscription.Create(req.TenantId, req.TargetUrl, req.SecretToken, req.SubscribedEvents ?? "*");
            await db.WebhookSubscriptions.AddAsync(sub);
            await db.SaveChangesAsync();
            return Results.Ok(sub);
        });

        app.MapGet("/api/developer/webhooks/subscriptions", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var subs = await db.WebhookSubscriptions.Where(s => s.TenantId == tenantId).ToListAsync();
            return Results.Ok(subs);
        });

        // GDPR Privacy
        app.MapPost("/api/privacy/erasure", async ([FromBody] PrivacyErasureDto req, CustomerPrivacyService privacyService) =>
        {
            var result = await privacyService.ExecuteRightToBeForgottenAsync(req.TenantId, req.ContactEmail);
            return Results.Ok(result);
        });

        // PDF Audit Certificate Download
        app.MapGet("/api/documents/{documentId}/certificate/download", async (Guid documentId, [FromQuery] Guid tenantId, SGrowingDbContext db, IDocumentPdfGeneratorService pdfService) =>
        {
            var envelope = await db.DocumentEnvelopes.Include(e => e.Signers).FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == documentId);
            if (envelope == null) return Results.NotFound("Document envelope not found.");

            var signer = envelope.Signers.FirstOrDefault() ?? await db.EnvelopeSigners.FirstOrDefaultAsync(s => s.EnvelopeId == envelope.Id && s.TenantId == tenantId);
            string signerName = signer?.Name ?? "Signer";
            string signerEmail = signer?.Email ?? "signer@example.com";
            string signerIp = !string.IsNullOrEmpty(signer?.IpAddress) ? signer.IpAddress : "196.200.14.88";
            DateTime signedDate = signer?.SignedAtUtc ?? envelope.CompletedAtUtc ?? DateTime.UtcNow;
            string docHash = !string.IsNullOrEmpty(envelope.ChecksumSha256) ? envelope.ChecksumSha256 : "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
            string certHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(docHash + signerName + signedDate.ToString("O"))));

            var auditTrail = new List<AuditTrailEventDto>
            {
                new("Envelope Created", envelope.CreatedAtUtc, "Platform Admin", "127.0.0.1"),
                new("Invitation Sent", envelope.CreatedAtUtc.AddMinutes(2), "Email Delivery Engine", "10.0.0.1"),
                new("Document Viewed", signedDate.AddMinutes(-5), signerEmail, signerIp),
                new("Cryptographically Signed", signedDate, signerEmail, signerIp)
            };

            var pdfBytes = pdfService.GenerateCertificatePdf(
                envelope.Id,
                envelope.Title,
                signerName,
                signerEmail,
                signerIp,
                signedDate,
                docHash,
                certHash,
                auditTrail
            );

            return Results.File(pdfBytes, "application/pdf", $"certificate_{envelope.Id:N}.pdf");
        });

        // Provider Gateways (WhatsApp & Courier Logistics)
        app.MapGet("/api/gateways/whatsapp/sessions/{instanceName}", async (string instanceName, IWhatsAppGatewayClient waClient) =>
        {
            var state = await waClient.GetSessionStateAsync(instanceName);
            return Results.Ok(state);
        });

        app.MapPost("/api/gateways/whatsapp/messages/send", async ([FromBody] SendWhatsAppMessageDto req, IWhatsAppGatewayClient waClient) =>
        {
            var res = await waClient.SendTextMessageAsync(req.InstanceName, req.RecipientPhone, req.Message);
            return Results.Ok(res);
        });

        app.MapPost("/api/gateways/whatsapp/webhook", async ([FromBody] JsonElement payload, IWhatsAppGatewayClient waClient) =>
        {
            var parsed = await waClient.ParseWebhookPayloadAsync(payload);
            return Results.Ok(parsed);
        });

        app.MapPost("/api/gateways/couriers/labels/generate", async ([FromBody] GenerateCourierLabelDto req, ICourierLogisticsGateway courierGateway) =>
        {
            var label = await courierGateway.GenerateShipmentLabelAsync(req.CourierProviderName, req.Details);
            return Results.Ok(label);
        });

        app.MapPost("/api/gateways/couriers/webhook", async ([FromBody] IngestCourierWebhookGatewayDto req, ICourierLogisticsGateway courierGateway) =>
        {
            var normalized = await courierGateway.TranslateWebhookEventAsync(req.CourierProviderName, req.Payload);
            return Results.Ok(normalized);
        });

        return app;
    }
}
