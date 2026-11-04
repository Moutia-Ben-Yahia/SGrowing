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

    [Fact]
    public void BotSimulator_EmptyNodesJson_ReturnsCompletedSessionWithLog()
    {
        var workflow = WorkflowGraph.Create(Guid.NewGuid(), "EmptyBot", "BotCanvas", "[]");
        var session = _simulator.StartSimulation(workflow);

        Assert.True(session.IsCompleted);
        Assert.False(session.IsWaitingForUserInput);
        Assert.False(session.IsHandedOverToHuman);
        Assert.Null(session.CurrentNodeId);
    }

    [Fact]
    public void BotSimulator_ConditionNode_RoutesToTrueBranch_WhenConditionMet()
    {
        var nodesJson = """
            [
              {
                "id": "node_cond",
                "type": "Condition",
                "name": "Check VIP Status",
                "config": { "field": "tier", "operator": "==", "value": "vip" },
                "trueNext": "node_vip_msg",
                "falseNext": "node_std_msg"
              },
              {
                "id": "node_vip_msg",
                "type": "SendMessage",
                "name": "VIP Welcome",
                "config": { "message": "Welcome back honored VIP member!", "waitForReply": true }
              },
              {
                "id": "node_std_msg",
                "type": "SendMessage",
                "name": "Standard Welcome",
                "config": { "message": "Welcome standard member!", "waitForReply": true }
              }
            ]
            """;

        var workflow = WorkflowGraph.Create(Guid.NewGuid(), "VipBranchBot", "BotCanvas", nodesJson);
        var initialVars = new Dictionary<string, object> { { "tier", "vip" } };

        var session = _simulator.StartSimulation(workflow, initialVars);

        Assert.Contains(session.Transcript, m => m.Content == "Welcome back honored VIP member!");
        Assert.DoesNotContain(session.Transcript, m => m.Content == "Welcome standard member!");
    }

    [Fact]
    public void BotSimulator_ConditionNode_RoutesToFalseBranch_WhenConditionNotMet()
    {
        var nodesJson = """
            [
              {
                "id": "node_cond",
                "type": "Condition",
                "name": "Check VIP Status",
                "config": { "field": "tier", "operator": "==", "value": "vip" },
                "trueNext": "node_vip_msg",
                "falseNext": "node_std_msg"
              },
              {
                "id": "node_vip_msg",
                "type": "SendMessage",
                "name": "VIP Welcome",
                "config": { "message": "Welcome back honored VIP member!", "waitForReply": true }
              },
              {
                "id": "node_std_msg",
                "type": "SendMessage",
                "name": "Standard Welcome",
                "config": { "message": "Welcome standard member!", "waitForReply": true }
              }
            ]
            """;

        var workflow = WorkflowGraph.Create(Guid.NewGuid(), "StdBranchBot", "BotCanvas", nodesJson);
        var initialVars = new Dictionary<string, object> { { "tier", "basic" } };

        var session = _simulator.StartSimulation(workflow, initialVars);

        Assert.Contains(session.Transcript, m => m.Content == "Welcome standard member!");
        Assert.DoesNotContain(session.Transcript, m => m.Content == "Welcome back honored VIP member!");
    }

    [Fact]
    public void BotSimulator_TemplateRendering_SubstitutesInitialVariables()
    {
        var nodesJson = """
            [
              {
                "id": "node_greeting",
                "type": "SendMessage",
                "name": "Personalized Hello",
                "config": { "message": "Hello {{first_name}}! Your discount code is {{promo_code}}." }
              }
            ]
            """;

        var workflow = WorkflowGraph.Create(Guid.NewGuid(), "TemplateBot", "BotCanvas", nodesJson);
        var vars = new Dictionary<string, object>
        {
            { "first_name", "Sarah" },
            { "promo_code", "GROWTH2026" }
        };

        var session = _simulator.StartSimulation(workflow, vars);

        Assert.Contains(session.Transcript, m => m.Content == "Hello Sarah! Your discount code is GROWTH2026.");
    }
}
