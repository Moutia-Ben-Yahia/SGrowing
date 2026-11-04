using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Channels;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Channels;

public class WhatsAppFlowEngineTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"WhatsAppFlowDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task WhatsAppFlowEngine_State_Machine_And_Sentiment_Escalation_Should_Succeed()
    {
        var db = CreateDbContext();
        var service = new WhatsAppFlowEngineService(db);
        var tenantId = Guid.NewGuid();

        // 1. Create WhatsApp Flow Definition
        var flow = FlowDefinition.Create(tenantId, "COD Customer Care Flow", "START", "Automated confirmation, tracking, and exchanges");
        await service.CreateFlowDefinitionAsync(tenantId, flow);

        // 2. Customer greets in Darija ("3aslema nheb na3ref 3la commande")
        var turn1 = await service.ExecuteFlowTurnAsync(tenantId, flow.Id, "+21698112233", "3aslema");
        Assert.Equal("main_menu", turn1.NextNodeKey);
        Assert.Equal("Darija", turn1.DetectedLanguage);
        Assert.False(turn1.IsEscalated);
        Assert.Contains("SGrowing", turn1.ResponseMessage);
        Assert.NotEmpty(turn1.Buttons);

        // 3. Create a Pending COD Order for this customer
        var order = CodOrder.Create(tenantId, "ORD-WA-881", "Slimen Ben Romdhane", "+21698112233", "Lac 2", "Tunis", "Lac", 160m);
        order.Status = CodOrderStatus.PendingConfirmation;
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // 4. Customer selects "1. تأكيد الطلب" (Confirm Order)
        var turn2 = await service.ExecuteFlowTurnAsync(tenantId, flow.Id, "+21698112233", "1. تأكيد الطلب");
        Assert.Equal("order_confirmed", turn2.NextNodeKey);
        Assert.Equal("OrderConfirmedInWms", turn2.ExecutedAction);
        Assert.Equal(CodOrderStatus.Confirmed, order.Status);
        Assert.NotNull(order.ConfirmedAtUtc);

        // 5. Customer expresses intense frustration / anger ("sarra9a w arnaque!")
        var turn3 = await service.ExecuteFlowTurnAsync(tenantId, flow.Id, "+21698112233", "sarra9a retard kbir!");
        Assert.Equal("human_queue", turn3.NextNodeKey);
        Assert.True(turn3.IsEscalated);
        Assert.Equal("EscalatedToHumanAgent", turn3.ExecutedAction);
    }

    [Fact]
    public void AnalyzeMessageText_CorrectlyDetectsSentimentAndLanguage()
    {
        var db = CreateDbContext();
        var service = new WhatsAppFlowEngineService(db);

        // Positive sentiment
        var (langPos, sentPos) = service.AnalyzeMessageText("merci beaucoup service top ya3tikom saha");
        Assert.Equal(0.9m, sentPos);

        // Negative sentiment
        var (langNeg, sentNeg) = service.AnalyzeMessageText("voleur arnaque scam ghachitou el ness");
        Assert.Equal(0.15m, sentNeg);

        // Arabic detection
        var (langAr, _) = service.AnalyzeMessageText("مرحبا بكم كيف يمكنني تتبع طلبيتي");
        Assert.Equal("Arabic", langAr);

        // French detection
        var (langFr, _) = service.AnalyzeMessageText("Bonjour, je souhaite suivre ma commande");
        Assert.Equal("French", langFr);

        // English detection
        var (langEn, _) = service.AnalyzeMessageText("Hello, could you track my order support?");
        Assert.Equal("English", langEn);

        // Darija detection
        var (langDar, _) = service.AnalyzeMessageText("3aslema chnawa el 7al behi");
        Assert.Equal("Darija", langDar);
    }

    [Fact]
    public async Task ExecuteFlowTurnAsync_WhenSelectingTracking_ReturnsCurrentOrderStatus()
    {
        var db = CreateDbContext();
        var service = new WhatsAppFlowEngineService(db);
        var tenantId = Guid.NewGuid();

        var flow = FlowDefinition.Create(tenantId, "Tracking Flow", "START", "Order tracking");
        await service.CreateFlowDefinitionAsync(tenantId, flow);

        var order = CodOrder.Create(tenantId, "ORD-TRK-771", "Karim Jaziri", "+21622334455", "Centre Ville", "Sfax", "Sfax", 95m);
        order.Status = CodOrderStatus.Dispatched;
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // 1. Initial greeting transitions to main_menu
        await service.ExecuteFlowTurnAsync(tenantId, flow.Id, "+21622334455", "3aslema");

        // 2. Second turn selects tracking
        var turn = await service.ExecuteFlowTurnAsync(tenantId, flow.Id, "+21622334455", "2. تتبع الطرد");

        Assert.Equal("tracking_info", turn.NextNodeKey);
        Assert.Contains("ORD-TRK-771", turn.ResponseMessage);
        Assert.Contains(nameof(CodOrderStatus.Dispatched), turn.ResponseMessage);
    }

    [Fact]
    public async Task ExecuteFlowTurnAsync_WhenConfirmingWithoutPendingOrder_ReturnsNoPendingOrderAction()
    {
        var db = CreateDbContext();
        var service = new WhatsAppFlowEngineService(db);
        var tenantId = Guid.NewGuid();

        var flow = FlowDefinition.Create(tenantId, "Care Flow", "START", "Care");
        await service.CreateFlowDefinitionAsync(tenantId, flow);

        // 1. Initial greeting transitions to main_menu
        await service.ExecuteFlowTurnAsync(tenantId, flow.Id, "+21650998877", "bonjour");

        // 2. Second turn attempts confirmation
        var turn = await service.ExecuteFlowTurnAsync(tenantId, flow.Id, "+21650998877", "1. Confirmer");

        Assert.Equal("order_confirmed", turn.NextNodeKey);
        Assert.Equal("NoPendingOrderFound", turn.ExecutedAction);
        Assert.Contains("en attente", turn.ResponseMessage);
    }

    [Fact]
    public async Task ListFlowsAsync_ReturnsFlowsForTenant()
    {
        var db = CreateDbContext();
        var service = new WhatsAppFlowEngineService(db);
        var tenantId = Guid.NewGuid();

        var flow1 = FlowDefinition.Create(tenantId, "Onboarding Flow", "START", "New user flow");
        var flow2 = FlowDefinition.Create(tenantId, "Retention Flow", "START", "Win-back flow");
        await service.CreateFlowDefinitionAsync(tenantId, flow1);
        await service.CreateFlowDefinitionAsync(tenantId, flow2);

        var flows = await service.ListFlowsAsync(tenantId);

        Assert.Equal(2, flows.Count);
        Assert.Contains(flows, f => f.Name == "Onboarding Flow");
        Assert.Contains(flows, f => f.Name == "Retention Flow");
    }

    [Fact]
    public async Task ExecuteFlowTurnAsync_GreetingMessage_ReturnsMainMenuInFrench()
    {
        var db = CreateDbContext();
        var service = new WhatsAppFlowEngineService(db);
        var tenantId = Guid.NewGuid();

        var flow = FlowDefinition.Create(tenantId, "French Flow", "START", "Support en francais");
        await service.CreateFlowDefinitionAsync(tenantId, flow);

        var turn = await service.ExecuteFlowTurnAsync(tenantId, flow.Id, "+33612345678", "Bonjour!");

        Assert.Equal("main_menu", turn.NextNodeKey);
        Assert.Equal("French", turn.DetectedLanguage);
        Assert.Contains("Bienvenue chez SGrowing", turn.ResponseMessage);
        Assert.Contains("1. Confirmer commande", turn.Buttons);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnalyzeMessageText_WithEmptyOrWhitespace_ReturnsDarijaAndNeutralSentiment(string? input)
    {
        var db = CreateDbContext();
        var service = new WhatsAppFlowEngineService(db);

        var (lang, sentiment) = service.AnalyzeMessageText(input!);

        Assert.Equal("Darija", lang);
        Assert.Equal(0.5m, sentiment);
    }

    [Fact]
    public async Task ExecuteFlowTurnAsync_WhenCustomerSpeaksEnglish_ReturnsEnglishGreeting()
    {
        var db = CreateDbContext();
        var service = new WhatsAppFlowEngineService(db);
        var tenantId = Guid.NewGuid();

        var flow = FlowDefinition.Create(tenantId, "English Support", "START", "English Support Flow");
        await service.CreateFlowDefinitionAsync(tenantId, flow);

        var turn = await service.ExecuteFlowTurnAsync(tenantId, flow.Id, "+14155551234", "Hello there, need help with my order!");

        Assert.Equal("main_menu", turn.NextNodeKey);
        Assert.Equal("English", turn.DetectedLanguage);
        Assert.Contains("SGrowing", turn.ResponseMessage);
        Assert.Contains("1. تأكيد الطلب", turn.Buttons);
    }
}
