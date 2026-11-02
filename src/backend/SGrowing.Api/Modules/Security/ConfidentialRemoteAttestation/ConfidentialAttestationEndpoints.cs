namespace SGrowing.Api.Modules.Security.ConfidentialRemoteAttestation;

public static class ConfidentialAttestationEndpoints
{
    public static IServiceCollection AddConfidentialRemoteAttestationServices(this IServiceCollection services)
    {
        services.AddSingleton<ConfidentialRemoteAttestationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapConfidentialRemoteAttestationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/confidential-remote-attestation")
            .WithTags("Confidential Enclave Remote Attestation");

        group.MapPost("/verify-mesh", (EnclaveAttestationRequest request, ConfidentialRemoteAttestationEngine engine) =>
        {
            var result = engine.VerifyAttestationMesh(request);
            return Results.Ok(result);
        })
        .WithName("VerifyConfidentialEnclaves")
        .WithSummary("Validates hardware root of trust, MRENCLAVE binary measurements, and TCB version compliance for confidential computing enclaves");

        return app;
    }
}
