using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.CallCompliance;

public static class CallComplianceEndpoints
{
    public static IServiceCollection AddCallComplianceServices(this IServiceCollection services)
    {
        services.AddSingleton<ICallComplianceEngine, CallComplianceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCallComplianceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/call-compliance")
            .WithTags("Voice - AI Voice QA & Call Compliance Scoring Mesh");

        group.MapGet("/corpus", (ICallComplianceEngine engine) =>
        {
            var corpus = engine.GetAuditCorpus();
            return Results.Ok(corpus);
        })
        .WithName("GetCallComplianceCorpus")
        .WithSummary("Retrieves all audited call records with script adherence scores and PII redaction status");

        group.MapPost("/decision", (AuditCallRequest request, ICallComplianceEngine engine) =>
        {
            try
            {
                var result = engine.RecordDecision(request);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Audit {request.AuditId} not found." });
            }
        })
        .WithName("RecordCallComplianceDecision")
        .WithSummary("Records supervisor or automated QA compliance decision on flagged call");

        return endpoints;
    }
}
