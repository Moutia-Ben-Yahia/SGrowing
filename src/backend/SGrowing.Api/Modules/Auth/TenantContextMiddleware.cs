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
        }

        // 2. Fallback for unauthenticated service hooks or public widgets (forms, webhooks, tracking)
        if (!tenantContext.HasTenant && context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerVal))
        {
            if (Guid.TryParse(headerVal.FirstOrDefault(), out var headerTenantId))
            {
                tenantContext.SetTenant(headerTenantId);
            }
        }

        await _next(context);
    }
}
