using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Channels.WhatsApp;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Billing.Invoicing;

public record StripeWebhookPayload(
    string Id,
    string Type,
    StripeDataObject? Data
);

public record StripeDataObject(
    StripePaymentObject? Object
);

public record StripePaymentObject(
    string Id,
    long? Amount,
    string? Currency,
    string? Status,
    Dictionary<string, string>? Metadata
);

public record FlouciWebhookPayload(
    string PaymentId,
    string Status,
    decimal Amount,
    string? OrderId,
    string? DeveloperTrackingId
);

public record KonnectWebhookPayload(
    string PaymentRef,
    string Status,
    decimal Amount,
    string? OrderId
);

public static class PaymentWebhookEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static IEndpointRouteBuilder MapPaymentWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/billing/webhooks")
            .WithTags("Payment Webhooks & Automated Reconciliation (Stripe, Flouci, Konnect)");

        // 1. Stripe Payment Webhook
        group.MapPost("/stripe", async (
            [FromBody] StripeWebhookPayload payload,
            [FromHeader(Name = "Stripe-Signature")] string? signature,
            SGrowingDbContext db,
            IInvoiceService invoiceService,
            IWhatsAppGatewayService whatsappGateway,
            CancellationToken ct) =>
        {
            var paymentIntent = payload.Data?.Object;
            if (paymentIntent == null) return Results.BadRequest(new { error = "Missing payment intent payload" });

            if (payload.Type != "payment_intent.succeeded" && paymentIntent.Status != "succeeded")
            {
                return Results.Ok(new { status = "ignored", reason = $"Unhandled event type {payload.Type}" });
            }

            Guid targetInvoiceId = Guid.Empty;
            Guid tenantId = DefaultTenantId;

            if (paymentIntent.Metadata != null)
            {
                if (paymentIntent.Metadata.TryGetValue("invoice_id", out var invStr) && Guid.TryParse(invStr, out var parsedInv))
                    targetInvoiceId = parsedInv;
                if (paymentIntent.Metadata.TryGetValue("tenant_id", out var tenStr) && Guid.TryParse(tenStr, out var parsedTen))
                    tenantId = parsedTen;
            }

            var invoice = await db.Invoices
                .FirstOrDefaultAsync(i => i.Id == targetInvoiceId || i.InvoiceNumber == paymentIntent.Id, ct);

            if (invoice == null)
            {
                return Results.NotFound(new { error = "Invoice referenced in webhook metadata not found" });
            }

            decimal paidAmount = (paymentIntent.Amount ?? (long)(invoice.TotalAmount * 100)) / 100m;

            var payment = await invoiceService.RecordPaymentAsync(
                invoice.TenantId,
                invoice.Id,
                new RecordPaymentRequest(
                    Amount: paidAmount,
                    PaymentMethod: "Stripe",
                    TransactionReference: paymentIntent.Id
                ),
                ct
            );

            return Results.Ok(new
            {
                status = "success",
                reconciled = true,
                invoiceNumber = invoice.InvoiceNumber,
                invoiceStatus = "Paid",
                amount = paidAmount,
                gateway = "Stripe",
                transactionId = paymentIntent.Id
            });
        })
        .WithName("StripePaymentWebhook")
        .WithSummary("Ingest Stripe payment_intent.succeeded webhooks and reconcile invoice status");

        // 2. Flouci Payment Webhook (North Africa / Tunisia E-Payment)
        group.MapPost("/flouci", async (
            [FromBody] FlouciWebhookPayload payload,
            SGrowingDbContext db,
            IInvoiceService invoiceService,
            CancellationToken ct) =>
        {
            if (!string.Equals(payload.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Ok(new { status = "ignored", reason = $"Flouci payment status '{payload.Status}' is not SUCCESS" });
            }

            Guid targetInvoiceId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(payload.OrderId) && Guid.TryParse(payload.OrderId, out var parsedId))
            {
                targetInvoiceId = parsedId;
            }

            var invoice = await db.Invoices
                .FirstOrDefaultAsync(i => i.Id == targetInvoiceId || i.InvoiceNumber == payload.OrderId || i.InvoiceNumber == payload.DeveloperTrackingId, ct);

            if (invoice == null)
            {
                return Results.NotFound(new { error = $"Invoice '{payload.OrderId}' not found" });
            }

            var payment = await invoiceService.RecordPaymentAsync(
                invoice.TenantId,
                invoice.Id,
                new RecordPaymentRequest(
                    Amount: payload.Amount > 0 ? payload.Amount : invoice.TotalAmount,
                    PaymentMethod: "Flouci",
                    TransactionReference: payload.PaymentId
                ),
                ct
            );

            return Results.Ok(new
            {
                status = "success",
                reconciled = true,
                invoiceNumber = invoice.InvoiceNumber,
                invoiceStatus = "Paid",
                amount = payload.Amount,
                gateway = "Flouci",
                transactionId = payload.PaymentId
            });
        })
        .WithName("FlouciPaymentWebhook")
        .WithSummary("Ingest Flouci instant mobile wallet payment webhooks and reconcile invoices");

        // 3. Konnect Payment Webhook
        group.MapPost("/konnect", async (
            [FromBody] KonnectWebhookPayload payload,
            SGrowingDbContext db,
            IInvoiceService invoiceService,
            CancellationToken ct) =>
        {
            if (!string.Equals(payload.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Ok(new { status = "ignored", reason = $"Konnect status '{payload.Status}' is not completed" });
            }

            Guid targetInvoiceId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(payload.OrderId) && Guid.TryParse(payload.OrderId, out var parsedId))
            {
                targetInvoiceId = parsedId;
            }

            var invoice = await db.Invoices
                .FirstOrDefaultAsync(i => i.Id == targetInvoiceId || i.InvoiceNumber == payload.OrderId, ct);

            if (invoice == null)
            {
                return Results.NotFound(new { error = $"Invoice '{payload.OrderId}' not found" });
            }

            var payment = await invoiceService.RecordPaymentAsync(
                invoice.TenantId,
                invoice.Id,
                new RecordPaymentRequest(
                    Amount: payload.Amount > 0 ? payload.Amount : invoice.TotalAmount,
                    PaymentMethod: "Konnect",
                    TransactionReference: payload.PaymentRef
                ),
                ct
            );

            return Results.Ok(new
            {
                status = "success",
                reconciled = true,
                invoiceNumber = invoice.InvoiceNumber,
                invoiceStatus = "Paid",
                amount = payload.Amount,
                gateway = "Konnect",
                transactionId = payload.PaymentRef
            });
        })
        .WithName("KonnectPaymentWebhook")
        .WithSummary("Ingest Konnect fintech card and bank payment webhooks");

        return app;
    }
}
