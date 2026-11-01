using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.Ledger;

public static class DoubleEntryLedgerEndpoints
{
    /// <summary>
    /// Service collection extension method to register all FinTech double-entry ledger,
    /// escrow settlement, and virtual wallet payout services into DI container.
    /// </summary>
    public static IServiceCollection AddDoubleEntryLedgerServices(this IServiceCollection services)
    {
        services.AddSingleton<IDoubleEntryLedgerService, DoubleEntryLedgerService>();
        services.AddSingleton<NorthAfricanPayoutAdapter>();
        services.AddSingleton<InternationalPayoutAdapter>();
        services.AddSingleton<IPayoutGatewayAdapter>(sp => sp.GetRequiredService<NorthAfricanPayoutAdapter>());
        services.AddSingleton<IPayoutGatewayAdapter>(sp => sp.GetRequiredService<InternationalPayoutAdapter>());
        services.AddSingleton<IVirtualWalletPayoutService, VirtualWalletPayoutService>();
        services.AddSingleton<IEscrowSettlementEngine, EscrowSettlementEngine>();

        return services;
    }

    /// <summary>
    /// Maps all minimal API endpoints under /api/v1/finance/ledger.
    /// </summary>
    public static IEndpointRouteBuilder MapDoubleEntryLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/ledger")
            .WithTags("FinTech Double-Entry Ledger, Virtual Wallets & Multi-Party Escrow Payouts");

        // 1. POST /api/v1/finance/ledger/journal
        group.MapPost("/journal", async (
            [FromBody] PostJournalEntryRequest request,
            [FromServices] IDoubleEntryLedgerService ledgerService,
            HttpContext httpContext) =>
        {
            try
            {
                var entry = await ledgerService.PostEntryAsync(request, httpContext.RequestAborted);
                return Results.Created($"/api/v1/finance/ledger/journal/{entry.EntryId}", entry);
            }
            catch (UnbalancedJournalEntryException ex)
            {
                return Results.BadRequest(new
                {
                    error = "UNBALANCED_JOURNAL_ENTRY",
                    message = ex.Message,
                    total_debits = ex.TotalDebits,
                    total_credits = ex.TotalCredits,
                    imbalance = ex.Difference,
                    reference_id = ex.ReferenceId
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_REQUEST", message = ex.Message });
            }
        })
        .WithName("PostJournalEntry")
        .WithSummary("Post an immutable balanced double-entry journal entry with SHA-256 integrity hash");

        // 2. POST /api/v1/finance/ledger/escrow/lock
        group.MapPost("/escrow/lock", async (
            [FromBody] EscrowLockRequest request,
            [FromServices] IEscrowSettlementEngine escrowEngine,
            HttpContext httpContext) =>
        {
            try
            {
                var holdRecord = await escrowEngine.LockOnCheckoutAsync(request, httpContext.RequestAborted);
                return Results.Ok(holdRecord);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_REQUEST", message = ex.Message });
            }
        })
        .WithName("LockEscrowOnCheckout")
        .WithSummary("Stage 1: Hold gross order amount in Customer Escrow Held (1100) and lock merchant wallet balance");

        // 3. POST /api/v1/finance/ledger/escrow/release-pod
        group.MapPost("/escrow/release-pod", async (
            [FromBody] ProofOfDeliveryConfirmationRequest request,
            [FromServices] IEscrowSettlementEngine escrowEngine,
            HttpContext httpContext) =>
        {
            try
            {
                var result = await escrowEngine.ReleaseOnProofOfDeliveryAsync(request, httpContext.RequestAborted);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "ESCROW_NOT_FOUND", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = "INVALID_ESCROW_STATE", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_REQUEST", message = ex.Message });
            }
        })
        .WithName("ReleaseEscrowOnProofOfDelivery")
        .WithSummary("Stage 2: Ingest courier POD confirmation and execute 4-way balanced settlement split to merchant, courier, affiliate and platform");

        // 4. POST /api/v1/finance/ledger/escrow/refund
        group.MapPost("/escrow/refund", async (
            [FromBody] EscrowRefundRequest request,
            [FromServices] IEscrowSettlementEngine escrowEngine,
            HttpContext httpContext) =>
        {
            try
            {
                var result = await escrowEngine.RefundOnReturnAsync(request, httpContext.RequestAborted);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "ESCROW_NOT_FOUND", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = "INVALID_REFUND_STATE", message = ex.Message });
            }
        })
        .WithName("RefundEscrowOnReturn")
        .WithSummary("Stage 3: Automatically reverse escrow and issue refund entries for returned or refused packages");

        // 5. GET /api/v1/finance/ledger/wallets/{walletId}/balance
        group.MapGet("/wallets/{walletId:guid}/balance", async (
            Guid walletId,
            [FromServices] IVirtualWalletPayoutService walletService,
            HttpContext httpContext) =>
        {
            try
            {
                var wallet = await walletService.GetWalletAsync(walletId, httpContext.RequestAborted);
                return Results.Ok(new
                {
                    wallet_id = wallet.WalletId,
                    tenant_id = wallet.TenantId,
                    entity_id = wallet.EntityId,
                    owner_type = wallet.OwnerType.ToString(),
                    currency = wallet.Currency,
                    available_balance = wallet.AvailableBalance,
                    escrow_locked_balance = wallet.EscrowLockedBalance,
                    pending_payout_balance = wallet.PendingPayoutBalance,
                    total_balance = wallet.TotalBalance,
                    is_fraud_hold_active = wallet.IsFraudHoldActive,
                    fraud_hold_reason = wallet.FraudHoldReason,
                    active_dispute_count = wallet.ActiveDisputeCount
                });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "WALLET_NOT_FOUND", message = $"Wallet '{walletId}' was not found." });
            }
        })
        .WithName("GetWalletBalance")
        .WithSummary("Get virtual wallet multi-tier balance breakdown (Available, Escrow Locked, Pending Payout)");

        // 6. POST /api/v1/finance/ledger/payouts/create-batch
        group.MapPost("/payouts/create-batch", async (
            [FromBody] CreatePayoutBatchRequest request,
            [FromServices] IVirtualWalletPayoutService walletService,
            HttpContext httpContext) =>
        {
            try
            {
                var batch = await walletService.CreatePayoutBatchAsync(request, httpContext.RequestAborted);
                return Results.Ok(batch);
            }
            catch (FraudHoldBlockedException ex)
            {
                return Results.Json(new
                {
                    error = "FRAUD_HOLD_BLOCKED",
                    message = ex.Message,
                    wallet_id = ex.WalletId,
                    entity_id = ex.EntityId,
                    reason = ex.Reason,
                    active_disputes = ex.ActiveDisputes
                }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InsufficientFundsException ex)
            {
                return Results.BadRequest(new
                {
                    error = "INSUFFICIENT_FUNDS",
                    message = ex.Message,
                    requested_amount = ex.RequestedAmount,
                    available_balance = ex.AvailableBalance,
                    escrow_locked_balance = ex.EscrowLockedBalance
                });
            }
            catch (UnsupportedPayoutCurrencyException ex)
            {
                return Results.BadRequest(new
                {
                    error = "UNSUPPORTED_CURRENCY",
                    message = ex.Message,
                    currency = ex.Currency,
                    gateway = ex.GatewayType.ToString()
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_REQUEST", message = ex.Message });
            }
        })
        .WithName("CreatePayoutBatch")
        .WithSummary("Create an idempotent payout batch formatted for North African (BaridiMob/CCP) or International (SEPA/Stripe) gateways");

        // 7. GET /api/v1/finance/ledger/trial-balance
        group.MapGet("/trial-balance", async (
            [FromQuery] Guid tenantId,
            [FromQuery] string? currency,
            [FromServices] IDoubleEntryLedgerService ledgerService,
            HttpContext httpContext) =>
        {
            var cur = string.IsNullOrWhiteSpace(currency) ? "DZD" : currency.ToUpperInvariant();
            var tb = await ledgerService.GetTrialBalanceAsync(tenantId, cur, httpContext.RequestAborted);
            return Results.Ok(tb);
        })
        .WithName("GetTrialBalance")
        .WithSummary("Get complete double-entry trial balance report verifying total debits equal total credits");

        // 8. GET /api/v1/finance/ledger/accounts/{accountCode}/balance
        group.MapGet("/accounts/{accountCode}/balance", async (
            string accountCode,
            [FromQuery] Guid tenantId,
            [FromQuery] string? currency,
            [FromServices] IDoubleEntryLedgerService ledgerService,
            HttpContext httpContext) =>
        {
            var cur = string.IsNullOrWhiteSpace(currency) ? "DZD" : currency.ToUpperInvariant();
            var balance = await ledgerService.GetAccountBalanceDetailAsync(tenantId, accountCode, cur, httpContext.RequestAborted);
            return Results.Ok(balance);
        })
        .WithName("GetAccountBalance")
        .WithSummary("Get specific chart-of-accounts balance with debits, credits, and normal balance classification");

        return group;
    }
}
