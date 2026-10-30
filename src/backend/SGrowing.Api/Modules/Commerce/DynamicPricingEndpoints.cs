using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Commerce;

#region Request Payloads

public record SelectArmApiRequest(string Sku);

public record RecordImpressionApiRequest(string Sku, string ArmId);

public record RecordConversionApiRequest(string Sku, string ArmId, decimal? Revenue = null);

public record MarkdownScheduleApiRequest(string? Sku = null);

public record CalculateElasticityApiRequest(
    string Sku,
    decimal Price0,
    decimal Price1,
    decimal Quantity0,
    decimal Quantity1);

public record EvaluateGuardrailApiRequest(string Sku, decimal CandidatePrice);

public record ResetPriorApiRequest(string Sku, double? AlphaPrior = null, double? BetaPrior = null);

public record UpdateInventoryTelemetryApiRequest(string Sku, int CurrentStock, decimal DailyRunRate);

#endregion

public static class DynamicPricingEndpoints
{
    public static IEndpointRouteBuilder MapDynamicPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/pricing/bandit")
            .WithTags("Algorithmic Dynamic Pricing & Multi-Armed Bandit");

        // 1. GET / - List all pricing experiments
        group.MapGet("/", async (
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IDynamicPricingBanditService)) as IDynamicPricingBanditService
                          ?? DynamicPricingBanditService.Instance;

            var experiments = await service.GetExperimentsAsync(ct);
            return Results.Ok(experiments);
        })
        .WithName("ListDynamicPricingExperiments")
        .WithSummary("List all active multi-armed bandit pricing experiments across SKUs.");

        // 2. GET /{sku} - Get experiment by SKU
        group.MapGet("/{sku}", async (
            [FromRoute] string sku,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IDynamicPricingBanditService)) as IDynamicPricingBanditService
                          ?? DynamicPricingBanditService.Instance;

            var experiment = await service.GetExperimentBySkuAsync(sku, ct);
            return experiment is not null
                ? Results.Ok(experiment)
                : Results.NotFound(new { error = $"Experiment for SKU '{sku}' not found." });
        })
        .WithName("GetDynamicPricingExperimentBySku")
        .WithSummary("Retrieve multi-armed bandit state and arm distributions for a specific SKU.");

        // 3. POST /select-arm - Run Thompson sampling for session
        group.MapPost("/select-arm", async (
            [FromBody] SelectArmApiRequest req,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Sku))
                return Results.BadRequest(new { error = "Sku is required." });

            var service = sp.GetService(typeof(IDynamicPricingBanditService)) as IDynamicPricingBanditService
                          ?? DynamicPricingBanditService.Instance;

            try
            {
                var selection = await service.SelectArmAsync(req.Sku, ct);
                return Results.Ok(selection);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("SelectPricingArm")
        .WithSummary("Execute Thompson sampling across arms and clamp against hard margin floor guardrail.");

        // 4. POST /record-impression - Record an impression on an arm
        group.MapPost("/record-impression", async (
            [FromBody] RecordImpressionApiRequest req,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Sku) || string.IsNullOrWhiteSpace(req.ArmId))
                return Results.BadRequest(new { error = "Both Sku and ArmId are required." });

            var service = sp.GetService(typeof(IDynamicPricingBanditService)) as IDynamicPricingBanditService
                          ?? DynamicPricingBanditService.Instance;

            try
            {
                var updated = await service.RecordImpressionAsync(req.Sku, req.ArmId, ct);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("RecordPricingImpression")
        .WithSummary("Record an impression, incrementing beta parameter prior.");

        // 5. POST /record-conversion - Record a conversion on an arm
        group.MapPost("/record-conversion", async (
            [FromBody] RecordConversionApiRequest req,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Sku) || string.IsNullOrWhiteSpace(req.ArmId))
                return Results.BadRequest(new { error = "Both Sku and ArmId are required." });

            var service = sp.GetService(typeof(IDynamicPricingBanditService)) as IDynamicPricingBanditService
                          ?? DynamicPricingBanditService.Instance;

            try
            {
                var updated = await service.RecordConversionAsync(req.Sku, req.ArmId, req.Revenue, ct);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("RecordPricingConversion")
        .WithSummary("Record a purchase conversion, incrementing alpha parameter and adjusting beta.");

        // 6. POST /markdown-schedule - Compute inventory liquidation markdowns
        group.MapPost("/markdown-schedule", async (
            [FromBody] MarkdownScheduleApiRequest? req,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IDynamicPricingBanditService)) as IDynamicPricingBanditService
                          ?? DynamicPricingBanditService.Instance;

            if (!string.IsNullOrWhiteSpace(req?.Sku))
            {
                try
                {
                    var single = await service.GetLiquidationScheduleForSkuAsync(req.Sku, ct);
                    return Results.Ok(new List<InventoryLiquidationSchedule> { single });
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
            }

            var schedule = await service.GetLiquidationScheduleAsync(ct);
            return Results.Ok(schedule);
        })
        .WithName("GetLiquidationMarkdownSchedule")
        .WithSummary("Compute inventory liquidation markdown recommendations guarded by hard margin floor.");

        // 7. POST /elasticity - Calculate arc price elasticity of demand
        group.MapPost("/elasticity", async (
            [FromBody] CalculateElasticityApiRequest req,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IDynamicPricingBanditService)) as IDynamicPricingBanditService
                          ?? DynamicPricingBanditService.Instance;

            var report = await service.CalculateElasticityAsync(
                req.Sku,
                req.Price0,
                req.Price1,
                req.Quantity0,
                req.Quantity1,
                ct);

            return Results.Ok(report);
        })
        .WithName("CalculatePriceElasticity")
        .WithSummary("Compute arc elasticity coefficient, classification (Inelastic/Unitary/Elastic), and tactical pricing strategy.");

        // 8. POST /evaluate-guardrail - Test margin floor guardrail clamping
        group.MapPost("/evaluate-guardrail", async (
            [FromBody] EvaluateGuardrailApiRequest req,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IDynamicPricingBanditService)) as IDynamicPricingBanditService
                          ?? DynamicPricingBanditService.Instance;

            try
            {
                var check = await service.EvaluatePriceGuardrailAsync(req.Sku, req.CandidatePrice, ct);
                return Results.Ok(check);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("EvaluateMarginFloorGuardrail")
        .WithSummary("Check candidate price against COGS, Hurdle rate, and shipping floor.");

        // 9. POST /reset-prior - Reset experiment priors for interactive testing
        group.MapPost("/reset-prior", async (
            [FromBody] ResetPriorApiRequest req,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IDynamicPricingBanditService)) as IDynamicPricingBanditService
                          ?? DynamicPricingBanditService.Instance;

            try
            {
                var updated = await service.ResetSkuPriorAsync(req.Sku, req.AlphaPrior, req.BetaPrior, ct);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ResetPricingExperimentPrior")
        .WithSummary("Reset Beta distributions to baseline prior (e.g. Beta(1,1)).");

        // 10. POST /update-telemetry - Update stock and daily run rate
        group.MapPost("/update-telemetry", async (
            [FromBody] UpdateInventoryTelemetryApiRequest req,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IDynamicPricingBanditService)) as IDynamicPricingBanditService
                          ?? DynamicPricingBanditService.Instance;

            try
            {
                var updated = await service.UpdateInventoryTelemetryAsync(req.Sku, req.CurrentStock, req.DailyRunRate, ct);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("UpdateInventoryTelemetry")
        .WithSummary("Update stock count and velocity to dynamically re-evaluate liquidation schedule.");

        return group;
    }
}
