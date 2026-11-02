namespace SGrowing.Api.Modules.Finance.OrderFlowAuctionMev;

public static class OrderFlowAuctionEndpoints
{
    public static IServiceCollection AddOrderFlowAuctionServices(this IServiceCollection services)
    {
        services.AddScoped<OrderFlowAuctionMevEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapOrderFlowAuctionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/order-flow-auction")
            .WithTags("Decentralized Order Flow Auction (OFA) MEV Redistribution Arbiter");

        group.MapPost("/resolve", (OrderFlowAuctionRequest request, OrderFlowAuctionMevEngine engine) =>
        {
            var result = engine.ResolveAuction(request);
            return Results.Ok(result);
        })
        .WithName("ResolveOrderFlowAuction")
        .WithSummary("Resolves decentralized Order Flow Auction (OFA) Vickrey clearing, user MEV rebates, and sandwich exploit neutralization.");

        return endpoints;
    }
}
