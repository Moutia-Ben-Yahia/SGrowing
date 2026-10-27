using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Commerce;

public static class CpqQuoteAndEavEndpoints
{
    public static IEndpointRouteBuilder MapCpqQuoteAndEavEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/cpq")
            .WithTags("CPQ Quotes & Polymorphic EAV");

        group.MapPost("/quotes", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] CreateCpqQuoteRequest request,
            ICpqQuoteAndEavService service) =>
        {
            var result = await service.GenerateCpqQuoteAsync(
                tenantId,
                request.CustomerName,
                request.CustomerEmail,
                request.Currency,
                request.LineItems,
                request.GlobalDiscountPercentage
            );
            return Results.Ok(result);
        });

        group.MapGet("/quotes/{id:guid}/pdf", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            ICpqQuoteAndEavService service) =>
        {
            var pdfBytes = await service.GenerateQuotePdfBytesAsync(tenantId, id);
            return Results.File(pdfBytes, "application/pdf", $"quote-{id}.pdf");
        });

        group.MapPost("/eav/attributes", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] SetEavAttributeRequest request,
            ICpqQuoteAndEavService service) =>
        {
            var result = await service.SetEavAttributeAsync(
                tenantId,
                request.EntityType,
                request.EntityId,
                request.Key,
                request.Value,
                request.DataType ?? "String"
            );
            return Results.Ok(result);
        });

        group.MapGet("/eav/attributes", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromQuery] string entityType,
            [FromQuery] Guid entityId,
            ICpqQuoteAndEavService service) =>
        {
            var result = await service.GetEavAttributesAsync(tenantId, entityType, entityId);
            return Results.Ok(result);
        });

        return app;
    }
}

public record CreateCpqQuoteRequest(
    string CustomerName,
    string CustomerEmail,
    string Currency,
    List<CpqLineItemDto> LineItems,
    decimal GlobalDiscountPercentage
);

public record SetEavAttributeRequest(
    string EntityType,
    Guid EntityId,
    string Key,
    string Value,
    string? DataType
);
