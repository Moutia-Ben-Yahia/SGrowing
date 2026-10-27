using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Organizations;

public class OrganizationHierarchyService : IOrganizationHierarchyService
{
    private readonly SGrowingDbContext _db;

    public OrganizationHierarchyService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<OrganizationTreeNode> GetHierarchyTreeAsync(Guid tenantId, CancellationToken ct = default)
    {
        await EnsureDefaultHierarchyAsync(tenantId, ct);

        var organizations = await _db.OrganizationRecords
            .Where(o => o.TenantId == tenantId)
            .ToListAsync(ct);

        var branches = await _db.BranchRecords
            .Where(b => b.TenantId == tenantId)
            .ToListAsync(ct);

        var teams = await _db.TeamRecords
            .Where(t => t.TenantId == tenantId)
            .ToListAsync(ct);

        var members = await _db.TeamMemberRecords
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(ct);

        var deals = await _db.DealMarginRecords
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(ct);

        var orgNodes = new List<OrganizationTreeNode>();

        foreach (var org in organizations)
        {
            var orgBranches = branches.Where(b => b.OrganizationId == org.Id).ToList();
            var branchNodes = new List<OrganizationTreeNode>();

            foreach (var br in orgBranches)
            {
                var brTeams = teams.Where(t => t.BranchId == br.Id).ToList();
                var teamNodes = new List<OrganizationTreeNode>();

                foreach (var tm in brTeams)
                {
                    var tmMembers = members.Where(m => m.TeamId == tm.Id).ToList();
                    var memberNodes = tmMembers.Select(m => new OrganizationTreeNode(
                        m.Id,
                        $"{m.FullName} ({m.Role} - ${m.HourlyLaborRate}/hr)",
                        "TeamMember",
                        new List<OrganizationTreeNode>(),
                        1,
                        0,
                        0
                    )).ToList();

                    teamNodes.Add(new OrganizationTreeNode(
                        tm.Id,
                        tm.Name,
                        "Team",
                        memberNodes,
                        tmMembers.Count,
                        0,
                        0
                    ));
                }

                branchNodes.Add(new OrganizationTreeNode(
                    br.Id,
                    $"{br.Name} ({br.City})",
                    "Branch",
                    teamNodes,
                    teamNodes.Sum(t => t.Headcount),
                    0,
                    0
                ));
            }

            var orgDeals = deals.Where(d => d.OrganizationId == org.Id).ToList();
            decimal orgRev = orgDeals.Sum(d => d.Revenue);
            decimal orgMargin = orgDeals.Sum(d => d.TrueGrossMargin);

            orgNodes.Add(new OrganizationTreeNode(
                org.Id,
                $"{org.Name} [{org.Code}]",
                "Organization",
                branchNodes,
                branchNodes.Sum(b => b.Headcount),
                orgRev,
                orgMargin
            ));
        }

        decimal totalRev = deals.Sum(d => d.Revenue);
        decimal totalMargin = deals.Sum(d => d.TrueGrossMargin);

        return new OrganizationTreeNode(
            tenantId,
            "Enterprise Tenant Hierarchy",
            "Tenant",
            orgNodes,
            orgNodes.Sum(o => o.Headcount),
            totalRev,
            totalMargin
        );
    }

    public async Task<DealMarginRecord> CalculateDealMarginAsync(Guid tenantId, DealMarginCalculationRequest req, CancellationToken ct = default)
    {
        var laborHours = Math.Max(0, req.OperatorLaborHours);
        var hourlyRate = Math.Max(0, req.OperatorHourlyRate);
        var cogs = Math.Max(0, req.CostOfGoodsSold);
        var shipping = Math.Max(0, req.CourierShippingFees);

        var laborCost = Math.Round(laborHours * hourlyRate, 2);
        var trueGrossMargin = Math.Round(req.Revenue - cogs - shipping - laborCost, 2);
        var marginPct = req.Revenue > 0
            ? Math.Round((trueGrossMargin / req.Revenue) * 100, 2)
            : (req.Revenue < 0 ? Math.Round((trueGrossMargin / Math.Abs(req.Revenue)) * 100, 2) : 0);

        var existing = await _db.DealMarginRecords
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.DealId == req.DealId, ct);

        if (existing != null)
        {
            existing.OrganizationId = req.OrganizationId;
            existing.DealTitle = req.DealTitle.Trim();
            existing.Revenue = req.Revenue;
            existing.CostOfGoodsSold = cogs;
            existing.CourierShippingFees = shipping;
            existing.OperatorLaborHours = laborHours;
            existing.OperatorHourlyRate = hourlyRate;
            existing.OperatorLaborCost = laborCost;
            existing.TrueGrossMargin = trueGrossMargin;
            existing.GrossMarginPercentage = marginPct;
            existing.CalculatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var record = new DealMarginRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DealId = req.DealId,
            OrganizationId = req.OrganizationId,
            DealTitle = req.DealTitle.Trim(),
            Revenue = req.Revenue,
            CostOfGoodsSold = cogs,
            CourierShippingFees = shipping,
            OperatorLaborHours = laborHours,
            OperatorHourlyRate = hourlyRate,
            OperatorLaborCost = laborCost,
            TrueGrossMargin = trueGrossMargin,
            GrossMarginPercentage = marginPct,
            CalculatedAtUtc = DateTime.UtcNow
        };

        _db.DealMarginRecords.Add(record);
        await _db.SaveChangesAsync(ct);

        return record;
    }

    public async Task<ProfitabilitySummary> GetProfitabilitySummaryAsync(Guid tenantId, Guid? organizationId = null, CancellationToken ct = default)
    {
        await EnsureDefaultHierarchyAsync(tenantId, ct);

        var query = _db.DealMarginRecords.Where(d => d.TenantId == tenantId);
        if (organizationId.HasValue && organizationId.Value != Guid.Empty)
        {
            query = query.Where(d => d.OrganizationId == organizationId.Value);
        }

        var list = await query.OrderByDescending(d => d.CalculatedAtUtc).ToListAsync(ct);

        decimal totalRevenue = list.Sum(d => d.Revenue);
        decimal totalCogs = list.Sum(d => d.CostOfGoodsSold);
        decimal totalShipping = list.Sum(d => d.CourierShippingFees);
        decimal totalLaborCost = list.Sum(d => d.OperatorLaborCost);
        decimal overallMargin = list.Sum(d => d.TrueGrossMargin);
        decimal overallPct = totalRevenue > 0 ? Math.Round((overallMargin / totalRevenue) * 100, 2) : 0;

        return new ProfitabilitySummary(
            tenantId,
            totalRevenue,
            totalCogs,
            totalShipping,
            totalLaborCost,
            overallMargin,
            overallPct,
            list.Count,
            list
        );
    }

    public async Task<OrganizationRecord> CreateOrganizationAsync(Guid tenantId, string name, string code, string currency = "USD", CancellationToken ct = default)
    {
        var org = new OrganizationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            Currency = currency.Trim().ToUpperInvariant(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.OrganizationRecords.Add(org);
        await _db.SaveChangesAsync(ct);
        return org;
    }

    public async Task<BranchRecord> CreateBranchAsync(Guid tenantId, Guid organizationId, string name, string region, string city, CancellationToken ct = default)
    {
        var branch = new BranchRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            Name = name.Trim(),
            Region = region.Trim(),
            City = city.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.BranchRecords.Add(branch);
        await _db.SaveChangesAsync(ct);
        return branch;
    }

    public async Task<TeamRecord> CreateTeamAsync(Guid tenantId, Guid branchId, Guid organizationId, string name, string department = "Operations", CancellationToken ct = default)
    {
        var team = new TeamRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = branchId,
            OrganizationId = organizationId,
            Name = name.Trim(),
            Department = department.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.TeamRecords.Add(team);
        await _db.SaveChangesAsync(ct);
        return team;
    }

    public async Task<TeamMemberRecord> AddTeamMemberAsync(Guid tenantId, Guid teamId, string fullName, string role, decimal hourlyRate = 25.00m, CancellationToken ct = default)
    {
        var member = new TeamMemberRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TeamId = teamId,
            FullName = fullName.Trim(),
            Role = role.Trim(),
            HourlyLaborRate = hourlyRate,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.TeamMemberRecords.Add(member);
        await _db.SaveChangesAsync(ct);
        return member;
    }

    private async Task EnsureDefaultHierarchyAsync(Guid tenantId, CancellationToken ct)
    {
        var anyOrg = await _db.OrganizationRecords.AnyAsync(o => o.TenantId == tenantId, ct);
        if (!anyOrg)
        {
            var orgHolding = new OrganizationRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "SGrowing Global Holding",
                Code = "SG-HOLDING",
                Currency = "USD",
                CreatedAtUtc = DateTime.UtcNow
            };

            var branchTunis = new BranchRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrganizationId = orgHolding.Id,
                Name = "North Africa Regional Hub",
                Region = "Tunisia & Maghreb",
                City = "Tunis",
                CreatedAtUtc = DateTime.UtcNow
            };

            var branchDubai = new BranchRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrganizationId = orgHolding.Id,
                Name = "Middle East Regional Hub",
                Region = "GCC & UAE",
                City = "Dubai",
                CreatedAtUtc = DateTime.UtcNow
            };

            var teamSdr = new TeamRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrganizationId = orgHolding.Id,
                BranchId = branchTunis.Id,
                Name = "High-Velocity COD Outbound SDRs",
                Department = "Sales & Outreach",
                CreatedAtUtc = DateTime.UtcNow
            };

            var teamLogistics = new TeamRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrganizationId = orgHolding.Id,
                BranchId = branchTunis.Id,
                Name = "Logistics & Fulfillment Team",
                Department = "Supply Chain",
                CreatedAtUtc = DateTime.UtcNow
            };

            var member1 = new TeamMemberRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TeamId = teamSdr.Id,
                FullName = "Sarah Al-Mansoor",
                Role = "Senior Outbound SDR",
                HourlyLaborRate = 30.00m,
                CreatedAtUtc = DateTime.UtcNow
            };

            var member2 = new TeamMemberRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TeamId = teamSdr.Id,
                FullName = "Omar Ben Salem",
                Role = "SDR Lead",
                HourlyLaborRate = 35.00m,
                CreatedAtUtc = DateTime.UtcNow
            };

            var member3 = new TeamMemberRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TeamId = teamLogistics.Id,
                FullName = "Karim Trabelsi",
                Role = "Warehouse Operations Lead",
                HourlyLaborRate = 28.00m,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.OrganizationRecords.Add(orgHolding);
            _db.BranchRecords.AddRange(branchTunis, branchDubai);
            _db.TeamRecords.AddRange(teamSdr, teamLogistics);
            _db.TeamMemberRecords.AddRange(member1, member2, member3);

            // Seed sample deal margin records
            var deal1 = new DealMarginRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DealId = Guid.NewGuid(),
                OrganizationId = orgHolding.Id,
                DealTitle = "Enterprise Multi-Store Omnichannel Rollout",
                Revenue = 12500.00m,
                CostOfGoodsSold = 3800.00m,
                CourierShippingFees = 950.00m,
                OperatorLaborHours = 40.00m,
                OperatorHourlyRate = 30.00m,
                OperatorLaborCost = 1200.00m,
                TrueGrossMargin = 6550.00m,
                GrossMarginPercentage = 52.40m,
                CalculatedAtUtc = DateTime.UtcNow.AddDays(-2)
            };

            var deal2 = new DealMarginRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DealId = Guid.NewGuid(),
                OrganizationId = orgHolding.Id,
                DealTitle = "MENA Regional COD Fulfillment Contract",
                Revenue = 8400.00m,
                CostOfGoodsSold = 3100.00m,
                CourierShippingFees = 1100.00m,
                OperatorLaborHours = 25.00m,
                OperatorHourlyRate = 28.00m,
                OperatorLaborCost = 700.00m,
                TrueGrossMargin = 3500.00m,
                GrossMarginPercentage = 41.67m,
                CalculatedAtUtc = DateTime.UtcNow.AddDays(-1)
            };

            _db.DealMarginRecords.AddRange(deal1, deal2);
            await _db.SaveChangesAsync(ct);
        }
    }
}
