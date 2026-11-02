using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Telecom.Terahertz6gBeamSquint;

public static class Terahertz6gEndpoints
{
    public static IServiceCollection AddTerahertz6gBeamSquintServices(this IServiceCollection services)
    {
        services.AddScoped<ITerahertz6gBeamSquintEngine, Terahertz6gBeamSquintEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTerahertz6gEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/telecom/6g-beam-squint")
            .WithTags("Terahertz 6G Ultra-Massive MIMO Beam Squint");

        group.MapPost("/evaluate", (
            [FromBody] BeamSquintEvaluationRequest request,
            [FromServices] ITerahertz6gBeamSquintEngine engine) =>
        {
            var plan = engine.EvaluateBeamSquint(request);
            return Results.Ok(plan);
        })
        .WithName("Evaluate6gBeamSquint")
        .WithSummary("Calculates spatial beam squint angular drift, array gain loss, and optimal True-Time-Delay (TTD) sub-array count for THz 6G arrays.");

        group.MapGet("/array/{arrayId}", (string arrayId) =>
        {
            var nominal = new UmMimoArrayConfig(arrayId);
            return Results.Ok(new
            {
                ArrayId = arrayId,
                Elements = nominal.TotalAntennaElements,
                CarrierGhz = nominal.CarrierFrequencyGigaHertz,
                BandwidthGhz = nominal.TransmissionBandwidthGigaHertz,
                Status = "PHOTONIC_TERAHERTZ_FRONTEND_ALIGNED"
            });
        })
        .WithName("Get6gArrayProfile")
        .WithSummary("Returns antenna count, carrier frequency, and bandwidth for a terahertz 6G ultra-massive MIMO array.");

        return app;
    }
}
