using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public record CpqLineItemDto(
    string Sku,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRatePercentage,
    decimal DiscountPercentage,
    decimal TotalAmount
);

public record CpqQuoteDto(
    Guid QuoteId,
    string QuoteNumber,
    string CustomerName,
    string CustomerEmail,
    string Currency,
    List<CpqLineItemDto> LineItems,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal GrandTotal,
    DateTime ValidUntilUtc,
    string Status
);

public record EavAttributeDto(
    Guid AttributeId,
    string EntityType, // "Lead", "Contact", "Organization", "Deal"
    Guid EntityId,
    string AttributeKey,
    string AttributeValue,
    string DataType // "String", "Number", "Boolean", "Date", "Json"
);

public interface ICpqQuoteAndEavService
{
    Task<CpqQuoteDto> GenerateCpqQuoteAsync(Guid tenantId, string customerName, string customerEmail, string currency, List<CpqLineItemDto> items, decimal globalDiscountPercentage = 0);
    Task<byte[]> GenerateQuotePdfBytesAsync(Guid tenantId, Guid quoteId);
    Task<EavAttributeDto> SetEavAttributeAsync(Guid tenantId, string entityType, Guid entityId, string key, string value, string dataType = "String");
    Task<List<EavAttributeDto>> GetEavAttributesAsync(Guid tenantId, string entityType, Guid entityId);
}

public class CpqQuoteAndEavService : ICpqQuoteAndEavService
{
    private static readonly Dictionary<Guid, CpqQuoteDto> _quoteStore = new();
    private static readonly List<EavAttributeDto> _eavStore = new();

    public Task<CpqQuoteDto> GenerateCpqQuoteAsync(
        Guid tenantId,
        string customerName,
        string customerEmail,
        string currency,
        List<CpqLineItemDto> items,
        decimal globalDiscountPercentage = 0)
    {
        var quoteId = Guid.NewGuid();
        var quoteNumber = $"QTE-2026-{Random.Shared.Next(1000, 9999)}";

        decimal subtotal = 0;
        decimal totalTax = 0;
        decimal totalDiscount = 0;
        var calculatedItems = new List<CpqLineItemDto>();

        foreach (var item in items)
        {
            var lineGross = item.Quantity * item.UnitPrice;
            var lineDiscount = lineGross * (item.DiscountPercentage / 100m);
            var lineNet = lineGross - lineDiscount;
            var lineTax = lineNet * (item.TaxRatePercentage / 100m);
            var lineTotal = lineNet + lineTax;

            subtotal += lineGross;
            totalDiscount += lineDiscount;
            totalTax += lineTax;

            calculatedItems.Add(item with { TotalAmount = Math.Round(lineTotal, 2) });
        }

        var globalDiscountAmt = (subtotal - totalDiscount) * (globalDiscountPercentage / 100m);
        totalDiscount += globalDiscountAmt;
        var grandTotal = Math.Round((subtotal - totalDiscount) + totalTax, 2);

        var quote = new CpqQuoteDto(
            quoteId,
            quoteNumber,
            customerName,
            customerEmail,
            currency,
            calculatedItems,
            Math.Round(subtotal, 2),
            Math.Round(totalDiscount, 2),
            Math.Round(totalTax, 2),
            grandTotal,
            DateTime.UtcNow.AddDays(30),
            "Draft"
        );

        _quoteStore[quoteId] = quote;
        return Task.FromResult(quote);
    }

    public Task<byte[]> GenerateQuotePdfBytesAsync(Guid tenantId, Guid quoteId)
    {
        // Generates valid sample PDF quotation payload
        var mockPdf = System.Text.Encoding.UTF8.GetBytes($"%PDF-1.4\n% SGrowing CPQ Quotation Engine\n1 0 obj << /Title (Quotation {quoteId}) >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF");
        return Task.FromResult(mockPdf);
    }

    public Task<EavAttributeDto> SetEavAttributeAsync(Guid tenantId, string entityType, Guid entityId, string key, string value, string dataType = "String")
    {
        _eavStore.RemoveAll(a => a.EntityType == entityType && a.EntityId == entityId && a.AttributeKey == key);

        var dto = new EavAttributeDto(Guid.NewGuid(), entityType, entityId, key, value, dataType);
        _eavStore.Add(dto);

        return Task.FromResult(dto);
    }

    public Task<List<EavAttributeDto>> GetEavAttributesAsync(Guid tenantId, string entityType, Guid entityId)
    {
        var list = _eavStore
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .ToList();

        return Task.FromResult(list);
    }
}
