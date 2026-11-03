namespace SGrowing.Api.Modules.Infrastructure.SpiffeSpire;

public static class SpiffeSpireEndpoints
{
    public static IServiceCollection AddSpiffeSpireServices(this IServiceCollection services)
    {
        services.AddSingleton<SpiffeSpireMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSpiffeSpireEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/spiffe-spire")
            .WithTags("Infrastructure - SPIFFE/SPIRE Workload Attestation & Identity Federation");

        group.MapGet("/overview", (SpiffeSpireMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/attest-workload", (SpiffeSpireMeshEngine engine, AttestWorkloadRequest request) =>
        {
            var svid = engine.AttestWorkload(request);
            return Results.Created($"/api/v1/infrastructure/spiffe-spire/svids/{svid.SvidId}", svid);
        });

        group.MapPost("/federate-domain", (SpiffeSpireMeshEngine engine, FederateDomainRequest request) =>
        {
            var record = engine.FederateDomain(request);
            return Results.Created($"/api/v1/infrastructure/spiffe-spire/federations/{record.FederationId}", record);
        });

        return app;
    }
}
