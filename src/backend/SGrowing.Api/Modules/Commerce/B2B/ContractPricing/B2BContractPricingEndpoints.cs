using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Commerce.B2B.ContractPricing;

/// <summary>
/// Minimal API endpoints for B2B Contract Pricing Matrix and CPQ Governance.
/// Implements <see cref="IEndpointModule"/> for modular endpoint auto-discovery.
/// </summary>
public class B2BContractPricingEndpoints : IEndpointModule
{
    public B2BContractPricingEndpoints()
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/b2b")
            .WithTags("Commerce - B2B Contract Pricing & CPQ Matrix");

        group.MapPost("/pricing/calculate", (CalculateB2BPriceRequest request, IB2BContractPricingService service) =>
        {
            try
            {
                var result = service.CalculatePrice(request);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("CalculateB2BPrice")
        .WithSummary("Calculates real-time B2B tiered price for a SKU, account, and volume quantity.");

        group.MapPost("/quotes", (CreateB2BQuoteRequest request, IB2BContractPricingService service) =>
        {
            try
            {
                var quote = service.CreateQuote(request);
                return Results.Created($"/api/v1/b2b/quotes/{quote.QuoteId}", quote);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("CreateB2BQuote")
        .WithSummary("Creates an enterprise B2B quote and applies governance discount approval routing.");

        group.MapGet("/quotes/{id:guid}", (Guid id, [FromQuery] Guid? tenantId, IB2BContractPricingService service) =>
        {
            var targetTenantId = tenantId ?? B2BContractPricingService.DefaultTenantId;
            var quote = service.GetQuote(targetTenantId, id);
            return quote != null
                ? Results.Ok(quote)
                : Results.NotFound(new { error = $"Quote '{id}' not found for tenant '{targetTenantId}'." });
        })
        .WithName("GetB2BQuote")
        .WithSummary("Retrieves a B2B quote by its identifier with tenant isolation.");

        group.MapPost("/quotes/{id:guid}/approve", (Guid id, ApproveB2BQuoteRequest request, IB2BContractPricingService service) =>
        {
            try
            {
                var command = request.QuoteId == id ? request : request with { QuoteId = id };
                var result = service.ApproveQuote(command);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ApproveB2BQuote")
        .WithSummary("Approves or rejects a B2B quote in accordance with enterprise CPQ governance.");

        group.MapGet("/pricelists", ([FromQuery] Guid? tenantId, IB2BContractPricingService service) =>
        {
            var targetTenantId = tenantId ?? B2BContractPricingService.DefaultTenantId;
            var priceLists = service.GetPriceLists(targetTenantId);
            return Results.Ok(priceLists);
        })
        .WithName("GetB2BPriceLists")
        .WithSummary("Lists active contract price lists and volume tiers for a given tenant.");
    }
}
