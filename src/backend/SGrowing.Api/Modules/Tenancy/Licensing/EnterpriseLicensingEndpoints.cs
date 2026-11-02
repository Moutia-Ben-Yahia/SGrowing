namespace SGrowing.Api.Modules.Tenancy.Licensing;

public static class EnterpriseLicensingEndpoints
{
    public static IServiceCollection AddEnterpriseLicensingServices(this IServiceCollection services)
    {
        services.AddSingleton<IEnterpriseLicensingEngine, EnterpriseLicensingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEnterpriseLicensingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenancy/licensing")
            .WithTags("Self-Hosted Enterprise Licensing");

        group.MapGet("/status", (IEnterpriseLicensingEngine engine) =>
        {
            var status = engine.GetLicenseStatus();
            return Results.Ok(status);
        })
        .WithName("GetLicenseStatus")
        .WithSummary("Query the current self-hosted enterprise license state and expiration grace period");

        group.MapPost("/activate", (IEnterpriseLicensingEngine engine, ActivateLicenseRequest request) =>
        {
            var result = engine.ActivateLicense(request);
            return result.IsValid ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ActivateEnterpriseLicense")
        .WithSummary("Activate a new enterprise license key with offline cryptographic entitlement validation");

        group.MapPost("/verify-airgap", (IEnterpriseLicensingEngine engine, AirGapVerificationRequest request) =>
        {
            var result = engine.VerifyAirGappedPayload(request);
            return result.IsValid ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("VerifyAirGappedLicense")
        .WithSummary("Perform hardware node fingerprint and offline signature verification for air-gapped clusters");

        return app;
    }
}
