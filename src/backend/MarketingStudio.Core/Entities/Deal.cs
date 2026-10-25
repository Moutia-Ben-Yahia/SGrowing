namespace MarketingStudio.Core.Entities;

public enum DealStage
{
    Prospecting = 1,
    Qualified = 2,
    Proposal = 3,
    Won = 4,
    Lost = 5
}

public class Deal
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ContactId { get; private set; }
    public Guid? CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DealStage Stage { get; set; } = DealStage.Prospecting;
    public Guid? SourceCampaignId { get; set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Deal() { }

    public static Deal Create(
        Guid tenantId,
        Guid contactId,
        string title,
        decimal amount,
        DealStage stage = DealStage.Qualified,
        Guid? companyId = null,
        Guid? sourceCampaignId = null)
    {
        return new Deal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contactId,
            CompanyId = companyId,
            Title = title,
            Amount = amount,
            Stage = stage,
            SourceCampaignId = sourceCampaignId,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
