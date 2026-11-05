using System;
using System.Collections.Generic;

namespace SGrowing.Core.Entities;

public class B2BProposalEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string CustomerCompany { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal MarginPercent { get; set; }
    public string Status { get; set; } = "Draft";
    public string? SignedPdfHash { get; set; }
    public string? SignerName { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<B2BProposalLineItemEntity> LineItems { get; set; } = new();
}

public class B2BProposalLineItemEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProposalId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal CostPrice { get; set; }

    public B2BProposalEntity? Proposal { get; set; }
}

public class B2BUsageRecordEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public long Quantity { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
