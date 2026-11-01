using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.Factoring;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for
/// Autonomous Reverse Factoring, Dynamic Discounting & Early Supplier Payment Matrix.
/// </summary>
public static class FactoringEndpoints
{
    /// <summary>
    /// Registers all factoring, dynamic discounting, and bandit services into the DI container as Singletons.
    /// </summary>
    public static IServiceCollection AddFactoringServices(this IServiceCollection services)
    {
        services.AddSingleton<IDynamicDiscountingEngine, DynamicDiscountingEngine>();
        services.AddSingleton<IReverseFactoringLiquidityPoolService, ReverseFactoringLiquidityPoolService>();
        services.AddSingleton<ISupplierDiscountAcceptanceBandit, SupplierDiscountAcceptanceBandit>();

        return services;
    }

    /// <summary>
    /// Maps factoring endpoints under '/api/v1/finance/factoring'.
    /// </summary>
    public static IEndpointRouteBuilder MapFactoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/factoring")
            .WithTags("Autonomous Reverse Factoring & Dynamic Discounting Matrix");

        // 1. POST /api/v1/finance/factoring/early-pay/calculate
        group.MapPost("/early-pay/calculate", (
            [FromBody] CalculateDiscountRequest request,
            [FromServices] IDynamicDiscountingEngine engine) =>
        {
            try
            {
                var offer = engine.CalculateEarlyPaymentOffer(
                    request.Invoice,
                    request.TargetPaymentDateUtc,
                    request.AnnualAprTarget ?? 0.24m,
                    request.ApplyDynamicCurve);

                return Results.Ok(offer);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_REQUEST", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "INVOICE_NOT_APPROVED", message = ex.Message });
            }
        })
        .WithName("CalculateEarlyPaymentOffer")
        .WithSummary("Calculates a sliding-scale APR yield early payment offer for an invoice.");

        // 2. POST /api/v1/finance/factoring/offers/generate
        group.MapPost("/offers/generate", (
            [FromBody] GenerateOffersRequest request,
            [FromServices] IDynamicDiscountingEngine engine) =>
        {
            try
            {
                var offers = engine.GenerateOffersForPayables(
                    request.Invoices,
                    request.AvailableLiquidityDzd,
                    request.TargetPaymentDateUtc,
                    request.AnnualAprTarget ?? 0.24m,
                    request.ApplyDynamicCurve);

                return Results.Ok(offers);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_REQUEST", message = ex.Message });
            }
        })
        .WithName("GenerateEarlyPaymentOffers")
        .WithSummary("Generates prioritized early payment offers constrained by buyer's working capital liquidity.");

        // 3. POST /api/v1/finance/factoring/offers/{offerId}/accept
        group.MapPost("/offers/{offerId}/accept", (
            [FromRoute] string offerId,
            [FromBody] AcceptOfferRequest request,
            [FromServices] IDynamicDiscountingEngine engine) =>
        {
            try
            {
                var acceptedOffer = engine.AcceptOffer(offerId, request.SupplierTenantId);
                return Results.Ok(acceptedOffer);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "OFFER_NOT_FOUND", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "OFFER_ACCEPTANCE_FAILED", message = ex.Message });
            }
        })
        .WithName("AcceptEarlyPaymentOffer")
        .WithSummary("Executes supplier acceptance of an early payment offer.");

        // 4. POST /api/v1/finance/factoring/consortium/assign
        group.MapPost("/consortium/assign", (
            [FromBody] AssignToBankRequest request,
            [FromServices] IReverseFactoringLiquidityPoolService poolService) =>
        {
            try
            {
                var record = poolService.AssignInvoiceToBank(
                    request.Invoice,
                    request.BankCode,
                    request.BankDiscountFeePct);

                return Results.Ok(record);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_ASSIGNMENT_REQUEST", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.UnprocessableEntity(new { error = "BANK_ASSIGNMENT_REJECTED", message = ex.Message });
            }
        })
        .WithName("AssignInvoiceToBankConsortium")
        .WithSummary("Assigns an approved invoice to a partner bank under non-recourse reverse factoring.");

        // 5. GET /api/v1/finance/factoring/consortium/liquidity
        group.MapGet("/consortium/liquidity", (
            [FromServices] IReverseFactoringLiquidityPoolService poolService) =>
        {
            var summary = poolService.GetConsortiumLiquiditySummary();
            return Results.Ok(summary);
        })
        .WithName("GetConsortiumLiquidity")
        .WithSummary("Returns real-time capacity and utilization across all consortium member banks.");

        // 6. POST /api/v1/finance/factoring/bandit/optimize
        group.MapPost("/bandit/optimize", (
            [FromBody] BanditOptimizeRequest request,
            [FromServices] ISupplierDiscountAcceptanceBandit bandit) =>
        {
            try
            {
                var response = bandit.GetOptimalDiscountRecommendation(request.SupplierId, request.Profile);
                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_OPTIMIZE_REQUEST", message = ex.Message });
            }
        })
        .WithName("OptimizeSupplierDiscountBandit")
        .WithSummary("Predicts optimal discount rate using contextual Thompson Sampling conditioned on cash urgency.");

        // 7. POST /api/v1/finance/factoring/bandit/feedback
        group.MapPost("/bandit/feedback", (
            [FromBody] BanditFeedbackRequest request,
            [FromServices] ISupplierDiscountAcceptanceBandit bandit) =>
        {
            try
            {
                if (request.Expired)
                {
                    bandit.RecordSupplierOutcome(request.SupplierId, request.OfferedDiscountRatePct, SupplierOfferOutcome.Expired);
                }
                else
                {
                    bandit.RecordSupplierDecision(request.SupplierId, request.OfferedDiscountRatePct, request.Accepted);
                }

                return Results.Ok(new
                {
                    status = "SUCCESS",
                    message = "Bayesian posterior updated successfully",
                    supplierId = request.SupplierId
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_FEEDBACK_REQUEST", message = ex.Message });
            }
        })
        .WithName("IngestBanditFeedback")
        .WithSummary("Ingests supplier offer acceptance/rejection/expiry to update Bayesian posterior parameters.");

        return app;
    }
}
