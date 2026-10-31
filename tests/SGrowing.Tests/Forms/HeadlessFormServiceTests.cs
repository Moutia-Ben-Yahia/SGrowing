using SGrowing.Api.Modules.Forms;
using Xunit;

namespace SGrowing.Tests.Forms;

public class HeadlessFormServiceTests
{
    private readonly HeadlessFormService _service;
    private readonly Guid _defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public HeadlessFormServiceTests()
    {
        _service = new HeadlessFormService();
    }

    [Fact]
    public async Task SeedData_ContainsExpectedPreSeededForms()
    {
        var forms = await _service.GetFormsAsync(_defaultTenantId);

        Assert.NotNull(forms);
        Assert.True(forms.Count >= 3);
        Assert.Contains(forms, f => f.Slug == "enterprise-demo-request");
        Assert.Contains(forms, f => f.Slug == "b2b-wholesale-ingestion");
        Assert.Contains(forms, f => f.Slug == "customer-satisfaction-nps");
    }

    [Fact]
    public async Task SeedData_EnterpriseDemo_HasCorrectStepsAndFields()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");

        Assert.NotNull(form);
        Assert.Equal(FormTargetAction.CreateLead, form.TargetAction);
        Assert.True(form.RequireTurnstile);
        Assert.True(form.Fields.Count >= 10);

        // Step 1 fields
        Assert.Contains(form.Fields, f => f.Name == "first_name" && f.Step == 1 && f.IsRequired);
        Assert.Contains(form.Fields, f => f.Name == "last_name" && f.Step == 1 && f.IsRequired);
        Assert.Contains(form.Fields, f => f.Name == "email" && f.Step == 1 && f.Type == FormFieldType.Email);
        Assert.Contains(form.Fields, f => f.Name == "phone" && f.Step == 1 && f.Type == FormFieldType.Tel);

        // Step 2 fields
        Assert.Contains(form.Fields, f => f.Name == "company" && f.Step == 2 && f.IsRequired);
        Assert.Contains(form.Fields, f => f.Name == "company_size" && f.Step == 2 && f.Type == FormFieldType.Select);

        // Step 3 fields
        Assert.Contains(form.Fields, f => f.Name == "interest_areas" && f.Step == 3 && f.Type == FormFieldType.MultiSelect);
    }

    [Fact]
    public async Task SeedData_B2BWholesale_HasDealTargetActionAndWebhook()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "b2b-wholesale-ingestion");

        Assert.NotNull(form);
        Assert.Equal(FormTargetAction.CreateDeal, form.TargetAction);
        Assert.False(string.IsNullOrEmpty(form.WebhookUrl));
        Assert.False(string.IsNullOrEmpty(form.WebhookSecret));
        Assert.Contains(form.Fields, f => f.Name == "tax_id" && !string.IsNullOrEmpty(f.ValidationRegex));
        Assert.Contains(form.Fields, f => f.Name == "reseller_certificate" && f.Type == FormFieldType.FileUpload);
    }

    [Fact]
    public async Task SeedData_CsatNps_HasSupportTicketActionAndRatings()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "customer-satisfaction-nps");

        Assert.NotNull(form);
        Assert.Equal(FormTargetAction.CreateSupportTicket, form.TargetAction);
        Assert.Contains(form.Fields, f => f.Name == "overall_satisfaction" && f.Type == FormFieldType.Rating && f.Max == 5);
        Assert.Contains(form.Fields, f => f.Name == "nps_score" && f.Type == FormFieldType.Rating && f.Max == 10);
        Assert.Contains(form.Fields, f => f.Name == "would_recommend" && f.Type == FormFieldType.Checkbox && f.IsRequired);
    }

    [Fact]
    public async Task CreateForm_SetsPropertiesAndSlugProperly()
    {
        var request = new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Partner Registration Application",
            Description = "Onboarding application for certified integration partners.",
            TargetAction = FormTargetAction.CreateDeal,
            Fields = new List<FormFieldDefinition>
            {
                new() { Name = "partner_name", Label = "Partner Name", Type = FormFieldType.Text, IsRequired = true },
                new() { Name = "contact_email", Label = "Contact Email", Type = FormFieldType.Email, IsRequired = true }
            }
        };

        var created = await _service.CreateFormAsync(request);

        Assert.NotNull(created);
        Assert.Equal("partner-registration-application", created.Slug);
        Assert.Equal("Partner Registration Application", created.Title);
        Assert.Equal(FormTargetAction.CreateDeal, created.TargetAction);
        Assert.Equal(2, created.Fields.Count);
    }

    [Fact]
    public async Task UpdateForm_ModifiesPropertiesSuccessfully()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var updateReq = new UpdateHeadlessFormRequest
        {
            Title = "Updated Enterprise Demo Portal",
            RateLimitPerMinutePerIp = 15,
            RequireTurnstile = false
        };

        var updated = await _service.UpdateFormAsync(form.Id, updateReq);

        Assert.Equal("Updated Enterprise Demo Portal", updated.Title);
        Assert.Equal(15, updated.RateLimitPerMinutePerIp);
        Assert.False(updated.RequireTurnstile);
    }

    [Fact]
    public async Task UpdateForm_NonExistentId_ThrowsKeyNotFoundException()
    {
        var invalidId = Guid.NewGuid();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateFormAsync(invalidId, new UpdateHeadlessFormRequest { Title = "Test" }));
    }

    [Fact]
    public async Task ArchiveForm_MarksFormArchivedAndHidesFromDefaultList()
    {
        var req = new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Temporary Event RSVP",
            Fields = new() { new() { Name = "name", Label = "Name", IsRequired = true } }
        };
        var created = await _service.CreateFormAsync(req);

        var archived = await _service.ArchiveFormAsync(created.Id);
        Assert.True(archived);

        var activeForms = await _service.GetFormsAsync(_defaultTenantId, includeArchived: false);
        Assert.DoesNotContain(activeForms, f => f.Id == created.Id);

        var allForms = await _service.GetFormsAsync(_defaultTenantId, includeArchived: true);
        Assert.Contains(allForms, f => f.Id == created.Id && f.IsArchived);
    }

    [Fact]
    public async Task ArchiveForm_NonExistent_ReturnsFalse()
    {
        var result = await _service.ArchiveFormAsync(Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task GetFormById_ReturnsCorrectForm()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "b2b-wholesale-ingestion");
        Assert.NotNull(form);

        var retrieved = await _service.GetFormByIdAsync(form.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(form.Id, retrieved.Id);
    }

    [Fact]
    public async Task GetFormBySlug_CaseInsensitiveMatch_ReturnsForm()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "ENTERPRISE-DEMO-REQUEST");
        Assert.NotNull(form);
        Assert.Equal("enterprise-demo-request", form.Slug);
    }

    [Fact]
    public async Task Validate_RequiredField_Missing_ReturnsValidationError()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["last_name"] = "Doe",
                ["email"] = "valid@acme.com",
                ["phone"] = "+1 555-123-4567"
                // Missing required "first_name"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "first_name" && e.Error.Contains("required"));
    }

    [Fact]
    public async Task Validate_RequiredCheckbox_Unchecked_ReturnsValidationError()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "customer-satisfaction-nps");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["overall_satisfaction"] = 5,
                ["nps_score"] = 9,
                ["would_recommend"] = false // required checkbox not checked
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "would_recommend" && e.Error.Contains("must be checked"));
    }

    [Fact]
    public async Task Validate_RequiredCheckbox_Checked_Passes()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "customer-satisfaction-nps");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["overall_satisfaction"] = 5,
                ["nps_score"] = 9,
                ["would_recommend"] = true
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_RequiredMultiSelect_Empty_ReturnsValidationError()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "b2b-wholesale-ingestion");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["company"] = "Apex Wholesale",
                ["tax_id"] = "US-123456",
                ["email"] = "buyer@apex.com",
                ["annual_order_volume"] = "$50k - $250k",
                ["product_lines"] = new List<string>(), // Empty multiselect
                ["delivery_address"] = "123 Port St"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "product_lines" && e.Error.Contains("requires at least one selection"));
    }

    [Fact]
    public async Task Validate_RequiredMultiSelect_Valid_Passes()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "b2b-wholesale-ingestion");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["company"] = "Apex Wholesale",
                ["tax_id"] = "US-123456",
                ["email"] = "buyer@apex.com",
                ["annual_order_volume"] = "$50k - $250k",
                ["product_lines"] = new List<string> { "Raw Materials", "Bulk Pallets" },
                ["delivery_address"] = "123 Port St"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_EmailField_InvalidFormat_ReturnsValidationError()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["first_name"] = "John",
                ["last_name"] = "Doe",
                ["email"] = "not-a-valid-email",
                ["phone"] = "+1 555-123-4567",
                ["company"] = "Acme",
                ["company_size"] = "1-10",
                ["industry"] = "Software / SaaS"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "email" && e.Error.Contains("not a valid email address"));
    }

    [Fact]
    public async Task Validate_EmailField_Valid_Passes()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            CurrentStep = 1,
            Answers = new Dictionary<string, object?>
            {
                ["first_name"] = "John",
                ["last_name"] = "Doe",
                ["email"] = "valid.user@corporate.com",
                ["phone"] = "+1 555-123-4567"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("spammer@tempmail.com")]
    [InlineData("bot@guerrillamail.com")]
    [InlineData("fake@10minutemail.com")]
    [InlineData("abuser@mailinator.com")]
    [InlineData("throwaway@trashmail.com")]
    public async Task Validate_DisposableEmail_RejectedAsSpam(string disposableEmail)
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            CurrentStep = 1,
            Answers = new Dictionary<string, object?>
            {
                ["first_name"] = "Bad",
                ["last_name"] = "Actor",
                ["email"] = disposableEmail,
                ["phone"] = "+1 555-999-8888"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);

        Assert.False(result.IsValid);
        Assert.True(result.IsSpam);
        Assert.Contains("prohibited", result.SpamReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Validate_TelField_InvalidFormat_ReturnsValidationError()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            CurrentStep = 1,
            Answers = new Dictionary<string, object?>
            {
                ["first_name"] = "John",
                ["last_name"] = "Doe",
                ["email"] = "john@example.com",
                ["phone"] = "abc-no-digits"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "phone" && e.Error.Contains("valid phone number"));
    }

    [Fact]
    public async Task Validate_TelField_Valid_Passes()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            CurrentStep = 1,
            Answers = new Dictionary<string, object?>
            {
                ["first_name"] = "John",
                ["last_name"] = "Doe",
                ["email"] = "john@example.com",
                ["phone"] = "+1 (800) 555-0199"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_NumberField_NonNumeric_ReturnsValidationError()
    {
        var customForm = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Number Test",
            Fields = new() { new() { Name = "quantity", Label = "Quantity", Type = FormFieldType.Number, Min = 1, Max = 100 } }
        });

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?> { ["quantity"] = "invalid_number" }
        };

        var result = await _service.ValidateSubmissionAsync(customForm.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "quantity" && e.Error.Contains("valid number"));
    }

    [Fact]
    public async Task Validate_NumberField_BelowMin_ReturnsValidationError()
    {
        var customForm = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Number Min Test",
            Fields = new() { new() { Name = "quantity", Label = "Quantity", Type = FormFieldType.Number, Min = 10, Max = 100 } }
        });

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?> { ["quantity"] = 5 }
        };

        var result = await _service.ValidateSubmissionAsync(customForm.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "quantity" && e.Error.Contains("at least 10"));
    }

    [Fact]
    public async Task Validate_NumberField_AboveMax_ReturnsValidationError()
    {
        var customForm = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Number Max Test",
            Fields = new() { new() { Name = "quantity", Label = "Quantity", Type = FormFieldType.Number, Min = 1, Max = 50 } }
        });

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?> { ["quantity"] = 75 }
        };

        var result = await _service.ValidateSubmissionAsync(customForm.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "quantity" && e.Error.Contains("at most 50"));
    }

    [Fact]
    public async Task Validate_SelectField_InvalidOption_ReturnsValidationError()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            CurrentStep = 2,
            Answers = new Dictionary<string, object?>
            {
                ["company"] = "Acme Corp",
                ["company_size"] = "invalid-size-option",
                ["industry"] = "Software / SaaS"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "company_size" && e.Error.Contains("invalid selection"));
    }

    [Fact]
    public async Task Validate_SelectField_ValidOption_Passes()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            CurrentStep = 2,
            Answers = new Dictionary<string, object?>
            {
                ["company"] = "Acme Corp",
                ["company_size"] = "51-200",
                ["industry"] = "Software / SaaS"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_MultiSelectField_InvalidOption_ReturnsValidationError()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            CurrentStep = 3,
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["interest_areas"] = new List<string> { "Omnichannel Messaging", "NonExistentFeature" }
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "interest_areas" && e.Error.Contains("not allowed"));
    }

    [Fact]
    public async Task Validate_RatingField_OutOfBounds_ReturnsValidationError()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "customer-satisfaction-nps");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["overall_satisfaction"] = 6, // max is 5
                ["nps_score"] = 10,
                ["would_recommend"] = true
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "overall_satisfaction" && e.Error.Contains("between 1 and 5"));
    }

    [Fact]
    public async Task Validate_RatingField_Valid_Passes()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "customer-satisfaction-nps");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["overall_satisfaction"] = 5,
                ["nps_score"] = 9,
                ["would_recommend"] = true
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_TextLength_MinConstraint_ReturnsValidationError()
    {
        var customForm = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Text Min Constraint",
            Fields = new() { new() { Name = "short_code", Label = "Code", Type = FormFieldType.Text, Min = 5 } }
        });

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?> { ["short_code"] = "abc" }
        };

        var result = await _service.ValidateSubmissionAsync(customForm.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "short_code" && e.Error.Contains("at least 5 characters"));
    }

    [Fact]
    public async Task Validate_TextLength_MaxConstraint_ReturnsValidationError()
    {
        var customForm = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Text Max Constraint",
            Fields = new() { new() { Name = "short_code", Label = "Code", Type = FormFieldType.Text, Max = 4 } }
        });

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?> { ["short_code"] = "too_long_code" }
        };

        var result = await _service.ValidateSubmissionAsync(customForm.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "short_code" && e.Error.Contains("at most 4 characters"));
    }

    [Fact]
    public async Task Validate_RegexConstraint_Mismatch_ReturnsValidationError()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "b2b-wholesale-ingestion");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["company"] = "Apex Wholesale",
                ["tax_id"] = "bad tax id with invalid spaces!",
                ["email"] = "buyer@apex.com",
                ["annual_order_volume"] = "$50k - $250k",
                ["product_lines"] = new List<string> { "Raw Materials" },
                ["delivery_address"] = "123 Port St"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldName == "tax_id" && e.Error.Contains("required format"));
    }

    [Fact]
    public async Task Validate_RegexConstraint_Match_Passes()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "b2b-wholesale-ingestion");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["company"] = "Apex Wholesale",
                ["tax_id"] = "US-12345678",
                ["email"] = "buyer@apex.com",
                ["annual_order_volume"] = "$50k - $250k",
                ["product_lines"] = new List<string> { "Raw Materials" },
                ["delivery_address"] = "123 Port St"
            }
        };

        var result = await _service.ValidateSubmissionAsync(form.Id, submission);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Turnstile_MissingToken_RejectsSubmission()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "customer-satisfaction-nps");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "", // Empty token
            Answers = new Dictionary<string, object?>
            {
                ["overall_satisfaction"] = 5,
                ["nps_score"] = 10,
                ["would_recommend"] = true
            }
        };

        var result = await _service.SubmitFormAsync(form.Id, submission);

        Assert.False(result.Success);
        Assert.True(result.IsSpam);
        Assert.Contains("Turnstile", result.SpamReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("cf-fail")]
    [InlineData("invalid-turnstile-token")]
    [InlineData("spam-token")]
    [InlineData("2x00000000000000000000BB")]
    public async Task Turnstile_ExplicitFailToken_RejectsSubmission(string failToken)
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "customer-satisfaction-nps");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = failToken,
            Answers = new Dictionary<string, object?>
            {
                ["overall_satisfaction"] = 5,
                ["nps_score"] = 10,
                ["would_recommend"] = true
            }
        };

        var result = await _service.SubmitFormAsync(form.Id, submission);

        Assert.False(result.Success);
        Assert.True(result.IsSpam);
        Assert.Contains("Turnstile", result.SpamReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("turnstile-valid-token")]
    [InlineData("1x00000000000000000000AA")]
    [InlineData("valid-turnstile-token")]
    [InlineData("cf-pass-token-verified")]
    public async Task Turnstile_ValidToken_Passes(string validToken)
    {
        _service.ResetRateLimits();
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "customer-satisfaction-nps");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = validToken,
            ClientIp = "10.0.0.99",
            Answers = new Dictionary<string, object?>
            {
                ["overall_satisfaction"] = 5,
                ["nps_score"] = 9,
                ["would_recommend"] = true
            }
        };

        var result = await _service.SubmitFormAsync(form.Id, submission);

        Assert.True(result.Success);
        Assert.False(result.IsSpam);
        Assert.NotNull(result.SubmissionId);
    }

    [Fact]
    public async Task Turnstile_Disabled_DoesNotRequireToken()
    {
        var customForm = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "No Turnstile Required Form",
            RequireTurnstile = false,
            Fields = new() { new() { Name = "feedback", Label = "Feedback", Type = FormFieldType.Text, IsRequired = true } }
        });

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = null, // No token provided
            ClientIp = "10.0.0.100",
            Answers = new Dictionary<string, object?> { ["feedback"] = "Great platform!" }
        };

        var result = await _service.SubmitFormAsync(customForm.Id, submission);

        Assert.True(result.Success);
        Assert.False(result.IsSpam);
    }

    [Fact]
    public async Task RateLimiter_ExceedsPerIpLimit_RejectsSubmissions()
    {
        _service.ResetRateLimits();

        var customForm = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Strict Rate Limit Form",
            RequireTurnstile = false,
            RateLimitPerMinutePerIp = 3,
            Fields = new() { new() { Name = "test", Label = "Test", Type = FormFieldType.Text } }
        });

        var ip = "198.51.100.1";

        // First 3 submissions should succeed
        for (int i = 0; i < 3; i++)
        {
            var res = await _service.SubmitFormAsync(customForm.Id, new FormSubmissionRequest
            {
                ClientIp = ip,
                Answers = new() { ["test"] = $"submission {i}" }
            });
            Assert.True(res.Success);
        }

        // 4th submission must be rejected due to rate limiting
        var blockedRes = await _service.SubmitFormAsync(customForm.Id, new FormSubmissionRequest
        {
            ClientIp = ip,
            Answers = new() { ["test"] = "submission 4" }
        });

        Assert.False(blockedRes.Success);
        Assert.True(blockedRes.IsSpam);
        Assert.Contains("Rate limit exceeded", blockedRes.Message);
    }

    [Fact]
    public async Task RateLimiter_Reset_AllowsSubmissionsAgain()
    {
        var customForm = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Rate Limit Reset Test",
            RequireTurnstile = false,
            RateLimitPerMinutePerIp = 2,
            Fields = new() { new() { Name = "test", Label = "Test", Type = FormFieldType.Text } }
        });

        var ip = "198.51.100.2";

        await _service.SubmitFormAsync(customForm.Id, new() { ClientIp = ip, Answers = new() { ["test"] = "1" } });
        await _service.SubmitFormAsync(customForm.Id, new() { ClientIp = ip, Answers = new() { ["test"] = "2" } });

        var blocked = await _service.SubmitFormAsync(customForm.Id, new() { ClientIp = ip, Answers = new() { ["test"] = "3" } });
        Assert.False(blocked.Success);

        // Reset
        _service.ResetRateLimits();

        var allowedAfterReset = await _service.SubmitFormAsync(customForm.Id, new() { ClientIp = ip, Answers = new() { ["test"] = "new 1" } });
        Assert.True(allowedAfterReset.Success);
    }

    [Fact]
    public async Task FormVelocityThrottle_ExceedsLimit_RejectsSubmissions()
    {
        _service.ResetRateLimits();

        var customForm = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Velocity Throttle Test",
            RequireTurnstile = false,
            RateLimitPerMinutePerIp = 100, // high per-ip limit
            VelocityThrottlePerMinute = 2, // low form-wide velocity
            Fields = new() { new() { Name = "test", Label = "Test", Type = FormFieldType.Text } }
        });

        // 2 distinct IPs
        await _service.SubmitFormAsync(customForm.Id, new() { ClientIp = "1.1.1.1", Answers = new() { ["test"] = "1" } });
        await _service.SubmitFormAsync(customForm.Id, new() { ClientIp = "2.2.2.2", Answers = new() { ["test"] = "2" } });

        // 3rd from 3.3.3.3 should be throttled by form velocity
        var throttled = await _service.SubmitFormAsync(customForm.Id, new() { ClientIp = "3.3.3.3", Answers = new() { ["test"] = "3" } });

        Assert.False(throttled.Success);
        Assert.True(throttled.IsSpam);
        Assert.Contains("velocity", throttled.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultiStep_Validation_OnlyValidatesCurrentStep()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        // Validating step 1: only first_name, last_name, email, phone are evaluated
        var step1Req = new FormSubmissionRequest
        {
            CurrentStep = 1,
            Answers = new Dictionary<string, object?>
            {
                ["first_name"] = "Alice",
                ["last_name"] = "Smith",
                ["email"] = "alice@bigcorp.com",
                ["phone"] = "+1 555-456-7890"
                // No step 2 or 3 fields provided
            }
        };

        var step1Result = await _service.ValidateSubmissionAsync(form.Id, step1Req);
        Assert.True(step1Result.IsValid);
        Assert.Empty(step1Result.Errors);
    }

    [Fact]
    public async Task MultiStep_FullSubmission_ValidatesAllSteps()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        // Submitting with missing step 2 fields without specifying step validates all
        var fullReq = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            Answers = new Dictionary<string, object?>
            {
                ["first_name"] = "Alice",
                ["last_name"] = "Smith",
                ["email"] = "alice@bigcorp.com",
                ["phone"] = "+1 555-456-7890"
                // Missing "company" and "company_size" from step 2
            }
        };

        var result = await _service.SubmitFormAsync(form.Id, fullReq);

        Assert.False(result.Success);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains(result.ValidationErrors, e => e.FieldName == "company");
    }

    [Fact]
    public async Task CrmMapping_CreateLead_MapsContactAndCustomProperties()
    {
        _service.ResetRateLimits();
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            ClientIp = "172.16.0.5",
            Answers = new Dictionary<string, object?>
            {
                ["first_name"] = "Samantha",
                ["last_name"] = "Vance",
                ["email"] = "samantha.vance@enterprise.io",
                ["phone"] = "+1 (555) 888-9999",
                ["company"] = "Vance Astro Labs",
                ["company_size"] = "201-1000",
                ["industry"] = "Software / SaaS",
                ["interest_areas"] = new List<string> { "Omnichannel Messaging" },
                ["go_live_date"] = "Q1 2027",
                ["notes"] = "Custom enterprise deployment needed."
            }
        };

        var result = await _service.SubmitFormAsync(form.Id, submission);

        Assert.True(result.Success);
        Assert.NotNull(result.CrmRecord);
        Assert.Equal(FormTargetAction.CreateLead, result.CrmRecord.Action);
        Assert.Equal("Lead: Samantha Vance", result.CrmRecord.Title);

        var contact = result.CrmRecord.Contact;
        Assert.Equal("Samantha", contact.FirstName);
        Assert.Equal("Vance", contact.LastName);
        Assert.Equal("samantha.vance@enterprise.io", contact.Email);
        Assert.Equal("+1 (555) 888-9999", contact.Phone);
        Assert.Equal("Vance Astro Labs", contact.Company);

        // Custom properties mapped
        Assert.True(contact.CustomProperties.ContainsKey("go_live_date"));
        Assert.True(contact.CustomProperties.ContainsKey("notes"));
    }

    [Fact]
    public async Task CrmMapping_CreateDeal_MapsWholesaleDealRecord()
    {
        _service.ResetRateLimits();
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "b2b-wholesale-ingestion");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            ClientIp = "172.16.0.6",
            Answers = new Dictionary<string, object?>
            {
                ["company"] = "Northwind Wholesale",
                ["tax_id"] = "US-98765432",
                ["email"] = "procurement@northwind.com",
                ["annual_order_volume"] = "$1M - $5M",
                ["product_lines"] = new List<string> { "Finished Goods", "Bulk Pallets" },
                ["delivery_address"] = "Bay 7, North Logistics Hub"
            }
        };

        var result = await _service.SubmitFormAsync(form.Id, submission);

        Assert.True(result.Success);
        Assert.NotNull(result.CrmRecord);
        Assert.Equal(FormTargetAction.CreateDeal, result.CrmRecord.Action);
        Assert.Contains("Deal (B2B Wholesale Ingestion Form): Northwind Wholesale", result.CrmRecord.Title);
        Assert.Equal("procurement@northwind.com", result.CrmRecord.Contact.Email);
    }

    [Fact]
    public async Task CrmMapping_CreateSupportTicket_MapsTicketRecord()
    {
        _service.ResetRateLimits();
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "customer-satisfaction-nps");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            ClientIp = "172.16.0.7",
            Answers = new Dictionary<string, object?>
            {
                ["overall_satisfaction"] = 4,
                ["nps_score"] = 8,
                ["liked_most"] = "Clean UI and great APIs",
                ["improvement_areas"] = "More webhooks",
                ["would_recommend"] = true,
                ["email"] = "reviewer@client.org"
            }
        };

        var result = await _service.SubmitFormAsync(form.Id, submission);

        Assert.True(result.Success);
        Assert.NotNull(result.CrmRecord);
        Assert.Equal(FormTargetAction.CreateSupportTicket, result.CrmRecord.Action);
        Assert.Contains("reviewer@client.org", result.CrmRecord.Title);
    }

    [Fact]
    public void Webhook_ComputesValidHmacSha256Signature()
    {
        var payload = "{\"event\":\"form.submission.created\",\"id\":\"sub_123\"}";
        var secret = "secret_key_12345";

        var signature = _service.GenerateHmacSignature(payload, secret);

        Assert.NotNull(signature);
        Assert.StartsWith("sha256=", signature);

        var isValid = _service.VerifyHmacSignature(payload, signature, secret);
        Assert.True(isValid);
    }

    [Fact]
    public void Webhook_TamperedPayload_FailsVerification()
    {
        var originalPayload = "{\"event\":\"form.submission.created\",\"id\":\"sub_123\"}";
        var tamperedPayload = "{\"event\":\"form.submission.created\",\"id\":\"sub_456\"}";
        var secret = "secret_key_12345";

        var signature = _service.GenerateHmacSignature(originalPayload, secret);
        var isValid = _service.VerifyHmacSignature(tamperedPayload, signature, secret);

        Assert.False(isValid);
    }

    [Fact]
    public async Task Webhook_OutboundDispatched_GeneratesSignatureOnSubmission()
    {
        _service.ResetRateLimits();
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "b2b-wholesale-ingestion");
        Assert.NotNull(form);

        var submission = new FormSubmissionRequest
        {
            TurnstileToken = "turnstile-valid-token",
            ClientIp = "172.16.0.8",
            Answers = new Dictionary<string, object?>
            {
                ["company"] = "Apex Logistics",
                ["tax_id"] = "US-11223344",
                ["email"] = "proc@apexlogistics.com",
                ["annual_order_volume"] = "$250k - $1M",
                ["product_lines"] = new List<string> { "Raw Materials" },
                ["delivery_address"] = "Terminal 4"
            }
        };

        var result = await _service.SubmitFormAsync(form.Id, submission);

        Assert.True(result.Success);
        Assert.NotNull(result.WebhookSignature);
        Assert.StartsWith("sha256=", result.WebhookSignature);

        Assert.NotNull(result.Submission);
        Assert.Equal("Dispatched", result.Submission.WebhookDeliveryStatus);
        Assert.NotNull(result.Submission.WebhookPayloadJson);
        Assert.True(_service.VerifyHmacSignature(result.Submission.WebhookPayloadJson, result.WebhookSignature, form.WebhookSecret!));
    }

    [Fact]
    public async Task Metrics_Tracking_UpdatesImpressionsSubmissionsAndConversionRate()
    {
        var customForm = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Metrics Test Form",
            RequireTurnstile = false,
            Fields = new() { new() { Name = "test", Label = "Test", Type = FormFieldType.Text } }
        });

        // Record 10 impressions
        for (int i = 0; i < 10; i++)
        {
            await _service.RecordImpressionAsync(customForm.Id);
        }

        // Submit 2 answers
        await _service.SubmitFormAsync(customForm.Id, new() { ClientIp = "5.5.5.1", CompletionTimeSeconds = 20, Answers = new() { ["test"] = "1" } });
        await _service.SubmitFormAsync(customForm.Id, new() { ClientIp = "5.5.5.2", CompletionTimeSeconds = 40, Answers = new() { ["test"] = "2" } });

        var metrics = await _service.GetFormAnalyticsAsync(customForm.Id);

        Assert.NotNull(metrics);
        Assert.Equal(10, metrics.Impressions);
        Assert.Equal(2, metrics.Submissions);
        Assert.Equal(20.0, metrics.ConversionRatePercentage); // 2/10 = 20%
        Assert.Equal(30.0, metrics.AverageCompletionTimeSeconds); // (20+40)/2 = 30s
    }

    [Fact]
    public async Task ArchivedForm_RejectsSubmission()
    {
        var form = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Form To Be Archived",
            RequireTurnstile = false,
            Fields = new() { new() { Name = "name", Label = "Name", Type = FormFieldType.Text } }
        });

        await _service.ArchiveFormAsync(form.Id);

        var result = await _service.SubmitFormAsync(form.Id, new FormSubmissionRequest
        {
            ClientIp = "1.2.3.4",
            Answers = new() { ["name"] = "Test" }
        });

        Assert.False(result.Success);
        Assert.Contains("archived", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Submissions_ListWithFilter_ReturnsCleanVsSpam()
    {
        var form = await _service.GetFormBySlugAsync(_defaultTenantId, "enterprise-demo-request");
        Assert.NotNull(form);

        var submissions = await _service.GetSubmissionsAsync(form.Id, page: 1, pageSize: 10, spamFilter: "clean");
        Assert.NotNull(submissions);
        Assert.All(submissions, s => Assert.False(s.IsSpam));
    }

    [Fact]
    public async Task Concurrency_50SimultaneousSubmissions_ThreadSafeAndAccurate()
    {
        _service.ResetRateLimits();

        var customForm = await _service.CreateFormAsync(new CreateHeadlessFormRequest
        {
            TenantId = _defaultTenantId,
            Title = "Stress Test High Concurrency Form",
            RequireTurnstile = false,
            RateLimitPerMinutePerIp = 10,
            VelocityThrottlePerMinute = 200,
            Fields = new()
            {
                new() { Name = "worker_id", Label = "Worker ID", Type = FormFieldType.Text, IsRequired = true },
                new() { Name = "value", Label = "Value", Type = FormFieldType.Number, IsRequired = true }
            }
        });

        const int concurrency = 50;
        var tasks = Enumerable.Range(1, concurrency).Select(async i =>
        {
            var req = new FormSubmissionRequest
            {
                ClientIp = $"10.50.{i / 255}.{i % 255 + 1}", // Unique IP per request
                CompletionTimeSeconds = 10 + i,
                Answers = new Dictionary<string, object?>
                {
                    ["worker_id"] = $"worker_{i}",
                    ["value"] = i * 10
                }
            };
            return await _service.SubmitFormAsync(customForm.Id, req);
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(concurrency, results.Length);
        Assert.All(results, r => Assert.True(r.Success));

        var submissions = await _service.GetSubmissionsAsync(customForm.Id, pageSize: 100);
        Assert.Equal(concurrency, submissions.Count);

        var metrics = await _service.GetFormAnalyticsAsync(customForm.Id);
        Assert.NotNull(metrics);
        Assert.Equal(concurrency, metrics.Submissions);
    }
}
