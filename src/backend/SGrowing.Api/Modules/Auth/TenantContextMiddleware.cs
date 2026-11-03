using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Auth;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // 1. Authoritative resolution: Authenticated User Claims (Bearer JWT)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value
                              ?? context.User.FindFirst("TenantId")?.Value;
            if (!string.IsNullOrWhiteSpace(tenantClaim) && Guid.TryParse(tenantClaim, out var claimTenantId))
            {
                tenantContext.SetTenant(claimTenantId);
            }

            var workspaceClaim = context.User.FindFirst("workspace_id")?.Value
                                 ?? context.User.FindFirst("WorkspaceId")?.Value;
            if (!string.IsNullOrWhiteSpace(workspaceClaim) && Guid.TryParse(workspaceClaim, out var claimWorkspaceId))
            {
                tenantContext.SetWorkspace(claimWorkspaceId);
            }
        }

        // 2. Fallback for unauthenticated service hooks or public widgets (forms, webhooks, tracking)
        if (!tenantContext.HasTenant && context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerVal))
        {
            if (Guid.TryParse(headerVal.FirstOrDefault(), out var headerTenantId))
            {
                tenantContext.SetTenant(headerTenantId);
            }
        }

        if (context.Request.Headers.TryGetValue("X-Workspace-Id", out var wsHeaderVal))
        {
            if (Guid.TryParse(wsHeaderVal.FirstOrDefault(), out var headerWsId))
            {
                tenantContext.SetWorkspace(headerWsId);
            }
        }

        await _next(context);
    }
}
