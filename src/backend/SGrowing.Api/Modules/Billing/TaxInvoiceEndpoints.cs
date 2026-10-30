using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Billing;

public static class TaxInvoiceEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapTaxInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/billing/tax-invoicing")
            .WithTags("Tax & Multi-Currency Invoice Ledger");

        // 1. Evaluate Tax
        group.MapPost("/evaluate-tax", async (
            [FromBody] TaxEvaluationRequest request,
            ITaxAndInvoiceLedgerService service,
            CancellationToken ct) =>
        {
            var result = await service.EvaluateTaxAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("EvaluateTax")
        .WithSummary("Evaluate applicable VAT/tax rate, EU reverse charge eligibility, and VIES validity");

        // 2. Currencies and Spot Rates
        group.MapGet("/currencies", (ITaxAndInvoiceLedgerService service) =>
        {
            var currencies = service.GetCurrencies();
            var response = currencies.Select(c => new
            {
                c.Code,
                c.CurrencyName,
                c.Symbol,
                c.SpotRateToUsd,
                c.SpreadMarginPct,
                EffectiveRateToUsd = service.CalculateEffectiveRate(c.SpotRateToUsd, c.SpreadMarginPct),
                c.LastUpdated
            });
            return Results.Ok(response);
        })
        .WithName("GetTaxCurrencies")
        .WithSummary("List active currency spot rates, spread margins, and effective FX exchange rates");

        // 3. List Invoices with Filters
        group.MapGet("/invoices", async (
            [FromQuery] string? status,
            [FromQuery] string? currency,
            [FromQuery] string? customer,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ITaxAndInvoiceLedgerService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var invoices = await service.GetInvoicesAsync(effectiveTenantId, status, currency, customer, startDate, endDate, ct);
            return Results.Ok(invoices);
        })
        .WithName("GetCommercialInvoices")
        .WithSummary("List commercial invoices with multi-currency filter, status, and jurisdiction");

        // 4. Get Invoice by ID (with ZATCA QR and Factur-X XML)
        group.MapGet("/invoices/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ITaxAndInvoiceLedgerService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var invoice = await service.GetInvoiceByIdAsync(effectiveTenantId, id, ct);
            if (invoice == null)
            {
                return Results.NotFound(new { message = $"Invoice '{id}' not found." });
            }
            return Results.Ok(invoice);
        })
        .WithName("GetCommercialInvoiceById")
        .WithSummary("Retrieve detailed invoice record with itemized line items, ZATCA Phase 2 QR, and Factur-X payload");

        // 5. Issue Commercial Invoice
        group.MapPost("/invoices", async (
            [FromBody] CreateCommercialInvoiceRequest request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ITaxAndInvoiceLedgerService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var invoice = await service.IssueInvoiceAsync(effectiveTenantId, request, ct);
            return Results.Created($"/api/v1/billing/tax-invoicing/invoices/{invoice.Id}", invoice);
        })
        .WithName("IssueCommercialInvoice")
        .WithSummary("Issue a compliant commercial invoice with automated tax determination and numbering");

        // 6. Issue Credit Note
        group.MapPost("/invoices/{id:guid}/credit-note", async (
            Guid id,
            [FromBody] IssueCreditNoteRequest request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ITaxAndInvoiceLedgerService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            try
            {
                var creditNote = await service.IssueCreditNoteAsync(effectiveTenantId, id, request, ct);
                return Results.Created($"/api/v1/billing/tax-invoicing/credit-notes?invoiceId={id}", creditNote);
            }
            catch (KeyNotFoundException knf)
            {
                return Results.NotFound(new { message = knf.Message });
            }
            catch (InvalidOperationException ioe)
            {
                return Results.BadRequest(new { message = ioe.Message });
            }
            catch (ArgumentException ae)
            {
                return Results.BadRequest(new { message = ae.Message });
            }
        })
        .WithName("IssueInvoiceCreditNote")
        .WithSummary("Issue partial or full credit note against an invoice, adjusting remaining balance and ledger tax");

        // 7. List Credit Notes
        group.MapGet("/credit-notes", async (
            [FromQuery] Guid? invoiceId,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ITaxAndInvoiceLedgerService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var creditNotes = await service.GetCreditNotesAsync(effectiveTenantId, invoiceId, ct);
            return Results.Ok(creditNotes);
        })
        .WithName("GetCreditNotes")
        .WithSummary("List all issued credit notes linked to original invoice records");

        // 8. Tax Liability Summary
        group.MapGet("/tax-liability-summary", async (
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ITaxAndInvoiceLedgerService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var summary = await service.GetTaxLiabilitySummaryAsync(effectiveTenantId, ct);
            return Results.Ok(summary);
        })
        .WithName("GetTaxLiabilitySummary")
        .WithSummary("Aggregated tax liability report grouped by tax jurisdiction and transaction currency");

        return app;
    }
}
