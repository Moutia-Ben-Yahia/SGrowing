using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.CRM;

public static class FrappeCrmEnhancementsEndpoints
{
    public static IEndpointRouteBuilder MapFrappeCrmEnhancementsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/crm/advanced")
            .WithTags("Frappe CRM Pipelines, Rotten Deals & Territories");

        group.MapGet("/deals/rotten", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromQuery] int thresholdDays,
            IFrappeCrmEnhancementsService service) =>
        {
            var result = await service.EvaluateRottenDealsAsync(tenantId, thresholdDays > 0 ? thresholdDays : 7);
            return Results.Ok(result);
        });

        group.MapPost("/doctypes/validate", (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] ValidateDocTypeRequest request,
            IFrappeCrmEnhancementsService service) =>
        {
            var result = service.ValidateDocTypeSchemaAsync(tenantId, request.DocTypeName, request.Fields);
            return Results.Ok(result);
        });

        group.MapPost("/territories/assign", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] AssignTerritoryRequest request,
            IFrappeCrmEnhancementsService service) =>
        {
            var result = await service.AssignLeadTerritoryAsync(
                tenantId,
                request.LeadId,
                request.LeadAddress,
                request.Latitude,
                request.Longitude
            );
            return Results.Ok(result);
        });

        return app;
    }
}

public record ValidateDocTypeRequest(
    string DocTypeName,
    Dictionary<string, object> Fields
);

public record AssignTerritoryRequest(
    Guid LeadId,
    string LeadAddress,
    double Latitude,
    double Longitude
);
