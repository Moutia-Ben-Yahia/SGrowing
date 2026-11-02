using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.CbdcAtomicDvpLiquiditySaving;

public static class CbdcLiquiditySavingEndpoints
{
    public static IServiceCollection AddCbdcLiquiditySavingServices(this IServiceCollection services)
    {
        services.AddScoped<ICbdcAtomicDvpLiquiditySavingEngine, CbdcAtomicDvpLiquiditySavingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCbdcLiquiditySavingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/cbdc-liquidity-saving")
            .WithTags("Finance - CBDC Atomic DvP & Liquidity Saving Mechanism (LSM)");

        group.MapPost("/analyze-batch", (
            CbdcSettlementTelemetry telemetry,
            ICbdcAtomicDvpLiquiditySavingEngine engine) =>
        {
            var result = engine.AnalyzeSettlementQueue(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCbdcBatch")
        .WithSummary("Analyzes cross-border wholesale CBDC atomic DvP/PvP settlement, cycle-cancelling netting, and liquidity conservation.");

        return app;
    }
}
