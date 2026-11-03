using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Sales.RfpResponse;

public static class RfpResponseEndpoints
{
    public static IServiceCollection AddRfpResponseServices(this IServiceCollection services)
    {
        services.AddSingleton<RfpResponseEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRfpResponseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sales/rfp-response")
            .WithTags("Sales - Enterprise RFP Response & Security Questionnaire Mesh");

        group.MapGet("/overview", (RfpResponseEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetRfpResponseOverview")
        .WithSummary("Retrieve active security questionnaires, completion rates, and synthesized answer repository");

        group.MapPost("/generate-answer", (GenerateRfpAnswerRequest request, RfpResponseEngine engine) =>
        {
            var answer = engine.GenerateAnswer(request);
            return Results.Ok(answer);
        })
        .WithName("GenerateRfpAnswer")
        .WithSummary("Semantically synthesizes verified enterprise compliance and technical answers from audit corpus");

        group.MapPost("/import-questionnaire", (ImportQuestionnaireRequest request, RfpResponseEngine engine) =>
        {
            var record = engine.ImportQuestionnaire(request);
            return Results.Ok(record);
        })
        .WithName("ImportRfpQuestionnaire")
        .WithSummary("Imports enterprise customer RFP document and auto-parses questions for autonomous synthesis");

        return endpoints;
    }
}
