using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.CreditRiskUnderwriting;

public static class CreditRiskEndpoints
{
    public static IServiceCollection AddCreditRiskUnderwritingServices(this IServiceCollection services)
    {
        services.AddSingleton<AlgorithmicCreditRiskEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCreditRiskUnderwritingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/credit-risk-underwriting")
            .WithTags("Autonomous Algorithmic Credit Risk Underwriting & Dynamic Invoice Factoring Discount Engine");

        group.MapPost("/underwrite-invoice", (
            InvoiceFactoringApplication appReq,
            AlgorithmicCreditRiskEngine engine) =>
        {
            var assessment = engine.UnderwriteInvoice(appReq);
            return Results.Ok(assessment);
        });

        return app;
    }
}
