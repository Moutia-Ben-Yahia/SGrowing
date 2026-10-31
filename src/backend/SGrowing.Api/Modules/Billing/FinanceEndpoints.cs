using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Analytics;
using SGrowing.Api.Modules.Attribution;
using SGrowing.Api.Modules.Billing;
using SGrowing.Api.Modules.Documents;
using SGrowing.Api.Modules.Finance;
using SGrowing.Api.Modules.Tenancy;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Billing;

public static class FinanceEndpoints
{
    public static IEndpointRouteBuilder MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        // Revenue Attribution & Executive KPIs
        app.MapPost("/api/analytics/touchpoints", async ([FromBody] RecordTouchpointDto req, MultiTouchAttributionService attribution) =>
        {
            var tp = await attribution.RecordTouchpointAsync(req.TenantId, req.ContactId, req.Channel, req.CampaignName, req.Stage, req.DealId, req.Cost, req.UtmSource, req.UtmMedium, req.UtmCampaign);
            return Results.Ok(tp);
        });

        app.MapPost("/api/analytics/attribution/calculate", async ([FromBody] CalculateAttributionDto req, MultiTouchAttributionService attribution) =>
        {
            var results = await attribution.CalculateAttributionAsync(req.TenantId, req.DealId, req.Model);
            return Results.Ok(results);
        });

        app.MapGet("/api/analytics/kpis", async ([FromQuery] Guid tenantId, MultiTouchAttributionService attribution) =>
        {
            var kpis = await attribution.CalculateExecutiveKpisAsync(tenantId);
            return Results.Ok(kpis);
        });

        // SaaS Usage Metering & Plan Entitlements
        app.MapGet("/api/saas/usage/summary", async ([FromQuery] Guid tenantId, TenantUsageMeteringService metering) =>
        {
            var summary = await metering.GetUsageSummaryAsync(tenantId);
            return Results.Ok(summary);
        });

        app.MapPost("/api/saas/usage/record", async ([FromBody] RecordUsageDto req, TenantUsageMeteringService metering) =>
        {
            var record = await metering.RecordUsageAsync(req.TenantId, req.Metric, req.Quantity, req.Metadata);
            return Results.Ok(record);
        });

        app.MapPost("/api/saas/entitlements/check", async ([FromBody] CheckEntitlementDto req, TenantUsageMeteringService metering) =>
        {
            var check = await metering.CheckEntitlementAsync(req.TenantId, req.Metric, req.IncomingQuantity);
            return Results.Ok(check);
        });

        // Treasury Cash Float & Forecasting
        app.MapGet("/api/finance/treasury/float", async ([FromQuery] Guid tenantId, ITreasuryFloatService treasuryService) =>
        {
            var valuation = await treasuryService.CalculateFloatValuationAsync(tenantId);
            return Results.Ok(valuation);
        });

        app.MapGet("/api/finance/treasury/forecast-14day", async ([FromQuery] Guid tenantId, ITreasuryFloatService treasuryService) =>
        {
            var forecast = await treasuryService.Generate14DayCashFlowForecastAsync(tenantId, DateTime.UtcNow);
            return Results.Ok(forecast);
        });

        // PDF Invoice Generation
        app.MapGet("/api/finance/invoices/{invoiceId}/download", async (Guid invoiceId, [FromQuery] Guid tenantId, SGrowingDbContext db, IDocumentPdfGeneratorService pdfService) =>
        {
            var order = await db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == invoiceId);
            string orderNum = order?.OrderNumber ?? $"INV-{invoiceId:N}".Substring(0, 12).ToUpper();
            string customer = order?.CustomerName ?? "Enterprise Client";
            decimal total = order?.TotalAmount ?? 149.99m;
            decimal subtotal = Math.Round(total / 1.2m, 2);
            decimal tax = total - subtotal;
            string currency = order?.CodCurrency ?? "USD";

            var items = new List<InvoiceLineItemDto>
            {
                new("Enterprise SaaS & Commerce Platform Services", 1, subtotal, subtotal)
            };

            var pdfBytes = pdfService.GenerateInvoicePdf(
                invoiceId,
                orderNum,
                "Inspire Global Commerce & SaaS",
                customer,
                subtotal,
                tax,
                total,
                currency,
                items
            );

            return Results.File(pdfBytes, "application/pdf", $"invoice_{orderNum}.pdf");
        });

        // Enterprise B2B Financials & Factoring
        app.MapPost("/api/billing/b2b-credit/accounts", async ([FromBody] EstablishCreditDto req, IB2BCreditAndFactoringService b2bService) =>
        {
            var account = await b2bService.EstablishCreditAccountAsync(req.TenantId, req.CompanyId, req.CompanyName, req.CreditLimit, req.PaymentTermsDays, req.CreditScoreRating ?? "AA");
            return Results.Ok(account);
        });

        app.MapPost("/api/billing/b2b-credit/invoice-drawdown", async ([FromBody] InvoiceDrawdownDto req, IB2BCreditAndFactoringService b2bService) =>
        {
            var account = await b2bService.RecordInvoiceDrawdownAsync(req.TenantId, req.CompanyId, req.InvoiceAmount);
            return Results.Ok(account);
        });

        app.MapPost("/api/billing/b2b-credit/settlement", async ([FromBody] PaymentSettlementDto req, IB2BCreditAndFactoringService b2bService) =>
        {
            var account = await b2bService.RecordPaymentSettlementAsync(req.TenantId, req.CompanyId, req.PaymentAmount);
            return Results.Ok(account);
        });

        app.MapPost("/api/billing/factoring/request", async ([FromBody] RequestFactoringDto req, IB2BCreditAndFactoringService b2bService) =>
        {
            var record = await b2bService.RequestCourierCashFactoringAsync(req.TenantId, req.CourierId, req.CourierName, req.InTransitFloat, req.DeliveredFloat, req.DiscountRate);
            return Results.Ok(record);
        });

        app.MapPost("/api/billing/factoring/fund/{factoringId}", async (Guid factoringId, [FromQuery] Guid tenantId, IB2BCreditAndFactoringService b2bService) =>
        {
            var funded = await b2bService.FundFactoringAdvanceAsync(tenantId, factoringId);
            return Results.Ok(funded);
        });

        app.MapGet("/api/billing/b2b-financials/summary", async ([FromQuery] Guid tenantId, IB2BCreditAndFactoringService b2bService) =>
        {
            var summary = await b2bService.GetFinancialSummaryAsync(tenantId);
            return Results.Ok(summary);
        });

        return app;
    }
}
