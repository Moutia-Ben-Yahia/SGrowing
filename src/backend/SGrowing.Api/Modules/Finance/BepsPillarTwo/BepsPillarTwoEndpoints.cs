namespace SGrowing.Api.Modules.Finance.BepsPillarTwo;

public static class BepsPillarTwoEndpoints
{
    public static IServiceCollection AddBepsPillarTwoServices(this IServiceCollection services)
    {
        services.AddSingleton<BepsPillarTwoTaxCalculatorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBepsPillarTwoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/beps-pillar-two")
            .WithTags("BEPS Pillar Two");

        group.MapPost("/calculate-top-up-tax", (BepsPillarTwoCalculationRequest request, BepsPillarTwoTaxCalculatorEngine engine) =>
        {
            var result = engine.CalculateTopUpTax(request);
            return Results.Ok(result);
        })
        .WithName("CalculateBepsPillarTwoTopUpTax")
        .WithSummary("Calculates OECD BEPS Pillar Two GloBE Effective Tax Rates, Substance-Based Income Exclusions, and Top-Up Tax liabilities");

        return app;
    }
}
