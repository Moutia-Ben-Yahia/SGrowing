using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Automation;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.Automation;

public class WorkflowEngineTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public async Task ExecuteWorkflow_ConditionTrue_ShouldRouteToTrueBranch()
    {
        var tenantId = Guid.NewGuid();
        var engine = new WorkflowEngineService();

        var nodesJson = """
        [
          {
            "id": "node_condition",
            "type": "Condition",
            "name": "Check Enterprise Revenue",
            "config": {
              "field": "AnnualRevenue",
              "operator": ">=",
              "value": 100000
            },
            "trueNext": "node_vip_msg",
            "falseNext": "node_standard_msg"
          },
          {
            "id": "node_vip_msg",
            "type": "Action: SendMessage",
            "name": "Send VIP Executive Welcome",
            "config": {
              "channel": "WhatsApp",
              "recipient": "{{phone}}",
              "message": "Welcome {{name}}! An enterprise account director has been assigned."
            }
          },
          {
            "id": "node_standard_msg",
            "type": "Action: SendMessage",
            "name": "Send Self-Serve Email",
            "config": {
              "channel": "Email",
              "recipient": "{{email}}",
              "message": "Welcome {{name}}! Get started with your self-serve trial."
            }
          }
        ]
        """;

        var workflow = await engine.CreateWorkflowAsync(tenantId, "Lead Qualification Flow", "InboundMessage", nodesJson);

        var payload = """
        {
          "name": "Bruce Wayne",
          "email": "bruce@wayne-enterprises.com",
          "phone": "+1-555-0199",
          "AnnualRevenue": 500000
        }
        """;

        var execution = await engine.ExecuteWorkflowAsync(tenantId, workflow.Id, payload);

        execution.Should().NotBeNull();
        execution.Status.Should().Be("Completed");
        execution.ExecutedNodesCount.Should().Be(2); // Condition + VIP message
        execution.ExecutionLog.Should().Contain("[Condition: Check Enterprise Revenue] Evaluated condition => TRUE");
        execution.ExecutionLog.Should().Contain("[Action: SendMessage 'Send VIP Executive Welcome']");
        execution.ExecutionLog.Should().Contain("Welcome Bruce Wayne! An enterprise account director has been assigned.");
        execution.ExecutionLog.Should().NotContain("Send Self-Serve Email");
    }

    [Fact]
    public async Task ExecuteWorkflow_ConditionFalse_ShouldRouteToFalseBranch()
    {
        var tenantId = Guid.NewGuid();
        var engine = new WorkflowEngineService();

        var nodesJson = """
        [
          {
            "id": "node_condition",
            "type": "Condition",
            "name": "Check Enterprise Revenue",
            "config": {
              "field": "AnnualRevenue",
              "operator": ">=",
              "value": 100000
            },
            "trueNext": "node_vip_msg",
            "falseNext": "node_standard_msg"
          },
          {
            "id": "node_vip_msg",
            "type": "Action: SendMessage",
            "name": "Send VIP Executive Welcome",
            "config": {
              "channel": "WhatsApp",
              "recipient": "{{phone}}",
              "message": "Welcome {{name}}! An enterprise account director has been assigned."
            }
          },
          {
            "id": "node_standard_msg",
            "type": "Action: SendMessage",
            "name": "Send Self-Serve Email",
            "config": {
              "channel": "Email",
              "recipient": "{{email}}",
              "message": "Welcome {{name}}! Get started with your self-serve trial."
            }
          }
        ]
        """;

        var workflow = await engine.CreateWorkflowAsync(tenantId, "Lead Qualification Flow", "InboundMessage", nodesJson);

        var payload = """
        {
          "name": "Peter Parker",
          "email": "peter@dailybugle.com",
          "phone": "+1-555-0122",
          "AnnualRevenue": 25000
        }
        """;

        var execution = await engine.ExecuteWorkflowAsync(tenantId, workflow.Id, payload);

        execution.Should().NotBeNull();
        execution.Status.Should().Be("Completed");
        execution.ExecutedNodesCount.Should().Be(2); // Condition + Standard message
        execution.ExecutionLog.Should().Contain("[Condition: Check Enterprise Revenue] Evaluated condition => FALSE");
        execution.ExecutionLog.Should().Contain("[Action: SendMessage 'Send Self-Serve Email']");
        execution.ExecutionLog.Should().Contain("Welcome Peter Parker! Get started with your self-serve trial.");
        execution.ExecutionLog.Should().NotContain("Send VIP Executive Welcome");
    }

    [Fact]
    public async Task ExecuteWorkflow_MultiStepPipeline_ShouldExecuteAllNodeTypesAndLogSteps()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        // Seed contact in database
        var contact = Contact.Create(tenantId, "Clark", "Kent", "clark@planet.com", "+1-555-9999");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var engine = new WorkflowEngineService(db);

        var nodesJson = """
        [
          {
            "id": "step_1_condition",
            "type": "Condition",
            "name": "Verify Qualified Lead",
            "config": {
              "field": "score",
              "operator": ">",
              "value": 70
            },
            "trueNext": "step_2_message",
            "falseNext": null
          },
          {
            "id": "step_2_message",
            "type": "Action: SendMessage",
            "name": "Dispatch Confirmation",
            "config": {
              "channel": "Email",
              "recipient": "{{email}}",
              "subject": "Demo Confirmation for {{name}}",
              "message": "Hi {{name}}, your request has been confirmed."
            },
            "next": "step_3_crm"
          },
          {
            "id": "step_3_crm",
            "type": "Action: UpdateCRM",
            "name": "Elevate to Opportunity",
            "config": {
              "contactId": "{CONTACT_ID}",
              "stage": "Prospect",
              "lead_status": "Qualified"
            },
            "next": "step_4_webhook"
          },
          {
            "id": "step_4_webhook",
            "type": "Action: Webhook",
            "name": "Trigger External Dispatcher",
            "config": {
              "url": "https://api.partner.io/v1/crm-sync",
              "method": "POST"
            }
          }
        ]
        """.Replace("{CONTACT_ID}", contact.Id.ToString());

        var workflow = await engine.CreateWorkflowAsync(tenantId, "Full Orchestration Pipeline", "Event", nodesJson);

        var payload = """
        {
          "contactId": "{CONTACT_ID}",
          "name": "Clark Kent",
          "email": "clark@planet.com",
          "score": 92
        }
        """.Replace("{CONTACT_ID}", contact.Id.ToString());

        var execution = await engine.ExecuteWorkflowAsync(tenantId, workflow.Id, payload);

        execution.Status.Should().Be("Completed");
        execution.ExecutedNodesCount.Should().Be(4);

        // Verify logs for each node type
        execution.ExecutionLog.Should().Contain("[Condition: Verify Qualified Lead] Evaluated condition => TRUE");
        execution.ExecutionLog.Should().Contain("[Action: SendMessage 'Dispatch Confirmation']");
        execution.ExecutionLog.Should().Contain("Demo Confirmation for Clark Kent");
        execution.ExecutionLog.Should().Contain("[Action: UpdateCRM 'Elevate to Opportunity']");
        execution.ExecutionLog.Should().Contain("[Action: Webhook 'Trigger External Dispatcher'] Dispatched POST to https://api.partner.io/v1/crm-sync");

        // Verify database state mutation from Action: UpdateCRM
        var updatedContact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == contact.Id);
        updatedContact.Should().NotBeNull();
        updatedContact!.LifecycleStage.Should().Be(LifecycleStage.Prospect);
    }

    [Fact]
    public async Task ExecuteWorkflow_VisualCanvasNodesAndEdges_ShouldRouteProperly()
    {
        var tenantId = Guid.NewGuid();
        var engine = new WorkflowEngineService();

        var nodesWithEdgesJson = """
        {
          "nodes": [
            {
              "id": "n_start",
              "type": "Trigger",
              "title": "Inbound Webhook",
              "config": { "source": "Stripe" }
            },
            {
              "id": "n_check",
              "type": "Condition",
              "title": "Check Tier",
              "config": {
                "field": "tier",
                "operator": "==",
                "value": "Enterprise"
              }
            },
            {
              "id": "n_notify_slack",
              "type": "Action: Webhook",
              "title": "Notify Slack Channel",
              "config": {
                "url": "https://hooks.slack.com/services/enterprise-alerts",
                "method": "POST"
              }
            }
          ],
          "edges": [
            { "source": "n_start", "target": "n_check" },
            { "source": "n_check", "target": "n_notify_slack", "handle": "true" }
          ]
        }
        """;

        var workflow = await engine.CreateWorkflowAsync(tenantId, "Canvas Webhook Ingress", "Webhook", nodesWithEdgesJson);

        var payload = """
        {
          "tier": "Enterprise",
          "customer": "Wayne Corp"
        }
        """;

        var execution = await engine.ExecuteWorkflowAsync(tenantId, workflow.Id, payload);

        execution.Status.Should().Be("Completed");
        execution.ExecutedNodesCount.Should().Be(3);
        execution.ExecutionLog.Should().Contain("[Step: Inbound Webhook]");
        execution.ExecutionLog.Should().Contain("[Condition: Check Tier] Evaluated condition => TRUE");
        execution.ExecutionLog.Should().Contain("[Action: Webhook 'Notify Slack Channel']");
    }
}
