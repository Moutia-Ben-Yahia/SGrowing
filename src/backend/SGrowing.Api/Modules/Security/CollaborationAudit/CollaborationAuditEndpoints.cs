using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.CollaborationAudit;

public static class CollaborationAuditEndpoints
{
    public static IServiceCollection AddCollaborationAuditServices(this IServiceCollection services)
    {
        services.AddSingleton<ICollaborationAuditEngine, CollaborationAuditEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCollaborationAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/collaboration")
            .WithTags("Real-Time Collaboration & Security Audit");

        group.MapPost("/heartbeat", (CollaborationHeartbeatRequest request, ICollaborationAuditEngine engine) =>
        {
            var session = engine.SendHeartbeat(request);
            return Results.Ok(session);
        });

        group.MapGet("/active/{resourceId}", (string resourceId, ICollaborationAuditEngine engine) =>
        {
            var active = engine.ListActiveCollaborators(resourceId);
            return Results.Ok(active);
        });

        group.MapGet("/audit-logs", (ICollaborationAuditEngine engine) =>
        {
            var logs = engine.ListAuditLogs();
            return Results.Ok(logs);
        });

        group.MapPost("/revoke-sessions", (RevokeUserSessionsRequest request, ICollaborationAuditEngine engine) =>
        {
            var revoked = engine.RevokeUserSessions(request);
            return Results.Ok(new { success = revoked, userId = request.UserId });
        });

        group.MapGet("/snapshot", (ICollaborationAuditEngine engine) =>
        {
            var snapshot = engine.GetSnapshot();
            return Results.Ok(snapshot);
        });

        return app;
    }
}
