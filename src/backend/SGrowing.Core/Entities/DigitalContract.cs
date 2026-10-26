namespace SGrowing.Core.Entities;

public enum ContractStatus
{
    Draft = 1,
    Sent = 2,
    Viewed = 3,
    Signed = 4,
    Declined = 5
}

public class DigitalContract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? DealId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContractType { get; set; } = "OrderForm"; // "NDA", "OrderForm", "MSA", "SOW"
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public string SignerName { get; set; } = string.Empty;
    public string SignerEmail { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public string SigningToken { get; set; } = string.Empty;
    public string? SignedPdfUrl { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static DigitalContract Create(
        Guid tenantId,
        string title,
        string contractType,
        string signerName,
        string signerEmail,
        decimal contractValue,
        Guid? companyId = null,
        Guid? dealId = null)
    {
        return new DigitalContract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            DealId = dealId,
            Title = title,
            ContractType = contractType,
            Status = ContractStatus.Draft,
            SignerName = signerName,
            SignerEmail = signerEmail,
            ContractValue = contractValue,
            SigningToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkSent()
    {
        Status = ContractStatus.Sent;
    }

    public void MarkViewed()
    {
        if (Status == ContractStatus.Sent || Status == ContractStatus.Draft)
        {
            Status = ContractStatus.Viewed;
        }
    }

    public void MarkSigned(string? signedPdfUrl = null)
    {
        Status = ContractStatus.Signed;
        SignedPdfUrl = signedPdfUrl ?? $"/storage/contracts/{Id}_signed.pdf";
        SignedAtUtc = DateTime.UtcNow;
    }

    public void MarkDeclined()
    {
        Status = ContractStatus.Declined;
    }
}
