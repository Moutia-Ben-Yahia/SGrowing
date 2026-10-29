using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Sales;

public record RequestApprovalBody(string? Notes = null);

public static class CpqQuotationEndpoints
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapCpqQuotationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/cpq")
            .WithTags("CPQ & Margin-Guarded Quotation Engine");

        // 1. GET /price-books
        group.MapGet("/price-books", async (
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICpqQuotationService service) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var priceBooks = await service.GetPriceBooksAsync(effectiveTenant);
            return Results.Ok(priceBooks);
        })
        .WithName("ListPriceBooks")
        .WithSummary("List all price books and volume discount tier matrices for the tenant.");

        // 2. POST /price-books
        group.MapPost("/price-books", async (
            [FromBody] CreatePriceBookRequest request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICpqQuotationService service) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var priceBook = await service.CreatePriceBookAsync(effectiveTenant, request);
            return Results.Created($"/api/v1/sales/cpq/price-books/{priceBook.Id}", priceBook);
        })
        .WithName("CreatePriceBook")
        .WithSummary("Create a new enterprise price book with SKU entries and tiered volume breaks.");

        // 3. GET /quotes
        group.MapGet("/quotes", async (
            [FromQuery] CpqQuoteStatus? status,
            [FromQuery] Guid? customerId,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICpqQuotationService service) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var quotes = await service.GetQuotesAsync(effectiveTenant, status, customerId);
            return Results.Ok(quotes);
        })
        .WithName("ListCpqQuotes")
        .WithSummary("Query CPQ quotes filtered by status or customer.");

        // 4. POST /quotes
        group.MapPost("/quotes", async (
            [FromBody] CreateQuoteRequest request,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICpqQuotationService service) =>
        {
            var effectiveTenant = request.TenantId ?? headerTenantId ?? FallbackTenantId;
            var quote = await service.CreateQuoteAsync(effectiveTenant, request);
            return Results.Created($"/api/v1/sales/cpq/quotes/{quote.Id}", quote);
        })
        .WithName("CreateCpqQuote")
        .WithSummary("Create a draft CPQ quote with initial configuration and gross margin calculation.");

        // 5. GET /quotes/{id}
        group.MapGet("/quotes/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICpqQuotationService service) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            try
            {
                var quote = await service.GetQuoteByIdAsync(effectiveTenant, id);
                return Results.Ok(quote);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("GetCpqQuoteById")
        .WithSummary("Get quote details including line item pricing, gross margin%, and cryptographic seal.");

        // 6. POST /quotes/{id}/lines
        group.MapPost("/quotes/{id:guid}/lines", async (
            Guid id,
            [FromBody] AddOrUpdateLinesRequest request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICpqQuotationService service) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            try
            {
                var quote = await service.UpdateLinesAsync(effectiveTenant, id, request.Lines, request.PriceBookId);
                return Results.Ok(quote);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpdateCpqQuoteLines")
        .WithSummary("Add or update quote line items with automatic volume tier evaluation and margin guarding.");

        // 7. POST /quotes/{id}/request-approval
        group.MapPost("/quotes/{id:guid}/request-approval", async (
            Guid id,
            [FromBody] RequestApprovalBody? body,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICpqQuotationService service) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            try
            {
                var quote = await service.RequestApprovalAsync(effectiveTenant, id, body?.Notes);
                return Results.Ok(quote);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RequestQuoteManagerApproval")
        .WithSummary("Submit quote to executive/sales manager approval queue when margin is below threshold.");

        // 8. POST /quotes/{id}/approve
        group.MapPost("/quotes/{id:guid}/approve", async (
            Guid id,
            [FromBody] ApproveQuoteRequest request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICpqQuotationService service) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            try
            {
                var quote = await service.ApproveQuoteAsync(effectiveTenant, id, request.ManagerNameOrEmail, request.Notes);
                return Results.Ok(quote);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ApproveCpqQuote")
        .WithSummary("Manager approval for a quote that tripped the margin protection guard.");

        // 9. POST /quotes/{id}/send
        group.MapPost("/quotes/{id:guid}/send", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICpqQuotationService service) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            try
            {
                var quote = await service.SendQuoteAsync(effectiveTenant, id);
                return Results.Ok(quote);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, requiresManagerApproval = true });
            }
        })
        .WithName("SendCpqQuoteToClient")
        .WithSummary("Dispatch quote to customer; blocked if aggregate margin is below 25% and unapproved.");

        // 10. POST /quotes/{id}/accept
        group.MapPost("/quotes/{id:guid}/accept", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICpqQuotationService service) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            try
            {
                var conversion = await service.AcceptQuoteAsync(effectiveTenant, id);
                return Results.Ok(conversion);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("AcceptAndConvertCpqQuote")
        .WithSummary("Client acceptance of quote: converts line items to confirmed order and marks CRM deal as Won.");

        // 11. GET /quotes/{id}/pdf-preview
        group.MapGet("/quotes/{id:guid}/pdf-preview", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            ICpqQuotationService service) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            try
            {
                var preview = await service.GetPdfPreviewAsync(effectiveTenant, id);
                return Results.Ok(preview);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("GetCpqQuotePdfPreview")
        .WithSummary("Generate quotation PDF preview with company branding, terms, line items, and SHA-256 seal.");

        return app;
    }
}
