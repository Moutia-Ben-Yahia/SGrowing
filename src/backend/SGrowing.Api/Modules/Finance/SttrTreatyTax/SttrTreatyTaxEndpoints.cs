using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Finance.SttrTreatyTax;

public static class SttrTreatyTaxEndpoints
{
    public static IServiceCollection AddSttrTreatyTaxServices(this IServiceCollection services)
    {
        services.AddSingleton<ISttrTreatyTaxEngine, SttrTreatyTaxEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSttrTreatyTaxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/sttr-treaty-tax")
            .WithTags("Pillar Two Subject to Tax Rule (STTR) Treaty Withholding Engine");

        group.MapGet("/payments", ([FromServices] ISttrTreatyTaxEngine engine) =>
        {
            var payments = engine.GetPayments();
            return Results.Ok(payments);
        })
        .WithName("GetSttrPayments")
        .WithSummary("Retrieves all intragroup cross-border payments evaluated under STTR");

        group.MapGet("/evaluations", ([FromServices] ISttrTreatyTaxEngine engine) =>
        {
            var evaluations = engine.GetEvaluations();
            return Results.Ok(evaluations);
        })
        .WithName("GetSttrEvaluations")
        .WithSummary("Retrieves all 9% minimum tax treaty evaluations");

        group.MapPost("/assess", ([FromBody] AssessSttrPaymentRequest request, [FromServices] ISttrTreatyTaxEngine engine) =>
        {
            var result = engine.AssessPayment(request);
            return Results.Ok(result);
        })
        .WithName("AssessSttrPayment")
        .WithSummary("Assesses a cross-border intragroup payment for STTR top-up withholding liability");

        group.MapPost("/certificates/{paymentId}", (string paymentId, [FromServices] ISttrTreatyTaxEngine engine) =>
        {
            var cert = engine.GenerateClearanceCertificate(paymentId);
            return Results.Ok(cert);
        })
        .WithName("GenerateSttrClearanceCertificate")
        .WithSummary("Generates an OECD STTR clearance certificate with cryptographic SHA-256 seal");

        return app;
    }
}
