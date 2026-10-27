using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Billing;

public class PlanQuotaEnforcementMiddleware
{
    private readonly RequestDelegate _next;

    public PlanQuotaEnforcementMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IPlanQuotaEnforcementService quotaService, ITenantContext tenantContext)
    {
        if (context.Request.Headers.TryGetValue("X-Usage-Metric", out var metricHeader) &&
            Enum.TryParse<UsageMetricType>(metricHeader.ToString(), true, out var metric))
        {
            var tenantId = tenantContext.TenantId;
            if (tenantId == Guid.Empty && context.Request.Headers.TryGetValue("X-Tenant-Id", out var hVal) && Guid.TryParse(hVal.FirstOrDefault(), out var headerTenantId))
            {
                tenantId = headerTenantId;
            }
            if (tenantId == Guid.Empty && context.Request.Query.TryGetValue("tenantId", out var qVal) && Guid.TryParse(qVal, out var parsedTenantId))
            {
                tenantId = parsedTenantId;
            }

            if (tenantId != Guid.Empty)
            {
                int quantity = 1;
                if (context.Request.Headers.TryGetValue("X-Requested-Quantity", out var qtyHeader) &&
                    int.TryParse(qtyHeader.ToString(), out var parsedQty))
                {
                    quantity = Math.Max(1, parsedQty);
                }

                var check = await quotaService.CheckQuotaAsync(tenantId, metric, quantity, context.RequestAborted);
                if (check.IsHardBlocked)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        error = "QuotaExceeded",
                        isHardBlocked = true,
                        metric = metric.ToString(),
                        limit = check.QuotaLimit,
                        currentUsage = check.CurrentUsage,
                        message = check.Message
                    }));
                    return;
                }

                if (check.RequiresOverageWarning)
                {
                    context.Response.Headers.Append("X-Quota-Warning", "Soft limit reached. Overages applied.");
                }
            }
        }

        await _next(context);
    }
}
