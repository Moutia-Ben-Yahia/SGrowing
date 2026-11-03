using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.AssetDepreciation;

public static class AssetDepreciationEndpoints
{
    public static IServiceCollection AddAssetDepreciationServices(this IServiceCollection services)
    {
        services.AddSingleton<AssetDepreciationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAssetDepreciationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/asset-depreciation")
            .WithTags("Finance - Dynamic CapEx Depreciation & Asset Lifecycle Ledger");

        group.MapGet("/overview", (AssetDepreciationEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetAssetDepreciationOverview")
        .WithSummary("Retrieve fixed asset ledger value, accumulated depreciation, and active asset classes");

        group.MapPost("/calculate-depreciation", (CalculateDepreciationRequest request, AssetDepreciationEngine engine) =>
        {
            var result = engine.CalculateDepreciation(request);
            return Results.Ok(result);
        })
        .WithName("CalculateAssetDepreciation")
        .WithSummary("Compute dual-book depreciation (MACRS vs IFRS) for specified period and asset basis");

        group.MapPost("/record-impairment", (RecordAssetImpairmentRequest request, AssetDepreciationEngine engine) =>
        {
            var receipt = engine.RecordImpairment(request);
            return Results.Ok(receipt);
        })
        .WithName("RecordAssetImpairment")
        .WithSummary("Record fair value impairment write-down with statutory journal audit receipt");

        return endpoints;
    }
}
