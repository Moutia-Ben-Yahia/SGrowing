using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.FraudFirewall;

public static class FraudFirewallEndpoints
{
    public static IServiceCollection AddFraudFirewallServices(this IServiceCollection services)
    {
        services.AddSingleton<IFraudFirewallEngine, FraudFirewallEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapFraudFirewallEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/fraud")
            .WithTags("Transaction Fraud Firewall");

        group.MapPost("/evaluate", (EvaluateTransactionRequest request, IFraudFirewallEngine engine) =>
        {
            var evaluation = engine.EvaluateTransaction(request);
            return Results.Ok(evaluation);
        });

        group.MapGet("/stats", (IFraudFirewallEngine engine) =>
        {
            var stats = engine.GetStats();
            return Results.Ok(stats);
        });

        group.MapGet("/recent", (IFraudFirewallEngine engine) =>
        {
            var recent = engine.ListRecentEvaluations();
            return Results.Ok(recent);
        });

        group.MapGet("/rules", (IFraudFirewallEngine engine) =>
        {
            var rules = engine.ListRules();
            return Results.Ok(rules);
        });

        group.MapPost("/rules/{ruleId}/toggle", (string ruleId, bool isActive, IFraudFirewallEngine engine) =>
        {
            var success = engine.ToggleRule(ruleId, isActive);
            return success ? Results.Ok(new { ruleId, isActive }) : Results.NotFound();
        });

        return app;
    }
}
