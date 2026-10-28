using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Modules.Documents;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Billing.Invoicing;

public record ConvertCurrencyApiRequest(decimal Amount, string FromCurrency, string ToCurrency);
public record CreateInvoiceFromDealApiRequest(string Currency = "USD", decimal TaxPercentage = 19.0m);

public static class InvoiceEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/billing/invoices")
            .WithTags("Multi-Currency Enterprise Invoicing & Tax Ledger");

        group.MapGet("/", async (
            [FromQuery] InvoiceStatus? status,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IInvoiceService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var invoices = await service.GetInvoicesAsync(effectiveTenantId, status, ct);
            return Results.Ok(invoices);
        })
        .WithName("GetInvoices")
        .WithSummary("List tenant enterprise invoices filtered by status");

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IInvoiceService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var invoice = await service.GetInvoiceByIdAsync(effectiveTenantId, id, ct);
            if (invoice == null) return Results.NotFound(new { error = $"Invoice '{id}' not found" });
            return Results.Ok(invoice);
        })
        .WithName("GetInvoiceById")
        .WithSummary("Retrieve detailed invoice record with itemized line items and tax breakdown");

        group.MapGet("/{id:guid}/pdf", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IInvoiceService service,
            IDocumentPdfGeneratorService pdfService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var invoice = await service.GetInvoiceByIdAsync(effectiveTenantId, id, ct);
            if (invoice == null) return Results.NotFound(new { error = $"Invoice '{id}' not found" });

            var items = invoice.LineItems.Select(li => new InvoiceLineItemDto(
                Description: li.Description,
                Quantity: li.Quantity,
                UnitPrice: li.UnitPrice,
                TotalPrice: li.TotalPrice
            )).ToList();

            if (!items.Any())
            {
                items.Add(new InvoiceLineItemDto("Enterprise Services & SaaS Subscription", 1, invoice.SubTotal, invoice.SubTotal));
            }

            var pdfBytes = pdfService.GenerateInvoicePdf(
                invoiceId: invoice.Id,
                invoiceNumber: invoice.InvoiceNumber,
                sellerName: "SGrowing Enterprise Technologies SARL",
                buyerName: invoice.ClientName,
                subtotal: invoice.SubTotal,
                taxAmount: invoice.TaxAmount,
                totalAmount: invoice.TotalAmount,
                currency: invoice.Currency,
                items: items
            );

            return Results.File(pdfBytes, "application/pdf", $"Invoice-{invoice.InvoiceNumber}.pdf");
        })
        .WithName("DownloadInvoicePdf")
        .WithSummary("Generate and stream compliant cryptographic PDF invoice with 19% VAT breakdown");

        group.MapPost("/", async (
            [FromBody] CreateInvoiceRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IInvoiceService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var invoice = await service.CreateInvoiceAsync(effectiveTenantId, req, ct);
            return Results.Created($"/api/v1/billing/invoices/{invoice.Id}", invoice);
        })
        .WithName("CreateInvoice")
        .WithSummary("Create custom itemized B2B invoice with tax calculation and currency conversion");

        group.MapPost("/from-deal/{dealId:guid}", async (
            Guid dealId,
            [FromBody] CreateInvoiceFromDealApiRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IInvoiceService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var invoice = await service.CreateInvoiceFromDealAsync(effectiveTenantId, dealId, req.Currency, req.TaxPercentage, ct);
            if (invoice == null) return Results.NotFound(new { error = $"Deal '{dealId}' not found for tenant" });
            return Results.Created($"/api/v1/billing/invoices/{invoice.Id}", invoice);
        })
        .WithName("CreateInvoiceFromDeal")
        .WithSummary("Autonomous 1-click B2B invoice generation from won CRM deal");

        group.MapPost("/{id:guid}/payments", async (
            Guid id,
            [FromBody] RecordPaymentRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IInvoiceService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var payment = await service.RecordPaymentAsync(effectiveTenantId, id, req, ct);
            if (payment == null) return Results.NotFound(new { error = $"Invoice '{id}' not found" });
            return Results.Ok(payment);
        })
        .WithName("RecordInvoicePayment")
        .WithSummary("Record partial or full payment transaction against an invoice");

        group.MapGet("/rates", async (
            IInvoiceService service,
            CancellationToken ct) =>
        {
            var rates = await service.GetCurrencyRatesAsync(ct);
            return Results.Ok(rates);
        })
        .WithName("GetCurrencyRates")
        .WithSummary("Retrieve live multi-currency exchange rates (USD, EUR, TND, MAD, EGP)");

        group.MapPost("/convert", async (
            [FromBody] ConvertCurrencyApiRequest req,
            IInvoiceService service,
            CancellationToken ct) =>
        {
            var result = await service.ConvertCurrencyAsync(req.Amount, req.FromCurrency, req.ToCurrency, ct);
            return Results.Ok(result);
        })
        .WithName("ConvertCurrency")
        .WithSummary("Perform real-time multi-currency conversion calculation with effective rate");

        return app;
    }
}
