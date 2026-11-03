using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.PayrollTaxWithholding;

public static class PayrollTaxWithholdingEndpoints
{
    public static IServiceCollection AddPayrollTaxWithholdingServices(this IServiceCollection services)
    {
        services.AddSingleton<PayrollTaxWithholdingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPayrollTaxWithholdingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/payroll-tax-withholding")
            .WithTags("Finance - Global Multi-Jurisdiction Payroll Tax Withholding");

        group.MapGet("/overview", (PayrollTaxWithholdingEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetPayrollTaxOverview")
        .WithSummary("Retrieve active tax jurisdictions, monthly withholding balances, and remittance filings");

        group.MapPost("/calculate-withholding", (CalculateWithholdingRequest request, PayrollTaxWithholdingEngine engine) =>
        {
            var result = engine.CalculateWithholding(request);
            return Results.Ok(result);
        })
        .WithName("CalculatePayrollWithholding")
        .WithSummary("Compute gross-to-net payroll breakdown across federal, state, and social taxes");

        group.MapPost("/generate-filing", (GenerateFilingReceiptRequest request, PayrollTaxWithholdingEngine engine) =>
        {
            var receipt = engine.GenerateFilingReceipt(request);
            return Results.Ok(receipt);
        })
        .WithName("GeneratePayrollFilingReceipt")
        .WithSummary("Create cryptographic sovereign remittance filing receipt for tax authorities");

        return endpoints;
    }
}
