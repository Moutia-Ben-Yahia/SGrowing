using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.CRM;

public class ContractAndEnrichmentTests
{
    private readonly SGrowingDbContext _db;
    private readonly LeadEnrichmentService _enrichmentService;
    private readonly ContractSigningService _contractService;

    public ContractAndEnrichmentTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _enrichmentService = new LeadEnrichmentService(_db);
        _contractService = new ContractSigningService(_db);
    }

    [Fact]
    public void ExtractDomain_ParsesEmailsAndUrlsProperly()
    {
        Assert.Equal("stripe.com", LeadEnrichmentService.ExtractDomainFromEmailOrWeb("billing@stripe.com"));
        Assert.Equal("salesforce.com", LeadEnrichmentService.ExtractDomainFromEmailOrWeb("https://www.salesforce.com/crm/sales"));
        Assert.Equal("github.com", LeadEnrichmentService.ExtractDomainFromEmailOrWeb("www.github.com"));
    }

    [Fact]
    public async Task EnrichLead_GeneratesFirmographicProfileAndUpdatesCompany()
    {
        var tenantId = Guid.NewGuid();

        // Pre-create company with just domain
        var company = Company.Create(tenantId, "Stripe Inc", domain: "stripe.com");
        await _db.Companies.AddAsync(company);
        await _db.SaveChangesAsync();

        var profile = await _enrichmentService.EnrichLeadAsync(tenantId, "patrick@stripe.com");

        Assert.NotNull(profile);
        Assert.Equal("stripe.com", profile.Domain);
        Assert.Equal("Fintech & Payments", profile.Industry);
        Assert.Contains("PostgreSQL", profile.TechStack);
        Assert.NotEmpty(profile.EmployeeCountRange);
        Assert.NotEmpty(profile.EstimatedAnnualRevenue);

        // Verify company was automatically updated
        var refreshedCompany = await _db.Companies.FindAsync(company.Id);
        Assert.Equal("Fintech & Payments", refreshedCompany!.Industry);
        Assert.True(refreshedCompany.EmployeeCount > 0);
    }

    [Fact]
    public async Task InChatContracts_DraftDispatchViewAndSign_ClosesDealWon()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // 1. Create a Deal in Proposal stage
        var deal = Deal.Create(
            tenantId,
            contactId,
            "Annual Enterprise Tier - 100 Seats",
            amount: 48000m,
            stage: DealStage.Proposal);

        await _db.Deals.AddAsync(deal);
        await _db.SaveChangesAsync();

        // 2. Create contract linked to the deal
        var contract = await _contractService.CreateContractAsync(
            tenantId,
            "Master Services Agreement & Order Form",
            contractType: "MSA",
            signerName: "Elena Rostova",
            signerEmail: "elena@acmeglobal.com",
            contractValue: 48000m,
            dealId: deal.Id);

        Assert.Equal(ContractStatus.Draft, contract.Status);
        Assert.NotEmpty(contract.SigningToken);

        // 3. Dispatch in-chat
        var dispatch = await _contractService.DispatchInChatContractAsync(tenantId, contract.Id);
        Assert.Contains(contract.SigningToken, dispatch.SigningUrl);
        Assert.Contains("Elena Rostova", dispatch.FormattedChatMessage);
        Assert.Contains("$48,000", dispatch.FormattedChatMessage);

        // 4. Customer opens signing link (mark viewed)
        var viewedContract = await _contractService.GetAndMarkViewedAsync(contract.SigningToken);
        Assert.NotNull(viewedContract);
        Assert.Equal(ContractStatus.Viewed, viewedContract.Status);

        // 5. Customer signs the contract
        var signResult = await _contractService.CompleteSigningAsync(
            contract.SigningToken,
            signerSignatureName: "Elena Rostova");

        Assert.True(signResult.Success);
        Assert.Equal(ContractStatus.Signed, signResult.Status);
        Assert.Equal(deal.Id, signResult.ClosedWonDealId);

        // 6. Verify Deal was automatically closed WON!
        var refreshedDeal = await _db.Deals.FindAsync(deal.Id);
        Assert.Equal(DealStage.Won, refreshedDeal!.Stage);

        // 7. Verify outbox event generated
        var outboxEvent = await _db.OutboxMessages.FirstOrDefaultAsync(o => o.EventType == "ContractSignedDealWonEvent");
        Assert.NotNull(outboxEvent);
        Assert.Contains("Elena Rostova", outboxEvent.Payload);
        Assert.Contains("48000", outboxEvent.Payload);
    }
}
