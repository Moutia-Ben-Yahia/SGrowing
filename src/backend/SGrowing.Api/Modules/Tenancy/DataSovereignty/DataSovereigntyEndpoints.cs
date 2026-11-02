using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Tenancy.DataSovereignty;

public static class DataSovereigntyEndpoints
{
    public static IServiceCollection AddDataSovereigntyServices(this IServiceCollection services)
    {
        services.AddSingleton<GeodeticSovereigntyEnforcerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDataSovereigntyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenancy/data-sovereignty")
            .WithTags("Autonomous Multi-Tenant Data Sovereignty & Geodetic Boundary Enforcement");

        group.MapPost("/verify-residency", (
            DataResidencyVerificationRequest request,
            GeodeticSovereigntyEnforcerEngine engine) =>
        {
            var attestation = engine.VerifyResidency(request);
            return Results.Ok(attestation);
        });

        group.MapPost("/audit-transfer", (
            AuditTransferApiRequest request,
            GeodeticSovereigntyEnforcerEngine engine) =>
        {
            var result = engine.AuditCrossBorderTransfer(
                request.TenantId,
                request.Source,
                request.Target,
                request.Classification);

            return Results.Ok(result);
        });

        return app;
    }
}

public sealed record AuditTransferApiRequest(
    string TenantId,
    SovereignJurisdiction Source,
    SovereignJurisdiction Target,
    DataClassificationLevel Classification
);
