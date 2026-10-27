using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Modules.CRM.Import;

namespace SGrowing.Api.Modules.CRM;

public record CsvPreviewEndpointRequest(string CsvContent, char Delimiter = ',');
public record CsvExecuteEndpointRequest(
    string CsvContent,
    CsvImportMapping Mapping,
    CsvImportOptions? Options = null
);

public static class CrmImportEndpoints
{
    public static IEndpointRouteBuilder MapCrmImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/crm/contacts/import")
            .WithTags("B2B CRM & Data Operations");

        group.MapPost("/preview", (CsvPreviewEndpointRequest req, ICsvImportService importService) =>
        {
            var res = importService.PreviewCsv(req.CsvContent, new CsvImportOptions(Delimiter: req.Delimiter));
            return Results.Ok(res);
        })
        .WithName("PreviewContactCsv")
        .WithSummary("Preview uploaded CSV file structure, headers, and detected validation errors");

        group.MapPost("/execute", async (Guid tenantId, CsvExecuteEndpointRequest req, ICsvImportService importService, CancellationToken ct) =>
        {
            var options = req.Options ?? new CsvImportOptions();
            var res = await importService.ImportContactsAsync(tenantId, req.CsvContent, req.Mapping, options, ct);
            return Results.Ok(res);
        })
        .WithName("ExecuteContactCsvImport")
        .WithSummary("High-throughput contact import with duplicate detection heuristics, dynamic mapping, and atomic batch validation");

        return app;
    }
}
