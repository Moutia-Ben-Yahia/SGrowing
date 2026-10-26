using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Feedback;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Feedback;

public class DynamicFormsTests
{
    private readonly SGrowingDbContext _db;
    private readonly DynamicFormService _service;

    public DynamicFormsTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new DynamicFormService(_db);
    }

    [Fact]
    public async Task CreateForm_And_SubmitAnswers_ShouldTrackSubmissions()
    {
        var tenantId = Guid.NewGuid();

        var fields = new List<DynamicFormField>
        {
            new() { FieldKey = "fullName", Label = "Full Name", FieldType = "text", IsRequired = true },
            new() { FieldKey = "governorate", Label = "Governorate", FieldType = "select", Options = new() { "Tunis", "Sfax", "Sousse" } },
            new() { FieldKey = "satisfactionRating", Label = "Delivery Experience (1-5)", FieldType = "rating" }
        };

        var form = await _service.CreateFormDefinitionAsync(
            tenantId,
            "Post-Delivery Courier Satisfaction",
            "post-delivery-csat",
            "Collect customer feedback on courier speed and parcel condition",
            fields,
            submitAction: "ThankYou",
            actionPayload: "Thank you for rating your delivery!"
        );

        Assert.NotNull(form);
        Assert.Equal("post-delivery-csat", form.Slug);
        Assert.Equal(0, form.SubmissionCount);

        // Submit answers
        var answers = new Dictionary<string, string>
        {
            ["fullName"] = "Ali Trabelsi",
            ["governorate"] = "Tunis",
            ["satisfactionRating"] = "5"
        };

        var submission = await _service.SubmitFormAnswersAsync(tenantId, form.Id, answers, null, "197.2.3.4", 25);
        Assert.NotNull(submission);
        Assert.Equal(25, submission.CompletedInSeconds);

        var submissions = await _service.GetSubmissionsAsync(tenantId, form.Id);
        Assert.Single(submissions);

        var retrievedForm = await _service.GetFormBySlugAsync(tenantId, "post-delivery-csat");
        Assert.NotNull(retrievedForm);
        Assert.Equal(1, retrievedForm.SubmissionCount);
    }

    [Fact]
    public async Task CryptographicPdf_Seal_And_VerifyAuditChain_ShouldDetectTampering()
    {
        var tenantId = Guid.NewGuid();
        var content = "ENTERPRISE SERVICE AGREEMENT\nClient: Falcon Logistics TN\nMonthly Commitment: 2,500 TND";

        // 1. Create document
        var doc = await _service.CreateDocumentRecordAsync(
            tenantId, "Falcon Logistics SLA Contract", content, "[{\"signer\":\"ceo@falcon.tn\",\"page\":1,\"x\":100,\"y\":200}]");

        Assert.NotNull(doc);
        Assert.Equal("Draft", doc.Status);
        Assert.False(string.IsNullOrEmpty(doc.OriginalSha256));

        // 2. Seal document cryptographically
        var sealedDoc = await _service.SealDocumentCryptographicallyAsync(
            tenantId, doc.Id, "ceo@falcon.tn", "X:100,Y:200,P:1");

        Assert.Equal("Sealed", sealedDoc.Status);
        Assert.NotNull(sealedDoc.SealedAtUtc);
        Assert.NotEqual(doc.OriginalSha256, sealedDoc.SealedSha256);

        // 3. Verify clean chain
        var (isTamperFree, summary) = await _service.VerifyDocumentAuditChainAsync(tenantId, doc.Id);
        Assert.True(isTamperFree);
        Assert.Contains("Verified 2 cryptographically chained", summary);

        // 4. Simulate malicious tampering of document content
        sealedDoc.DocumentContent = "ENTERPRISE SERVICE AGREEMENT\nClient: Falcon Logistics TN\nMonthly Commitment: 50 TND"; // altered!
        await _db.SaveChangesAsync();

        var (tamperDetected, tamperSummary) = await _service.VerifyDocumentAuditChainAsync(tenantId, doc.Id);
        Assert.False(tamperDetected);
        Assert.Contains("Tamper alert", tamperSummary);
    }
}
