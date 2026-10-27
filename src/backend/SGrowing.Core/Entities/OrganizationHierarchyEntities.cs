namespace SGrowing.Core.Entities;

public class OrganizationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class BranchRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class TeamRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = "Operations";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class TeamMemberRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Operator";
    public decimal HourlyLaborRate { get; set; } = 25.00m;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DealMarginRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DealId { get; set; }
    public Guid OrganizationId { get; set; }
    public string DealTitle { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal CostOfGoodsSold { get; set; }
    public decimal CourierShippingFees { get; set; }
    public decimal OperatorLaborHours { get; set; }
    public decimal OperatorHourlyRate { get; set; }
    public decimal OperatorLaborCost { get; set; }
    public decimal TrueGrossMargin { get; set; }
    public decimal GrossMarginPercentage { get; set; }
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record OrganizationTreeNode(
    Guid Id,
    string Name,
    string Type, // "Tenant", "Organization", "Branch", "Team"
    List<OrganizationTreeNode> Children,
    int Headcount,
    decimal TotalRevenue,
    decimal TotalMargin);

public record DealMarginCalculationRequest(
    Guid DealId,
    Guid OrganizationId,
    string DealTitle,
    decimal Revenue,
    decimal CostOfGoodsSold,
    decimal CourierShippingFees,
    decimal OperatorLaborHours,
    decimal OperatorHourlyRate = 25.00m);

public record ProfitabilitySummary(
    Guid TenantId,
    decimal TotalRevenue,
    decimal TotalCogs,
    decimal TotalShipping,
    decimal TotalLaborCost,
    decimal OverallTrueGrossMargin,
    decimal OverallMarginPercentage,
    int AnalyzedDealsCount,
    List<DealMarginRecord> DealBreakdowns);
