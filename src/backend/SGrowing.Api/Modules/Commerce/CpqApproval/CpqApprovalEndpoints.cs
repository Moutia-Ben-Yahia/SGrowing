using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.CpqApproval;

public static class CpqApprovalEndpoints
{
    public static IServiceCollection AddCpqApprovalServices(this IServiceCollection services)
    {
        services.AddSingleton<ICpqApprovalEngine, CpqApprovalEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCpqApprovalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/commerce/cpq-approval")
            .WithTags("Commerce - Enterprise CPQ Approval Chain & Discount Governance");

        group.MapGet("/overview", (ICpqApprovalEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetCpqApprovalOverview")
        .WithSummary("Retrieves pending quotes, discount audit trails, and SLA compliance metrics");

        group.MapPost("/submit", (QuoteApprovalRequest request, ICpqApprovalEngine engine) =>
        {
            var record = engine.SubmitQuote(request);
            return Results.Ok(record);
        })
        .WithName("SubmitCpqQuote")
        .WithSummary("Submits an enterprise quote to the discount governance approval matrix");

        group.MapPost("/approve", (ApproveQuoteCommand command, ICpqApprovalEngine engine) =>
        {
            try
            {
                var record = engine.ApproveQuote(command);
                return Results.Ok(record);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Quote {command.QuoteId} not found." });
            }
        })
        .WithName("ApproveCpqQuote")
        .WithSummary("Approves a quote and seals it with a cryptographic HMAC audit token");

        group.MapPost("/reject", (RejectQuoteCommand command, ICpqApprovalEngine engine) =>
        {
            try
            {
                var record = engine.RejectQuote(command);
                return Results.Ok(record);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Quote {command.QuoteId} not found." });
            }
        })
        .WithName("RejectCpqQuote")
        .WithSummary("Rejects a quote with specific governance rationale");

        group.MapPost("/{quoteId}/escalate", (string quoteId, EscalateQuoteRequest request, ICpqApprovalEngine engine) =>
        {
            try
            {
                var record = engine.EscalateQuote(quoteId, request.Reason);
                return Results.Ok(record);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Quote {quoteId} not found." });
            }
        })
        .WithName("EscalateCpqQuote")
        .WithSummary("Escalates a stalled quote to the next hierarchical tier");

        return endpoints;
    }
}

public record EscalateQuoteRequest(string Reason);
