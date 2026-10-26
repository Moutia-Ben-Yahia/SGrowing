using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Feedback;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterFullPlatformEndToEndTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MasterFullPlatformDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Master_Full_Platform_Frontiers_1_to_4_EndToEnd_Should_Pass()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        // =========================================================================
        // FRONTIER 1: Database Health & Readiness
        // =========================================================================
        var canConnect = await db.Database.CanConnectAsync();
        Assert.True(canConnect);

        // =========================================================================
        // FRONTIER 2: Headless CMS, Digital Goods & PIM Variant Matrix
        // =========================================================================
        var digitalService = new DigitalGoodsService(db);

        // 1. Create Digital Asset (eBook/Training Course)
        var asset = await digitalService.CreateDigitalAssetAsync(
            tenantId,
            "Autonomous COD Scaling Guide",
            "cod-scaling-guide.pdf",
            "assets/books/guide.pdf",
            "application/pdf",
            sizeBytes: 12_500_000,
            maxDownloads: 3,
            expiryHours: 72
        );
        Assert.NotNull(asset);

        // 2. Issue Download Token
        var token = await digitalService.IssueDownloadTokenAsync(tenantId, asset.Id);
        Assert.NotNull(token);

        // 3. Consume download token
        var (isValid, _, downloadedAsset) = await digitalService.ValidateAndConsumeDownloadAsync(token.Token);
        Assert.True(isValid);
        Assert.NotNull(downloadedAsset);

        // 4. Issue License Key
        var licenses = await digitalService.GenerateLicenseKeysAsync(tenantId, "PRO-DESKTOP-SUITE", null, count: 1, seatsPerKey: 10);
        Assert.Single(licenses);
        Assert.StartsWith("SGROW-", licenses[0].LicenseKey);

        // 5. Generate PIM Variant Matrix & Feeds
        var matrix = await digitalService.GenerateVariantMatrixAsync(tenantId, "SHIRT-OVERSPEC", 49.99m, new()
        {
            ["Size"] = new() { "M", "L" },
            ["Color"] = new() { "Navy", "Beige" }
        });
        Assert.Equal(4, matrix.Count);

        var xmlFeed = await digitalService.GenerateGoogleMerchantFeedAsync(tenantId);
        Assert.Contains("http://base.google.com/ns/1.0", xmlFeed);

        var csvFeed = await digitalService.GenerateMetaCatalogFeedAsync(tenantId);
        Assert.Contains("SHIRT-OVERSPEC-M-NAVY", csvFeed);

        // =========================================================================
        // FRONTIER 3: Dynamic Form Builder & Real Cryptographic PDF Signing
        // =========================================================================
        var formService = new DynamicFormService(db);

        // 1. Create Dynamic Form
        var form = await formService.CreateFormDefinitionAsync(
            tenantId,
            "B2B Wholesale Lead Intake",
            "b2b-lead-intake",
            "Onboard high-volume COD merchants and distribution partners",
            new List<DynamicFormField>
            {
                new() { FieldKey = "companyName", Label = "Company Name", FieldType = "text", IsRequired = true },
                new() { FieldKey = "monthlyParcelVolume", Label = "Monthly Parcel Volume", FieldType = "select", Options = new() { "1,000 - 5,000", "5,000+" } }
            }
        );
        Assert.NotNull(form);

        // 2. Submit Answers
        var submission = await formService.SubmitFormAnswersAsync(tenantId, form.Id, new()
        {
            ["companyName"] = "Tunisia Express Fulfillment SARL",
            ["monthlyParcelVolume"] = "5,000+"
        });
        Assert.NotNull(submission);

        // 3. Create & Seal Cryptographic PDF Agreement
        var contract = await formService.CreateDocumentRecordAsync(
            tenantId, "Master Merchant Wholesale Agreement", "Wholesale Terms and Cross-Border Courier Tariffs for 2026-2027.");
        Assert.Equal("Draft", contract.Status);

        var sealedContract = await formService.SealDocumentCryptographicallyAsync(
            tenantId, contract.Id, "director@tunisiaexpress.tn", "X:350,Y:720,Page:1");
        Assert.Equal("Sealed", sealedContract.Status);

        var (isTamperFree, auditSummary) = await formService.VerifyDocumentAuditChainAsync(tenantId, contract.Id);
        Assert.True(isTamperFree);
        Assert.Contains("Verified 2 cryptographically chained state transitions", auditSummary);

        // =========================================================================
        // FRONTIER 4: Outbound Developer Webhooks & Public API Gateway
        // =========================================================================
        var devService = new DeveloperWebhookService(db);

        // 1. Self-Service Developer API Key
        var (keyEntity, plainKey) = await devService.GenerateApiKeyAsync(
            tenantId, "Logistics ERP Connector", new List<string> { "orders:read", "forms:read" }, isLive: true, rateLimit: 240);
        Assert.StartsWith("sg_live_", plainKey);

        var validatedKey = await devService.ValidateApiKeyAsync(plainKey);
        Assert.NotNull(validatedKey);
        Assert.Equal(keyEntity.Id, validatedKey.Id);

        // 2. Register Webhook Subscription
        var webhookSub = await devService.RegisterWebhookSubscriptionAsync(
            tenantId, "https://api.tunisiaexpress.tn/sgrowing/events", new List<string> { "form.submitted", "order.confirmed" });
        Assert.NotNull(webhookSub);

        // 3. Dispatch Event
        var dispatchLogs = await devService.DispatchWebhookEventAsync(tenantId, "form.submitted", new
        {
            FormId = form.Id,
            SubmissionId = submission.Id,
            SubmittedAtUtc = DateTime.UtcNow
        });
        Assert.Single(dispatchLogs);
        Assert.Equal(200, dispatchLogs[0].ResponseStatusCode);
        Assert.True(dispatchLogs[0].IsSuccess);

        // 4. Retry Webhook Delivery
        var retryLog = await devService.RetryDeliveryAsync(tenantId, dispatchLogs[0].Id);
        Assert.NotNull(retryLog);
        Assert.True(retryLog.IsSuccess);
    }
}
