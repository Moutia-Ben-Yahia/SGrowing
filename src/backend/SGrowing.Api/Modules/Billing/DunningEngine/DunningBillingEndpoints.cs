using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Billing.DunningEngine;

public static class DunningBillingEndpoints
{
    public static IServiceCollection AddDunningBillingServices(this IServiceCollection services)
    {
        services.AddSingleton<IDunningBillingEngine, DunningBillingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDunningBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/billing/dunning")
            .WithTags("Domain G1 & G2: Global Multi-Currency Tax, Recurring Invoicing & Smart Dunning Recovery");

        // 1. Calculate Tax
        group.MapGet("/tax-calculate", (
            [FromQuery] decimal subtotal,
            [FromQuery] string countryCode,
            [FromQuery] string? vatNumber,
            IDunningBillingEngine engine) =>
        {
            var result = engine.CalculateTax(subtotal, countryCode, vatNumber);
            return Results.Ok(result);
        })
        .WithName("CalculateGlobalTax")
        .WithSummary("Calculates real-time sales tax or EU VAT reverse charge exemption.");

        // 2. Generate Invoice
        group.MapPost("/invoices", (
            [FromBody] CreateInvoiceRequest request,
            IDunningBillingEngine engine) =>
        {
            var invoice = engine.GenerateInvoice(request);
            return Results.Created($"/api/v1/billing/dunning/invoices/{invoice.TenantId}/{invoice.InvoiceId}", invoice);
        })
        .WithName("GenerateBillingInvoice")
        .WithSummary("Generates an immutable invoice with SHA-256 cryptographic digital seal.");

        // 3. List Invoices
        group.MapGet("/invoices/{tenantId}", (
            string tenantId,
            IDunningBillingEngine engine) =>
        {
            var invoices = engine.ListInvoices(tenantId);
            return Results.Ok(invoices);
        })
        .WithName("ListBillingInvoices")
        .WithSummary("Lists all subscription invoices and their dunning retry status.");

        // 4. Process Dunning Retry
        group.MapPost("/retry", (
            [FromBody] ProcessDunningRetryRequest request,
            IDunningBillingEngine engine) =>
        {
            try
            {
                var summary = engine.ProcessDunningRetry(request);
                return Results.Ok(summary);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ProcessDunningPaymentRetry")
        .WithSummary("Executes a progressive dunning retry step with graceful downgrade policies.");

        // 5. Get Dunning Audit Log
        group.MapGet("/invoices/{invoiceId}/logs", (
            string invoiceId,
            IDunningBillingEngine engine) =>
        {
            var logs = engine.GetDunningAuditLog(invoiceId);
            return Results.Ok(logs);
        })
        .WithName("GetDunningAuditLog")
        .WithSummary("Retrieves immutable audit logs of all dunning attempts and notifications.");

        return endpoints;
    }
}
