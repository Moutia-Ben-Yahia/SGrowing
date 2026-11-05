using System;
using System.Collections.Generic;
using System.Linq;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Sales.Proposals;

public enum ProposalStatus
{
    Draft,
    Presented,
    Accepted,
    Signed,
    Declined
}

public enum UsageMetricType
{
    ApiRequests,
    CapiConversions,
    AiTokens,
    ActiveContacts
}

public record QuoteLineItem(
    string Sku,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TotalPrice,
    decimal CostPrice
);

public record CalculateQuoteRequest(
    string TenantId,
    string CustomerCompany,
    List<QuoteLineItem> Items,
    decimal CustomDiscountPercent = 0
);

public record QuoteCalculationResult(
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal TotalCost,
    decimal MarginPercent,
    bool MarginApproved,
    string MarginWarning
);

public record B2BProposal(
    Guid Id,
    string TenantId,
    string CustomerCompany,
    string CustomerEmail,
    List<QuoteLineItem> LineItems,
    decimal TotalAmount,
    decimal MarginPercent,
    ProposalStatus Status,
    string? SignedPdfHash,
    string? SignerName,
    DateTime? SignedAtUtc,
    DateTime CreatedAtUtc
);

public record SignProposalRequest(
    Guid ProposalId,
    string TenantId,
    string SignerName,
    string SignerEmail,
    string SignerIp
);

public record CreateProposalRequest(
    string TenantId,
    string CustomerCompany,
    string CustomerEmail,
    List<QuoteLineItem> Items,
    decimal DiscountPercent = 0
);

public record RecordUsageRequest(
    string TenantId,
    UsageMetricType MetricType,
    long Quantity
);

public record UsageInvoiceSummary(
    string TenantId,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    Dictionary<UsageMetricType, long> ConsumedUnits,
    Dictionary<UsageMetricType, decimal> OverageCharges,
    decimal TotalOverageAmount
);

public static class B2BProposalEntityExtensions
{
    public static B2BProposal ToModel(this B2BProposalEntity entity)
    {
        Enum.TryParse<ProposalStatus>(entity.Status, true, out var status);

        var items = entity.LineItems != null
            ? entity.LineItems.Select(i => i.ToModel()).ToList()
            : new List<QuoteLineItem>();

        return new B2BProposal(
            Id: entity.Id,
            TenantId: entity.TenantId,
            CustomerCompany: entity.CustomerCompany,
            CustomerEmail: entity.CustomerEmail,
            LineItems: items,
            TotalAmount: entity.TotalAmount,
            MarginPercent: entity.MarginPercent,
            Status: status,
            SignedPdfHash: entity.SignedPdfHash,
            SignerName: entity.SignerName,
            SignedAtUtc: entity.SignedAtUtc,
            CreatedAtUtc: entity.CreatedAtUtc
        );
    }

    public static QuoteLineItem ToModel(this B2BProposalLineItemEntity entity)
    {
        return new QuoteLineItem(
            Sku: entity.Sku,
            Description: entity.Description,
            Quantity: entity.Quantity,
            UnitPrice: entity.UnitPrice,
            DiscountPercent: entity.DiscountPercent,
            TotalPrice: entity.TotalPrice,
            CostPrice: entity.CostPrice
        );
    }

    public static B2BProposalEntity ToEntity(this B2BProposal model)
    {
        var entity = new B2BProposalEntity
        {
            Id = model.Id,
            TenantId = model.TenantId,
            CustomerCompany = model.CustomerCompany,
            CustomerEmail = model.CustomerEmail,
            TotalAmount = model.TotalAmount,
            MarginPercent = model.MarginPercent,
            Status = model.Status.ToString(),
            SignedPdfHash = model.SignedPdfHash,
            SignerName = model.SignerName,
            SignedAtUtc = model.SignedAtUtc,
            CreatedAtUtc = model.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            LineItems = new List<B2BProposalLineItemEntity>()
        };

        if (model.LineItems != null)
        {
            foreach (var item in model.LineItems)
            {
                entity.LineItems.Add(item.ToEntity(model.Id));
            }
        }

        return entity;
    }

    public static B2BProposalLineItemEntity ToEntity(this QuoteLineItem item, Guid proposalId)
    {
        return new B2BProposalLineItemEntity
        {
            Id = Guid.NewGuid(),
            ProposalId = proposalId,
            Sku = item.Sku,
            Description = item.Description,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            DiscountPercent = item.DiscountPercent,
            TotalPrice = item.TotalPrice,
            CostPrice = item.CostPrice
        };
    }
}
