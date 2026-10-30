using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Funnels;

public static class FunnelSplitTestingEndpoints
{
    public static IEndpointRouteBuilder MapFunnelSplitTestingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/funnels/split-testing")
            .WithTags("Funnel Continuous Multi-Armed Split Testing Bandit (Horizon 38)");

        // GET /api/v1/funnels/split-testing -> List all funnels with split-testing status
        group.MapGet("/", (IFunnelSplitTestingBanditService service) =>
        {
            var experiments = service.GetAllExperiments();
            return Results.Ok(experiments);
        })
        .WithName("ListFunnelSplitTestingExperiments")
        .WithSummary("List all active funnels with multi-armed bandit split-testing metrics.");

        // GET /api/v1/funnels/split-testing/{funnelId} -> Get experiment details and variants
        group.MapGet("/{funnelId}", (string funnelId, IFunnelSplitTestingBanditService service) =>
        {
            var experiment = service.GetExperiment(funnelId);
            if (experiment == null)
            {
                return Results.NotFound(new { error = $"Funnel experiment '{funnelId}' was not found." });
            }
            return Results.Ok(experiment);
        })
        .WithName("GetFunnelSplitTestingExperiment")
        .WithSummary("Get full Dirichlet-Multinomial Bayesian state and variant details for a funnel.");

        // POST /api/v1/funnels/split-testing/route-visitor -> Route visitor to optimal variant via Thompson sampling
        group.MapPost("/route-visitor", ([FromBody] RouteVisitorRequest req, IFunnelSplitTestingBanditService service) =>
        {
            try
            {
                var routingDecision = service.RouteVisitor(req.FunnelId, req.VisitorId);
                return Results.Ok(routingDecision);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RouteVisitorToVariant")
        .WithSummary("Route an incoming visitor to an optimal variant using Thompson sampling with an exploration floor.");

        // POST /api/v1/funnels/split-testing/record-outcome -> Record funnel step outcome
        group.MapPost("/record-outcome", ([FromBody] RecordOutcomeRequest req, IFunnelSplitTestingBanditService service) =>
        {
            try
            {
                var result = service.RecordOutcome(req.FunnelId, req.VariantId, req.Outcome, req.CustomValue, req.VisitorId);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RecordFunnelStepOutcome")
        .WithSummary("Atomically update Dirichlet parameter alpha_{v,j} for a variant outcome.");

        // POST /api/v1/funnels/split-testing/simulate-superiority -> Run Monte Carlo superiority calculation
        group.MapPost("/simulate-superiority", ([FromBody] SimulateSuperiorityRequest req, IFunnelSplitTestingBanditService service) =>
        {
            try
            {
                var simulation = service.RunSuperioritySimulation(req.FunnelId, req.Iterations, req.MinVisitorThreshold);
                return Results.Ok(simulation);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SimulateSuperiority")
        .WithSummary("Run Monte Carlo superiority simulation (500+ draws) to calculate win probabilities and check auto-cannibalization.");

        // POST /api/v1/funnels/split-testing/{funnelId}/reset -> Reset experiment state to default
        group.MapPost("/{funnelId}/reset", (string funnelId, IFunnelSplitTestingBanditService service) =>
        {
            try
            {
                service.ResetExperiment(funnelId);
                var resetExperiment = service.GetExperiment(funnelId);
                return Results.Ok(new { message = $"Funnel '{funnelId}' successfully reset.", experiment = resetExperiment });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ResetFunnelSplitTest")
        .WithSummary("Reset variant alphas and visitor counters back to baseline seed priors.");

        return app;
    }
}
