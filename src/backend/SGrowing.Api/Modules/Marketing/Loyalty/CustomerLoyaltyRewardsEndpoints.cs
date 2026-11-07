using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Marketing.Loyalty;

/// <summary>
/// Modular Minimal API endpoints for Omnichannel Customer Loyalty & Referral Rewards Ledger (PRD Domain H4).
/// Implements parameterless constructor for auto-discovery via IEndpointModule.
/// </summary>
public class CustomerLoyaltyRewardsEndpoints : IEndpointModule
{
    public CustomerLoyaltyRewardsEndpoints() { }

    public void MapEndpoints(IEndpointRouteBuilder app) => app.MapCustomerLoyaltyRewardsEndpoints();
}

public static class CustomerLoyaltyRewardsEndpointExtensions
{
    private static readonly Guid FallbackTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static IEndpointRouteBuilder MapCustomerLoyaltyRewardsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/loyalty")
            .WithTags("Omnichannel Customer Loyalty & Referral Rewards Ledger (Domain H4)");

        // GET /api/v1/loyalty/accounts/{contactId}
        group.MapGet("/accounts/{contactId:guid}", async (
            Guid contactId,
            [FromQuery] Guid? tenantId,
            ICustomerLoyaltyRewardsService loyaltyService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId.GetValueOrDefault(FallbackTenantId);
            if (effectiveTenantId == Guid.Empty)
            {
                effectiveTenantId = FallbackTenantId;
            }

            try
            {
                var summary = await loyaltyService.GetAccountSummary(effectiveTenantId, contactId, ct);
                return Results.Ok(summary);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("GetLoyaltyAccountSummary")
        .WithSummary("Retrieve member loyalty account summary, points balance, tier perks, and ledger entries.");

        // GET /api/v1/loyalty/accounts
        group.MapGet("/accounts", async (
            [FromQuery] Guid? tenantId,
            ICustomerLoyaltyRewardsService loyaltyService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId.GetValueOrDefault(FallbackTenantId);
            if (effectiveTenantId == Guid.Empty)
            {
                effectiveTenantId = FallbackTenantId;
            }

            var accounts = await loyaltyService.GetAllAccounts(effectiveTenantId, ct);
            return Results.Ok(accounts);
        })
        .WithName("GetAllLoyaltyAccounts")
        .WithSummary("List all loyalty accounts under the specified tenant.");

        // POST /api/v1/loyalty/accounts/{contactId}/earn
        group.MapPost("/accounts/{contactId:guid}/earn", async (
            Guid contactId,
            [FromBody] EarnPointsDto body,
            ICustomerLoyaltyRewardsService loyaltyService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = body.TenantId.GetValueOrDefault(FallbackTenantId);
            if (effectiveTenantId == Guid.Empty)
            {
                effectiveTenantId = FallbackTenantId;
            }

            var req = new EarnPointsRequest(
                TenantId: effectiveTenantId,
                ContactId: contactId,
                Type: body.Type,
                Points: body.Points,
                ReferenceId: body.ReferenceId,
                Description: body.Description
            );

            try
            {
                var summary = await loyaltyService.EarnPoints(req, ct);
                return Results.Ok(summary);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("EarnLoyaltyPoints")
        .WithSummary("Credit loyalty points to a member account and update tier thresholds.");

        // POST /api/v1/loyalty/accounts/{contactId}/redeem
        group.MapPost("/accounts/{contactId:guid}/redeem", async (
            Guid contactId,
            [FromBody] RedeemPointsDto body,
            ICustomerLoyaltyRewardsService loyaltyService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = body.TenantId.GetValueOrDefault(FallbackTenantId);
            if (effectiveTenantId == Guid.Empty)
            {
                effectiveTenantId = FallbackTenantId;
            }

            var req = new RedeemPointsRequest(
                TenantId: effectiveTenantId,
                ContactId: contactId,
                PointsToRedeem: body.PointsToRedeem,
                OrderReferenceId: body.OrderReferenceId
            );

            try
            {
                var summary = await loyaltyService.RedeemPoints(req, ct);
                return Results.Ok(summary);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RedeemLoyaltyPoints")
        .WithSummary("Burn points against an order or voucher with double-entry balance check.");

        // POST /api/v1/loyalty/referrals/attribute
        group.MapPost("/referrals/attribute", async (
            [FromBody] AttributeReferralRequest req,
            ICustomerLoyaltyRewardsService loyaltyService,
            CancellationToken ct) =>
        {
            if (req.TenantId == Guid.Empty)
            {
                req = req with { TenantId = FallbackTenantId };
            }

            try
            {
                var attribution = await loyaltyService.AttributeReferral(req, ct);
                return Results.Ok(attribution);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("AttributeReferral")
        .WithSummary("Attribute referee order to referral code, award points to referrer, and log attribution.");

        // GET /api/v1/loyalty/tiers
        group.MapGet("/tiers", (ICustomerLoyaltyRewardsService loyaltyService) =>
        {
            var tiers = loyaltyService.GetTierCatalog();
            return Results.Ok(tiers);
        })
        .WithName("GetLoyaltyTierCatalog")
        .WithSummary("Get loyalty tier definitions, qualification criteria, multipliers, and member perks.");

        return app;
    }
}
