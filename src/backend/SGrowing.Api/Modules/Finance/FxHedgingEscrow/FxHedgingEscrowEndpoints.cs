using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.FxHedgingEscrow;

public static class FxHedgingEscrowEndpoints
{
    public static IServiceCollection AddFxHedgingEscrowServices(this IServiceCollection services)
    {
        services.AddSingleton<IFxHedgingEscrowEngine, FxHedgingEscrowEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapFxHedgingEscrowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/fx-hedging")
            .WithTags("Finance - FX Hedging & Real-Time Settlement Escrow");

        group.MapGet("/portfolio", (IFxHedgingEscrowEngine engine) =>
        {
            var portfolio = engine.GetPortfolio();
            return Results.Ok(portfolio);
        })
        .WithName("GetFxHedgingPortfolio")
        .WithSummary("Retrieves multi-currency active FX forward contracts, notional hedge value, and VaR");

        group.MapPost("/lock-forward", (LockForwardRequest request, IFxHedgingEscrowEngine engine) =>
        {
            var contract = engine.LockForward(request);
            return Results.Ok(contract);
        })
        .WithName("LockFxForwardContract")
        .WithSummary("Locks forward exchange rate for cross-border software contract or payout");

        group.MapPost("/settle", (SettleEscrowRequest request, IFxHedgingEscrowEngine engine) =>
        {
            var outcome = engine.SettleContract(request);
            return Results.Ok(outcome);
        })
        .WithName("SettleFxContractEscrow")
        .WithSummary("Settles forward contract against spot rate, releasing hedged escrow funds without currency loss");

        return endpoints;
    }
}
