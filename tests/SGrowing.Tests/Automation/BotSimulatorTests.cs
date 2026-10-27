using SGrowing.Api.Modules.Automation;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Automation;

public class BotSimulatorTests
{
    private readonly BotSimulatorService _simulator = new();

    [Fact]
    public void BotSimulator_StepsThroughFlow_WithTypingDelayAndCarousel()
    {
        // Arrange: Workflow with Start -> Typing -> Message -> Carousel (waiting for input)
        var nodesJson = """
            [
              {
                "id": "node_start",
                "type": "Trigger",
                "name": "Start Node",
                "next": "node_typing"
              },
              {
                "id": "node_typing",
                "type": "Typing",
                "name": "Typing Indicator",
                "config": { "delaySeconds": 2 },
                "next": "node_msg"
              },
              {
                "id": "node_msg",
                "type": "SendMessage",
                "name": "Greeting Message",
                "config": { "message": "Welcome to SGrowing! How can we assist you?" },
                "next": "node_carousel"
              },
              {
                "id": "node_carousel",
                "type": "CarouselCard",
                "name": "Product Options",
                "config": {
                  "title": "Select a product solution:",
                  "cards": [
                    { "title": "Cold Outreach AI", "subtitle": "Automate outbound sequences", "buttons": [{ "title": "Explore Outreach", "value": "outreach" }] },
                    { "title": "COD Commerce", "subtitle": "Doorstep logistics and fraud shield", "buttons": [{ "title": "Explore COD", "value": "cod" }] }
                  ]
                }
              }
            ]
            """;

        var workflow = WorkflowGraph.Create(Guid.NewGuid(), "OnboardingBot", "BotCanvas", nodesJson);

        // Act
        var session = _simulator.StartSimulation(workflow);

        // Assert
        Assert.True(session.IsWaitingForUserInput);
        Assert.False(session.IsHandedOverToHuman);
        Assert.False(session.IsCompleted);

        // Check transcript
        Assert.Contains(session.Transcript, m => m.IsTypingIndicator && m.DelayMilliseconds == 2000);
        Assert.Contains(session.Transcript, m => m.Content == "Welcome to SGrowing! How can we assist you?");
        Assert.Contains(session.Transcript, m => m.Cards != null && m.Cards.Count == 2);
    }

    [Fact]
    public void BotSimulator_ExtractsJsonPathFromWebhookResponse_AndTagsContact()
    {
        // Arrange: Webhook extracting balance -> Tagging -> SendMessage
        var nodesJson = """
            [
              {
                "id": "node_webhook",
                "type": "Webhook",
                "name": "Fetch Wallet Balance",
                "config": {
                  "url": "https://api.sgrowing.io/wallet/balance",
                  "mockResponse": "{\"status\":\"ok\",\"account\":{\"id\":\"acc-4491\",\"balance\":1250.75}}",
                  "extractMappings": {
                    "account_id": "account.id",
                    "account_balance": "account.balance"
                  }
                },
                "next": "node_tag"
              },
              {
                "id": "node_tag",
                "type": "Tagging",
                "name": "Tag High Net Worth",
                "config": {
                  "tag": "vip-client",
                  "action": "add"
                },
                "next": "node_msg"
              },
              {
                "id": "node_msg",
                "type": "SendMessage",
                "name": "Balance Alert",
                "config": {
                  "message": "Your current balance is {{account_balance}}."
                }
              }
            ]
            """;

        var workflow = WorkflowGraph.Create(Guid.NewGuid(), "BalanceBot", "BotCanvas", nodesJson);

        // Act
        var session = _simulator.StartSimulation(workflow);

        // Assert
        Assert.True(session.Variables.ContainsKey("account_id"));
        Assert.Equal("acc-4491", session.Variables["account_id"].ToString());
        Assert.True(session.Variables.ContainsKey("account_balance"));
        Assert.Equal("1250.75", session.Variables["account_balance"].ToString());

        // Check tag added
        Assert.True(session.Variables.ContainsKey("tags"));
        var tags = (List<string>)session.Variables["tags"];
        Assert.Contains("vip-client", tags);

        // Check resolved message
        Assert.Contains(session.Transcript, m => m.Content == "Your current balance is 1250.75.");
    }

    [Fact]
    public void BotSimulator_HumanTakeover_PausesAutomationAndFlagsSession()
    {
        // Arrange
        var nodesJson = """
            [
              {
                "id": "node_handover",
                "type": "HumanTakeover",
                "name": "Escalate to Live Operator"
              }
            ]
            """;

        var workflow = WorkflowGraph.Create(Guid.NewGuid(), "TakeoverBot", "BotCanvas", nodesJson);

        // Act
        var session = _simulator.StartSimulation(workflow);

        // Assert
        Assert.True(session.IsHandedOverToHuman);
        Assert.Equal("HandedOver", session.Variables["bot_status"].ToString());
        Assert.Contains(session.Transcript, m => m.Content.Contains("live customer care agent"));
    }
}
