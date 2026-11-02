using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Sales.Proposals;

public static class B2BProposalEndpoints
{
    public static IServiceCollection AddB2BProposalServices(this IServiceCollection services)
    {
        services.AddSingleton<IB2BProposalEngine, B2BProposalEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapB2BProposalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sales/proposals")
            .WithTags("B2B Proposals & Usage Invoicing");

        group.MapPost("/quote/calculate", (CalculateQuoteRequest request, IB2BProposalEngine engine) =>
        {
            var result = engine.CalculateQuote(request);
            return Results.Ok(result);
        }).WithName("CalculateB2BQuote");

        group.MapPost("/create", (CreateProposalRequest request, IB2BProposalEngine engine) =>
        {
            var proposal = engine.CreateProposal(
                request.TenantId,
                request.CustomerCompany,
                request.CustomerEmail,
                request.Items,
                request.DiscountPercent
            );
            return Results.Created($"/api/v1/sales/proposals/{proposal.TenantId}/{proposal.Id}", proposal);
        }).WithName("CreateB2BProposal");

        group.MapPost("/sign", (SignProposalRequest request, IB2BProposalEngine engine) =>
        {
            try
            {
                var proposal = engine.SignProposal(request);
                return Results.Ok(proposal);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        }).WithName("SignB2BProposal");

        group.MapGet("/{tenantId}/{proposalId:guid}", (string tenantId, Guid proposalId, IB2BProposalEngine engine) =>
        {
            var proposal = engine.GetProposal(tenantId, proposalId);
            return proposal is not null
                ? Results.Ok(proposal)
                : Results.NotFound(new { message = $"Proposal '{proposalId}' for tenant '{tenantId}' was not found." });
        }).WithName("GetB2BProposal");

        group.MapPost("/usage/record", (RecordUsageRequest request, IB2BProposalEngine engine) =>
        {
            engine.RecordUsage(request);
            return Results.Accepted(null, new { message = "Usage metric recorded successfully." });
        }).WithName("RecordB2BUsage");

        group.MapGet("/usage/invoice/{tenantId}", (
            string tenantId,
            [FromQuery] DateTime? startUtc,
            [FromQuery] DateTime? endUtc,
            IB2BProposalEngine engine) =>
        {
            var periodStart = startUtc ?? DateTime.UtcNow.AddMonths(-1);
            var periodEnd = endUtc ?? DateTime.UtcNow;
            var invoice = engine.GenerateUsageInvoice(tenantId, periodStart, periodEnd);
            return Results.Ok(invoice);
        }).WithName("GetB2BUsageInvoice");

        return endpoints;
    }
}
