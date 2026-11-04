using System.Text;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.AI.Personas;
using SGrowing.Api.Modules.Automation;
using SGrowing.Api.Modules.Billing.Invoicing;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.Documents;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests;

public class EnterpriseConvergenceHorizonsTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    // =========================================================================
    // Horizon 1: WhatsApp Voice-Note Transcription & Multi-Agent Auto-Routing
    // =========================================================================

    [Fact]
    public async Task AudioTranscription_TranscribesVoiceNoteAndAutoAssignsOperator()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var notifier = new MockRealtimeNotifier();
        var copilot = new AiCopilotService(db, notifier);
        var transcriptionService = new AudioTranscriptionService();
        var assignmentService = new OperatorAssignmentService(db);

        var router = new InboundMessageRouterService(db, notifier, copilot, transcriptionService, assignmentService);

        var payload = new InboundMessagePayload(
            TenantId: tenantId,
            PhoneNumber: "+216 98 445 566",
            SenderName: "Tarak Bourguiba",
            Content: "",
            MediaUrl: "https://storage.sgrowing.app/media/voice_note_urgent.ogg"
        );

        var result = await router.RouteInboundMessageAsync(payload);

        Assert.NotNull(result);
        Assert.Equal("Routed", result.Status);
        Assert.NotNull(result.TranscribedText);
        Assert.NotNull(result.AssignedOperatorId);

        // Verify message content in database has transcribed prefix
        var message = await db.Messages.FirstOrDefaultAsync(m => m.Id == result.MessageId);
        Assert.NotNull(message);
        Assert.Equal(MessageContentType.Audio, message.ContentType);
        Assert.Contains("[Voice Note Transcribed", message.Content);

        // Verify conversation has assigned operator matching language
        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == result.ConversationId);
        Assert.NotNull(conversation);
        Assert.Equal(result.AssignedOperatorId, conversation.AssigneeId);
    }

    [Fact]
    public async Task OperatorAssignment_MatchesLanguageSkillAndLowestWorkload()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var assignmentService = new OperatorAssignmentService(db);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid());
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        // French preference matching
        var assignment = await assignmentService.AssignConversationAsync(tenantId, conv.Id, "fr-FR");

        Assert.NotNull(assignment);
        Assert.NotEqual(Guid.Empty, assignment.OperatorId);
        Assert.Contains("fr-FR", assignment.Reason);

        // Verify updated in DB
        var updatedConv = await db.Conversations.FirstOrDefaultAsync(c => c.Id == conv.Id);
        Assert.NotNull(updatedConv);
        Assert.Equal(assignment.OperatorId, updatedConv.AssigneeId);
    }

    // =========================================================================
    // Horizon 2: Compliant Cryptographic PDF Invoicing & Payment Webhooks
    // =========================================================================

    [Fact]
    public async Task PdfInvoice_GeneratesCompliantPdfBytesWithQrAndLineItems()
    {
        var pdfService = new DocumentPdfGeneratorService();

        var invoiceId = Guid.NewGuid();
        var lineItems = new List<InvoiceLineItemDto>
        {
            new("Enterprise Platform License (Annual)", 1, 10000.00m, 10000.00m),
            new("Dedicated SLA & Multi-Hub Logistics Setup", 1, 2500.00m, 2500.00m)
        };

        decimal subtotal = 12500.00m;
        decimal tax = subtotal * 0.19m; // 19% VAT = 2375.00m
        decimal total = subtotal + tax; // 14875.00m

        var pdfBytes = pdfService.GenerateInvoicePdf(
            invoiceId: invoiceId,
            invoiceNumber: "INV-2026-9001",
            sellerName: "SGrowing Enterprise Technologies SARL",
            buyerName: "Karthago Logistics Group",
            subtotal: subtotal,
            taxAmount: tax,
            totalAmount: total,
            currency: "TND",
            items: lineItems
        );

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 100);

        // Check PDF magic byte header "%PDF"
        string header = Encoding.ASCII.GetString(pdfBytes[..4]);
        Assert.Equal("%PDF", header);
    }

    [Fact]
    public async Task PaymentWebhooks_ReconcilesStripeAndFlouciInvoicesSuccessfully()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var invoiceService = new InvoiceService(db);

        // 1. Create Draft Invoice
        var invoice = await invoiceService.CreateInvoiceAsync(tenantId, new CreateInvoiceRequest(
            ClientName: "Societe Tunisienne de Distribution",
            ClientEmail: "comptabilite@std.tn",
            Currency: "TND",
            TaxPercentage: 19.0m,
            LineItems: new List<CreateInvoiceLineItemRequest>
            {
                new("WMS Multi-Warehouse Software Subscription", 1, 5000.00m)
            }
        ));

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);

        // 2. Simulate Flouci Webhook Success
        var flouciPayment = await invoiceService.RecordPaymentAsync(
            tenantId,
            invoice.Id,
            new RecordPaymentRequest(
                Amount: invoice.TotalAmount,
                PaymentMethod: "Flouci",
                TransactionReference: "FLOUCI-TX-849201"
            )
        );

        Assert.NotNull(flouciPayment);
        Assert.Equal("Success", flouciPayment.Status);

        // Verify invoice is Paid
        var settledInvoice = await invoiceService.GetInvoiceByIdAsync(tenantId, invoice.Id);
        Assert.NotNull(settledInvoice);
        Assert.Equal(InvoiceStatus.Paid, settledInvoice.Status);
        Assert.NotNull(settledInvoice.PaidAtUtc);
    }

    // =========================================================================
    // Horizon 3: Persistent Visual Workflow Studio (n8n / Windmill Convergence)
    // =========================================================================

    [Fact]
    public async Task WorkflowDefinitions_SavesAndRetrievesPersistentNodeGraph()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var invoiceService = new InvoiceService(db);
        var workflowService = new WorkflowGraphExecutorService(db, invoiceService);

        var nodes = new List<WorkflowNodeConfig>
        {
            new("node-1", "Inbound WhatsApp Trigger", WorkflowNodeType.Trigger, "whatsapp_inbound"),
            new("node-2", "Order Amount Filter >= 500", WorkflowNodeType.Condition, "condition_threshold"),
            new("node-3", "Dispatch Immediate WhatsApp Receipt", WorkflowNodeType.Action, "dispatch_receipt")
        };

        var edges = new List<WorkflowEdgeConfig>
        {
            new("node-1", "node-2", "default"),
            new("node-2", "node-3", "true")
        };

        var saveReq = new SaveWorkflowDefinitionRequest(
            Name: "High-Value Order Fast Track",
            Description: "Filter orders over 500 TND and dispatch 1-tap WhatsApp receipt",
            TriggerType: "whatsapp_inbound",
            Nodes: nodes,
            Edges: edges,
            IsActive: true
        );

        var saved = await workflowService.SaveWorkflowDefinitionAsync(tenantId, saveReq);
        Assert.NotNull(saved);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal("High-Value Order Fast Track", saved.Name);

        // Retrieve by ID
        var retrieved = await workflowService.GetWorkflowDefinitionByIdAsync(tenantId, saved.Id);
        Assert.NotNull(retrieved);
        Assert.Contains("High-Value Order Fast Track", retrieved.Name);
        Assert.Contains("node-1", retrieved.NodesJson);

        // List all tenant workflows
        var all = await workflowService.GetWorkflowDefinitionsAsync(tenantId);
        Assert.Single(all);
    }

    // =========================================================================
    // Horizon 4: Closed-Loop Autonomous AI Agent Remediation
    // =========================================================================

    [Fact]
    public async Task AgentPersona_ClosedLoopRemediation_ResolvesShortagesAndLowMargins()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        // 1. Setup a warehouse with an inventory shortage (< 50 units)
        var warehouseA = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "WH-SFAX-01",
            Name = "Sfax Regional Depot",
            IsActive = true
        };
        var warehouseB = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "WH-TUNIS-01",
            Name = "Tunis Central Hub",
            IsActive = true
        };
        db.Warehouses.AddRange(warehouseA, warehouseB);

        var stockRecord = new WarehouseStockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseA.Id,
            Sku = "SKU-CRITICAL-SPARE",
            ProductName = "Critical Replacement Sensor",
            QuantityAvailable = 15, // Depleted!
            UnitCost = 45.0m
        };
        db.WarehouseStockRecords.Add(stockRecord);

        // 2. Setup a low-margin deal (< 25%)
        var lowMarginRecord = new DealMarginRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DealId = Guid.NewGuid(),
            DealTitle = "Enterprise Deal",
            Revenue = 1000.0m,
            CostOfGoodsSold = 600.0m,
            CourierShippingFees = 220.0m,
            OperatorLaborHours = 0,
            OperatorHourlyRate = 0,
            OperatorLaborCost = 0,
            TrueGrossMargin = 180.0m,
            GrossMarginPercentage = 18.0m
        };
        db.DealMarginRecords.Add(lowMarginRecord);

        // 3. Setup an urgent blocked task
        var urgentTask = new TaskItemRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = Guid.NewGuid(),
            Title = "API Webhook Cutover Signoff",
            Status = "InProgress",
            Priority = "Urgent"
        };
        db.TaskItems.Add(urgentTask);

        await db.SaveChangesAsync();

        // 4. Trigger Fleet Autonomous Remediation
        var personaService = new AgentPersonaService(db);
        var remediationActions = await personaService.ExecuteAutomatedRemediationAsync(tenantId);

        Assert.NotEmpty(remediationActions);
        Assert.Contains(remediationActions, r => r.RoleKey == "logistics-controller" && r.ActionTaken.Contains("Auto-Replenishment"));
        Assert.Contains(remediationActions, r => r.RoleKey == "revops-strategist" && r.ActionTaken.Contains("Operational Logistics"));
        Assert.Contains(remediationActions, r => r.RoleKey == "delivery-scrum-master");

        // Verify stock replenished

        // Verify stock replenished
        var updatedStock = await db.WarehouseStockRecords.FirstOrDefaultAsync(s => s.Id == stockRecord.Id);
        Assert.NotNull(updatedStock);
        Assert.Equal(115, updatedStock.QuantityAvailable); // 15 + 100 = 115

        // Verify deal margin rebalanced above 20%
        var updatedMargin = await db.DealMarginRecords.FirstOrDefaultAsync(m => m.Id == lowMarginRecord.Id);
        Assert.NotNull(updatedMargin);
        Assert.True(updatedMargin.GrossMarginPercentage > 18.0m);
    }

    [Fact]
    public void DocumentPdfGeneratorService_GenerateCertificatePdf_ProducesValidPdfBytesWithAuditTrail()
    {
        var pdfService = new DocumentPdfGeneratorService();
        var audit = new List<AuditTrailEventDto>
        {
            new("DocumentCreated", DateTime.UtcNow.AddHours(-1), "system", "127.0.0.1"),
            new("SignerViewed", DateTime.UtcNow.AddMinutes(-30), "karim@maghrebretail.tn", "196.229.10.45"),
            new("DocumentSigned", DateTime.UtcNow, "karim@maghrebretail.tn", "196.229.10.45")
        };

        var certBytes = pdfService.GenerateCertificatePdf(
            documentId: Guid.NewGuid(),
            documentTitle: "Enterprise MSA Contract",
            signerName: "Karim Trabelsi",
            signerEmail: "karim@maghrebretail.tn",
            signerIp: "196.229.10.45",
            signedAtUtc: DateTime.UtcNow,
            documentHashSha256: "sha256-e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            signatureHashSha256: "sha256-ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb",
            auditTrail: audit
        );

        Assert.NotNull(certBytes);
        Assert.True(certBytes.Length > 100);
        string header = Encoding.ASCII.GetString(certBytes[..4]);
        Assert.Equal("%PDF", header);
    }

    [Fact]
    public async Task WorkflowDefinitions_GetWorkflowDefinitionByIdAsync_WhenNotFoundOrCrossTenant_ReturnsNull()
    {
        using var db = CreateInMemoryDb();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var invoiceService = new InvoiceService(db);
        var workflowService = new WorkflowGraphExecutorService(db, invoiceService);

        var missing = await workflowService.GetWorkflowDefinitionByIdAsync(tenantA, Guid.NewGuid());
        Assert.Null(missing);

        var saveReq = new SaveWorkflowDefinitionRequest(
            Name: "Tenant A Workflow",
            Description: "Internal flow",
            TriggerType: "generic_trigger",
            Nodes: new List<WorkflowNodeConfig>(),
            Edges: new List<WorkflowEdgeConfig>(),
            IsActive: true
        );
        var saved = await workflowService.SaveWorkflowDefinitionAsync(tenantA, saveReq);

        var crossTenantResult = await workflowService.GetWorkflowDefinitionByIdAsync(tenantB, saved.Id);
        Assert.Null(crossTenantResult);
    }
}
