using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Energy.QuantumDotMegSolarCell;

public static class QuantumDotMegEndpoints
{
    public static IServiceCollection AddQuantumDotMegSolarCellServices(this IServiceCollection services)
    {
        services.AddScoped<IQuantumDotMegEngine, QuantumDotMegEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapQuantumDotMegEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/quantum-dot-meg")
            .WithTags("Quantum Dot Solar Cell Multiple Exciton Generation");

        group.MapPost("/evaluate", (
            [FromBody] QuantumDotMegEvaluationRequest request,
            [FromServices] IQuantumDotMegEngine engine) =>
        {
            var assessment = engine.EvaluateMegPerformance(request);
            return Results.Ok(assessment);
        })
        .WithName("EvaluateQuantumDotMegPerformance")
        .WithSummary("Calculates carrier multiplication quantum yield, Auger recombination loss, and power conversion efficiency for colloidal QD solar cells.");

        group.MapGet("/cell/{cellId}", (string cellId) =>
        {
            var nominal = new QuantumDotParameters(cellId);
            return Results.Ok(new
            {
                CellId = cellId,
                Core = nominal.CoreMaterial,
                DiameterNm = nominal.QuantumDotDiameterNanometers,
                BandgapEv = nominal.BandgapEnergyElectronVolts,
                Status = "QUANTUM_CONFINED_COLLOID_READY"
            });
        })
        .WithName("GetQuantumDotCellProfile")
        .WithSummary("Returns core-shell composition, dot diameter, and bandgap energy for a quantum dot solar device.");

        return app;
    }
}
