using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Forms;

public static class HeadlessFormEndpoints
{
    public static IEndpointRouteBuilder MapHeadlessFormEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/forms/headless")
            .WithTags("Horizon 54: Headless Form Engine & Turnstile Anti-Spam Pipeline");

        // 1. GET / - List all forms for tenant
        group.MapGet("/", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] bool? includeArchived,
            IHeadlessFormService service) =>
        {
            var effectiveTenant = tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var forms = await service.GetFormsAsync(effectiveTenant, includeArchived.GetValueOrDefault(false));
            return Results.Ok(forms);
        })
        .WithName("ListHeadlessForms")
        .WithSummary("List all dynamic headless forms for a tenant with schema definitions and metrics.");

        // 2. POST / - Create new form with schema
        group.MapPost("/", async (
            [FromBody] CreateHeadlessFormRequest request,
            IHeadlessFormService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(new { error = "Form title is required." });
            }

            var created = await service.CreateFormAsync(request);
            return Results.Created($"/api/v1/forms/headless/{created.Id}", created);
        })
        .WithName("CreateHeadlessForm")
        .WithSummary("Create a new headless form schema with dynamic validation rules and target action.");

        // 3. GET /{id:guid} - Get form definition by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromQuery] bool? recordImpression,
            IHeadlessFormService service) =>
        {
            var form = await service.GetFormByIdAsync(id);
            if (form == null)
            {
                return Results.NotFound(new { error = $"Headless form with ID '{id}' was not found." });
            }

            if (recordImpression.GetValueOrDefault(false))
            {
                await service.RecordImpressionAsync(id);
            }

            return Results.Ok(form);
        })
        .WithName("GetHeadlessFormById")
        .WithSummary("Retrieve headless form schema definition, fields, and anti-spam configuration.");

        // 4. PUT /{id:guid} - Update form schema & settings
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateHeadlessFormRequest request,
            IHeadlessFormService service) =>
        {
            try
            {
                var updated = await service.UpdateFormAsync(id, request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("UpdateHeadlessForm")
        .WithSummary("Update form schema fields, webhook configuration, Turnstile settings, or rate limits.");

        // 5. DELETE /{id:guid} - Archive form
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IHeadlessFormService service) =>
        {
            var success = await service.ArchiveFormAsync(id);
            if (!success)
            {
                return Results.NotFound(new { error = $"Headless form with ID '{id}' was not found." });
            }
            return Results.Ok(new { message = $"Form '{id}' successfully archived." });
        })
        .WithName("ArchiveHeadlessForm")
        .WithSummary("Archive a headless form, preventing further public submissions.");

        // 6. POST /{id:guid}/submit - Public form submission endpoint
        group.MapPost("/{id:guid}/submit", async (
            Guid id,
            [FromBody] FormSubmissionRequest request,
            HttpContext httpContext,
            IHeadlessFormService service) =>
        {
            // Extract remote client IP if not set
            if (string.IsNullOrWhiteSpace(request.ClientIp))
            {
                var forwardedHeader = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(forwardedHeader))
                {
                    request.ClientIp = forwardedHeader.Split(',')[0].Trim();
                }
                else
                {
                    request.ClientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                }
            }

            // Extract User-Agent if not set
            if (string.IsNullOrWhiteSpace(request.UserAgent))
            {
                request.UserAgent = httpContext.Request.Headers["User-Agent"].FirstOrDefault();
            }

            var result = await service.SubmitFormAsync(id, request);

            if (result.Success)
            {
                return Results.Ok(result);
            }

            if (result.IsSpam)
            {
                if (result.SpamReason?.Contains("Rate limit", StringComparison.OrdinalIgnoreCase) == true ||
                    result.SpamReason?.Contains("throttle", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return Results.StatusCode(StatusCodes.Status429TooManyRequests);
                }

                return Results.UnprocessableEntity(result);
            }

            return Results.BadRequest(result);
        })
        .WithName("SubmitHeadlessForm")
        .WithSummary("Submit answers to a headless form with Turnstile challenge, anti-spam validation, and CRM pipeline mapping.");

        // 7. GET /{id:guid}/submissions - List submissions with filter/pagination
        group.MapGet("/{id:guid}/submissions", async (
            Guid id,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? spamFilter,
            IHeadlessFormService service) =>
        {
            var submissions = await service.GetSubmissionsAsync(
                id,
                page.GetValueOrDefault(1),
                pageSize.GetValueOrDefault(50),
                spamFilter);

            return Results.Ok(submissions);
        })
        .WithName("ListHeadlessFormSubmissions")
        .WithSummary("List all ingested form submissions with contact mapping, spam score, and webhook delivery status.");

        // 8. GET /{id:guid}/analytics - Form performance metrics
        group.MapGet("/{id:guid}/analytics", async (
            Guid id,
            IHeadlessFormService service) =>
        {
            var metrics = await service.GetFormAnalyticsAsync(id);
            if (metrics == null)
            {
                return Results.NotFound(new { error = $"Headless form with ID '{id}' was not found." });
            }

            return Results.Ok(metrics);
        })
        .WithName("GetHeadlessFormAnalytics")
        .WithSummary("Retrieve aggregated performance metrics: impressions, conversion rate %, spam rejection count, average completion time.");

        // 9. POST /{id:guid}/impression - Record view / impression
        group.MapPost("/{id:guid}/impression", async (
            Guid id,
            IHeadlessFormService service) =>
        {
            await service.RecordImpressionAsync(id);
            return Results.Ok(new { message = "Impression recorded." });
        })
        .WithName("RecordHeadlessFormImpression")
        .WithSummary("Record a form view or impression to calculate real-time conversion rates.");

        // =========================================================================
        // Phase 8: Dynamic Headless Form Engine, Progressive Profiling & Micro-Surveys
        // =========================================================================
        var formsBaseGroup = app.MapGroup("/api/v1/forms")
            .WithTags("Phase 8: Dynamic Headless Form Engine, Progressive Profiling & In-App Micro-Surveys");

        // 10. POST /api/v1/forms/surveys/evaluate-trigger - Event-driven targeting rules
        formsBaseGroup.MapPost("/surveys/evaluate-trigger", async (
            [FromBody] SurveyTriggerEvaluationRequest request,
            IInAppMicroSurveyEngine surveyEngine) =>
        {
            var response = await surveyEngine.EvaluateTriggerAsync(request);
            return Results.Ok(response);
        })
        .WithName("EvaluateMicroSurveyTrigger")
        .WithSummary("Evaluate event-driven targeting rules (URL pattern, visits, cart value, dwell time, segment) for micro-surveys.");

        // 11. POST /api/v1/forms/surveys/submit-response - Ingestion of survey responses
        formsBaseGroup.MapPost("/surveys/submit-response", async (
            [FromBody] SubmitSurveyResponseRequest request,
            IInAppMicroSurveyEngine surveyEngine) =>
        {
            var result = await surveyEngine.SubmitResponseAsync(request);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("SubmitMicroSurveyResponse")
        .WithSummary("Ingest micro-survey response with real-time sentiment scoring and NPS classification.");

        // 12. GET /api/v1/forms/progressive-profile/{contactId}/next-field - Next highest-priority unanswered attribute
        formsBaseGroup.MapGet("/progressive-profile/{contactId:guid}/next-field", async (
            Guid contactId,
            [FromQuery] Guid? tenantId,
            IProgressiveProfilingEngine progressiveEngine) =>
        {
            var effectiveTenant = tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var nextField = await progressiveEngine.GetNextUnansweredAttributeAsync(effectiveTenant, contactId);
            if (nextField == null)
            {
                return Results.Ok(new { message = "Profile is 100% complete. No additional fields required.", isComplete = true, nextField = (ProgressiveAttributeDefinition?)null });
            }
            return Results.Ok(new { isComplete = false, nextField });
        })
        .WithName("GetNextProgressiveField")
        .WithSummary("Inspect existing contact data and return the single next highest-priority unanswered attribute.");

        // 13. POST /api/v1/forms/progressive-profile/{contactId}/submit - Progressive enrichment submission
        formsBaseGroup.MapPost("/progressive-profile/{contactId:guid}/submit", async (
            Guid contactId,
            [FromBody] SaveProgressiveAnswerRequest request,
            [FromQuery] Guid? tenantId,
            IProgressiveProfilingEngine progressiveEngine) =>
        {
            var effectiveTenant = tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            if (string.IsNullOrWhiteSpace(request.AttributeKey))
            {
                return Results.BadRequest(new { error = "Attribute key is required." });
            }

            var result = await progressiveEngine.SaveProgressiveAnswerAsync(effectiveTenant, contactId, request.AttributeKey, request.Value);
            return Results.Ok(result);
        })
        .WithName("SubmitProgressiveAnswer")
        .WithSummary("Save single progressive profile answer, updating contact record and returning completeness percentage (0-100%).");

        // 14. POST /api/v1/forms/branching/evaluate - Conditional field visibility & requirement evaluator
        formsBaseGroup.MapPost("/branching/evaluate", (
            [FromBody] BranchingEvaluationRequest request,
            IConditionalBranchingEvaluator branchingEvaluator) =>
        {
            var result = branchingEvaluator.Evaluate(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateConditionalBranching")
        .WithSummary("Evaluate JSON schema conditional branching rules (Visible, Hidden, Required, Disabled).");

        // 15. POST /api/v1/forms/spam-check - Multi-layer spam defense
        formsBaseGroup.MapPost("/spam-check", async (
            [FromBody] FormSpamCheckRequest request,
            HttpContext httpContext,
            IFormSpamDefenseService spamDefense) =>
        {
            if (string.IsNullOrWhiteSpace(request.ClientIp))
            {
                var forwardedHeader = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                request.ClientIp = !string.IsNullOrWhiteSpace(forwardedHeader)
                    ? forwardedHeader.Split(',')[0].Trim()
                    : httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            }

            var result = await spamDefense.CheckSpamAsync(request);
            return result.StatusCode switch
            {
                StatusCodes.Status429TooManyRequests => Results.StatusCode(StatusCodes.Status429TooManyRequests),
                StatusCodes.Status422UnprocessableEntity => Results.UnprocessableEntity(result),
                _ => Results.Ok(result)
            };
        })
        .WithName("CheckFormSpamDefense")
        .WithSummary("Multi-layer spam defense: honeypot check, minimum submission duration, Turnstile verification, and IP rate limiter.");

        return app;
    }
}
