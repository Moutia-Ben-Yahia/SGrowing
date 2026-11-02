using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.AI.SyntheticData;

public static class DifferentialPrivacyEndpoints
{
    public static IServiceCollection AddDifferentialPrivacySandboxServices(this IServiceCollection services)
    {
        services.AddSingleton<DifferentialPrivacyEngine>();
        services.AddSingleton<CopulaSyntheticDataGenerator>();
        return services;
    }

    public static IEndpointRouteBuilder MapDifferentialPrivacySandboxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/differential-privacy")
            .WithTags("Autonomous Synthetic Data Generator & Differential Privacy Sandbox");

        group.MapPost("/perturb", (
            PerturbQueryRequest request,
            DifferentialPrivacyEngine engine) =>
        {
            try
            {
                var result = engine.PerturbQuery(request);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status429TooManyRequests);
            }
        });

        group.MapGet("/budget/{tenantId}", (
            string tenantId,
            DifferentialPrivacyEngine engine) =>
        {
            var budget = engine.GetBudget(tenantId);
            return Results.Ok(budget);
        });

        group.MapPost("/generate/leads", (
            GenerateSyntheticDatasetRequest request,
            CopulaSyntheticDataGenerator generator) =>
        {
            var leads = generator.GenerateLeads(request);
            return Results.Ok(leads);
        });

        return app;
    }
}
