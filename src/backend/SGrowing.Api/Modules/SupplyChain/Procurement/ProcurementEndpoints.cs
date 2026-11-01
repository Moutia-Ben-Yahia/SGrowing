using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.SupplyChain.Procurement;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 26:
/// Autonomous AI Procurement Negotiation &amp; Supplier Bidding Matrix Engine.
/// </summary>
public static class ProcurementEndpoints
{
    /// <summary>
    /// Registers all Phase 26 Autonomous AI Procurement Negotiation and Bidding Matrix services.
    /// </summary>
    public static IServiceCollection AddProcurementBiddingServices(this IServiceCollection services)
    {
        services.AddSingleton<ISupplierBiddingMatrixService, SupplierBiddingMatrixService>();
        services.AddSingleton<IAutonomousProcurementAgent, AutonomousProcurementAgent>();
        services.AddSingleton<IProcurementAwardSealingService, ProcurementAwardSealingService>();

        return services;
    }

    /// <summary>
    /// Maps the Phase 26 Procurement Minimal API endpoints under /api/v1/supply-chain/procurement.
    /// </summary>
    public static IEndpointRouteBuilder MapProcurementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/supply-chain/procurement")
            .WithTags("Procurement Negotiation & Supplier Bidding Matrix");

        // 1. POST /api/v1/supply-chain/procurement/auctions
        group.MapPost("/auctions", (
            [FromBody] CreateAuctionRequest request,
            [FromServices] ISupplierBiddingMatrixService matrixService) =>
        {
            try
            {
                var auction = matrixService.CreateAuction(request);
                return Results.Created($"/api/v1/supply-chain/procurement/auctions/{auction.AuctionId}",
                    new CreateAuctionResponse(true, "Auction created successfully.", auction));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new CreateAuctionResponse(false, ex.Message));
            }
        })
        .WithName("CreateProcurementAuction")
        .WithSummary("Creates a new reverse bidding auction for a target SKU and quantity.");

        // 2. GET /api/v1/supply-chain/procurement/auctions
        group.MapGet("/auctions", (
            [FromServices] ISupplierBiddingMatrixService matrixService) =>
        {
            var auctions = matrixService.GetAllAuctions();
            return Results.Ok(auctions);
        })
        .WithName("GetAllProcurementAuctions")
        .WithSummary("Returns all active and past procurement auctions.");

        // 3. GET /api/v1/supply-chain/procurement/auctions/{id:guid}
        group.MapGet("/auctions/{id:guid}", (
            [FromRoute] Guid id,
            [FromServices] ISupplierBiddingMatrixService matrixService) =>
        {
            var auction = matrixService.GetAuction(id);
            if (auction is null)
            {
                return Results.NotFound(new { Message = $"Auction '{id}' was not found." });
            }

            var rankedBids = matrixService.EvaluateAndRankBids(id);
            return Results.Ok(new
            {
                Auction = auction,
                RankedBids = rankedBids
            });
        })
        .WithName("GetProcurementAuctionById")
        .WithSummary("Retrieves an auction by ID along with its multi-criteria ranked bids.");

        // 4. POST /api/v1/supply-chain/procurement/auctions/{id:guid}/bids
        group.MapPost("/auctions/{id:guid}/bids", (
            [FromRoute] Guid id,
            [FromBody] SubmitBidRequest request,
            [FromServices] ISupplierBiddingMatrixService matrixService) =>
        {
            try
            {
                var bid = matrixService.SubmitBid(id, request);
                return Results.Ok(new SubmitBidResponse(true, "Bid submitted successfully.", bid));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new SubmitBidResponse(false, ex.Message));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new SubmitBidResponse(false, ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new SubmitBidResponse(false, ex.Message));
            }
        })
        .WithName("SubmitSupplierBid")
        .WithSummary("Submits a supplier bid (price, lead time, SLA, quantity) into an active reverse auction.");

        // 5. POST /api/v1/supply-chain/procurement/auctions/{id:guid}/negotiate
        group.MapPost("/auctions/{id:guid}/negotiate", (
            [FromRoute] Guid id,
            [FromBody] NegotiationStepRequest request,
            [FromServices] IAutonomousProcurementAgent agent) =>
        {
            try
            {
                var response = agent.ExecuteNegotiationStep(id, request);
                return Results.Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
        })
        .WithName("ExecuteNegotiationStep")
        .WithSummary("Executes an autonomous bargaining step using a parameterized concession curve.");

        // 6. POST /api/v1/supply-chain/procurement/auctions/{id:guid}/dual-source
        group.MapPost("/auctions/{id:guid}/dual-source", (
            [FromRoute] Guid id,
            [FromBody] DualSourcingRequest? request,
            [FromServices] IAutonomousProcurementAgent agent) =>
        {
            try
            {
                var recommendation = agent.EvaluateDualSourcing(id, request);
                return Results.Ok(new DualSourcingResponse(true, "Dual-sourcing split evaluated successfully.", recommendation));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new DualSourcingResponse(false, ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new DualSourcingResponse(false, ex.Message));
            }
        })
        .WithName("EvaluateDualSourcing")
        .WithSummary("Analyzes capacity constraints and risk hedging to recommend an optimal volume split across top 2 suppliers.");

        // 7. POST /api/v1/supply-chain/procurement/auctions/{id:guid}/award
        group.MapPost("/auctions/{id:guid}/award", (
            [FromRoute] Guid id,
            [FromBody] AwardContractRequest request,
            [FromServices] IProcurementAwardSealingService sealingService) =>
        {
            try
            {
                var contract = sealingService.SealAwardContract(
                    id,
                    request.WinningBidId,
                    request.AwardedQuantity,
                    request.SealingKey,
                    request.ApproverId);

                return Results.Ok(new AwardContractResponse(true, "Contract cryptographically sealed and awarded.", contract));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new AwardContractResponse(false, ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new AwardContractResponse(false, ex.Message));
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new AwardContractResponse(false, ex.Message));
            }
        })
        .WithName("SealContractAward")
        .WithSummary("Enforces budget authority thresholds (<= $10,000 autonomous, > $10,000 HITL) and seals contract with HMAC-SHA256 signature.");

        // 8. GET /api/v1/supply-chain/procurement/contracts/{contractId:guid}
        group.MapGet("/contracts/{contractId:guid}", (
            [FromRoute] Guid contractId,
            [FromServices] IProcurementAwardSealingService sealingService) =>
        {
            var contract = sealingService.GetContract(contractId);
            if (contract is null)
            {
                return Results.NotFound(new { Message = $"Contract '{contractId}' was not found." });
            }

            var isSignatureValid = sealingService.VerifyContractSignature(contract);
            return Results.Ok(new
            {
                Contract = contract,
                IsSignatureValid = isSignatureValid
            });
        })
        .WithName("GetContractByIdAndVerify")
        .WithSummary("Retrieves a sealed award contract and verifies its HMAC-SHA256 signature.");

        // 9. GET /api/v1/supply-chain/procurement/health
        group.MapGet("/health", () =>
        {
            var health = new ProcurementEngineHealth(
                Status: "Operational",
                Version: "1.0.0-phase26",
                TimestampUtc: DateTime.UtcNow,
                Capabilities: new Dictionary<string, string>
                {
                    ["BiddingMatrix"] = "Multi-criteria Utility (Cost w=0.50, SLA w=0.30, LeadTime w=0.20)",
                    ["Out-of-Spec Filtering"] = "Price > Reserve and Delivery > Deadline exclusion",
                    ["NegotiationCurve"] = "P(t) = P_target + (P_reserve - P_target) * (t/T)^(1/beta)",
                    ["DualSourcing"] = "Capacity-constrained and 60/40 strategic risk hedging split",
                    ["AwardSealing"] = "Budget cap threshold (<= $10k auto, > $10k HITL) with HMAC-SHA256 digest"
                });

            return Results.Ok(health);
        })
        .WithName("GetProcurementHealth")
        .WithSummary("Returns health check and capability metadata for Phase 26 Autonomous AI Procurement Engine.");

        return app;
    }
}
