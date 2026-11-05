using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Dev;

public class DevEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app) => app.MapDevEndpoints();

    public static IEndpointRouteBuilder MapDevEndpoints(IEndpointRouteBuilder app) => app.MapDevEndpoints();
}

public static class DevEndpointExtensions
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dev").WithTags("Developer Sandbox");

        group.MapPost("/seed-demo", async (DatabaseSeeder seeder, HttpContext context) =>
        {
            Guid? tenantId = null;
            if (context.Request.Query.TryGetValue("tenantId", out var tStr) && Guid.TryParse(tStr, out var parsed))
            {
                tenantId = parsed;
            }

            var seededTenantId = await seeder.SeedEnterpriseDemoAsync(tenantId);
            return Results.Ok(new
            {
                status = "Success",
                message = "Enterprise demo dataset seeded successfully.",
                tenantId = seededTenantId,
                demoAdmin = "admin@sgrowing.io",
                demoPassword = "SecurePassword999!",
                demoAgent = "agent.sarah@sgrowing.io"
            });
        });

        return endpoints;
    }
}
