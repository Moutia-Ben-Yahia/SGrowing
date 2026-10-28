using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Automation;
using SGrowing.Api.Modules.Billing.Invoicing;
using SGrowing.Api.Modules.Channels.WhatsApp;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.CRM.CustomFields;
using SGrowing.Api.Modules.Projects;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests;

public class EndToEndEnterpriseJourneyTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    // =========================================================================
    // 1. WhatsApp Interactive Messages & Webhook Callback Routing
    // =========================================================================

    [Fact]
    public async Task WhatsApp_InteractiveButtons_DispatchesAndRoutesCallbackToConversation()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var notifier = new MockRealtimeNotifier();
        var copilot = new AiCopilotService(db, notifier);

        // 1. Setup Gateway & Inbound Router Services
        var gatewayService = new WhatsAppGatewayService(db);
        var routerService = new InboundMessageRouterService(db, notifier, copilot);

        // 2. Dispatch Quick-Reply Interactive Buttons
        var btnRequest = new SendInteractiveButtonsRequest(
            RecipientPhoneNumber: "+21698765432",
            Title: "Onboarding Options",
            Body: "Welcome to SGrowing Enterprise! Please choose your preferred onboarding path:",
            Footer: "Reply 1-tap on WhatsApp",
            Buttons: new List<WhatsAppButton>
            {
                new("btn_saas", "🚀 Cloud SaaS Setup"),
                new("btn_custom", "🛠️ Custom Architecture"),
                new("btn_human", "📞 Speak to Advisor")
            }
        );

        var dispatchResult = await gatewayService.DispatchInteractiveButtonsAsync(tenantId, "default-wa", btnRequest);
        Assert.NotNull(dispatchResult);
        Assert.Equal("Dispatched", dispatchResult.Status);

        // 3. Simulate customer clicking "🚀 Cloud SaaS Setup" button callback
        var callbackPayload = new InboundMessagePayload(
            TenantId: tenantId,
            PhoneNumber: "+21698765432",
            SenderName: "Yassine Enterprise",
            Content: "Ready to start",
            InteractiveCallback: new WhatsAppInteractiveCallbackPayload(
                ButtonId: "btn_saas",
                ButtonText: "🚀 Cloud SaaS Setup",
                ContextMessageId: dispatchResult.MessageId
            )
        );

        var routeResult = await routerService.RouteInboundMessageAsync(callbackPayload);

        // 4. Verify Contact, Conversation and Message creation
        Assert.NotEqual(Guid.Empty, routeResult.ContactId);
        Assert.NotEqual(Guid.Empty, routeResult.ConversationId);

        var savedMsg = await db.Messages.FirstOrDefaultAsync(m => m.Id == routeResult.MessageId);
        Assert.NotNull(savedMsg);
        Assert.Contains("[Selected: 🚀 Cloud SaaS Setup]", savedMsg.Content);
        Assert.Equal(routeResult.ConversationId, savedMsg.ConversationId);
    }

    [Fact]
    public async Task WhatsApp_InteractiveList_DispatchesSectionedMenuSuccessfully()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var gatewayService = new WhatsAppGatewayService(db);

        var listRequest = new SendInteractiveListRequest(
            RecipientPhoneNumber: "+21650112233",
            Title: "Enterprise Resource Directory",
            Body: "Select an option from our service catalog below:",
            ButtonText: "View Services",
            Footer: "Powered by Evolution-Go & Baileys",
            Sections: new List<WhatsAppListSection>
            {
                new(
                    SectionTitle: "Growth Services",
                    Rows: new List<WhatsAppListRow>
                    {
                        new("opt_crm", "CRM Modernization", "Full pipeline migration & custom fields"),
                        new("opt_sdr", "Autonomous AI SDR", "Multi-touch outreach across WhatsApp & Email")
                    }
                ),
                new(
                    SectionTitle: "Billing & Plans",
                    Rows: new List<WhatsAppListRow>
                    {
                        new("opt_inv", "B2B Multi-Currency Invoicing", "Automated invoicing with 19% VAT")
                    }
                )
            }
        );

        var result = await gatewayService.DispatchInteractiveListAsync(tenantId, "default-wa", listRequest);
        Assert.NotNull(result);
        Assert.Equal("Dispatched", result.Status);
    }

    // =========================================================================
    // 2. Dynamic Typed Custom Field & Schema Engine (Twenty CRM Convergence)
    // =========================================================================

    [Fact]
    public async Task CustomFields_CreateDefinitionsAndValidateValues_EnforcesSchemaRules()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var customFieldService = new CustomFieldService(db);

        // 1. Create Typed Field Definitions
        var defVat = await customFieldService.CreateDefinitionAsync(tenantId, new CreateCustomFieldDefinitionRequest(
            EntityType: "Company",
            FieldName: "vat_matricule",
            FieldLabel: "Matricule Fiscale / Tax ID",
            FieldType: CustomFieldType.Text,
            ValidationRegex: @"^\d{7}/[A-Z]/\w{3}/\d{3}$",
            IsRequired: true
        ));

        var defAnnualRevenue = await customFieldService.CreateDefinitionAsync(tenantId, new CreateCustomFieldDefinitionRequest(
            EntityType: "Company",
            FieldName: "annual_revenue",
            FieldLabel: "Annual Recurring Revenue",
            FieldType: CustomFieldType.Currency,
            IsRequired: false
        ));

        var defIndustry = await customFieldService.CreateDefinitionAsync(tenantId, new CreateCustomFieldDefinitionRequest(
            EntityType: "Company",
            FieldName: "industry_vertical",
            FieldLabel: "Industry Vertical",
            FieldType: CustomFieldType.DropdownSelect,
            Options: new List<string> { "E-Commerce", "SaaS", "Logistics", "Fintech" }
        ));

        Assert.Equal("vat_matricule", defVat.FieldName);
        Assert.Equal(CustomFieldType.Currency, defAnnualRevenue.FieldType);

        // 2. Test Regex Validation Failure on Invalid Tax ID
        var companyId = Guid.NewGuid();
        var invalidFields = new List<SetFieldValueDto>
        {
            new("vat_matricule", "INVALID-TAX-ID")
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            customFieldService.SetValuesAsync(tenantId, "Company", companyId, invalidFields));

        // 3. Test Successful Assignment with Valid Regex and Values
        var validFields = new List<SetFieldValueDto>
        {
            new("vat_matricule", "1234567/A/M00/000"),
            new("annual_revenue", 450000.00m),
            new("industry_vertical", "Logistics")
        };

        var savedValuesDto = await customFieldService.SetValuesAsync(tenantId, "Company", companyId, validFields);
        Assert.Equal(companyId, savedValuesDto.EntityId);
        Assert.Equal(3, savedValuesDto.Fields.Count);

        // 4. Retrieve and verify persisted custom field values
        var retrievedDto = await customFieldService.GetValuesAsync(tenantId, "Company", companyId);
        Assert.Equal("1234567/A/M00/000", retrievedDto.Fields.First(f => f.FieldName == "vat_matricule").Value?.ToString());
        Assert.Equal(450000.00m, (decimal)retrievedDto.Fields.First(f => f.FieldName == "annual_revenue").Value!);
        Assert.Equal("Logistics", retrievedDto.Fields.First(f => f.FieldName == "industry_vertical").Value?.ToString());
    }

    // =========================================================================
    // 3. Visual Node Workflow Graph Engine (n8n / Windmill Convergence)
    // =========================================================================

    [Fact]
    public async Task WorkflowGraphEngine_ExecutesTriggerConditionActions_CreatesDealAndB2BInvoice()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var invoiceService = new InvoiceService(db);
        var workflowExecutor = new WorkflowGraphExecutorService(db, invoiceService);

        var request = new ExecuteWorkflowGraphRequest(
            WorkflowId: Guid.NewGuid(),
            InitialPayload: new Dictionary<string, object>
            {
                ["source"] = "WhatsApp",
                ["companyName"] = "Atlas Logistics SARL"
            }
        );

        var result = await workflowExecutor.ExecuteGraphAsync(tenantId, request);

        Assert.Equal("Success", result.Status);
        Assert.Equal(5, result.StepsExecutedCount);

        // Verify each pipeline step executed with recorded latency
        Assert.All(result.Steps, s => Assert.True(s.LatencyMs >= 0));
        Assert.Contains(result.Steps, s => s.Type == WorkflowNodeType.Trigger);
        Assert.Contains(result.Steps, s => s.Type == WorkflowNodeType.Condition && s.Status == "Executed");
        Assert.Contains(result.Steps, s => s.Type == WorkflowNodeType.Action && s.NodeName.Contains("CRM"));
        Assert.Contains(result.Steps, s => s.Type == WorkflowNodeType.Action && s.NodeName.Contains("Invoice"));

        // Verify Deal creation in DB
        Assert.True(result.FinalContext.ContainsKey("createdDealId"));
        var dealId = (Guid)result.FinalContext["createdDealId"];
        var createdDeal = await db.Deals.FirstOrDefaultAsync(d => d.Id == dealId);
        Assert.NotNull(createdDeal);
        Assert.Equal(DealStage.Won, createdDeal.Stage);

        // Verify Invoice creation with 19% VAT in TND
        Assert.True(result.FinalContext.ContainsKey("generatedInvoiceNumber"));
        var invNum = (string)result.FinalContext["generatedInvoiceNumber"];
        var invoice = await db.Invoices.Include(i => i.LineItems).FirstOrDefaultAsync(i => i.InvoiceNumber == invNum);
        Assert.NotNull(invoice);
        Assert.Equal("TND", invoice.Currency);
        Assert.Equal(19.0m, invoice.TaxPercentage);
        Assert.True(invoice.TaxAmount > 0);
        Assert.Equal(invoice.SubTotal + invoice.TaxAmount, invoice.TotalAmount);
    }

    // =========================================================================
    // 4. Milestone Sprints -> Autonomous Invoicing (Ever-Teams & Ever-Gauzy)
    // =========================================================================

    [Fact]
    public async Task MilestoneSprints_CompleteMilestoneWithBilling_TriggersB2BInvoiceWithVat()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var invoiceService = new InvoiceService(db);
        var projectService = new ProjectTaskService(db, invoiceService);

        // 1. Create a Deal and convert it to an Onboarding Project
        var deal = Deal.Create(tenantId, Guid.NewGuid(), "Enterprise Growth Contract", 50000m, DealStage.Won);
        db.Deals.Add(deal);
        await db.SaveChangesAsync();

        var project = await projectService.ConvertFromDealAsync(tenantId, deal.Id);
        var tasks = (await projectService.GetProjectTasksAsync(tenantId, project.Id)).ToList();
        Assert.NotEmpty(tasks);

        var milestoneTask = tasks.First(t => t.Title.Contains("Technical Infrastructure"));
        Assert.NotEqual("Done", milestoneTask.Status);

        // 2. Complete Milestone with Autonomous Invoicing Trigger
        var completionReq = new CompleteMilestoneRequest(
            TriggerInvoice: true,
            MilestoneAmount: 7500.00m,
            Currency: "USD"
        );

        var completionResult = await projectService.CompleteMilestoneWithBillingAsync(tenantId, milestoneTask.Id, completionReq);

        Assert.NotNull(completionResult);
        Assert.Equal("Done", completionResult.Task.Status);
        Assert.NotNull(completionResult.Invoice);

        // 3. Verify Invoice Math (7500 USD + 19% VAT = 8925 USD)
        var inv = completionResult.Invoice;
        Assert.Equal(7500.00m, inv.SubTotal);
        Assert.Equal(19.0m, inv.TaxPercentage);
        Assert.Equal(1425.00m, inv.TaxAmount); // 7500 * 0.19 = 1425
        Assert.Equal(8925.00m, inv.TotalAmount); // 7500 + 1425 = 8925
        Assert.Contains(milestoneTask.Title, inv.Notes);

        // Verify stored in DB
        var savedInvoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == inv.Id);
        Assert.NotNull(savedInvoice);
    }

    // =========================================================================
    // 5. Complete End-to-End Enterprise Customer Journey
    // =========================================================================

    [Fact]
    public async Task CompleteCustomerJourney_InboundMessageToWonDealMilestonesAndInvoicing()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var notifier = new MockRealtimeNotifier();
        var copilot = new AiCopilotService(db, notifier);

        var routerService = new InboundMessageRouterService(db, notifier, copilot);
        var customFieldService = new CustomFieldService(db);
        var invoiceService = new InvoiceService(db);
        var projectService = new ProjectTaskService(db, invoiceService);

        // Step 1: Inbound WhatsApp lead initiates inquiry with interactive button choice
        var callbackPayload = new InboundMessagePayload(
            TenantId: tenantId,
            PhoneNumber: "+21699887766",
            SenderName: "Karthago Distribution",
            Content: "We need an enterprise rollout",
            InteractiveCallback: new WhatsAppInteractiveCallbackPayload(
                ButtonId: "btn_enterprise",
                ButtonText: "Enterprise Full-Stack Suite"
            )
        );

        var routing = await routerService.RouteInboundMessageAsync(callbackPayload);
        Assert.NotEqual(Guid.Empty, routing.ContactId);
        Assert.NotEqual(Guid.Empty, routing.ConversationId);

        // Step 2: Tag Contact with dynamic custom fields
        await customFieldService.CreateDefinitionAsync(tenantId, new CreateCustomFieldDefinitionRequest(
            EntityType: "Contact",
            FieldName: "preferred_channel",
            FieldLabel: "Preferred Communication Channel",
            FieldType: CustomFieldType.DropdownSelect,
            Options: new List<string> { "WhatsApp", "Email", "Phone" }
        ));

        await customFieldService.SetValuesAsync(tenantId, "Contact", routing.ContactId, new List<SetFieldValueDto>
        {
            new("preferred_channel", "WhatsApp")
        });

        var contactFields = await customFieldService.GetValuesAsync(tenantId, "Contact", routing.ContactId);
        Assert.Equal("WhatsApp", contactFields.Fields.First(f => f.FieldName == "preferred_channel").Value?.ToString());

        // Step 3: Create Won Deal for this Contact
        var deal = Deal.Create(
            tenantId: tenantId,
            contactId: routing.ContactId,
            title: "Karthago Distribution Omnichannel Onboarding",
            amount: 12000.00m,
            stage: DealStage.Won
        );
        db.Deals.Add(deal);
        await db.SaveChangesAsync();

        // Step 4: Convert to Onboarding Sprint Project
        var project = await projectService.ConvertFromDealAsync(tenantId, deal.Id);
        Assert.Equal("Active", project.Status);

        // Step 5: Deliver kickoff sprint task and auto-trigger milestone invoice
        var tasks = (await projectService.GetProjectTasksAsync(tenantId, project.Id)).ToList();
        var kickoffTask = tasks.First(t => t.Title.Contains("Kickoff"));

        var milestoneResult = await projectService.CompleteMilestoneWithBillingAsync(
            tenantId,
            kickoffTask.Id,
            new CompleteMilestoneRequest(TriggerInvoice: true, MilestoneAmount: 3000.00m, Currency: "USD")
        );

        Assert.NotNull(milestoneResult);
        Assert.Equal("Done", milestoneResult.Task.Status);
        Assert.NotNull(milestoneResult.Invoice);
        Assert.Equal(3570.00m, milestoneResult.Invoice.TotalAmount); // 3000 + 19% VAT = 3570

        // Step 6: Verify payment can be recorded against the invoice
        var payment = await invoiceService.RecordPaymentAsync(
            tenantId,
            milestoneResult.Invoice.Id,
            new RecordPaymentRequest(Amount: 3570.00m, PaymentMethod: "WireTransfer", TransactionReference: "WT-2026-9901")
        );

        Assert.NotNull(payment);
        Assert.Equal("Success", payment.Status);

        // Verify invoice is marked Paid
        var updatedInvoice = await invoiceService.GetInvoiceByIdAsync(tenantId, milestoneResult.Invoice.Id);
        Assert.NotNull(updatedInvoice);
        Assert.Equal(InvoiceStatus.Paid, updatedInvoice.Status);
        Assert.NotNull(updatedInvoice.PaidAtUtc);
    }
}
