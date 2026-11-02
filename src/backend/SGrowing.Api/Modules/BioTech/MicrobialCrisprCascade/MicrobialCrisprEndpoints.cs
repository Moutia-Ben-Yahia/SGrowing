using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.BioTech.MicrobialCrisprCascade;

public static class MicrobialCrisprEndpoints
{
    public static IServiceCollection AddMicrobialCrisprCascadeServices(this IServiceCollection services)
    {
        services.AddScoped<IMicrobialCrisprCascadeEngine, MicrobialCrisprCascadeEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMicrobialCrisprEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/microbial-cascade")
            .WithTags("Microbial Cell Factory CRISPR-dCas9 Metabolic Cascade");

        group.MapPost("/evaluate", (
            [FromBody] MicrobialCascadeEvaluationRequest request,
            [FromServices] IMicrobialCrisprCascadeEngine engine) =>
        {
            var assessment = engine.EvaluateCascade(request);
            return Results.Ok(assessment);
        })
        .WithName("EvaluateMicrobialCascade")
        .WithSummary("Calculates dCas9 transcriptional redirection, product yield, and metabolic burden for microbial biomanufacturing.");

        group.MapGet("/status/{bioreactorId}", (string bioreactorId) =>
        {
            var nominal = new BioreactorState(bioreactorId);
            return Results.Ok(new
            {
                BioreactorId = bioreactorId,
                Host = nominal.HostOrganism,
                OD600 = nominal.OpticalDensity600,
                DissolvedOxygen = nominal.DissolvedOxygenPercent,
                Status = "BIOREACTOR_ONLINE"
            });
        })
        .WithName("GetMicrobialBioreactorStatus")
        .WithSummary("Returns host organism, optical density, and dissolved oxygen for an industrial biomanufacturing fermenter.");

        return app;
    }
}
