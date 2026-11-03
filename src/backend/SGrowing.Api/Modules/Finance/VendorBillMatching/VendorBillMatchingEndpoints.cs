using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.VendorBillMatching;

public static class VendorBillMatchingEndpoints
{
    public static IServiceCollection AddVendorBillMatchingServices(this IServiceCollection services)
    {
        services.AddSingleton<IVendorBillMatchingEngine, VendorBillMatchingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVendorBillMatchingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/vendor-bill-matching")
            .WithTags("Finance - Vendor Bill OCR, 3-Way Matching & Early-Discount Escrow");

        group.MapGet("/overview", (IVendorBillMatchingEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetVendorBillMatchingOverview")
        .WithSummary("Retrieves accounts payable bills, 3-way reconciliation audit states, and early discount savings");

        group.MapPost("/approve-early-discount", (ApproveEarlyDiscountCommand command, IVendorBillMatchingEngine engine) =>
        {
            try
            {
                var result = engine.ApproveEarlyDiscount(command);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Bill {command.BillId} not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ApproveVendorEarlyDiscount")
        .WithSummary("Approves 3-way matched bill and captures 2/10 Net 30 cash discount via automated escrow");

        return endpoints;
    }
}
