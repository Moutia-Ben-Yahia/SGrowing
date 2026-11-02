using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.AI.DocumentGrounding;

public static class DocumentGroundingEndpoints
{
    public static IServiceCollection AddDocumentGroundingServices(this IServiceCollection services)
    {
        services.AddSingleton<DocumentGroundingCitationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDocumentGroundingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/document-grounding")
            .WithTags("Autonomous Multimodal Conversational AI Document Q&A & Knowledge Grounding Citations Engine");

        group.MapPost("/verify-grounding", (
            AuditAnswerGroundingRequest request,
            DocumentGroundingCitationEngine engine) =>
        {
            var verification = engine.VerifyAnswerGrounding(request);
            return Results.Ok(verification);
        });

        return app;
    }
}
