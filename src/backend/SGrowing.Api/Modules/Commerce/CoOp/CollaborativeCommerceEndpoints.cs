using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.CoOp;

/// <summary>
/// Minimal API endpoints and Dependency Injection setup for Horizon 54:
/// Autonomous Dynamic SKU Bundle Synthesizer &amp; Cross-Merchant Collaborative Commerce.
/// </summary>
public static class CollaborativeCommerceEndpoints
{
    /// <summary>
    /// Registers collaborative commerce bundle synthesis, split-cart fulfillment attribution,
    /// and private set intersection (PSI) services into DI container.
    /// </summary>
    public static IServiceCollection AddCollaborativeCommerceServices(this IServiceCollection services)
    {
        services.AddSingleton<ICrossMerchantBundleSynthesizerEngine, CrossMerchantBundleSynthesizerEngine>();
        services.AddSingleton<ISplitCartFulfillmentAttributionService, SplitCartFulfillmentAttributionService>();
        services.AddSingleton<IPrivateSetIntersectionMatcherService, PrivateSetIntersectionMatcherService>();

        return services;
    }

    /// <summary>
    /// Maps collaborative commerce route group under /api/v1/commerce/co-op.
    /// </summary>
    public static IEndpointRouteBuilder MapCollaborativeCommerceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/co-op")
            .WithTags("Autonomous Dynamic SKU Bundle Synthesizer & Cross-Merchant Collaborative Commerce");

        // 1. POST /api/v1/commerce/co-op/bundles/discover
        group.MapPost("/bundles/discover", (
            [FromBody] DiscoverBundlesRequest req,
            [FromServices] ICrossMerchantBundleSynthesizerEngine engine) =>
        {
            var bundles = engine.DiscoverCrossMerchantBundles(req);
            return Results.Ok(new
            {
                Success = true,
                TotalDiscovered = bundles.Count,
                Bundles = bundles
            });
        })
        .WithName("DiscoverCrossMerchantBundles")
        .WithSummary("Discovers high-affinity cross-merchant SKU bundles with margin-optimized dynamic discounts.");

        // 2. POST /api/v1/commerce/co-op/cart/split
        group.MapPost("/cart/split", (
            [FromBody] SplitCartRequest req,
            [FromServices] ISplitCartFulfillmentAttributionService service) =>
        {
            if (string.IsNullOrWhiteSpace(req.CustomerId))
            {
                return Results.BadRequest(new { Success = false, Message = "CustomerId is required." });
            }

            if (req.Items == null || req.Items.Count == 0)
            {
                return Results.BadRequest(new { Success = false, Message = "Cart must contain at least one item." });
            }

            try
            {
                var splitOrder = service.SplitCartAndAllocate(req);
                return Results.Ok(new
                {
                    Success = true,
                    Order = splitOrder
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Success = false, Message = ex.Message });
            }
        })
        .WithName("SplitMultiMerchantCart")
        .WithSummary("Unifies multi-merchant checkout into single transaction with split backend fulfillment and proportional shipping.");

        // 3. POST /api/v1/commerce/co-op/psi/exchange
        group.MapPost("/psi/exchange", (
            [FromBody] PsiExchangeApiRequest req,
            [FromServices] IPrivateSetIntersectionMatcherService psiService) =>
        {
            if (string.IsNullOrWhiteSpace(req.SessionId))
            {
                return Results.BadRequest(new { Success = false, Message = "SessionId is required." });
            }

            try
            {
                if (req.Step == 2)
                {
                    if (req.RawIdentifiers == null || req.RawIdentifiers.Count == 0)
                    {
                        return Results.BadRequest(new { Success = false, Message = "RawIdentifiers required for Step 2 exchange." });
                    }

                    var session = psiService.ExchangePartnerTokens(req.SessionId, req.MerchantId, req.RawIdentifiers);
                    return Results.Ok(new { Success = true, Session = session });
                }
                else if (req.Step == 3)
                {
                    var result = psiService.ComputeIntersection(req.SessionId);
                    return Results.Ok(new { Success = true, Result = result });
                }
                else
                {
                    return Results.BadRequest(new { Success = false, Message = "Invalid protocol Step. Valid values are 2 (Exchange) or 3 (Compute)." });
                }
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Success = false, Message = ex.Message });
            }
        })
        .WithName("ExchangePsiTokens")
        .WithSummary("Executes cryptographic Diffie-Hellman token exchange or finalizes intersection computation without PII exposure.");

        // 4. GET /api/v1/commerce/co-op/partnerships/{merchantId}
        group.MapGet("/partnerships/{merchantId}", (
            [FromRoute] string merchantId,
            [FromServices] IPrivateSetIntersectionMatcherService psiService,
            [FromServices] ICrossMerchantBundleSynthesizerEngine bundleEngine) =>
        {
            if (string.IsNullOrWhiteSpace(merchantId))
            {
                return Results.BadRequest(new { Success = false, Message = "MerchantId is required." });
            }

            var bundles = bundleEngine.DiscoverCrossMerchantBundles(new DiscoverBundlesRequest(
                MerchantFilter: [merchantId],
                MinSupport: 0.001,
                MinLift: 1.5,
                MaxBundles: 100
            ));

            var partnerIds = bundles
                .SelectMany(b => b.MerchantIds)
                .Where(m => !string.Equals(m, merchantId, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var summaries = new List<MerchantPartnershipSummary>();
            foreach (var partnerId in partnerIds)
            {
                var merchantBundles = bundles
                    .Where(b => b.MerchantIds.Contains(partnerId, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                decimal potentialGrossRevenue = merchantBundles.Sum(b => b.CombinedPriceDzd);

                var summary = psiService.EvaluatePartnershipPotential(
                    merchantId,
                    partnerId,
                    discoveredBundleCount: merchantBundles.Count,
                    potentialGrossRevenueDzd: potentialGrossRevenue);

                summaries.Add(summary);
            }

            return Results.Ok(new
            {
                Success = true,
                MerchantId = merchantId,
                TotalPartnerships = summaries.Count,
                Partnerships = summaries
            });
        })
        .WithName("GetMerchantPartnerships")
        .WithSummary("Retrieves collaborative merchant partnership summaries combining mutual customer overlap and co-op bundles.");

        // Additional supporting endpoints
        group.MapPost("/psi/initiate", (
            [FromBody] PsiInitiateRequest req,
            [FromServices] IPrivateSetIntersectionMatcherService psiService) =>
        {
            if (string.IsNullOrWhiteSpace(req.InitiatorMerchantId) || string.IsNullOrWhiteSpace(req.PartnerMerchantId))
            {
                return Results.BadRequest(new { Success = false, Message = "InitiatorMerchantId and PartnerMerchantId are required." });
            }

            var session = psiService.InitiateSession(req.InitiatorMerchantId, req.PartnerMerchantId, req.RawIdentifiers ?? []);
            return Results.Ok(new { Success = true, Session = session });
        })
        .WithName("InitiatePsiSession")
        .WithSummary("Initiates a privacy-preserving customer intersection discovery session.");

        group.MapPost("/cart/refund", (
            [FromBody] PartialRefundRequest req,
            [FromServices] ISplitCartFulfillmentAttributionService attributionService) =>
        {
            try
            {
                var result = attributionService.ProcessPartialRefund(req);
                return Results.Ok(new { Success = true, Refund = result });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Success = false, Message = ex.Message });
            }
        })
        .WithName("ProcessPartialCartRefund")
        .WithSummary("Processes partial line-item cancellation with prorated shipping and fee clawback.");

        group.MapGet("/cart/{orderId}", (
            [FromRoute] string orderId,
            [FromServices] ISplitCartFulfillmentAttributionService attributionService) =>
        {
            var order = attributionService.GetOrder(orderId);
            return order != null
                ? Results.Ok(order)
                : Results.NotFound(new { Success = false, Message = $"Order '{orderId}' not found." });
        })
        .WithName("GetSplitCartOrder")
        .WithSummary("Retrieves detailed split order status and merchant attribution breakdown.");

        return app;
    }
}

/// <summary>
/// DTO for PSI exchange API requests.
/// </summary>
public record PsiExchangeApiRequest(
    string SessionId,
    string MerchantId,
    IReadOnlyList<string>? RawIdentifiers,
    int Step
);
