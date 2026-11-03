using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Trade.RulesOfOriginTariff;

public static class RulesOfOriginEndpoints
{
    public static IServiceCollection AddRulesOfOriginTariffServices(this IServiceCollection services)
    {
        services.AddSingleton<IRulesOfOriginTariffEngine, RulesOfOriginTariffEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRulesOfOriginTariffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/trade/tariff")
            .WithTags("Trade - Rules of Origin & Anti-Dumping Tariff Engine");

        group.MapPost("/evaluate-origin", (
            RulesOfOriginEvaluationRequest request,
            IRulesOfOriginTariffEngine engine) =>
        {
            var result = engine.EvaluateOriginAndTariffs(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateRulesOfOriginTariff")
        .WithSummary("Evaluates Free Trade Agreement Regional Value Content (RVC), tariff shifts, and anti-dumping circumvention risks")
        .Produces<TariffAssessmentResult>(StatusCodes.Status200OK);

        return endpoints;
    }
}
