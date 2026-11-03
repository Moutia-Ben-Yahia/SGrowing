using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.SovereignEInvoicing;

public static class SovereignEInvoicingEndpoints
{
    public static IServiceCollection AddSovereignEInvoicingServices(this IServiceCollection services)
    {
        services.AddSingleton<SovereignEInvoicingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSovereignEInvoicingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/sovereign-e-invoicing")
            .WithTags("Finance - Sovereign E-Invoicing & CTC Clearance Mesh");

        group.MapGet("/overview", (SovereignEInvoicingEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetSovereignEInvoicingOverview")
        .WithSummary("Retrieve cleared statutory invoices, clearance success rate, and supported tax portals");

        group.MapPost("/generate-clearance-invoice", (GenerateClearanceInvoiceRequest request, SovereignEInvoicingEngine engine) =>
        {
            var result = engine.GenerateClearanceInvoice(request);
            return Results.Ok(result);
        })
        .WithName("GenerateClearanceEInvoice")
        .WithSummary("Generate sovereign XML/UBL invoice, sign with sovereign HSM, and obtain clearance seal");

        group.MapPost("/verify-clearance-status", (VerifyClearanceRequest request, SovereignEInvoicingEngine engine) =>
        {
            var receipt = engine.VerifyClearance(request);
            return Results.Ok(receipt);
        })
        .WithName("VerifyEInvoiceClearanceStatus")
        .WithSummary("Verify government tax portal cryptographic acceptance digest");

        return endpoints;
    }
}
