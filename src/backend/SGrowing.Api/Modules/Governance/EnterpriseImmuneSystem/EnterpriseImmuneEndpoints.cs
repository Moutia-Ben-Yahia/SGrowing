namespace SGrowing.Api.Modules.Governance.EnterpriseImmuneSystem;

public static class EnterpriseImmuneEndpoints
{
    public static IServiceCollection AddEnterpriseImmuneServices(this IServiceCollection services)
    {
        services.AddSingleton<EnterpriseImmuneSystemEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEnterpriseImmuneEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/governance/enterprise-immune-system")
            .WithTags("Enterprise Immune System");

        group.MapPost("/diagnose-and-heal", (EnterpriseImmuneDiagnosisRequest request, EnterpriseImmuneSystemEngine engine) =>
        {
            var result = engine.DiagnoseAndHeal(request);
            return Results.Ok(result);
        })
        .WithName("DiagnoseAndHealEnterprise")
        .WithSummary("Ingests systemic telemetry signals, achieves Byzantine consensus, and executes autonomous self-healing mitigations");

        return app;
    }
}
