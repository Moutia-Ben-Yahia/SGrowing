using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Energy.SuperhotEgsMicroSeismicity;

public static class SuperhotEgsEndpoints
{
    public static IServiceCollection AddSuperhotEgsMicroSeismicityServices(this IServiceCollection services)
    {
        services.AddScoped<ISuperhotEgsSeismicityEngine, SuperhotEgsSeismicityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSuperhotEgsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/superhot-egs")
            .WithTags("Superhot Enhanced Geothermal Micro-Seismicity");

        group.MapPost("/evaluate", (
            [FromBody] SuperhotEgsEvaluationRequest request,
            [FromServices] ISuperhotEgsSeismicityEngine engine) =>
        {
            var plan = engine.EvaluateSeismicity(request);
            return Results.Ok(plan);
        })
        .WithName("EvaluateSuperhotEgsSeismicity")
        .WithSummary("Applies the regulatory Traffic Light System (TLS) and Gutenberg-Richter analysis to induced micro-seismic events in EGS wells.");

        group.MapGet("/well/{wellId}", (string wellId) =>
        {
            var nominal = new EgsWellTelemetry(wellId);
            return Results.Ok(new
            {
                WellId = wellId,
                DepthMeters = nominal.WellDepthMeters,
                BottomHoleTempCelsius = nominal.BottomHoleTemperatureCelsius,
                PressureBar = nominal.WellheadPressureBar,
                Status = "SUPERCRITICAL_STIMULATION_ONLINE"
            });
        })
        .WithName("GetSuperhotEgsWellTelemetry")
        .WithSummary("Returns well depth, supercritical temperature, and wellhead pressure for an enhanced geothermal well.");

        return app;
    }
}
