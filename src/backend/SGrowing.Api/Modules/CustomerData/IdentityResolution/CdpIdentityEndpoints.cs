using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.CustomerData.IdentityResolution;

public static class CdpIdentityEndpoints
{
    public static IServiceCollection AddCdpIdentityServices(this IServiceCollection services)
    {
        services.AddSingleton<ICdpIdentityEngine, CdpIdentityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCdpIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/customer-data").WithTags("Customer Data Platform Identity");

        group.MapPost("/identities/stitch", (StitchIdentityRequest request, ICdpIdentityEngine engine) =>
        {
            var result = engine.StitchIdentity(request);
            return Results.Ok(result);
        }).WithName("StitchCustomerIdentity");

        group.MapGet("/profiles/{tenantId}/{profileId}/graph", (string tenantId, string profileId, ICdpIdentityEngine engine) =>
        {
            var profile = engine.GetProfileGraph(tenantId, profileId);
            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        }).WithName("GetCustomerIdentityGraph");

        group.MapPost("/identities/resolve", (IdentityResolutionQuery query, ICdpIdentityEngine engine) =>
        {
            var profile = engine.ResolveIdentifier(query.TenantId, query.Identifier);
            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        }).WithName("ResolveCustomerIdentity");

        group.MapPost("/sessions/ingest/{tenantId}", (string tenantId, VisitorSessionRecord session, ICdpIdentityEngine engine) =>
        {
            var success = engine.IngestSession(tenantId, session);
            return Results.Ok(new { success, sessionId = session.SessionId });
        }).WithName("IngestVisitorSession");

        return endpoints;
    }
}
