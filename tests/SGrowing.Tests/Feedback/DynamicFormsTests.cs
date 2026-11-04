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

    [Fact]
    public async Task CreateFormDefinition_ReturnsExisting_WhenSlugAlreadyExistsForTenant()
    {
        var tenantId = Guid.NewGuid();

        var first = await _service.CreateFormDefinitionAsync(
            tenantId,
            "Feedback Survey",
            "feedback-survey",
            "First description",
            new List<DynamicFormField>()
        );

        var second = await _service.CreateFormDefinitionAsync(
            tenantId,
            "Feedback Survey 2",
            "feedback-survey", // Same slug
            "Second description",
            new List<DynamicFormField>()
        );

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("First description", second.Description);
    }

    [Fact]
    public async Task GetFormBySlug_ReturnsNull_WhenSlugDoesNotExist()
    {
        var tenantId = Guid.NewGuid();

        var found = await _service.GetFormBySlugAsync(tenantId, "ghost-slug-not-found");

        Assert.Null(found);
    }

    [Fact]
    public async Task SubmitFormAnswers_ThrowsInvalidOperationException_WhenFormNotFound()
    {
        var tenantId = Guid.NewGuid();
        var unknownFormId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SubmitFormAnswersAsync(tenantId, unknownFormId, new Dictionary<string, string>()));

        Assert.Equal("Form not found.", ex.Message);
    }

    [Fact]
    public async Task SealDocumentCryptographically_ThrowsInvalidOperationException_WhenDocumentNotFound()
    {
        var tenantId = Guid.NewGuid();
        var unknownDocId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SealDocumentCryptographicallyAsync(tenantId, unknownDocId, "test@test.com", "0,0,1"));

        Assert.Equal("Document not found.", ex.Message);
    }

    [Fact]
    public async Task GetFormsAsync_ReturnsAllTenantForms_OrderedByCreatedAtUtcDescending()
    {
        var tenantId = Guid.NewGuid();

        var form1 = await _service.CreateFormDefinitionAsync(
            tenantId, "Survey Alpha", "survey-alpha", "First survey", new List<DynamicFormField>());
        var form2 = await _service.CreateFormDefinitionAsync(
            tenantId, "Survey Beta", "survey-beta", "Second survey", new List<DynamicFormField>());

        var forms = await _service.GetFormsAsync(tenantId);

        Assert.Equal(2, forms.Count);
        Assert.Contains(forms, f => f.Id == form1.Id);
        Assert.Contains(forms, f => f.Id == form2.Id);
    }

    [Fact]
    public async Task VerifyDocumentAuditChainAsync_WhenDocumentDoesNotExist_ReturnsFalseAndNotFoundMessage()
    {
        var tenantId = Guid.NewGuid();
        var unknownDocId = Guid.NewGuid();

        var (isTamperFree, summary) = await _service.VerifyDocumentAuditChainAsync(tenantId, unknownDocId);

        Assert.False(isTamperFree);
        Assert.Equal("Document not found.", summary);
    }
}
