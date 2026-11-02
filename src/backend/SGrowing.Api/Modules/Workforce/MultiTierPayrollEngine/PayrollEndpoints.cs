using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Workforce.MultiTierPayrollEngine;

public static class PayrollEndpoints
{
    public static IServiceCollection AddMultiTierPayrollServices(this IServiceCollection services)
    {
        services.AddSingleton<IPayrollEngine, PayrollEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMultiTierPayrollEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/workforce/payroll").WithTags("Workforce Payroll");

        group.MapPost("/process-run", (PayrollRunRequest request, IPayrollEngine engine) =>
        {
            var result = engine.ProcessPayrollRun(request);
            return Results.Ok(result);
        }).WithName("ProcessPayrollRun");

        return endpoints;
    }
}
