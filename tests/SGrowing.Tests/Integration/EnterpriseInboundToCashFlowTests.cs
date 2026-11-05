using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Common.Outbox;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Commerce.DynamicPricing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Integration;

public class EnterpriseInboundToCashFlowTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task FullLifecycle_InboundVisitor_To_StripePayment_To_OutboxDispatch()
    {
        using var db = CreateInMemoryDb();
        var outboxService = new OutboxService(db);
        var tenantId = Guid.NewGuid();

        // -------------------------------------------------------------
        // Stage 1: Anonymous Inbound Visitor Session Ingestion
        // -------------------------------------------------------------
        var anonymousSessionId = "anon_sess_" + Guid.NewGuid().ToString("N")[..8];
        var visitorSession = new VisitorSessionEntity
        {
            SessionId = anonymousSessionId,
            TenantId = tenantId,
            AnonymousId = "anon_user_99",
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            PageViewsJson = "[\"/solutions/enterprise-ai-growth\"]",
            UtmParametersJson = "{\"utm_source\":\"google\",\"utm_campaign\":\"ai_b2b\"}"
        };
        await db.VisitorSessionEntities.AddAsync(visitorSession);
        await db.SaveChangesAsync();

        visitorSession.SessionId.Should().Be(anonymousSessionId);

        // -------------------------------------------------------------
        // Stage 2: CDP Identity Stitching & CRM Contact Creation
        // -------------------------------------------------------------
        var contact = Contact.Create(
            tenantId: tenantId,
            firstName: "Karim",
            lastName: "Boussaid",
            email: "karim.boussaid@enterprise-dz.com",
            phone: "+213550123456",
            stage: LifecycleStage.Prospect);
        await db.Contacts.AddAsync(contact);

        var identityLink = new IdentityLinkEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceIdentifierJson = JsonSerializer.Serialize(new { SessionId = anonymousSessionId }),
            TargetIdentifierJson = JsonSerializer.Serialize(new { ContactId = contact.Id }),
            ConfidenceScore = 0.98,
            MatchReason = "FormSubmissionWithMatchingCookieSession"
        };
        await db.IdentityLinks.AddAsync(identityLink);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // Stage 3: CRM Deal Creation in B2B Pipeline
        // -------------------------------------------------------------
        var deal = Deal.Create(
            tenantId: tenantId,
            contactId: contact.Id,
            title: "Enterprise AI Growth OS - Annual Sovereign Cluster",
            amount: 150000m,
            stage: DealStage.Proposal,
            expectedCloseDate: DateTime.UtcNow.AddDays(7));
        await db.Deals.AddAsync(deal);
        await db.SaveChangesAsync();

        deal.Stage.Should().Be(DealStage.Proposal);

        // -------------------------------------------------------------
        // Stage 4: CPQ Quote & Dynamic Elasticity Pricing Evaluation
        // -------------------------------------------------------------
        var elasticityEngine = new PersistentPriceElasticityEngine(db);
        var pricingModel = await elasticityEngine.ComputeAndPersistOptimalPriceAsync(
            tenantId: tenantId,
            productId: "PROD-AI-OS-ENTERPRISE",
            basePrice: 150000m,
            baseDemand: 50m,
            unitCost: 45000m,
            elasticity: -1.2m);

        pricingModel.OptimalPrice.Should().BeGreaterThan(45000m);

        var quote = new QuoteProposal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contact.Id,
            DealId = deal.Id,
            QuoteNumber = "Q-2026-DZ-001",
            Subtotal = pricingModel.OptimalPrice,
            DiscountPercent = 10.0m,
            TaxPercent = 19.0m,
            TotalAmount = Math.Round(pricingModel.OptimalPrice * 0.9m * 1.19m, 2),
            Status = QuoteStatus.Accepted,
            Currency = "DZD",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        };
        await db.QuoteProposals.AddAsync(quote);
        await db.SaveChangesAsync();

        quote.Status.Should().Be(QuoteStatus.Accepted);

        // -------------------------------------------------------------
        // Stage 5: Digital Contract E-Sign Execution
        // -------------------------------------------------------------
        var contractPayload = $"{quote.QuoteNumber}:{quote.TotalAmount}:{contact.Email}";
        var sha256Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contractPayload)));

        var digitalContract = new DigitalContract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DealId = deal.Id,
            Title = "Master Subscription Agreement - SGrowing Sovereign Cluster",
            ContractType = "OrderForm",
            Status = ContractStatus.Signed,
            SignerName = contact.Name,
            SignerEmail = contact.Email ?? "billing@enterprise-dz.com",
            ContractValue = quote.TotalAmount,
            SigningToken = Guid.NewGuid().ToString("N"),
            SignedPdfUrl = "https://vault.internal/contracts/dz-2026-001.pdf",
            SignedAtUtc = DateTime.UtcNow
        };
        await db.DigitalContracts.AddAsync(digitalContract);
        await db.SaveChangesAsync();

        digitalContract.Status.Should().Be(ContractStatus.Signed);

        // -------------------------------------------------------------
        // Stage 6: Resilient Stripe Webhook Verification & Outbox Ingestion
        // -------------------------------------------------------------
        var webhookSecret = "whsec_e2e_flow_verification_secret_99";
        var stripePayload = JsonSerializer.Serialize(new
        {
            id = "evt_e2e_stripe_checkout_" + Guid.NewGuid().ToString("N")[..8],
            type = "checkout.session.completed",
            data = new
            {
                @object = new
                {
                    amount_total = (long)(quote.TotalAmount * 100),
                    metadata = new
                    {
                        order_id = quote.QuoteNumber,
                        quote_id = quote.Id.ToString()
                    }
                }
            }
        });

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{stripePayload}";
        var hmac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret), Encoding.UTF8.GetBytes(signedPayload));
        var signatureHeader = $"t={timestamp},v1={Convert.ToHexString(hmac).ToLowerInvariant()}";

        var webhookProcessor = new ResilientStripeWebhookProcessor(db, outboxService);
        var webhookResult = await webhookProcessor.ProcessWebhookAsync(
            tenantId: tenantId,
            payloadJson: stripePayload,
            signatureHeader: signatureHeader,
            configuredSecret: webhookSecret);

        webhookResult.Success.Should().BeTrue();
        webhookResult.OutboxMessageId.Should().NotBeNull();
        webhookResult.AmountReceived.Should().Be(quote.TotalAmount);

        // -------------------------------------------------------------
        // Stage 7: Transactional Outbox Dispatch & Commission Statement
        // -------------------------------------------------------------
        var pendingMessages = await outboxService.GetPendingMessagesAsync(batchSize: 10);
        pendingMessages.Should().Contain(m => m.Id == webhookResult.OutboxMessageId);

        var targetMsg = pendingMessages.First(m => m.Id == webhookResult.OutboxMessageId);
        targetMsg.Status.Should().Be(OutboxMessageStatus.Processing);

        // Transition outbox to published
        await outboxService.MarkAsPublishedAsync(targetMsg.Id);

        var dispatched = await db.TransactionalOutboxMessages.FindAsync(targetMsg.Id);
        dispatched!.Status.Should().Be(OutboxMessageStatus.Published);
        dispatched.ProcessedAtUtc.Should().NotBeNull();

        // Create commission statement for the closed deal
        var commissionStatement = SalesCommissionStatement.Create(
            tenantId: tenantId,
            periodId: Guid.NewGuid(),
            salesRepUserId: Guid.NewGuid(),
            salesRepName: "Amine Meziani",
            dealsClosed: 1,
            totalDealsValue: quote.TotalAmount,
            commission: Math.Round(quote.TotalAmount * 0.08m, 2));

        commissionStatement.Status = CommissionStatementStatus.Approved;
        await db.SalesCommissionStatements.AddAsync(commissionStatement);
        await db.SaveChangesAsync();

        commissionStatement.CalculatedCommission.Should().BeGreaterThan(0);
        commissionStatement.Status.Should().Be(CommissionStatementStatus.Approved);
    }
}
