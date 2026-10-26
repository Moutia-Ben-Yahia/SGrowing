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
}
