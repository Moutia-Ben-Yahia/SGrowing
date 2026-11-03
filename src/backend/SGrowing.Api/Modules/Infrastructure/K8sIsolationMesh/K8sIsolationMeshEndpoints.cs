using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.K8sIsolationMesh;

public static class K8sIsolationMeshEndpoints
{
    public static IServiceCollection AddK8sIsolationMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<IK8sIsolationMeshEngine, K8sIsolationMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapK8sIsolationMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/k8s-isolation-mesh")
            .WithTags("Infrastructure - Multi-Cloud Kubernetes Namespace & Secret Isolation Mesh");

        group.MapGet("/overview", (IK8sIsolationMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetK8sIsolationMeshOverview")
        .WithSummary("Retrieves multi-cloud tenant namespaces, eBPF quarantine statuses, and resource quotas");

        group.MapPost("/provision", (ProvisionNamespaceCommand command, IK8sIsolationMeshEngine engine) =>
        {
            var result = engine.ProvisionNamespace(command);
            return Results.Ok(result);
        })
        .WithName("ProvisionK8sTenantNamespace")
        .WithSummary("Provisions a zero-trust multi-cloud Kubernetes namespace with CPU/Memory quotas");

        group.MapPost("/quarantine", (QuarantineNamespaceCommand command, IK8sIsolationMeshEngine engine) =>
        {
            try
            {
                var result = engine.QuarantineNamespace(command);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Namespace {command.NamespaceId} not found." });
            }
        })
        .WithName("QuarantineK8sTenantNamespace")
        .WithSummary("Enforces eBPF network quarantine to block egress/ingress on compromised tenant pods");

        group.MapPost("/{namespaceId}/restore", (string namespaceId, IK8sIsolationMeshEngine engine) =>
        {
            try
            {
                var result = engine.RestoreNamespace(namespaceId);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Namespace {namespaceId} not found." });
            }
        })
        .WithName("RestoreK8sTenantNamespace")
        .WithSummary("Restores quarantined namespace to normal zero-trust mTLS operation");

        return endpoints;
    }
}
