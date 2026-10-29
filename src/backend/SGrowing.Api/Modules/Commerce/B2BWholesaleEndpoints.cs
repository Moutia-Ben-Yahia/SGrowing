using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Commerce;

#region Request Payloads

public record CreateCorporateAccountApiRequest(
    Guid? TenantId,
    string CompanyName,
    string TaxId,
    decimal CreditLimit,
    int PaymentTermsDays = 30,
    decimal RequiresApprovalAboveAmount = 5000m,
    string Currency = "USD",
    List<CreateBranchRequest>? Branches = null,
    List<CreateOfficerRequest>? Officers = null);

public record CalculateMatrixApiRequest(
    Guid? TenantId,
    string ProductStyle,
    string BaseSku,
    decimal BaseUnitPrice,
    List<string> Colors,
    List<string> Sizes,
    List<MatrixCellInputDto> MatrixQuantities,
    decimal TaxRatePercent = 19.0m,
    decimal ShippingFee = 45.0m);

public record SubmitBulkMatrixOrderApiRequest(
    Guid? TenantId,
    Guid CorporateAccountId,
    Guid BranchId,
    Guid OfficerId,
    string ExternalPoNumber,
    string ProductStyle,
    string BaseSku,
    decimal BaseUnitPrice,
    List<string> Colors,
    List<string> Sizes,
    List<MatrixCellInputDto> MatrixQuantities,
    decimal TaxRatePercent = 19.0m,
    decimal ShippingFee = 45.0m,
    string? ShippingNotes = null,
    bool AllowCreditOverride = false);

public record ApproveOrderApiRequest(
    Guid ApproverOfficerId,
    string? ApprovalNotes = null);

public record RejectOrderApiRequest(
    Guid ApproverOfficerId,
    string Reason);

public record ReorderApiRequest(
    Guid? TenantId,
    Guid OfficerId,
    Guid BranchId,
    string? NewExternalPoNumber = null);

public record RecordInvoicePaymentApiRequest(
    decimal AmountPaid,
    string? Reference = null);

#endregion

public static class B2BWholesaleEndpoints
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapB2BWholesaleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/b2b-wholesale")
            .WithTags("B2B Wholesale Portal, Corporate Sub-Accounts & Credit Limit Ledger");

        // 1. GET /accounts - List all corporate accounts
        group.MapGet("/accounts", async (
            [FromQuery] Guid? tenantId,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var accounts = await service.GetAccountsAsync(effectiveTenantId);
            return Results.Ok(new { success = true, count = accounts.Count, accounts });
        })
        .WithName("ListCorporateAccounts")
        .WithSummary("List all corporate accounts for the tenant hierarchy");

        // 2. POST /accounts - Create corporate account with credit limit & terms
        group.MapPost("/accounts", async (
            [FromBody] CreateCorporateAccountApiRequest request,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = request.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var domainReq = new CreateCorporateAccountRequest(
                request.CompanyName,
                request.TaxId,
                request.CreditLimit,
                request.PaymentTermsDays,
                request.RequiresApprovalAboveAmount,
                request.Currency,
                request.Branches,
                request.Officers);

            var created = await service.CreateAccountAsync(effectiveTenantId, domainReq);
            return Results.Created($"/api/v1/commerce/b2b-wholesale/accounts/{created.Id}", new { success = true, account = created });
        })
        .WithName("CreateCorporateAccount")
        .WithSummary("Create corporate account with revolving credit limit and payment terms");

        // 3. GET /accounts/{id} - Get single corporate account details
        group.MapGet("/accounts/{id:guid}", async (
            [FromRoute] Guid id,
            [FromQuery] Guid? tenantId,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var account = await service.GetAccountByIdAsync(effectiveTenantId, id);
            return account is not null
                ? Results.Ok(new { success = true, account })
                : Results.NotFound(new { success = false, message = $"Corporate account '{id}' not found." });
        })
        .WithName("GetCorporateAccount")
        .WithSummary("Get corporate account details including branches and authorized officers");

        // 4. GET /accounts/{id}/credit-status - Available credit, unpaid invoices, pending holds
        group.MapGet("/accounts/{id:guid}/credit-status", async (
            [FromRoute] Guid id,
            [FromQuery] Guid? tenantId,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            try
            {
                var creditStatus = await service.GetCreditStatusAsync(effectiveTenantId, id);
                return Results.Ok(new { success = true, creditStatus });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { success = false, message = ex.Message });
            }
        })
        .WithName("GetCorporateCreditStatus")
        .WithSummary("Get revolving credit limit status, active holds, unpaid invoices and utilization gauge");

        // 5. GET /accounts/{id}/ledger - Audit trail of revolving credit entries
        group.MapGet("/accounts/{id:guid}/ledger", async (
            [FromRoute] Guid id,
            [FromQuery] Guid? tenantId,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var entries = await service.GetCreditLedgerEntriesAsync(effectiveTenantId, id);
            return Results.Ok(new { success = true, count = entries.Count, entries });
        })
        .WithName("GetCreditLedgerEntries")
        .WithSummary("Get revolving credit transaction ledger for account");

        // 6. POST /accounts/{id}/branches - Add branch to corporate account
        group.MapPost("/accounts/{id:guid}/branches", async (
            [FromRoute] Guid id,
            [FromBody] CreateBranchRequest request,
            [FromQuery] Guid? tenantId,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            try
            {
                var branch = await service.AddBranchAsync(effectiveTenantId, id, request);
                return Results.Created($"/api/v1/commerce/b2b-wholesale/accounts/{id}", new { success = true, branch });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { success = false, message = ex.Message });
            }
        })
        .WithName("AddCorporateBranch")
        .WithSummary("Add delivery branch / sub-entity to corporate account");

        // 7. POST /accounts/{id}/officers - Add authorized corporate officer
        group.MapPost("/accounts/{id:guid}/officers", async (
            [FromRoute] Guid id,
            [FromBody] CreateOfficerRequest request,
            [FromQuery] Guid? tenantId,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            try
            {
                var officer = await service.AddOfficerAsync(effectiveTenantId, id, request);
                return Results.Created($"/api/v1/commerce/b2b-wholesale/accounts/{id}", new { success = true, officer });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { success = false, message = ex.Message });
            }
        })
        .WithName("AddCorporateOfficer")
        .WithSummary("Add authorized officer with role and spending limit");

        // 8. POST /orders/calculate-matrix - Preview bulk matrix calculations & tier discounts
        group.MapPost("/orders/calculate-matrix", async (
            [FromBody] CalculateMatrixApiRequest request,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = request.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var domainReq = new BulkMatrixCalculationRequest(
                request.ProductStyle,
                request.BaseSku,
                request.BaseUnitPrice,
                request.Colors,
                request.Sizes,
                request.MatrixQuantities,
                request.TaxRatePercent,
                request.ShippingFee);

            var result = await service.CalculateBulkMatrixOrderAsync(effectiveTenantId, domainReq);
            return Results.Ok(new { success = true, calculation = result });
        })
        .WithName("CalculateBulkMatrixOrder")
        .WithSummary("Calculate bulk matrix SKU pricing, volume tiers, taxes and order subtotal");

        // 9. POST /orders/bulk-matrix - Submit bulk SKU matrix purchase order
        group.MapPost("/orders/bulk-matrix", async (
            [FromBody] SubmitBulkMatrixOrderApiRequest request,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = request.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var domainReq = new SubmitBulkMatrixOrderRequest(
                request.CorporateAccountId,
                request.BranchId,
                request.OfficerId,
                request.ExternalPoNumber,
                request.ProductStyle,
                request.BaseSku,
                request.BaseUnitPrice,
                request.Colors,
                request.Sizes,
                request.MatrixQuantities,
                request.TaxRatePercent,
                request.ShippingFee,
                request.ShippingNotes,
                request.AllowCreditOverride);

            try
            {
                var order = await service.SubmitBulkMatrixOrderAsync(effectiveTenantId, domainReq);
                return Results.Created($"/api/v1/commerce/b2b-wholesale/orders/{order.Id}", new { success = true, order });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { success = false, error = "CREDIT_OR_APPROVAL_ERROR", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { success = false, error = "VALIDATION_ERROR", message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { success = false, message = ex.Message });
            }
        })
        .WithName("SubmitBulkMatrixOrder")
        .WithSummary("Submit bulk SKU matrix purchase order with credit limit check and spending approval routing");

        // 10. GET /orders - List wholesale purchase orders
        group.MapGet("/orders", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] Guid? accountId,
            [FromQuery] Guid? branchId,
            [FromQuery] string? status,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var orders = await service.GetOrdersAsync(effectiveTenantId, accountId, branchId, status);
            return Results.Ok(new { success = true, count = orders.Count, orders });
        })
        .WithName("ListWholesaleOrders")
        .WithSummary("List wholesale purchase orders with multi-status filters");

        // 11. GET /orders/{id} - Get single order details
        group.MapGet("/orders/{id:guid}", async (
            [FromRoute] Guid id,
            [FromQuery] Guid? tenantId,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var order = await service.GetOrderByIdAsync(effectiveTenantId, id);
            return order is not null
                ? Results.Ok(new { success = true, order })
                : Results.NotFound(new { success = false, message = $"Wholesale order '{id}' not found." });
        })
        .WithName("GetWholesaleOrder")
        .WithSummary("Get purchase order breakdown and matrix items");

        // 12. POST /orders/{id}/approve - Corporate manager approval
        group.MapPost("/orders/{id:guid}/approve", async (
            [FromRoute] Guid id,
            [FromBody] ApproveOrderApiRequest request,
            [FromQuery] Guid? tenantId,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            try
            {
                var order = await service.ApproveOrderAsync(effectiveTenantId, id, new ApproveOrderRequest(request.ApproverOfficerId, request.ApprovalNotes));
                return Results.Ok(new { success = true, message = $"Order {order.OrderNumber} successfully approved and stock reserved in warehouse.", order });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { success = false, error = "UNAUTHORIZED_APPROVAL", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { success = false, error = "INVALID_STATE", message = ex.Message });
            }
            catch (Exception ex) when (ex is KeyNotFoundException or ArgumentException)
            {
                return Results.NotFound(new { success = false, message = ex.Message });
            }
        })
        .WithName("ApproveWholesaleOrder")
        .WithSummary("Approve pending wholesale order by department director or CFO and reserve stock");

        // 13. POST /orders/{id}/reject - Corporate manager rejection
        group.MapPost("/orders/{id:guid}/reject", async (
            [FromRoute] Guid id,
            [FromBody] RejectOrderApiRequest request,
            [FromQuery] Guid? tenantId,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            try
            {
                var order = await service.RejectOrderAsync(effectiveTenantId, id, new RejectOrderRequest(request.ApproverOfficerId, request.Reason));
                return Results.Ok(new { success = true, message = $"Order {order.OrderNumber} rejected and credit hold released.", order });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { success = false, error = "UNAUTHORIZED_REJECTION", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { success = false, error = "INVALID_STATE", message = ex.Message });
            }
            catch (Exception ex) when (ex is KeyNotFoundException or ArgumentException)
            {
                return Results.NotFound(new { success = false, message = ex.Message });
            }
        })
        .WithName("RejectWholesaleOrder")
        .WithSummary("Reject pending wholesale order and release credit ledger hold");

        // 14. GET /approvals/pending - List pending approval requests for manager
        group.MapGet("/approvals/pending", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] Guid? accountId,
            [FromQuery] Guid? officerId,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var pendingOrders = await service.GetPendingApprovalsAsync(effectiveTenantId, accountId, officerId);
            return Results.Ok(new { success = true, count = pendingOrders.Count, pendingOrders });
        })
        .WithName("ListPendingCorporateApprovals")
        .WithSummary("List orders waiting for manager corporate spending approval");

        // 15. POST /orders/{id}/reorder - 1-Click reorder
        group.MapPost("/orders/{id:guid}/reorder", async (
            [FromRoute] Guid id,
            [FromBody] ReorderApiRequest request,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = request.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            try
            {
                var order = await service.ReorderPreviousOrderAsync(effectiveTenantId, id, new ReorderRequest(request.OfficerId, request.BranchId, request.NewExternalPoNumber));
                return Results.Created($"/api/v1/commerce/b2b-wholesale/orders/{order.Id}", new { success = true, order });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        })
        .WithName("ReorderPreviousWholesaleOrder")
        .WithSummary("1-Click reorder previous wholesale order with updated external PO number");

        // 16. POST /accounts/{id}/invoices/{invoiceId}/pay - Record invoice payment & replenish credit
        group.MapPost("/accounts/{id:guid}/invoices/{invoiceId:guid}/pay", async (
            [FromRoute] Guid id,
            [FromRoute] Guid invoiceId,
            [FromBody] RecordInvoicePaymentApiRequest request,
            [FromQuery] Guid? tenantId,
            [FromServices] IB2BWholesalePortalService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            try
            {
                var invoice = await service.RecordInvoicePaymentAsync(effectiveTenantId, id, invoiceId, request.AmountPaid, request.Reference);
                var creditStatus = await service.GetCreditStatusAsync(effectiveTenantId, id);
                return Results.Ok(new { success = true, invoice, creditStatus });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        })
        .WithName("RecordCorporateInvoicePayment")
        .WithSummary("Record invoice payment and replenish revolving credit available limit");

        return app;
    }
}
