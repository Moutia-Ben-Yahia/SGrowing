using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Modules.Commerce.AntiFraud;

namespace SGrowing.Api.Modules.Commerce;

public static class CommercePillarEndpoints
{
    public static IEndpointRouteBuilder MapCommercePillarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce")
            .WithTags("COD Anti-Fraud");

        group.MapPost("/cod/evaluate-fraud", async (Guid tenantId, CodFraudEvaluationRequest req, ICodAntiFraudEngine antiFraud, CancellationToken ct) =>
        {
            var res = await antiFraud.EvaluateOrderRiskAsync(tenantId, req, ct);
            return Results.Ok(res);
        })
        .WithName("EvaluateCodOrderRisk")
        .WithSummary("Multi-factor COD risk scoring and fake order detection engine");

        return app;
    }
}
