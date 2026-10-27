using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce.Warehouses;
using SGrowing.Api.Modules.Organizations;
using SGrowing.Api.Modules.Outbound.Research;
using SGrowing.Api.Modules.Projects;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests;

public class StrategicRecommendationsTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    // =========================================================================
    // Recommendation 1: Ever-Teams Post-Close Client Onboarding & Sprint Tasks
    // =========================================================================

    [Fact]
    public async Task Recommendation1_ConvertDealToOnboardingProject_GeneratesMilestones()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var deal = Deal.Create(
            tenantId,
            contactId,
            "Acme Enterprise Expansion",
            45000m,
            DealStage.Won
        );
        db.Deals.Add(deal);
        await db.SaveChangesAsync();

        var service = new ProjectTaskService(db);
        var project = await service.ConvertFromDealAsync(tenantId, deal.Id);

        Assert.NotNull(project);
        Assert.Equal(tenantId, project.TenantId);
        Assert.Equal(deal.Id, project.DealId);
        Assert.Contains("Acme Enterprise Expansion", project.Name);
        Assert.Equal("Active", project.Status);

        var tasks = (await service.GetProjectTasksAsync(tenantId, project.Id)).ToList();
        Assert.Equal(5, tasks.Count);

        // Check Linear-style milestone templates
        var kickoff = tasks.FirstOrDefault(t => t.Title.Contains("Kickoff"));
        Assert.NotNull(kickoff);
        Assert.Equal("Todo", kickoff.Status);
        Assert.Equal("Urgent", kickoff.Priority);
        Assert.Equal(3, kickoff.SubtasksTotal);

        var provisioning = tasks.FirstOrDefault(t => t.Title.Contains("Provisioning"));
        Assert.NotNull(provisioning);
        Assert.Equal("InProgress", provisioning.Status);
        Assert.Equal("High", provisioning.Priority);
    }

    [Fact]
    public async Task Recommendation1_ConvertContractToDeploymentProject_GeneratesMilestones()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var contract = DigitalContract.Create(
            tenantId,
            "SaaS Master Services Agreement",
            "MSA",
            "John Smith",
            "john@smith.io",
            24000m
        );
        contract.Status = ContractStatus.Signed;
        db.DigitalContracts.Add(contract);
        await db.SaveChangesAsync();

        var service = new ProjectTaskService(db);
        var project = await service.ConvertFromContractAsync(tenantId, contract.Id);

        Assert.NotNull(project);
        Assert.Equal(contract.Id, project.ContractId);
        Assert.Contains("Deployment Sprint", project.Name);

        var tasks = (await service.GetProjectTasksAsync(tenantId, project.Id)).ToList();
        Assert.True(tasks.Count >= 3);
    }

    [Fact]
    public async Task Recommendation1_CreateTask_And_OptimisticUpdate_Works()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new ProjectTaskService(db);

        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Sprint Alpha",
            Status = "Active"
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // Create Task
        var req = new CreateTaskRequest(
            Title: "Setup Webhook Ingestion Engine",
            Description: "Configure endpoints for high-throughput inbound webhooks",
            Status: "Todo",
            Priority: "High",
            AssigneeName: "Omar Vance",
            SubtasksCompleted: 0,
            SubtasksTotal: 4
        );
        var created = await service.CreateTaskAsync(tenantId, project.Id, req);

        Assert.NotNull(created);
        Assert.Equal("Setup Webhook Ingestion Engine", created.Title);
        Assert.Equal("Todo", created.Status);
        Assert.Equal("OV", created.AssigneeAvatar);

        // Optimistic Status & Progress Update
        var updateReq = new UpdateTaskRequest(
            Status: "InProgress",
            Priority: "Urgent",
            SubtasksCompleted: 3
        );
        var updated = await service.UpdateTaskAsync(tenantId, created.Id, updateReq);

        Assert.NotNull(updated);
        Assert.Equal("InProgress", updated.Status);
        Assert.Equal("Urgent", updated.Priority);
        Assert.Equal(3, updated.SubtasksCompleted);
        Assert.Equal(4, updated.SubtasksTotal);

        // Comments
        var comment = await service.AddCommentAsync(tenantId, created.Id, new AddCommentRequest("Lead Engineer", "Verified endpoint performance with 500 req/s."));
        Assert.NotNull(comment);

        var comments = (await service.GetCommentsAsync(tenantId, created.Id)).ToList();
        Assert.Single(comments);
        Assert.Equal("Lead Engineer", comments[0].AuthorName);
    }

    // =========================================================================
    // Recommendation 2: Open-Mercato Deep AI Web Research Scraper
    // =========================================================================

    [Fact]
    public async Task Recommendation2_CrawlAndExtract_GeneratesIntelligenceAndHooks()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new DeepWebResearchService(db);

        var req = new WebResearchCrawlRequest(
            Domain: "https://hyper-logistics.tn",
            CompanyName: "HyperLogistics",
            Engine: "MultiEngine (Tavily+Firecrawl+Exa)",
            ExtractBios: true,
            ExtractTechStack: true,
            GenerateSalesHooks: true
        );

        var result = await service.CrawlAndExtractAsync(tenantId, req);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("hyper-logistics.tn", result.Domain);
        Assert.Equal("HyperLogistics", result.CompanyName);
        Assert.NotEmpty(result.CorporateSummary);
        Assert.NotEmpty(result.ExecutiveBios);
        Assert.NotEmpty(result.TechTags);
        Assert.Equal(3, result.SalesHooks.Count);

        // Verify key executive roles exist
        Assert.Contains(result.ExecutiveBios, b => b.Title.Contains("Chief Executive Officer") || b.Title.Contains("CEO"));
        Assert.Contains(result.ExecutiveBios, b => b.Title.Contains("Sales") || b.Title.Contains("Revenue"));

        // Verify tech tags
        Assert.Contains(result.TechTags, t => t.Name.Contains("React") || t.Name.Contains("Next.js"));

        // Verify 3-bullet sales hooks
        Assert.All(result.SalesHooks, hook => Assert.False(string.IsNullOrWhiteSpace(hook)));

        // Verify cached brief retrieval
        var brief = await service.GetBriefAsync(tenantId, "hyper-logistics.tn");
        Assert.NotNull(brief);
        Assert.Equal("HyperLogistics", brief.CompanyName);
        Assert.Equal(3, brief.SalesHooks.Count);
    }

    // =========================================================================
    // Recommendation 3: Ever-Demand Multi-Warehouse Allocation & Geo-Routing
    // =========================================================================

    [Fact]
    public async Task Recommendation3_ProximityCalculation_RoutesToClosestHub()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new WarehouseRoutingService(db);

        // Customer in Tunis (Charguia / Ariana approx 36.84, 10.20)
        var routingReq = new RoutingRequest(
            TenantId: tenantId,
            OrderId: "ORD-9901",
            CustomerAddress: "Rue Alain Savary, Tunis",
            CustomerCity: "Tunis",
            CustomerLatitude: 36.8300,
            CustomerLongitude: 10.1900,
            Items: new List<OrderItemRequirement>
            {
                new("SKU-GROW-PRO", 2),
                new("SKU-LABEL-PRINT", 1)
            }
        );

        var result = await service.RouteOrderAsync(routingReq);

        Assert.NotNull(result);
        Assert.True(result.IsAllocated);
        Assert.Contains("Tunis Nord", result.SelectedWarehouseName);
        Assert.True(result.DistanceKm < 15.0); // Within 15 km of Tunis Nord hub
        Assert.Equal(2, result.AllocatedItems.Count);
        Assert.Empty(result.AutoTriggeredTransfers); // Sufficient inventory in closest
    }

    [Fact]
    public async Task Recommendation3_CustomerInSousse_RoutesToSousseHub()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new WarehouseRoutingService(db);

        // Customer in Sousse (Kantaoui lat 35.89, lon 10.60)
        var routingReq = new RoutingRequest(
            TenantId: tenantId,
            OrderId: "ORD-9902",
            CustomerAddress: "Port El Kantaoui, Sousse",
            CustomerCity: "Sousse",
            CustomerLatitude: 35.8900,
            CustomerLongitude: 10.6000,
            Items: new List<OrderItemRequirement>
            {
                new("SKU-SMART-CAM", 3)
            }
        );

        var result = await service.RouteOrderAsync(routingReq);

        Assert.NotNull(result);
        Assert.True(result.IsAllocated);
        Assert.Contains("Sousse Sahel", result.SelectedWarehouseName);
        Assert.True(result.DistanceKm < 20.0);
    }

    [Fact]
    public async Task Recommendation3_ShortageInClosest_TriggersInterWarehouseTransfer()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new WarehouseRoutingService(db);

        // First trigger seeding
        await service.GetWarehousesAsync(tenantId);

        // Reduce stock in Tunis Nord to 5 units
        var tunisWh = await db.WarehouseRecords.FirstAsync(w => w.TenantId == tenantId && w.City == "Tunis");
        var tunisStock = await db.WarehouseStockRecords.FirstAsync(s => s.TenantId == tenantId && s.WarehouseId == tunisWh.Id && s.Sku == "SKU-BARCODE-RUGGED");
        tunisStock.QuantityAvailable = 5;
        await db.SaveChangesAsync();

        // Customer orders 12 units in Tunis
        var routingReq = new RoutingRequest(
            TenantId: tenantId,
            OrderId: "ORD-9903-LARGE",
            CustomerAddress: "Les Berges du Lac, Tunis",
            CustomerCity: "Tunis",
            CustomerLatitude: 36.8350,
            CustomerLongitude: 10.2300,
            Items: new List<OrderItemRequirement>
            {
                new("SKU-BARCODE-RUGGED", 12)
            }
        );

        var result = await service.RouteOrderAsync(routingReq);

        Assert.NotNull(result);
        Assert.True(result.IsAllocated);
        Assert.Contains("Tunis Nord", result.SelectedWarehouseName);

        // Must have allocated 5 from Tunis and 7 from secondary warehouse
        Assert.Equal(2, result.AllocatedItems.Count);
        Assert.Equal(5, result.AllocatedItems.First(a => a.WarehouseId == tunisWh.Id).QuantityAllocated);

        // Automated transfer manifest should be triggered for the shortage
        Assert.Single(result.AutoTriggeredTransfers);
        var transfer = result.AutoTriggeredTransfers[0];
        Assert.Equal(7, transfer.Quantity);
        Assert.Equal("SKU-BARCODE-RUGGED", transfer.Sku);
        Assert.Equal(tunisWh.Id, transfer.DestinationWarehouseId);
    }

    // =========================================================================
    // Recommendation 4: Ever-Gauzy Multi-Organization Hierarchy & Margin Tracking
    // =========================================================================

    [Fact]
    public async Task Recommendation4_OrganizationTreeHierarchy_BuildsProperLevels()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new OrganizationHierarchyService(db);

        var root = await service.GetHierarchyTreeAsync(tenantId);

        Assert.NotNull(root);
        Assert.Equal("Tenant", root.Type);
        Assert.NotEmpty(root.Children);

        var orgNode = root.Children.First();
        Assert.Equal("Organization", orgNode.Type);
        Assert.NotEmpty(orgNode.Children);

        var branchNode = orgNode.Children.First();
        Assert.Equal("Branch", branchNode.Type);
        Assert.NotEmpty(branchNode.Children);

        var teamNode = branchNode.Children.First();
        Assert.Equal("Team", teamNode.Type);
        Assert.NotEmpty(teamNode.Children);

        var memberNode = teamNode.Children.First();
        Assert.Equal("TeamMember", memberNode.Type);
    }

    [Fact]
    public async Task Recommendation4_DealMarginCalculation_MatchesExactFormula()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new OrganizationHierarchyService(db);

        var dealId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        // Revenue: 10,000, COGS: 3,000, Courier: 800, Labor: 20 hours * $25 = 500
        // Expected True Gross Margin = 10,000 - 3,000 - 800 - 500 = 5,700
        // Expected Margin % = 57.00%
        var req = new DealMarginCalculationRequest(
            DealId: dealId,
            OrganizationId: orgId,
            DealTitle: "Omnichannel Logistics Implementation",
            Revenue: 10000m,
            CostOfGoodsSold: 3000m,
            CourierShippingFees: 800m,
            OperatorLaborHours: 20m,
            OperatorHourlyRate: 25.00m
        );

        var result = await service.CalculateDealMarginAsync(tenantId, req);

        Assert.NotNull(result);
        Assert.Equal(10000m, result.Revenue);
        Assert.Equal(3000m, result.CostOfGoodsSold);
        Assert.Equal(800m, result.CourierShippingFees);
        Assert.Equal(500m, result.OperatorLaborCost);
        Assert.Equal(5700m, result.TrueGrossMargin);
        Assert.Equal(57.00m, result.GrossMarginPercentage);

        // Verify summary rollup
        var summary = await service.GetProfitabilitySummaryAsync(tenantId);
        Assert.NotNull(summary);
        Assert.True(summary.TotalRevenue >= 10000m);
        Assert.True(summary.OverallTrueGrossMargin >= 5700m);
        Assert.True(summary.OverallMarginPercentage > 0);
    }

    [Fact]
    public async Task Recommendation1_ConvertDealToOnboardingProject_IsIdempotent()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var deal = Deal.Create(tenantId, Guid.NewGuid(), "Enterprise Expansion", 25000m, DealStage.Won);
        db.Deals.Add(deal);
        await db.SaveChangesAsync();

        var service = new ProjectTaskService(db);
        var proj1 = await service.ConvertFromDealAsync(tenantId, deal.Id);
        var proj2 = await service.ConvertFromDealAsync(tenantId, deal.Id);

        Assert.Equal(proj1.Id, proj2.Id);
        var allProjects = await db.Projects.Where(p => p.TenantId == tenantId).ToListAsync();
        Assert.Single(allProjects);

        var tasks = await db.TaskItems.Where(t => t.TenantId == tenantId && t.ProjectId == proj1.Id).ToListAsync();
        Assert.Equal(5, tasks.Count); // Does not duplicate template tasks
    }

    [Fact]
    public async Task Recommendation1_ConvertContractToDeploymentProject_IsIdempotent()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var contract = DigitalContract.Create(tenantId, "Enterprise SLA Contract", "MSA", "Jane Doe", "jane@corp.com", 35000m);
        db.DigitalContracts.Add(contract);
        await db.SaveChangesAsync();

        var service = new ProjectTaskService(db);
        var proj1 = await service.ConvertFromContractAsync(tenantId, contract.Id);
        var proj2 = await service.ConvertFromContractAsync(tenantId, contract.Id);

        Assert.Equal(proj1.Id, proj2.Id);
        var allProjects = await db.Projects.Where(p => p.TenantId == tenantId).ToListAsync();
        Assert.Single(allProjects);
    }

    [Fact]
    public async Task Recommendation1_CreateAndPatchTask_SanitizesSubtaskBounds()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new ProjectTaskService(db);
        var projectId = Guid.NewGuid();

        var req = new CreateTaskRequest("Test Negative Bounds", "", "Todo", "Low", "Alex", SubtasksCompleted: -5, SubtasksTotal: -10);
        var task = await service.CreateTaskAsync(tenantId, projectId, req);

        Assert.Equal(0, task.SubtasksCompleted);
        Assert.Equal(0, task.SubtasksTotal);

        var updated = await service.UpdateTaskAsync(tenantId, task.Id, new UpdateTaskRequest(SubtasksCompleted: -2, SubtasksTotal: -4));
        Assert.NotNull(updated);
        Assert.Equal(0, updated.SubtasksCompleted);
        Assert.Equal(0, updated.SubtasksTotal);
    }

    [Fact]
    public async Task Recommendation2_DomainExtraction_HandlesWwwPrefix_And_UrlSchemes()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new DeepWebResearchService(db);

        // 1. Domain with www prefix
        var reqWww = new WebResearchCrawlRequest(Domain: "www.hyper-logistics.tn");
        var resWww = await service.CrawlAndExtractAsync(tenantId, reqWww);
        Assert.Equal("Hyper-Logistics", resWww.CompanyName);
        Assert.DoesNotContain("Www", resWww.CompanyName);

        // 2. Domain with https scheme and trailing slash
        var reqHttps = new WebResearchCrawlRequest(Domain: "https://acme-global.com/");
        var resHttps = await service.CrawlAndExtractAsync(tenantId, reqHttps);
        Assert.Equal("Acme-Global", resHttps.CompanyName);

        // 3. Retrieval with www prefix matches brief stored without www
        var brief = await service.GetBriefAsync(tenantId, "www.hyper-logistics.tn");
        Assert.NotNull(brief);
        Assert.Equal("Hyper-Logistics", brief.CompanyName);

        // 4. Graceful handling of empty domain
        var reqEmpty = new WebResearchCrawlRequest(Domain: "");
        var resEmpty = await service.CrawlAndExtractAsync(tenantId, reqEmpty);
        Assert.True(resEmpty.Success);
        Assert.NotEmpty(resEmpty.CompanyName);
    }

    [Fact]
    public async Task Recommendation3_Shortage_SplitsAcrossMultipleSecondaryHubs()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new WarehouseRoutingService(db);

        // Seed default hubs
        await service.GetWarehousesAsync(tenantId);

        var tunisWh = await db.WarehouseRecords.FirstAsync(w => w.TenantId == tenantId && w.City == "Tunis");
        var sousseWh = await db.WarehouseRecords.FirstAsync(w => w.TenantId == tenantId && w.City == "Sousse");
        var sfaxWh = await db.WarehouseRecords.FirstAsync(w => w.TenantId == tenantId && w.City == "Sfax");

        // Set up split inventory:
        // Tunis (closest): 2 units
        // Sousse (secondary 1): 4 units
        // Sfax (secondary 2): 4 units
        db.WarehouseStockRecords.AddRange(
            new WarehouseStockRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WarehouseId = tunisWh.Id,
                Sku = "SKU-SPLIT-TEST",
                ProductName = "Split Item",
                QuantityAvailable = 2,
                UnitCost = 50m
            },
            new WarehouseStockRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WarehouseId = sousseWh.Id,
                Sku = "SKU-SPLIT-TEST",
                ProductName = "Split Item",
                QuantityAvailable = 4,
                UnitCost = 50m
            },
            new WarehouseStockRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WarehouseId = sfaxWh.Id,
                Sku = "SKU-SPLIT-TEST",
                ProductName = "Split Item",
                QuantityAvailable = 4,
                UnitCost = 50m
            }
        );
        await db.SaveChangesAsync();

        // Customer in Tunis orders 10 units:
        // 2 from Tunis, 4 from Sousse, 4 from Sfax = 10 total!
        var routingReq = new RoutingRequest(
            TenantId: tenantId,
            OrderId: "ORD-SPLIT-10",
            CustomerAddress: "Avenue Habib Bourguiba, Tunis",
            CustomerCity: "Tunis",
            CustomerLatitude: 36.8000,
            CustomerLongitude: 10.1800,
            Items: new List<OrderItemRequirement>
            {
                new("SKU-SPLIT-TEST", 10)
            }
        );

        var result = await service.RouteOrderAsync(routingReq);

        Assert.NotNull(result);
        Assert.True(result.IsAllocated);
        Assert.Equal(3, result.AllocatedItems.Count);
        Assert.Equal(2, result.AllocatedItems.First(a => a.WarehouseId == tunisWh.Id).QuantityAllocated);
        Assert.Equal(4, result.AllocatedItems.First(a => a.WarehouseId == sousseWh.Id).QuantityAllocated);
        Assert.Equal(4, result.AllocatedItems.First(a => a.WarehouseId == sfaxWh.Id).QuantityAllocated);

        // Two transfer manifests generated to rebalance stock
        Assert.Equal(2, result.AutoTriggeredTransfers.Count);
        Assert.Contains(result.AutoTriggeredTransfers, t => t.SourceWarehouseId == sousseWh.Id && t.Quantity == 4);
        Assert.Contains(result.AutoTriggeredTransfers, t => t.SourceWarehouseId == sfaxWh.Id && t.Quantity == 4);
    }

    [Fact]
    public async Task Recommendation3_Haversine_HandlesAntipodes_And_InternationalDateLine()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new WarehouseRoutingService(db);

        // Warehouse near North Pole (lat 89.9, lon 0.0)
        var polarWh = await service.CreateWarehouseAsync(tenantId, new CreateWarehouseRequest(
            Code: "WH-NORTH-POLE",
            Name: "Arctic Fulfillment Hub",
            Address: "Ice Shelf",
            City: "Arctic",
            Country: "Svalbard",
            Latitude: 89.9999,
            Longitude: 0.0
        ));

        // Customer near South Pole (lat -89.9, lon 0.0) — near antipodes
        var routingReq = new RoutingRequest(
            TenantId: tenantId,
            OrderId: "ORD-ANTIPODE",
            CustomerAddress: "South Pole Station",
            CustomerCity: "Antarctica",
            CustomerLatitude: -89.9999,
            CustomerLongitude: 0.0,
            Items: new List<OrderItemRequirement> { new("SKU-GROW-PRO", 1) }
        );

        var result = await service.RouteOrderAsync(routingReq);
        Assert.NotNull(result);
        Assert.False(double.IsNaN(result.DistanceKm));
        Assert.True(result.DistanceKm > 19000); // Earth half-circumference ~20,000 km

        // Warehouse across international date line: 179° Lon and -179° Lon
        var fijiWh = await service.CreateWarehouseAsync(tenantId, new CreateWarehouseRequest(
            Code: "WH-FIJI-179",
            Name: "Fiji Island Hub",
            Address: "Port",
            City: "Suva",
            Country: "Fiji",
            Latitude: 0.0,
            Longitude: 179.0
        ));

        var dateLineReq = new RoutingRequest(
            TenantId: tenantId,
            OrderId: "ORD-DATELINE",
            CustomerAddress: "Samoa Point",
            CustomerCity: "Apia",
            CustomerLatitude: 0.0,
            CustomerLongitude: -179.0,
            Items: new List<OrderItemRequirement> { new("SKU-GROW-PRO", 1) }
        );

        var dateLineResult = await service.RouteOrderAsync(dateLineReq);
        Assert.NotNull(dateLineResult);
        Assert.False(double.IsNaN(dateLineResult.DistanceKm));
        // 2 degrees across equator is ~222 km (NOT ~39,800 km)
        Assert.True(dateLineResult.DistanceKm < 500);
    }

    [Fact]
    public async Task Recommendation4_DealMarginCalculation_IsIdempotent_AndDoesNotDuplicateRevenue()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new OrganizationHierarchyService(db);
        var dealId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var initialReq = new DealMarginCalculationRequest(
            DealId: dealId,
            OrganizationId: orgId,
            DealTitle: "Logistics SaaS Contract",
            Revenue: 50000m,
            CostOfGoodsSold: 15000m,
            CourierShippingFees: 3000m,
            OperatorLaborHours: 100m,
            OperatorHourlyRate: 30.00m
        );

        // First calculation: Margin = 50000 - 15000 - 3000 - 3000 = 29000 (58.00%)
        var record1 = await service.CalculateDealMarginAsync(tenantId, initialReq);
        Assert.Equal(29000m, record1.TrueGrossMargin);

        // Recalculate margin for same deal with increased labor hours (150 hrs = $4500)
        // Margin = 50000 - 15000 - 3000 - 4500 = 27500 (55.00%)
        var updatedReq = initialReq with { OperatorLaborHours = 150m };
        var record2 = await service.CalculateDealMarginAsync(tenantId, updatedReq);

        Assert.Equal(record1.Id, record2.Id); // Same record updated
        Assert.Equal(27500m, record2.TrueGrossMargin);
        Assert.Equal(55.00m, record2.GrossMarginPercentage);

        // Verify only 1 record exists in DB for this deal
        var dealRecords = await db.DealMarginRecords.Where(d => d.TenantId == tenantId && d.DealId == dealId).ToListAsync();
        Assert.Single(dealRecords);

        // Verify total revenue in summary is 50,000 (not 100,000 duplicated)
        var summary = await service.GetProfitabilitySummaryAsync(tenantId, orgId);
        Assert.Equal(50000m, summary.TotalRevenue);
        Assert.Equal(27500m, summary.OverallTrueGrossMargin);
    }

    [Fact]
    public async Task Recommendation4_DealMarginCalculation_HandlesZeroAndNegativeRevenue()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new OrganizationHierarchyService(db);

        // Zero revenue with non-zero expenses
        var zeroRevReq = new DealMarginCalculationRequest(
            DealId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            DealTitle: "Zero Revenue Exploration",
            Revenue: 0m,
            CostOfGoodsSold: 500m,
            CourierShippingFees: 100m,
            OperatorLaborHours: 10m,
            OperatorHourlyRate: 20m
        );
        var zeroResult = await service.CalculateDealMarginAsync(tenantId, zeroRevReq);
        Assert.Equal(-800m, zeroResult.TrueGrossMargin);
        Assert.Equal(0m, zeroResult.GrossMarginPercentage);

        // Negative revenue (credit note / refund deal)
        var negRevReq = new DealMarginCalculationRequest(
            DealId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            DealTitle: "Refund Adjustment",
            Revenue: -2000m,
            CostOfGoodsSold: 0m,
            CourierShippingFees: 0m,
            OperatorLaborHours: 5m,
            OperatorHourlyRate: 20m
        );
        var negResult = await service.CalculateDealMarginAsync(tenantId, negRevReq);
        Assert.Equal(-2100m, negResult.TrueGrossMargin);
        Assert.Equal(-105.00m, negResult.GrossMarginPercentage);
    }
}
