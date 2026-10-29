using FluentAssertions;
using SGrowing.Api.Modules.Agents;
using Xunit;

namespace SGrowing.Tests.Agents;

public class AgentFleetOrchestrationTests
{
    private readonly AgentFleetOrchestrationService _service;
    private readonly Guid _testTenantId = Guid.NewGuid();

    public AgentFleetOrchestrationTests()
    {
        _service = new AgentFleetOrchestrationService();
    }

    [Fact]
    public void RolesCatalog_ContainsExactly38Roles_With17BusinessAnd21Engineering()
    {
        // Act
        var catalog = _service.GetRolesCatalog();

        // Assert
        catalog.Should().NotBeNull();
        catalog.TotalRolesCount.Should().Be(38);
        catalog.BusinessRolesCount.Should().Be(17);
        catalog.EngineeringRolesCount.Should().Be(21);
        catalog.BusinessRoles.Should().HaveCount(17);
        catalog.EngineeringRoles.Should().HaveCount(21);

        // Verify the 17 Business Roles
        var expectedBusinessRoles = new[]
        {
            "RevOpsStrategist",
            "LogisticsController",
            "ColdOutreachSdr",
            "CustomerEscalationHandler",
            "SocialGrowthHacker",
            "FinancialMarginAuditor",
            "CommunityModerator",
            "OnboardingConcierge",
            "AffiliateManager",
            "CatalogMerchandiser",
            "ComplianceOfficer",
            "ExecutiveChiefOfStaff",
            "BrandVoiceStylist",
            "RetentionSpecialist",
            "MarketplaceScout",
            "AdSpendOptimizer",
            "LocalizationAnalyst"
        };

        var actualBusinessCodes = catalog.BusinessRoles.Select(r => r.RoleCode).ToList();
        foreach (var expectedRole in expectedBusinessRoles)
        {
            actualBusinessCodes.Should().Contain(expectedRole, $"Business role {expectedRole} must be present in catalog");
        }

        // Verify the 21 Engineering & Ops Roles
        var expectedEngineeringRoles = new[]
        {
            "SoftwareArchitect",
            "BackendDeveloper",
            "FrontendSpecialist",
            "DatabaseSpecialist",
            "TestEngineer",
            "SecurityAuditor",
            "DevOpsEngineer",
            "PerformanceAnalyst",
            "ApiDesigner",
            "BuildErrorResolver",
            "RefactorCleaner",
            "CodeReviewer",
            "SlaGuardian",
            "TelemetryEngineer",
            "ScraperEngineer",
            "DockerSpecialist",
            "AspireOrchestrator",
            "MobileCapacitorEngineer",
            "DnnAiSpecialist",
            "DisasterRecoverySpecialist",
            "PromptEngineer"
        };

        var actualEngineeringCodes = catalog.EngineeringRoles.Select(r => r.RoleCode).ToList();
        foreach (var expectedRole in expectedEngineeringRoles)
        {
            actualEngineeringCodes.Should().Contain(expectedRole, $"Engineering role {expectedRole} must be present in catalog");
        }

        // Verify metadata completeness across all 38 roles
        var allRoles = catalog.BusinessRoles.Concat(catalog.EngineeringRoles);
        foreach (var role in allRoles)
        {
            role.DisplayName.Should().NotBeNullOrWhiteSpace();
            role.Description.Should().NotBeNullOrWhiteSpace();
            role.Competencies.Should().NotBeEmpty();
            role.Icon.Should().NotBeNullOrWhiteSpace();
            role.DefaultStatus.Should().NotBeNullOrWhiteSpace();
            role.SystemObjective.Should().NotBeNullOrWhiteSpace();
            Enum.IsDefined(typeof(ModelTier), role.DefaultModelTier).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("revopsstrategist", "RevOps Strategist")]
    [InlineData("LOGISTICSCONTROLLER", "Logistics Controller")]
    [InlineData("SoftwareArchitect", "Software Architect")]
    [InlineData("testengineer", "Test Engineer")]
    public void GetRoleByCode_ReturnsCorrectRole_CaseInsensitive(string roleCode, string expectedDisplayName)
    {
        // Act
        var role = _service.GetRoleByCode(roleCode);

        // Assert
        role.Should().NotBeNull();
        role!.DisplayName.Should().Be(expectedDisplayName);
    }

    [Fact]
    public void GetRoleByCode_UnknownRole_ReturnsNull()
    {
        // Act
        var role = _service.GetRoleByCode("NonExistentSpecialist");

        // Assert
        role.Should().BeNull();
    }

    [Fact]
    public async Task GoalTreeDecomposition_CodRemittanceGoal_GeneratesValidDagWithPrerequisites()
    {
        // Arrange
        var request = new DecomposeGoalRequest(
            TenantId: _testTenantId,
            ExecutiveGoalText: "Scale Monthly Cash Remittance by +35% with < 12% Return Rate",
            TargetTimeframe: "90 Days"
        );

        // Act
        var goalTree = await _service.DecomposeGoalAsync(request);

        // Assert
        goalTree.Should().NotBeNull();
        goalTree.TenantId.Should().Be(_testTenantId);
        goalTree.Status.Should().Be("Active");
        goalTree.Nodes.Should().HaveCountGreaterThanOrEqualTo(8);

        // Validate DAG structure
        bool isValidDag = _service.ValidateDag(goalTree.Nodes, out var cycleError);
        isValidDag.Should().BeTrue($"DAG must be valid and free of cycles. Error: {cycleError}");
        cycleError.Should().BeNull();

        // Check Layer 1 Strategic Pillars
        var pillars = goalTree.Nodes.Where(n => n.LayerLevel == 1).ToList();
        pillars.Should().HaveCountGreaterThanOrEqualTo(2);
        pillars.All(p => p.PrerequisiteTaskIds.Count == 0).Should().BeTrue("Strategic pillars should have 0 prerequisites");

        // Check assigned roles span both echelons
        var assignedRoles = goalTree.Nodes.Select(n => n.AssignedRoleCode).Distinct().ToList();
        assignedRoles.Should().Contain("FinancialMarginAuditor");
        assignedRoles.Should().Contain("LogisticsController");
        assignedRoles.Should().Contain("SoftwareArchitect");
        assignedRoles.Should().Contain("BackendDeveloper");
        assignedRoles.Should().Contain("TestEngineer");
        assignedRoles.Should().Contain("SlaGuardian");

        // Verify all prerequisite IDs exist
        var allNodeIds = goalTree.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var node in goalTree.Nodes)
        {
            foreach (var pid in node.PrerequisiteTaskIds)
            {
                allNodeIds.Should().Contain(pid, $"Prerequisite ID '{pid}' in node '{node.Id}' must exist in goal tree");
            }
        }
    }

    [Fact]
    public async Task GoalTreeDecomposition_ArchitectureMigrationGoal_GeneratesValidEngineeringDag()
    {
        // Arrange
        var request = new DecomposeGoalRequest(
            TenantId: _testTenantId,
            ExecutiveGoalText: "Modernize Monolith to Modular Domain Contexts with 99.99% Availability",
            TargetTimeframe: "60 Days"
        );

        // Act
        var goalTree = await _service.DecomposeGoalAsync(request);

        // Assert
        goalTree.Should().NotBeNull();
        bool isValid = _service.ValidateDag(goalTree.Nodes, out var cycleError);
        isValid.Should().BeTrue($"DAG must be acyclic. Error: {cycleError}");

        var roleCodes = goalTree.Nodes.Select(n => n.AssignedRoleCode).ToHashSet();
        roleCodes.Should().Contain("SoftwareArchitect");
        roleCodes.Should().Contain("SecurityAuditor");
        roleCodes.Should().Contain("BackendDeveloper");
        roleCodes.Should().Contain("TestEngineer");
    }

    [Theory]
    [InlineData("Audit courier COD remittance float and fee reconciliation", "FinancialMarginAuditor")]
    [InlineData("Multi-warehouse inventory routing and courier SLA oversight", "LogisticsController")]
    [InlineData("Send cold outreach cadence with personalized spintax", "ColdOutreachSdr")]
    [InlineData("Resolve escalated VIP customer dispute and prevent churn", "CustomerEscalationHandler")]
    [InlineData("Social media viral loops and TikTok growth strategy", "SocialGrowthHacker")]
    [InlineData("ROAS ad spend optimization and Meta CAPI bidding", "AdSpendOptimizer")]
    [InlineData("Translate and culturally adapt French and Arabic sales copy", "LocalizationAnalyst")]
    [InlineData("System design and modular monolith bounded contexts", "SoftwareArchitect")]
    [InlineData("PostgreSQL 17 spatial index optimization and PostGIS queries", "DatabaseSpecialist")]
    [InlineData("Automated regression test suite using xUnit v3 and TDD", "TestEngineer")]
    [InlineData("OWASP Top 10 security audit and AES-256 vault verification", "SecurityAuditor")]
    [InlineData("CI/CD pipeline automation and Docker Swarm deployment", "DevOpsEngineer")]
    [InlineData("Service uptime monitoring and SLA breach mitigation", "SlaGuardian")]
    public void RoleCompetencyMatching_AssignsCorrectRoleBasedOnTaskKeywords(string taskDescription, string expectedRoleCode)
    {
        // Act
        var matchedRole = _service.MatchRoleForTask("Task", taskDescription);

        // Assert
        matchedRole.Should().Be(expectedRoleCode);
    }

    [Theory]
    [InlineData("SoftwareArchitect", "Modular monolith system architecture design", ModelTier.ReasoningTier)]
    [InlineData("FinancialMarginAuditor", "Deep unit economics and courier remittance audit", ModelTier.ReasoningTier)]
    [InlineData("SecurityAuditor", "OWASP cryptographic vault threat model inspection", ModelTier.ReasoningTier)]
    [InlineData("SlaGuardian", "SLA latency check and heartbeat triage ping", ModelTier.FastTier)]
    [InlineData("CommunityModerator", "Channel spam filter and message toxicity detection", ModelTier.FastTier)]
    [InlineData("ColdOutreachSdr", "Draft personalized cold outreach sequence for prospects", ModelTier.StandardTier)]
    [InlineData("BackendDeveloper", "Implement C# Minimal API endpoint handler", ModelTier.StandardTier)]
    public void MultiModelLlmRouting_AssignsAppropriateModelTier(string roleCode, string taskDescription, ModelTier expectedTier)
    {
        // Act
        var resolvedTier = _service.DetermineModelTier(roleCode, taskDescription);

        // Assert
        resolvedTier.Should().Be(expectedTier);
    }

    [Fact]
    public async Task AutonomousHeartbeat_AdvancesTasks_UpdatesGoalAchievement_AndLogsActivity()
    {
        // Arrange
        var request = new DecomposeGoalRequest(
            TenantId: _testTenantId,
            ExecutiveGoalText: "Scale Monthly Cash Remittance by +35% with < 12% Return Rate",
            TargetTimeframe: "90 Days"
        );

        var tree = await _service.DecomposeGoalAsync(request);
        tree.OverallAchievementPercentage.Should().Be(0);

        // Act - Heartbeat Sweep 1
        var heartbeat1 = await _service.TriggerHeartbeatAsync(_testTenantId);

        // Assert Sweep 1
        heartbeat1.Should().NotBeNull();
        heartbeat1.TenantId.Should().Be(_testTenantId);
        heartbeat1.TasksAdvancedCount.Should().BeGreaterThan(0);
        heartbeat1.GeneratedLogs.Should().NotBeEmpty();

        // Verify task progress occurred
        var updatedTree1 = await _service.GetGoalByIdAsync(_testTenantId, tree.Id);
        updatedTree1.Should().NotBeNull();
        updatedTree1!.OverallAchievementPercentage.Should().BeGreaterThan(0);
        int sweep1Progress = updatedTree1.OverallAchievementPercentage;

        // Act - Heartbeat Sweep 2
        var heartbeat2 = await _service.TriggerHeartbeatAsync(_testTenantId);

        // Assert Sweep 2
        heartbeat2.GeneratedLogs.Should().NotBeEmpty();
        var updatedTree2 = await _service.GetGoalByIdAsync(_testTenantId, tree.Id);
        updatedTree2!.OverallAchievementPercentage.Should().BeGreaterThan(sweep1Progress);

        // Check Activity Feed
        var activityFeed = await _service.GetActivityFeedAsync(_testTenantId, limit: 50);
        activityFeed.Should().NotBeEmpty();
        activityFeed.All(l => l.TenantId == _testTenantId).Should().BeTrue();
        activityFeed.All(l => l.TokensConsumed > 0).Should().BeTrue();
        activityFeed.All(l => l.LatencyMs > 0).Should().BeTrue();
    }

    [Fact]
    public async Task DecomposeGoalAsync_EmptyOrWhitespaceGoalText_ThrowsArgumentException()
    {
        // Arrange
        var emptyReq = new DecomposeGoalRequest(_testTenantId, "");
        var whitespaceReq = new DecomposeGoalRequest(_testTenantId, "   ");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DecomposeGoalAsync(emptyReq));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DecomposeGoalAsync(whitespaceReq));
    }

    [Fact]
    public void ValidateDag_CyclicalDependency_ReturnsFalseAndReportsCycle()
    {
        // Arrange: Node A -> Node B -> Node C -> Node A (Cycle!)
        var nodeA = new MissionNode
        {
            Id = "node-A",
            Title = "Task A",
            PrerequisiteTaskIds = new List<string> { "node-C" }
        };
        var nodeB = new MissionNode
        {
            Id = "node-B",
            Title = "Task B",
            PrerequisiteTaskIds = new List<string> { "node-A" }
        };
        var nodeC = new MissionNode
        {
            Id = "node-C",
            Title = "Task C",
            PrerequisiteTaskIds = new List<string> { "node-B" }
        };

        var nodes = new List<MissionNode> { nodeA, nodeB, nodeC };

        // Act
        bool isValid = _service.ValidateDag(nodes, out var cycleError);

        // Assert
        isValid.Should().BeFalse();
        cycleError.Should().NotBeNullOrWhiteSpace();
        cycleError.Should().Contain("Cyclical dependency detected");
    }

    [Fact]
    public void ValidateDag_NonexistentPrerequisiteId_ReturnsFalseAndReportsDanglingReference()
    {
        // Arrange
        var node = new MissionNode
        {
            Id = "node-1",
            Title = "Orphaned Prereq Task",
            PrerequisiteTaskIds = new List<string> { "phantom-node-404" }
        };

        var nodes = new List<MissionNode> { node };

        // Act
        bool isValid = _service.ValidateDag(nodes, out var error);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Contain("nonexistent prerequisite ID 'phantom-node-404'");
    }
}
