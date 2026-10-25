using System.Text.Json;

namespace SGrowing.Core.Entities;

public enum CampaignStatus
{
    Draft = 1,
    Active = 2,
    Paused = 3,
    Completed = 4
}

public class OutboundCampaign
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; set; } = string.Empty;
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public string SenderPoolIdsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; private set; }

    private OutboundCampaign() { }

    public static OutboundCampaign Create(Guid tenantId, string name, List<Guid> senderPoolIds)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

        return new OutboundCampaign
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Status = CampaignStatus.Active,
            SenderPoolIdsJson = JsonSerializer.Serialize(senderPoolIds),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public List<Guid> GetSenderPoolIds()
    {
        if (string.IsNullOrWhiteSpace(SenderPoolIdsJson) || SenderPoolIdsJson == "[]")
            return new List<Guid>();
        return JsonSerializer.Deserialize<List<Guid>>(SenderPoolIdsJson) ?? new List<Guid>();
    }
}
