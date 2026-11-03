using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.RetailSignageMesh;

public static class RetailSignageMeshEndpoints
{
    public static IServiceCollection AddRetailSignageMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<RetailSignageMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRetailSignageMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/retail-signage-mesh")
            .WithTags("Advertising - In-Store Digital Signage & Smart Shelf Bidding Mesh");

        group.MapGet("/overview", (RetailSignageMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetRetailSignageOverview")
        .WithSummary("Retrieve active in-store screens, shelf stock levels, and ad fill metrics");

        group.MapPost("/evaluate-shelf-bid", (EvaluateShelfBidRequest request, RetailSignageMeshEngine engine) =>
        {
            var result = engine.EvaluateShelfBid(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateShelfSignageBid")
        .WithSummary("Evaluate real-time shelf inventory levels and shopper footfall to return winning ad creative");

        group.MapPost("/update-shelf-stock", (UpdateShelfStockRequest request, RetailSignageMeshEngine engine) =>
        {
            var receipt = engine.UpdateShelfStock(request);
            return Results.Ok(receipt);
        })
        .WithName("UpdateShelfStockSensor")
        .WithSummary("Update real-time shelf stock and automatically toggle ad suppression");

        return endpoints;
    }
}
