using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.SettlementBridge;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for
/// Horizon 53: Decentralized Multi-Currency Settlement Bridges &amp; Atomic Stablecoin Liquidity Aggregator.
/// </summary>
public static class SettlementBridgeEndpoints
{
    /// <summary>
    /// Registers all settlement bridge, HTLC engine, FX hedging oracle, and PvP coordinator services as Singletons.
    /// </summary>
    public static IServiceCollection AddSettlementBridgeServices(this IServiceCollection services)
    {
        services.AddSingleton<IAtomicCrossRailSettlementEngine, AtomicCrossRailSettlementEngine>();
        services.AddSingleton<IRealTimeFxHedgingOracle, RealTimeFxHedgingOracle>();
        services.AddSingleton<IHerstattRiskEliminatorService, HerstattRiskEliminatorService>();

        return services;
    }

    /// <summary>
    /// Maps settlement bridge endpoints under '/api/v1/finance/settlement-bridge'.
    /// </summary>
    public static IEndpointRouteBuilder MapSettlementBridgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/settlement-bridge")
            .WithTags("Multi-Currency Settlement Bridges & Atomic Stablecoin Liquidity Aggregator");

        // 1. POST /api/v1/finance/settlement-bridge/swaps/initiate
        group.MapPost("/swaps/initiate", (
            [FromBody] InitiateCrossRailSwapRequest request,
            [FromServices] IAtomicCrossRailSettlementEngine engine,
            [FromServices] IRealTimeFxHedgingOracle oracle) =>
        {
            try
            {
                decimal spot = oracle.GetSpotRate(request.SourceCurrency, request.TargetCurrency);
                decimal targetAmount = Math.Round(request.SourceAmount * spot, 4);

                var swap = engine.InitiateSwap(
                    initiator: request.Initiator,
                    counterparty: request.Counterparty,
                    sourceRail: request.SourceRail,
                    targetRail: request.TargetRail,
                    sourceCurrency: request.SourceCurrency,
                    targetCurrency: request.TargetCurrency,
                    sourceAmount: request.SourceAmount,
                    targetAmount: targetAmount,
                    hashLockHex: request.HashLockHex,
                    timeToLive: TimeSpan.FromSeconds(request.ExpirySeconds)
                );

                var response = new InitiateSwapResponse(
                    SwapId: swap.SwapId,
                    SourceContractId: swap.SourceContractId,
                    TargetContractId: swap.TargetContractId,
                    SourceAmount: swap.SourceAmount,
                    TargetAmount: swap.TargetAmount,
                    ExchangeRate: swap.ExchangeRate,
                    HashLockHex: swap.HashLockHex,
                    ExpiryTimestampUtc: swap.ExpiryUtc,
                    Status: swap.Status
                );

                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_SWAP_REQUEST", message = ex.Message });
            }
        })
        .WithName("InitiateCrossRailSwap")
        .WithSummary("Initiates an atomic dual-rail HTLC swap locking funds on both rails with identical hash lock.");

        // 2. POST /api/v1/finance/settlement-bridge/swaps/claim
        group.MapPost("/swaps/claim", (
            [FromBody] ClaimSwapRequest request,
            [FromServices] IAtomicCrossRailSettlementEngine engine) =>
        {
            try
            {
                var completedSwap = engine.ClaimSwap(request.SwapId, request.SecretHex);
                var sourceContract = engine.GetContract(completedSwap.SourceContractId);
                var targetContract = engine.GetContract(completedSwap.TargetContractId);

                var response = new ClaimSwapResponse(
                    SwapId: completedSwap.SwapId,
                    Success: true,
                    SecretHex: request.SecretHex,
                    SourceContractStatus: sourceContract.Status,
                    TargetContractStatus: targetContract.Status,
                    ClaimedAtUtc: completedSwap.CompletedAtUtc ?? DateTime.UtcNow,
                    Message: "Atomic swap claimed successfully across both payment rails."
                );

                return Results.Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "SWAP_NOT_FOUND", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_PREIMAGE", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "SWAP_CLAIM_FAILED", message = ex.Message });
            }
        })
        .WithName("ClaimCrossRailSwap")
        .WithSummary("Unlocks and claims both legs of an active HTLC swap by presenting the valid preimage secret.");

        // 3. POST /api/v1/finance/settlement-bridge/swaps/refund
        group.MapPost("/swaps/refund", (
            [FromBody] RefundSwapRequest request,
            [FromServices] IAtomicCrossRailSettlementEngine engine) =>
        {
            try
            {
                var refundedSwap = engine.RefundSwap(request.SwapId);
                var sourceContract = engine.GetContract(refundedSwap.SourceContractId);
                var targetContract = engine.GetContract(refundedSwap.TargetContractId);

                var response = new RefundSwapResponse(
                    SwapId: refundedSwap.SwapId,
                    Success: true,
                    SourceContractStatus: sourceContract.Status,
                    TargetContractStatus: targetContract.Status,
                    RefundedAtUtc: refundedSwap.CompletedAtUtc ?? DateTime.UtcNow,
                    Message: "Expired swap aborted and refunded successfully."
                );

                return Results.Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "SWAP_NOT_FOUND", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "SWAP_REFUND_FAILED", message = ex.Message });
            }
        })
        .WithName("RefundCrossRailSwap")
        .WithSummary("Refunds funds to senders when an HTLC swap has expired without a valid claim.");

        // 4. POST /api/v1/finance/settlement-bridge/hedging/quote
        group.MapPost("/hedging/quote", (
            [FromBody] HedgingQuoteRequest request,
            [FromServices] IRealTimeFxHedgingOracle oracle) =>
        {
            try
            {
                var quote = oracle.GetHedgingQuote(
                    request.BaseCurrency,
                    request.QuoteCurrency,
                    request.NotionalAmount,
                    request.TenureDays);

                var slippage = oracle.EstimateSlippage(
                    request.BaseCurrency,
                    request.QuoteCurrency,
                    request.NotionalAmount);

                var response = new HedgingQuoteResponse(
                    Quote: quote,
                    Slippage: slippage,
                    IsValid: !slippage.ExceedsMaxLiquidity
                );

                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_HEDGING_REQUEST", message = ex.Message });
            }
        })
        .WithName("GetFxHedgingQuote")
        .WithSummary("Calculates dynamic Interest Rate Parity forward rates, spreads, and pool slippage.");

        // 5. GET /api/v1/finance/settlement-bridge/pools/liquidity
        group.MapGet("/pools/liquidity", (
            [FromServices] IRealTimeFxHedgingOracle oracle) =>
        {
            var pools = oracle.GetLiquidityPools();

            decimal totalEur = 0m;
            foreach (var pool in pools)
            {
                decimal eurRate = oracle.GetSpotRate(pool.Currency, CurrencyCode.EUR);
                totalEur += pool.AvailableBalance * eurRate;
            }

            var response = new LiquidityPoolsResponse(
                Pools: pools,
                TotalLiquidityEurEquivalent: Math.Round(totalEur, 2),
                GeneratedAtUtc: DateTime.UtcNow
            );

            return Results.Ok(response);
        })
        .WithName("GetLiquidityPoolBalances")
        .WithSummary("Retrieves aggregated multi-currency and stablecoin liquidity pool balances.");

        // 6. GET /api/v1/finance/settlement-bridge/rails/health
        group.MapGet("/rails/health", (
            [FromServices] IHerstattRiskEliminatorService herstattService) =>
        {
            var statuses = herstattService.GetAllRailHealthStatuses();
            return Results.Ok(statuses);
        })
        .WithName("GetRailHealthStatuses")
        .WithSummary("Returns real-time circuit breaker status and failure ratios for all payment rails.");

        // 7. POST /api/v1/finance/settlement-bridge/pvp/settle
        group.MapPost("/pvp/settle", async (
            [FromBody] PvpSettlementRequest request,
            [FromServices] IHerstattRiskEliminatorService herstattService,
            CancellationToken ct) =>
        {
            try
            {
                var result = await herstattService.ExecutePvpSettlementAsync(request, ct);
                return Results.Ok(result);
            }
            catch (RailDisabledException ex)
            {
                return Results.BadRequest(new { error = "RAIL_DISABLED", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_PVP_REQUEST", message = ex.Message });
            }
        })
        .WithName("ExecutePvpSettlement")
        .WithSummary("Executes Payment-versus-Payment atomic cross-rail settlement eliminating Herstatt risk.");

        return app;
    }
}
