using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.CRM.SupportSwarm;
using Xunit;

namespace SGrowing.Tests.CRM;

/// <summary>
/// Comprehensive test suite for Phase 44:
/// Autonomous Omnichannel AI Support Fleet, Agentic Swarm Ticket Routing & Real-Time Escalation Radar.
/// </summary>
public sealed class AgenticSupportSwarmAndCsatRadarTests
{
    private readonly AgenticSupportSwarmRouter _router = new(
        logisticsCapacity: 10,
        billingCapacity: 10,
        productAdvisorCapacity: 10,
        supervisorCapacity: 5);

    private readonly InConversationCsatRadarService _csatRadar = new();
    private readonly AutonomousMacroSynthesizerService _macroSynthesizer = new();

    #region 1. Domain Category Routing to AI Specialist Personas

    [Fact]
    public void RouteTicket_OrderDeliveryLogisticsCategory_RoutesToLogisticsDispatcherAi()
    {
        // Arrange
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-LOG-01",
            CustomerId: "CUST-DZ-101",
            Channel: SupportChannel.WhatsApp,
            Category: TicketCategory.OrderDeliveryLogistics,
            Priority: TicketPriority.Medium,
            CustomerLtvDzd: 45_000m,
            SentimentScore: 0.10,
            AssignedPersona: AgentPersona.ProductAdvisorAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.Should().NotBeNull();
        response.AssignedPersona.Should().Be(AgentPersona.LogisticsDispatcherAi);
        response.Status.Should().Be(TicketStatus.InProgress);
        response.FinalPriority.Should().Be(TicketPriority.Medium);
        response.RoutingReason.Should().Contain("LogisticsDispatcherAi");
    }

    [Fact]
    public void RouteTicket_BillingAndRefundsCategory_RoutesToBillingSpecialistAi()
    {
        // Arrange
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-BIL-01",
            CustomerId: "CUST-DZ-102",
            Channel: SupportChannel.WebChat,
            Category: TicketCategory.BillingAndRefunds,
            Priority: TicketPriority.Medium,
            CustomerLtvDzd: 60_000m,
            SentimentScore: -0.10,
            AssignedPersona: AgentPersona.LogisticsDispatcherAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.BillingSpecialistAi);
        response.Status.Should().Be(TicketStatus.InProgress);
        response.FinalPriority.Should().Be(TicketPriority.Medium);
        response.RoutingReason.Should().Contain("BillingSpecialistAi");
    }

    [Fact]
    public void RouteTicket_TechnicalProductCategory_RoutesToProductAdvisorAi()
    {
        // Arrange
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-TECH-01",
            CustomerId: "CUST-DZ-103",
            Channel: SupportChannel.Telegram,
            Category: TicketCategory.TechnicalProduct,
            Priority: TicketPriority.Low,
            CustomerLtvDzd: 30_000m,
            SentimentScore: 0.25,
            AssignedPersona: AgentPersona.BillingSpecialistAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.ProductAdvisorAi);
        response.Status.Should().Be(TicketStatus.InProgress);
        response.FinalPriority.Should().Be(TicketPriority.Low);
    }

    [Fact]
    public void RouteTicket_WholesaleB2BCategory_RoutesToProductAdvisorAi()
    {
        // Arrange
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-B2B-01",
            CustomerId: "CUST-DZ-104",
            Channel: SupportChannel.Email,
            Category: TicketCategory.WholesaleB2B,
            Priority: TicketPriority.Medium,
            CustomerLtvDzd: 120_000m, // Below 150k threshold
            SentimentScore: 0.05,
            AssignedPersona: AgentPersona.BillingSpecialistAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.ProductAdvisorAi);
        response.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public void RouteTicket_AccountAndSecurityCategory_RoutesToSeniorHumanSupervisor()
    {
        // Arrange
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-SEC-01",
            CustomerId: "CUST-DZ-105",
            Channel: SupportChannel.WebChat,
            Category: TicketCategory.AccountAndSecurity,
            Priority: TicketPriority.Medium,
            CustomerLtvDzd: 25_000m,
            SentimentScore: 0.0,
            AssignedPersona: AgentPersona.LogisticsDispatcherAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.SeniorHumanSupervisor);
        response.Status.Should().Be(TicketStatus.Escalated);
    }

    #endregion

    #region 2. VIP High-LTV Escalation & Urgent Priority

    [Fact]
    public void RouteTicket_HighLtvExceeding150kDzd_EscalatesDirectlyToSeniorHumanSupervisor()
    {
        // Arrange: VIP Customer with 280,000 DZD LTV
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-VIP-01",
            CustomerId: "CUST-VIP-777",
            Channel: SupportChannel.WhatsApp,
            Category: TicketCategory.OrderDeliveryLogistics,
            Priority: TicketPriority.Medium,
            CustomerLtvDzd: 280_000m,
            SentimentScore: 0.10,
            AssignedPersona: AgentPersona.LogisticsDispatcherAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.SeniorHumanSupervisor);
        response.FinalPriority.Should().Be(TicketPriority.UrgentVip);
        response.Status.Should().Be(TicketStatus.Escalated);
        response.RoutingReason.Should().Contain("VIP Customer Escalation");
    }

    [Fact]
    public void RouteTicket_LtvBoundary150kDzdExactly_DoesNotTriggerVipRuleOnLtvAlone()
    {
        // Arrange: Exactly 150,000 DZD (not > 150,000)
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-BOUNDARY-01",
            CustomerId: "CUST-DZ-106",
            Channel: SupportChannel.Email,
            Category: TicketCategory.OrderDeliveryLogistics,
            Priority: TicketPriority.Medium,
            CustomerLtvDzd: 150_000.00m,
            SentimentScore: 0.20,
            AssignedPersona: AgentPersona.ProductAdvisorAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.LogisticsDispatcherAi);
        response.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public void RouteTicket_LtvBoundary150kAndOneCent_TriggersVipEscalation()
    {
        // Arrange: 150,000.01 DZD
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-BOUNDARY-02",
            CustomerId: "CUST-DZ-107",
            Channel: SupportChannel.Email,
            Category: TicketCategory.BillingAndRefunds,
            Priority: TicketPriority.Low,
            CustomerLtvDzd: 150_000.01m,
            SentimentScore: 0.05,
            AssignedPersona: AgentPersona.ProductAdvisorAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.SeniorHumanSupervisor);
        response.FinalPriority.Should().Be(TicketPriority.UrgentVip);
        response.Status.Should().Be(TicketStatus.Escalated);
    }

    [Fact]
    public void RouteTicket_UrgentVipPriorityWithLowLtv_EscalatesToSeniorHumanSupervisor()
    {
        // Arrange: Low LTV but explicitly flagged UrgentVip
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-URGENT-01",
            CustomerId: "CUST-DZ-108",
            Channel: SupportChannel.Telegram,
            Category: TicketCategory.TechnicalProduct,
            Priority: TicketPriority.UrgentVip,
            CustomerLtvDzd: 12_000m,
            SentimentScore: 0.0,
            AssignedPersona: AgentPersona.ProductAdvisorAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.SeniorHumanSupervisor);
        response.FinalPriority.Should().Be(TicketPriority.UrgentVip);
        response.Status.Should().Be(TicketStatus.Escalated);
    }

    #endregion

    #region 3. Severe Negative Sentiment Escalation

    [Fact]
    public void RouteTicket_SevereNegativeSentimentMinus060_EscalatesToSeniorHumanSupervisorAndHighPriority()
    {
        // Arrange: Sentiment exactly -0.60
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-SENT-01",
            CustomerId: "CUST-DZ-109",
            Channel: SupportChannel.WhatsApp,
            Category: TicketCategory.OrderDeliveryLogistics,
            Priority: TicketPriority.Low,
            CustomerLtvDzd: 20_000m,
            SentimentScore: -0.60,
            AssignedPersona: AgentPersona.LogisticsDispatcherAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.SeniorHumanSupervisor);
        response.FinalPriority.Should().Be(TicketPriority.High);
        response.Status.Should().Be(TicketStatus.Escalated);
        response.RoutingReason.Should().Contain("Severe negative sentiment");
    }

    [Fact]
    public void RouteTicket_SevereNegativeSentimentMinus085_ElevatesMediumPriorityToHigh()
    {
        // Arrange: Sentiment -0.85
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-SENT-02",
            CustomerId: "CUST-DZ-110",
            Channel: SupportChannel.WebChat,
            Category: TicketCategory.BillingAndRefunds,
            Priority: TicketPriority.Medium,
            CustomerLtvDzd: 50_000m,
            SentimentScore: -0.85,
            AssignedPersona: AgentPersona.BillingSpecialistAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.SeniorHumanSupervisor);
        response.FinalPriority.Should().Be(TicketPriority.High);
        response.Status.Should().Be(TicketStatus.Escalated);
    }

    [Fact]
    public void RouteTicket_ModerateNegativeSentimentMinus059_DoesNotEscalateToSupervisor()
    {
        // Arrange: Sentiment -0.59 (just above -0.60 threshold)
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-SENT-03",
            CustomerId: "CUST-DZ-111",
            Channel: SupportChannel.Email,
            Category: TicketCategory.BillingAndRefunds,
            Priority: TicketPriority.Medium,
            CustomerLtvDzd: 35_000m,
            SentimentScore: -0.59,
            AssignedPersona: AgentPersona.BillingSpecialistAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var response = _router.RouteTicket(ticket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.BillingSpecialistAi);
        response.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public void RouteTicket_InvalidSentimentScore_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidTicket = new CustomerSupportTicket(
            TicketId: "TCK-ERR-01",
            CustomerId: "CUST-DZ-112",
            Channel: SupportChannel.Email,
            Category: TicketCategory.OrderDeliveryLogistics,
            Priority: TicketPriority.Low,
            CustomerLtvDzd: 10_000m,
            SentimentScore: 1.50, // Invalid: must be <= 1.0
            AssignedPersona: AgentPersona.LogisticsDispatcherAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act & Assert
        var act = () => _router.RouteTicket(invalidTicket);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RouteTicket_NegativeLtv_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidTicket = new CustomerSupportTicket(
            TicketId: "TCK-ERR-02",
            CustomerId: "CUST-DZ-113",
            Channel: SupportChannel.Email,
            Category: TicketCategory.OrderDeliveryLogistics,
            Priority: TicketPriority.Low,
            CustomerLtvDzd: -100m,
            SentimentScore: 0.0,
            AssignedPersona: AgentPersona.LogisticsDispatcherAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act & Assert
        var act = () => _router.RouteTicket(invalidTicket);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region 4. Fleet Capacity Constraints, Reassignment & Ticket Lifecycle

    [Fact]
    public void RouteTicket_AiPersonaAtMaxCapacity_OverflowsToSeniorHumanSupervisor()
    {
        // Arrange: Router with logistics capacity = 2
        var localRouter = new AgenticSupportSwarmRouter(logisticsCapacity: 2, supervisorCapacity: 5);

        for (int i = 1; i <= 2; i++)
        {
            localRouter.RouteTicket(new CustomerSupportTicket(
                TicketId: $"TCK-CAP-{i}",
                CustomerId: $"CUST-{i}",
                Channel: SupportChannel.WhatsApp,
                Category: TicketCategory.OrderDeliveryLogistics,
                Priority: TicketPriority.Low,
                CustomerLtvDzd: 10_000m,
                SentimentScore: 0.0,
                AssignedPersona: AgentPersona.LogisticsDispatcherAi,
                Status: TicketStatus.Open,
                CreatedAtUtc: DateTime.UtcNow
            ));
        }

        // Act: 3rd ticket arrives when Logistics capacity (2) is saturated
        var overflowTicket = new CustomerSupportTicket(
            TicketId: "TCK-CAP-OVERFLOW",
            CustomerId: "CUST-99",
            Channel: SupportChannel.WhatsApp,
            Category: TicketCategory.OrderDeliveryLogistics,
            Priority: TicketPriority.Low,
            CustomerLtvDzd: 10_000m,
            SentimentScore: 0.0,
            AssignedPersona: AgentPersona.LogisticsDispatcherAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );

        var response = localRouter.RouteTicket(overflowTicket);

        // Assert
        response.AssignedPersona.Should().Be(AgentPersona.SeniorHumanSupervisor);
        response.Status.Should().Be(TicketStatus.Escalated);
        response.RoutingReason.Should().Contain("Capacity Alert");
    }

    [Fact]
    public void ReassignTicket_ValidTicket_UpdatesAssignedPersonaAndWorkload()
    {
        // Arrange
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-REASSIGN-01",
            CustomerId: "CUST-DZ-114",
            Channel: SupportChannel.WebChat,
            Category: TicketCategory.OrderDeliveryLogistics,
            Priority: TicketPriority.Medium,
            CustomerLtvDzd: 20_000m,
            SentimentScore: 0.0,
            AssignedPersona: AgentPersona.LogisticsDispatcherAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );
        _router.RouteTicket(ticket);

        // Act
        var updated = _router.ReassignTicket("TCK-REASSIGN-01", AgentPersona.BillingSpecialistAi, "Customer inquiry shifted to refund terms.");

        // Assert
        updated.AssignedPersona.Should().Be(AgentPersona.BillingSpecialistAi);
        updated.ReassignedReason.Should().Be("Customer inquiry shifted to refund terms.");
        updated.Status.Should().Be(TicketStatus.InProgress);

        var workloads = _router.GetAgentWorkloadSummary();
        var logisticsLoad = workloads.First(w => w.Persona == AgentPersona.LogisticsDispatcherAi).ActiveTicketsCount;
        var billingLoad = workloads.First(w => w.Persona == AgentPersona.BillingSpecialistAi).ActiveTicketsCount;

        logisticsLoad.Should().Be(0);
        billingLoad.Should().Be(1);
    }

    [Fact]
    public void ReassignTicket_NonExistentTicket_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var act = () => _router.ReassignTicket("NON-EXISTENT", AgentPersona.ProductAdvisorAi, "Test reason");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void ReassignTicket_AlreadyResolvedTicket_ThrowsInvalidOperationException()
    {
        // Arrange
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-RESOLVED-01",
            CustomerId: "CUST-DZ-115",
            Channel: SupportChannel.Email,
            Category: TicketCategory.TechnicalProduct,
            Priority: TicketPriority.Low,
            CustomerLtvDzd: 15_000m,
            SentimentScore: 0.5,
            AssignedPersona: AgentPersona.ProductAdvisorAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );
        _router.RouteTicket(ticket);
        _router.ResolveTicket("TCK-RESOLVED-01", "Issue solved via macro.");

        // Act & Assert
        var act = () => _router.ReassignTicket("TCK-RESOLVED-01", AgentPersona.SeniorHumanSupervisor, "Try reassigning");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResolveTicket_DecrementsActiveTicketsCountAndMarksStatusResolved()
    {
        // Arrange
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-TO-RESOLVE",
            CustomerId: "CUST-DZ-116",
            Channel: SupportChannel.Telegram,
            Category: TicketCategory.BillingAndRefunds,
            Priority: TicketPriority.Medium,
            CustomerLtvDzd: 30_000m,
            SentimentScore: 0.0,
            AssignedPersona: AgentPersona.BillingSpecialistAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );
        _router.RouteTicket(ticket);

        var beforeWorkload = _router.GetAgentWorkloadSummary().First(w => w.Persona == AgentPersona.BillingSpecialistAi).ActiveTicketsCount;
        beforeWorkload.Should().Be(1);

        // Act
        var resolved = _router.ResolveTicket("TCK-TO-RESOLVE", "Refund processed via SATIM gateway.");

        // Assert
        resolved.Status.Should().Be(TicketStatus.Resolved);
        resolved.ResolutionNotes.Should().Be("Refund processed via SATIM gateway.");
        resolved.ResolvedAtUtc.Should().NotBeNull();

        var afterWorkload = _router.GetAgentWorkloadSummary().First(w => w.Persona == AgentPersona.BillingSpecialistAi).ActiveTicketsCount;
        afterWorkload.Should().Be(0);
    }

    [Fact]
    public void GetTicket_ExistingTicket_ReturnsTicketDetails()
    {
        // Arrange
        var ticket = new CustomerSupportTicket(
            TicketId: "TCK-GET-01",
            CustomerId: "CUST-DZ-117",
            Channel: SupportChannel.Sms,
            Category: TicketCategory.TechnicalProduct,
            Priority: TicketPriority.Low,
            CustomerLtvDzd: 10_000m,
            SentimentScore: 0.2,
            AssignedPersona: AgentPersona.ProductAdvisorAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        );
        _router.RouteTicket(ticket);

        // Act
        var retrieved = _router.GetTicket("TCK-GET-01");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.TicketId.Should().Be("TCK-GET-01");
        retrieved.CustomerId.Should().Be("CUST-DZ-117");
    }

    [Fact]
    public void GetTicket_NonExistentId_ReturnsNull()
    {
        // Act
        var result = _router.GetTicket("UNKNOWN-TICKET-ID");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetAllTickets_And_GetTicketsByStatus_FilterCorrectly()
    {
        // Arrange
        var router = new AgenticSupportSwarmRouter();
        router.RouteTicket(new CustomerSupportTicket(
            TicketId: "TCK-STATUS-01",
            CustomerId: "CUST-1",
            Channel: SupportChannel.WhatsApp,
            Category: TicketCategory.OrderDeliveryLogistics,
            Priority: TicketPriority.Low,
            CustomerLtvDzd: 10_000m,
            SentimentScore: 0.1,
            AssignedPersona: AgentPersona.LogisticsDispatcherAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        ));

        router.RouteTicket(new CustomerSupportTicket(
            TicketId: "TCK-STATUS-02",
            CustomerId: "CUST-2",
            Channel: SupportChannel.WhatsApp,
            Category: TicketCategory.OrderDeliveryLogistics,
            Priority: TicketPriority.UrgentVip,
            CustomerLtvDzd: 200_000m,
            SentimentScore: 0.1,
            AssignedPersona: AgentPersona.LogisticsDispatcherAi,
            Status: TicketStatus.Open,
            CreatedAtUtc: DateTime.UtcNow
        ));

        // Act
        var all = router.GetAllTickets();
        var escalated = router.GetTicketsByStatus(TicketStatus.Escalated);
        var inProgress = router.GetTicketsByStatus(TicketStatus.InProgress);

        // Assert
        all.Should().HaveCount(2);
        escalated.Should().HaveCount(1);
        inProgress.Should().HaveCount(1);
    }

    #endregion

    #region 5. In-Conversation CSAT Radar: Sentiment Drops, Keywords & CSAT Prediction

    [Fact]
    public void AnalyzeConversation_EmptyCustomerMessages_ReturnsBaselineCsatWithNoAlerts()
    {
        // Arrange: Agent messages only, no customer replies yet
        var messages = new List<SupportConversationMessage>
        {
            new("MSG-01", "TCK-01", "AiAgent", "Bonjour, comment puis-je vous aider ?", 0.5, DateTime.UtcNow)
        };

        // Act
        var result = _csatRadar.AnalyzeConversation("TCK-01", "CUST-DZ-201", messages);

        // Assert
        result.PredictedCsatScore.Should().Be(4.50);
        result.RiskLevel.Should().Be(CsatRiskLevel.LowRisk);
        result.Alert.Should().BeNull();
        result.SuggestedDeEscalationDiscountPct.Should().Be(0.0m);
    }

    [Fact]
    public void AnalyzeConversation_SentimentDropExceedingPoint40_TriggersHighRiskChurnCandidate()
    {
        // Arrange: Initial sentiment = +0.30, Final sentiment = -0.30 (Drop = 0.60 > 0.40)
        var messages = new List<SupportConversationMessage>
        {
            new("MSG-01", "TCK-02", "Customer", "Bonjour, je voudrais savoir où en est mon colis.", 0.30, DateTime.UtcNow.AddMinutes(-30)),
            new("MSG-02", "TCK-02", "AiAgent", "Le transporteur nous signale un retard d'acheminement.", 0.10, DateTime.UtcNow.AddMinutes(-20)),
            new("MSG-03", "TCK-02", "Customer", "C'est inacceptable, j'avais besoin de ce colis aujourd'hui !", -0.30, DateTime.UtcNow.AddMinutes(-10))
        };

        // Act
        var result = _csatRadar.AnalyzeConversation("TCK-02", "CUST-DZ-202", messages);

        // Assert
        result.SentimentDelta.Should().Be(0.60);
        result.RiskLevel.Should().Be(CsatRiskLevel.HighRiskChurnCandidate);
        result.Alert.Should().NotBeNull();
        result.Alert!.SuggestedDeEscalationDiscountPct.Should().Be(10.0m);
        result.Alert.TriggerReason.Should().Contain("Severe sentiment degradation");
    }

    [Fact]
    public void AnalyzeConversation_SentimentDropBelowPoint40_DoesNotTriggerHighRisk()
    {
        // Arrange: Initial = +0.20, Final = -0.10 (Drop = 0.30 <= 0.40)
        var messages = new List<SupportConversationMessage>
        {
            new("MSG-01", "TCK-03", "Customer", "Bonjour, où est ma facture ?", 0.20, DateTime.UtcNow.AddMinutes(-20)),
            new("MSG-02", "TCK-03", "Customer", "Je ne la trouve toujours pas dans mon espace.", -0.10, DateTime.UtcNow.AddMinutes(-10))
        };

        // Act
        var result = _csatRadar.AnalyzeConversation("TCK-03", "CUST-DZ-203", messages);

        // Assert
        result.SentimentDelta.Should().Be(0.30);
        result.RiskLevel.Should().NotBe(CsatRiskLevel.HighRiskChurnCandidate);
        result.RiskLevel.Should().NotBe(CsatRiskLevel.CriticalInterventionNeeded);
    }

    [Theory]
    [InlineData("C'est une arnaque pure et simple !", "arnaque")]
    [InlineData("Je vais porter plainte à la police !", "police")]
    [InlineData("Mon avocat va s'occuper de votre dossier.", "avocat")]
    [InlineData("Vous êtes des escrocs professionnels.", "escroc")]
    [InlineData("Passez-moi le directeur immédiatement !", "directeur")]
    [InlineData("Je veux un remboursement immédiatement !", "remboursement immédiatement")]
    [InlineData("This is a total scam service.", "scam")]
    [InlineData("I will report this fraud to the authorities.", "fraud")]
    public void AnalyzeConversation_EscalationTriggerKeywords_TriggersCriticalInterventionNeeded(string customerText, string expectedKeyword)
    {
        // Arrange
        var messages = new List<SupportConversationMessage>
        {
            new("MSG-01", "TCK-KW", "Customer", customerText, -0.50, DateTime.UtcNow)
        };

        // Act
        var result = _csatRadar.AnalyzeConversation("TCK-KW", "CUST-DZ-KW", messages);

        // Assert
        result.TriggerKeywordsDetected.Should().BeTrue();
        result.MatchedKeywords.Should().Contain(expectedKeyword);
        result.RiskLevel.Should().Be(CsatRiskLevel.CriticalInterventionNeeded);
        result.PredictedCsatScore.Should().BeLessThanOrEqualTo(1.90);
        result.SuggestedDeEscalationDiscountPct.Should().Be(15.0m);
        result.Alert.Should().NotBeNull();
        result.Alert!.RiskLevel.Should().Be(CsatRiskLevel.CriticalInterventionNeeded);
        result.Alert.TriggerReason.Should().Contain("Legal/fraud escalation keywords detected");
    }

    [Fact]
    public void AnalyzeConversation_MultipleKeywordsInThread_CollectsAllMatchedKeywords()
    {
        // Arrange
        var messages = new List<SupportConversationMessage>
        {
            new("MSG-01", "TCK-MULTI", "Customer", "Une vraie arnaque, je préviens mon avocat et la police !", -0.70, DateTime.UtcNow)
        };

        // Act
        var result = _csatRadar.AnalyzeConversation("TCK-MULTI", "CUST-MULTI", messages);

        // Assert
        result.MatchedKeywords.Should().Contain("arnaque");
        result.MatchedKeywords.Should().Contain("avocat");
        result.MatchedKeywords.Should().Contain("police");
        result.RiskLevel.Should().Be(CsatRiskLevel.CriticalInterventionNeeded);
    }

    [Fact]
    public void AnalyzeConversation_FrustrationRepetitionLoop3NegativeMessages_FlagsFrustrationLoop()
    {
        // Arrange: 3 consecutive customer messages with negative sentiment
        var messages = new List<SupportConversationMessage>
        {
            new("MSG-01", "TCK-LOOP", "Customer", "Le bouton ne marche pas.", -0.20, DateTime.UtcNow.AddMinutes(-15)),
            new("MSG-02", "TCK-LOOP", "AiAgent", "Avez-vous vidé le cache de votre navigateur ?", 0.0, DateTime.UtcNow.AddMinutes(-12)),
            new("MSG-03", "TCK-LOOP", "Customer", "Oui déjà fait, toujours rien.", -0.30, DateTime.UtcNow.AddMinutes(-10)),
            new("MSG-04", "TCK-LOOP", "AiAgent", "Essayez en navigation privée.", 0.0, DateTime.UtcNow.AddMinutes(-8)),
            new("MSG-05", "TCK-LOOP", "Customer", "Arrêtez de me répéter la même chose !", -0.40, DateTime.UtcNow.AddMinutes(-5))
        };

        // Act
        var result = _csatRadar.AnalyzeConversation("TCK-LOOP", "CUST-DZ-LOOP", messages);

        // Assert
        result.FrustrationLoopDetected.Should().BeTrue();
        result.Alert.Should().NotBeNull();
        result.Alert!.TriggerReason.Should().Contain("Repetition frustration loop");
    }

    [Fact]
    public void AnalyzeConversation_ExtremelyNegativeSentiment_ClampsCsatToMinimumOne()
    {
        // Arrange: Heavily negative text with multiple keywords and huge sentiment drop
        var messages = new List<SupportConversationMessage>
        {
            new("MSG-01", "TCK-CLAMP", "Customer", "Bonjour !", 0.90, DateTime.UtcNow.AddMinutes(-30)),
            new("MSG-02", "TCK-CLAMP", "Customer", "Arnaque ! Police ! Escroc ! Remboursement immédiatement !", -1.0, DateTime.UtcNow)
        };

        // Act
        var result = _csatRadar.AnalyzeConversation("TCK-CLAMP", "CUST-CLAMP", messages);

        // Assert
        result.PredictedCsatScore.Should().BeInRange(1.0, 5.0);
        result.PredictedCsatScore.Should().Be(1.0);
    }

    [Fact]
    public void AnalyzeConversation_HighlyPositiveCustomer_ClampsCsatToMaximumFive()
    {
        // Arrange
        var messages = new List<SupportConversationMessage>
        {
            new("MSG-01", "TCK-PERFECT", "Customer", "Merci infiniment, tout fonctionne à merveille !", 1.0, DateTime.UtcNow)
        };

        // Act
        var result = _csatRadar.AnalyzeConversation("TCK-PERFECT", "CUST-PERFECT", messages);

        // Assert
        result.PredictedCsatScore.Should().Be(5.0);
        result.RiskLevel.Should().Be(CsatRiskLevel.LowRisk);
        result.Alert.Should().BeNull();
    }

    [Fact]
    public void AlertLifecycle_GetActiveAlertsAndAcknowledgeAlert_TransitionsStateCorrectly()
    {
        // Arrange: Generate an alert
        var radar = new InConversationCsatRadarService();
        var messages = new List<SupportConversationMessage>
        {
            new("MSG-01", "TCK-ALERT-01", "Customer", "C'est une arnaque !", -0.60, DateTime.UtcNow)
        };
        var scan = radar.AnalyzeConversation("TCK-ALERT-01", "CUST-DZ-999", messages);
        scan.Alert.Should().NotBeNull();
        var alertId = scan.Alert!.AlertId;

        // Act: Get active alerts
        var activeAlertsBefore = radar.GetActiveAlerts();
        activeAlertsBefore.Should().Contain(a => a.AlertId == alertId);

        // Acknowledge alert
        var acknowledged = radar.AcknowledgeAlert(alertId, acknowledgedBy: "Supervisor-Amina");

        // Assert
        acknowledged.Acknowledged.Should().BeTrue();
        acknowledged.AcknowledgedBy.Should().Be("Supervisor-Amina");
        acknowledged.AcknowledgedAtUtc.Should().NotBeNull();

        var activeAlertsAfter = radar.GetActiveAlerts();
        activeAlertsAfter.Should().NotContain(a => a.AlertId == alertId);

        var retrieved = radar.GetAlert(alertId);
        retrieved.Should().NotBeNull();
        retrieved!.Acknowledged.Should().BeTrue();
    }

    [Fact]
    public void AcknowledgeAlert_NonExistentAlertId_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var act = () => _csatRadar.AcknowledgeAlert("NON-EXISTENT-ALERT");
        act.Should().Throw<KeyNotFoundException>();
    }

    #endregion

    #region 6. Autonomous Macro Synthesis & Variable Parameterization

    [Fact]
    public void ParameterizeResolutionText_ReplacesSalutationsAndCustomerNames()
    {
        // Arrange
        var rawText = "Bonjour Karim, nous avons vérifié votre dossier.";

        // Act
        var result = AutonomousMacroSynthesizerService.ParameterizeResolutionText(rawText);

        // Assert
        result.Should().Be("Bonjour {CustomerName}, nous avons vérifié votre dossier.");
    }

    [Fact]
    public void ParameterizeResolutionText_ReplacesOrderNumbersAccurately()
    {
        // Arrange
        var rawText = "Votre commande CMD-2026-88129 a été expédiée avec succès.";

        // Act
        var result = AutonomousMacroSynthesizerService.ParameterizeResolutionText(rawText);

        // Assert
        result.Should().Be("Votre commande {OrderNumber} a été expédiée avec succès.");
    }

    [Fact]
    public void ParameterizeResolutionText_ReplacesCarrierNamesAccurately()
    {
        // Arrange
        var rawText1 = "Le colis est pris en charge par Yalidine Express aujourd'hui.";
        var rawText2 = "Votre livraison est confiée à ZR Express pour Alger.";
        var rawText3 = "Acheminement via Maystro Delivery en cours.";

        // Act
        var result1 = AutonomousMacroSynthesizerService.ParameterizeResolutionText(rawText1);
        var result2 = AutonomousMacroSynthesizerService.ParameterizeResolutionText(rawText2);
        var result3 = AutonomousMacroSynthesizerService.ParameterizeResolutionText(rawText3);

        // Assert
        result1.Should().Be("Le colis est pris en charge par {CarrierName} aujourd'hui.");
        result2.Should().Be("Votre livraison est confiée à {CarrierName} pour Alger.");
        result3.Should().Be("Acheminement via {CarrierName} en cours.");
    }

    [Fact]
    public void ParameterizeResolutionText_ReplacesTrackingNumbersAccurately()
    {
        // Arrange
        var rawText = "Voici le numéro de suivi : TRK-99881123 pour votre colis.";

        // Act
        var result = AutonomousMacroSynthesizerService.ParameterizeResolutionText(rawText);

        // Assert
        result.Should().Be("Voici le numéro de suivi : {TrackingNumber} pour votre colis.");
    }

    [Fact]
    public void ParameterizeResolutionText_CombinedMultiEntityExtraction()
    {
        // Arrange
        var rawText = "Bonjour M. Benali, votre commande CMD-98124 a été remise à Yalidine avec le suivi TRK-5544321.";

        // Act
        var result = AutonomousMacroSynthesizerService.ParameterizeResolutionText(rawText);

        // Assert
        result.Should().Be("Bonjour {CustomerName}, votre commande {OrderNumber} a été remise à {CarrierName} avec le suivi {TrackingNumber}.");
    }

    [Fact]
    public void SynthesizeMacroFromResolution_ValidInput_CreatesMacroWithDefaultConfidenceAndExtractsVariables()
    {
        // Arrange
        var rawResolution = "Cher client Mohamed, votre remboursement pour la commande ORD-44512 a été validé.";

        // Act
        var macro = _macroSynthesizer.SynthesizeMacroFromResolution(
            TicketCategory.BillingAndRefunds,
            "Remboursement commande validé",
            rawResolution,
            confidence: 0.94);

        // Assert
        macro.Should().NotBeNull();
        macro.MacroId.Should().StartWith("MCR-");
        macro.Category.Should().Be(TicketCategory.BillingAndRefunds);
        macro.Title.Should().Be("Remboursement commande validé");
        macro.TemplateText.Should().Be("Cher client {CustomerName}, votre remboursement pour la commande {OrderNumber} a été validé.");
        macro.ConfidenceScore.Should().Be(0.94);
        macro.ConversionSuccessRate.Should().Be(1.0);
        macro.UsageCount.Should().Be(1);
    }

    [Fact]
    public void SynthesizeMacroFromResolution_InvalidConfidence_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => _macroSynthesizer.SynthesizeMacroFromResolution(
            TicketCategory.OrderDeliveryLogistics,
            "Test",
            "Resolution text",
            confidence: 1.50);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetRecommendedMacros_ReturnsCategoryMacrosRankedBySuccessRateAndConfidence()
    {
        // Act
        var macros = _macroSynthesizer.GetRecommendedMacros(TicketCategory.BillingAndRefunds);

        // Assert
        macros.Should().NotBeEmpty();
        macros.Should().OnlyContain(m => m.Category == TicketCategory.BillingAndRefunds);
        macros.Should().BeInDescendingOrder(m => m.ConversionSuccessRate);
    }

    [Fact]
    public void RecordMacroUsage_SuccessfulResolution_UpdatesUsageCountAndSuccessRate()
    {
        // Arrange: Create a macro with initial 1 usage / 1 success
        var macro = _macroSynthesizer.SynthesizeMacroFromResolution(
            TicketCategory.TechnicalProduct,
            "Guide de dépannage",
            "Bonjour Samy, voici le guide pour le produit #12345.",
            0.90);

        // Act: 2nd usage succeeds -> 2 / 2 = 1.0 (100%)
        var updated1 = _macroSynthesizer.RecordMacroUsage(macro.MacroId, resolvedIssue: true);

        // Act: 3rd usage fails -> 2 / 3 = 0.6667 (66.67%)
        var updated2 = _macroSynthesizer.RecordMacroUsage(macro.MacroId, resolvedIssue: false);

        // Assert
        updated1.UsageCount.Should().Be(2);
        updated1.SuccessfulResolutionsCount.Should().Be(2);
        updated1.ConversionSuccessRate.Should().Be(1.0);

        updated2.UsageCount.Should().Be(3);
        updated2.SuccessfulResolutionsCount.Should().Be(2);
        updated2.ConversionSuccessRate.Should().Be(0.6667);
    }

    [Fact]
    public void RecordMacroUsage_NonExistentMacroId_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var act = () => _macroSynthesizer.RecordMacroUsage("MCR-UNKNOWN", resolvedIssue: true);
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GetAllMacros_And_GetMacro_ReturnExpectedData()
    {
        // Act
        var all = _macroSynthesizer.GetAllMacros();
        var sample = all.First();
        var fetched = _macroSynthesizer.GetMacro(sample.MacroId);

        // Assert
        all.Should().HaveCountGreaterThanOrEqualTo(5); // At least the 5 seed macros
        fetched.Should().NotBeNull();
        fetched!.MacroId.Should().Be(sample.MacroId);
    }

    #endregion

    #region 7. Concurrency & Multi-Threaded Stress Testing

    [Fact]
    public void ConcurrentTicketRouting_UnderMultiThreadedSwarm_PreservesWorkloadConsistency()
    {
        // Arrange: High-throughput parallel routing across multiple threads
        var router = new AgenticSupportSwarmRouter(
            logisticsCapacity: 500,
            billingCapacity: 500,
            productAdvisorCapacity: 500,
            supervisorCapacity: 200);

        const int ticketCount = 120;

        // Act
        Parallel.For(0, ticketCount, i =>
        {
            var category = (i % 3) switch
            {
                0 => TicketCategory.OrderDeliveryLogistics,
                1 => TicketCategory.BillingAndRefunds,
                _ => TicketCategory.TechnicalProduct
            };

            var ticket = new CustomerSupportTicket(
                TicketId: $"CONC-TCK-{i}",
                CustomerId: $"CUST-CONC-{i}",
                Channel: SupportChannel.WhatsApp,
                Category: category,
                Priority: TicketPriority.Medium,
                CustomerLtvDzd: 10_000m + i,
                SentimentScore: 0.10,
                AssignedPersona: AgentPersona.LogisticsDispatcherAi,
                Status: TicketStatus.Open,
                CreatedAtUtc: DateTime.UtcNow
            );

            router.RouteTicket(ticket);
        });

        // Assert
        var allTickets = router.GetAllTickets();
        allTickets.Should().HaveCount(ticketCount);

        var workloads = router.GetAgentWorkloadSummary();
        var totalActive = workloads.Sum(w => w.ActiveTicketsCount);
        totalActive.Should().Be(ticketCount);
    }

    [Fact]
    public void ConcurrentConversationScanning_GeneratesDistinctAlertsWithoutCollisions()
    {
        // Arrange
        var radar = new InConversationCsatRadarService();
        const int parallelScans = 40;

        // Act
        Parallel.For(0, parallelScans, i =>
        {
            var messages = new List<SupportConversationMessage>
            {
                new($"CONC-MSG-{i}", $"TCK-CONC-{i}", "Customer", $"C'est une arnaque intolérable thread {i} !", -0.75, DateTime.UtcNow)
            };

            radar.AnalyzeConversation($"TCK-CONC-{i}", $"CUST-CONC-{i}", messages);
        });

        // Assert
        var activeAlerts = radar.GetActiveAlerts();
        activeAlerts.Should().HaveCount(parallelScans);
        activeAlerts.Select(a => a.AlertId).Distinct().Should().HaveCount(parallelScans);
    }

    [Fact]
    public void ConcurrentMacroUsageFeedback_UpdatesCountersAtomically()
    {
        // Arrange
        var synthesizer = new AutonomousMacroSynthesizerService();
        var macro = synthesizer.SynthesizeMacroFromResolution(
            TicketCategory.WholesaleB2B,
            "Validation B2B",
            "Bonjour Omar, devis ORD-1199 validé.",
            0.90);

        const int iterations = 50;

        // Act
        Parallel.For(0, iterations, i =>
        {
            var isSuccess = (i % 2 == 0); // 25 successes, 25 failures
            synthesizer.RecordMacroUsage(macro.MacroId, resolvedIssue: isSuccess);
        });

        // Assert: initial was 1 usage / 1 success, plus 50 updates (25 successes)
        // total usage = 51, total success = 26
        var updated = synthesizer.GetMacro(macro.MacroId);
        updated.Should().NotBeNull();
        updated!.UsageCount.Should().Be(51);
        updated.SuccessfulResolutionsCount.Should().Be(26);
        updated.ConversionSuccessRate.Should().Be(Math.Round(26.0 / 51.0, 4));
    }

    #endregion

    #region 8. ServiceCollection Registration Verification (Without BuildServiceProvider)

    [Fact]
    public void AddSupportSwarmServices_RegistersAllServicesAsSingletons_WithoutCallingBuildServiceProvider()
    {
        // Arrange: Rule 5 enforcement - strictly assert directly on ServiceCollection descriptors!
        var services = new ServiceCollection();

        // Act
        services.AddSupportSwarmServices();

        // Assert
        services.Should().Contain(d =>
            d.ServiceType == typeof(IAgenticSupportSwarmRouter) &&
            d.ImplementationType == typeof(AgenticSupportSwarmRouter) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IInConversationCsatRadarService) &&
            d.ImplementationType == typeof(InConversationCsatRadarService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IAutonomousMacroSynthesizerService) &&
            d.ImplementationType == typeof(AutonomousMacroSynthesizerService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
