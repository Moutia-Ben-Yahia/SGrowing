using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.CtvAdInsertion;

public static class CtvAdInsertionEndpoints
{
    public static IServiceCollection AddCtvAdInsertionServices(this IServiceCollection services)
    {
        services.AddSingleton<ICtvAdInsertionEngine, CtvAdInsertionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCtvAdInsertionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/advertising/ctv-ad-insertion")
            .WithTags("Advertising - CTV Ad Insertion");

        group.MapGet("/overview", (ICtvAdInsertionEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetCtvAdInsertionOverview")
        .WithSummary("Retrieves active CTV ad pod inventory, VAST impression velocity, and dual-screen QR scan rates");

        group.MapPost("/bid-stream", (ProcessBidStreamCommand command, ICtvAdInsertionEngine engine) =>
        {
            var result = engine.ProcessBidStream(command);
            return Results.Ok(result);
        })
        .WithName("ProcessCtvBidStream")
        .WithSummary("Processes real-time RTB bid requests for CTV ad pods with dynamic QR watermark injection");

        group.MapPost("/attribute-scan", (AttributeScanCommand command, ICtvAdInsertionEngine engine) =>
        {
            var result = engine.AttributeScan(command);
            return Results.Ok(result);
        })
        .WithName("AttributeCtvQrScan")
        .WithSummary("Ingests mobile QR scan events and correlates with CTV broadcast session fingerprints");

        return app;
    }
}
