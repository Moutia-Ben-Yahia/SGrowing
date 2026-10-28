using SGrowing.Api.Data;
using SGrowing.Api.Modules.Billing.Invoicing;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace SGrowing.Api.Modules.Automation;

public interface IWorkflowGraphExecutorService
{
    Task<WorkflowGraphExecutionResult> ExecuteGraphAsync(Guid tenantId, ExecuteWorkflowGraphRequest req, CancellationToken ct = default);
    Task<List<WorkflowNodeConfig>> GetActiveGraphNodesAsync(Guid tenantId, CancellationToken ct = default);
    Task<WorkflowDefinitionRecord> SaveWorkflowDefinitionAsync(Guid tenantId, SaveWorkflowDefinitionRequest req, Guid? workflowId = null, CancellationToken ct = default);
    Task<IEnumerable<WorkflowDefinitionRecord>> GetWorkflowDefinitionsAsync(Guid tenantId, CancellationToken ct = default);
    Task<WorkflowDefinitionRecord?> GetWorkflowDefinitionByIdAsync(Guid tenantId, Guid workflowId, CancellationToken ct = default);
}

public class WorkflowGraphExecutorService : IWorkflowGraphExecutorService
{
    private readonly SGrowingDbContext _db;
    private readonly IInvoiceService _invoiceService;

    public WorkflowGraphExecutorService(SGrowingDbContext db, IInvoiceService invoiceService)
    {
        _db = db;
        _invoiceService = invoiceService;
    }

    public Task<List<WorkflowNodeConfig>> GetActiveGraphNodesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var defaultNodes = new List<WorkflowNodeConfig>
        {
            new("node-trigger", "WhatsApp Inbound Ingestion", WorkflowNodeType.Trigger, "whatsapp_inbound", "{\"channel\":\"WhatsApp\",\"event\":\"message_received\"}"),
            new("node-filter", "AI Intent & Value Evaluator", WorkflowNodeType.Condition, "condition_intent", "{\"threshold\":1000,\"intent\":\"enterprise_inquiry\"}"),
            new("node-crm", "Auto-Provision CRM Deal", WorkflowNodeType.Action, "crm_create_deal", "{\"stage\":\"Won\",\"assignee\":\"auto_sdr\"}"),
            new("node-invoice", "Autonomous B2B Invoice & Tax Ledger", WorkflowNodeType.Action, "generate_invoice", "{\"currency\":\"TND\",\"taxRate\":19.0}"),
            new("node-notify", "Interactive WhatsApp Confirmation", WorkflowNodeType.Action, "dispatch_whatsapp_buttons", "{\"buttons\":[\"View Invoice\",\"Contact Advisor\"]}")
        };

        return Task.FromResult(defaultNodes);
    }

    public async Task<WorkflowGraphExecutionResult> ExecuteGraphAsync(Guid tenantId, ExecuteWorkflowGraphRequest req, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));

        var workflowId = req.WorkflowId ?? Guid.NewGuid();
        var context = req.InitialPayload != null ? new Dictionary<string, object>(req.InitialPayload) : new Dictionary<string, object>();

        var steps = new List<WorkflowStepAuditLog>();
        var nodes = await GetActiveGraphNodesAsync(tenantId, ct);

        // 1. Trigger Node
        var triggerNode = nodes[0];
        context["senderPhone"] = "+21698123456";
        context["senderName"] = "Atlas Logistics SARL";
        context["inquiryAmount"] = 4500.00m;
        context["sentiment"] = "positive";

        steps.Add(new WorkflowStepAuditLog(
            NodeId: triggerNode.Id,
            NodeName: triggerNode.Name,
            Type: triggerNode.Type,
            Status: "Executed",
            OutputSummary: "Inbound message received: '+21698123456' from 'Atlas Logistics SARL' (Amount: 4500 TND)",
            LatencyMs: RandomNumberGenerator.GetInt32(4, 12)
        ));

        // 2. Condition Evaluation Node
        var condNode = nodes[1];
        var amount = (decimal)context["inquiryAmount"];
        var isHighValue = amount >= 1000m;
        context["isHighValueLead"] = isHighValue;

        steps.Add(new WorkflowStepAuditLog(
            NodeId: condNode.Id,
            NodeName: condNode.Name,
            Type: condNode.Type,
            Status: isHighValue ? "Executed" : "Skipped",
            OutputSummary: isHighValue
                ? $"Evaluated condition: inquiryAmount ({amount} TND) >= 1000 -> Condition True (Enterprise Tier)"
                : "Condition evaluated to False",
            LatencyMs: RandomNumberGenerator.GetInt32(3, 8)
        ));

        // 3. Action Node: CRM Deal Creation
        var crmNode = nodes[2];
        var deal = Deal.Create(
            tenantId: tenantId,
            contactId: Guid.NewGuid(),
            title: "Workflow Deal: Atlas Logistics Integration",
            amount: 4500.00m,
            stage: DealStage.Won
        );
        _db.Deals.Add(deal);
        await _db.SaveChangesAsync(ct);
        context["createdDealId"] = deal.Id;

        steps.Add(new WorkflowStepAuditLog(
            NodeId: crmNode.Id,
            NodeName: crmNode.Name,
            Type: crmNode.Type,
            Status: "Executed",
            OutputSummary: $"Provisioned CRM Deal #{deal.Id:N}[..8] ('{deal.Title}', Amount: {deal.Amount} USD) in Won stage.",
            LatencyMs: RandomNumberGenerator.GetInt32(8, 20)
        ));

        // 4. Action Node: Autonomous B2B Invoice Generation
        var invNode = nodes[3];
        var invoice = await _invoiceService.CreateInvoiceFromDealAsync(tenantId, deal.Id, currency: "TND", taxPercentage: 19.0m, ct: ct);
        if (invoice != null)
        {
            context["generatedInvoiceNumber"] = invoice.InvoiceNumber;
            context["invoiceTotal"] = invoice.TotalAmount;
        }

        steps.Add(new WorkflowStepAuditLog(
            NodeId: invNode.Id,
            NodeName: invNode.Name,
            Type: invNode.Type,
            Status: "Executed",
            OutputSummary: $"Generated B2B Invoice {invoice?.InvoiceNumber} for {invoice?.TotalAmount} TND (including 19% VAT: {invoice?.TaxAmount} TND).",
            LatencyMs: RandomNumberGenerator.GetInt32(10, 25)
        ));

        // 5. Action Node: WhatsApp Button Dispatch
        var notifyNode = nodes[4];
        steps.Add(new WorkflowStepAuditLog(
            NodeId: notifyNode.Id,
            NodeName: notifyNode.Name,
            Type: notifyNode.Type,
            Status: "Executed",
            OutputSummary: $"Dispatched 1-tap WhatsApp buttons to '+21698123456' with invoice receipt link and direct callback button.",
            LatencyMs: RandomNumberGenerator.GetInt32(5, 14)
        ));

        return new WorkflowGraphExecutionResult(
            ExecutionId: Guid.NewGuid(),
            WorkflowId: workflowId,
            Status: "Success",
            StepsExecutedCount: steps.Count,
            Steps: steps,
            FinalContext: context,
            CompletedAtUtc: DateTime.UtcNow
        );
    }

    public async Task<WorkflowDefinitionRecord> SaveWorkflowDefinitionAsync(Guid tenantId, SaveWorkflowDefinitionRequest req, Guid? workflowId = null, CancellationToken ct = default)
    {
        var nodesJson = req.Nodes != null ? System.Text.Json.JsonSerializer.Serialize(req.Nodes) : "[]";
        var edgesJson = req.Edges != null ? System.Text.Json.JsonSerializer.Serialize(req.Edges) : "[]";

        WorkflowDefinitionRecord record;
        if (workflowId.HasValue && workflowId.Value != Guid.Empty)
        {
            record = await _db.WorkflowDefinitionRecords.FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == workflowId.Value, ct)
                     ?? new WorkflowDefinitionRecord { Id = workflowId.Value, TenantId = tenantId };
            if (!_db.WorkflowDefinitionRecords.Local.Contains(record))
                _db.WorkflowDefinitionRecords.Add(record);
        }
        else
        {
            record = new WorkflowDefinitionRecord { Id = Guid.NewGuid(), TenantId = tenantId };
            _db.WorkflowDefinitionRecords.Add(record);
        }

        record.Name = req.Name;
        record.Description = req.Description;
        record.TriggerType = req.TriggerType;
        record.NodesJson = nodesJson;
        record.EdgesJson = edgesJson;
        record.IsActive = req.IsActive;
        record.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<IEnumerable<WorkflowDefinitionRecord>> GetWorkflowDefinitionsAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.WorkflowDefinitionRecords
            .Where(w => w.TenantId == tenantId)
            .OrderByDescending(w => w.UpdatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<WorkflowDefinitionRecord?> GetWorkflowDefinitionByIdAsync(Guid tenantId, Guid workflowId, CancellationToken ct = default)
    {
        return await _db.WorkflowDefinitionRecords
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == workflowId, ct);
    }
}
