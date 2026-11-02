namespace SGrowing.Api.Modules.Environmental.QclPhotoacousticSpectroscopy;

public static class QclPhotoacousticEndpoints
{
    public static IServiceCollection AddQclPhotoacousticServices(this IServiceCollection services)
    {
        services.AddScoped<QclPhotoacousticSpectroscopyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapQclPhotoacousticEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/environmental/qcl-photoacoustic")
            .WithTags("QCL Mid-Infrared Photoacoustic Trace Gas Spectroscopy Arbiter");

        group.MapPost("/analyze", (QclPhotoacousticTelemetry telemetry, QclPhotoacousticSpectroscopyEngine engine) =>
        {
            var result = engine.AnalyzeSpectroscopy(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeQclPhotoacousticTraceGas")
        .WithSummary("Analyzes mid-infrared QCL photoacoustic spectroscopy harmonic signals, SNR, and sub-ppb trace gas leak surges.");

        return endpoints;
    }
}
