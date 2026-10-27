using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Organizations;

public interface IOrganizationHierarchyService
{
    Task<OrganizationTreeNode> GetHierarchyTreeAsync(Guid tenantId, CancellationToken ct = default);
    Task<DealMarginRecord> CalculateDealMarginAsync(Guid tenantId, DealMarginCalculationRequest req, CancellationToken ct = default);
    Task<ProfitabilitySummary> GetProfitabilitySummaryAsync(Guid tenantId, Guid? organizationId = null, CancellationToken ct = default);
    Task<OrganizationRecord> CreateOrganizationAsync(Guid tenantId, string name, string code, string currency = "USD", CancellationToken ct = default);
    Task<BranchRecord> CreateBranchAsync(Guid tenantId, Guid organizationId, string name, string region, string city, CancellationToken ct = default);
    Task<TeamRecord> CreateTeamAsync(Guid tenantId, Guid branchId, Guid organizationId, string name, string department = "Operations", CancellationToken ct = default);
    Task<TeamMemberRecord> AddTeamMemberAsync(Guid tenantId, Guid teamId, string fullName, string role, decimal hourlyRate = 25.00m, CancellationToken ct = default);
}
