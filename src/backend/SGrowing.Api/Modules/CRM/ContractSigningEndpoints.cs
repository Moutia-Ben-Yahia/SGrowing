using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Modules.CRM.ContractSigning;

namespace SGrowing.Api.Modules.CRM;

public record SendOtpEndpointRequest(string Destination = "email");
public record VerifyOtpEndpointRequest(string OtpCode);

public static class ContractSigningEndpoints
{
    public static IEndpointRouteBuilder MapContractSigningEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/contracts")
            .WithTags("CPQ & Cryptographic E-Signatures");

        group.MapPost("/multi-party", async (Guid tenantId, CreateMultiPartyContractRequest req, IMultiPartySigningService service, CancellationToken ct) =>
        {
            var res = await service.CreateMultiPartyContractAsync(tenantId, req, ct);
            return Results.Ok(res);
        })
        .WithName("CreateMultiPartyContract")
        .WithSummary("Create contract with multi-party sequential/parallel signers and visual PDF field coordinates");

        group.MapPost("/{id:guid}/workflow/start", async (Guid tenantId, Guid id, IMultiPartySigningService service, CancellationToken ct) =>
        {
            var res = await service.InitiateWorkflowAsync(tenantId, id, ct);
            return Results.Ok(res);
        })
        .WithName("InitiateSigningWorkflow")
        .WithSummary("Start sequential or parallel multi-signer invitation workflow");

        group.MapGet("/{id:guid}/status", async (Guid id, IMultiPartySigningService service, CancellationToken ct) =>
        {
            var res = await service.GetContractStatusAsync(id, ct);
            return Results.Ok(res);
        })
        .WithName("GetMultiPartyContractStatus")
        .WithSummary("Get contract execution status, signers audit trail, and field values");

        group.MapGet("/signing/{token}", async (string token, IMultiPartySigningService service, CancellationToken ct) =>
        {
            var res = await service.GetContractBySigningTokenAsync(token, ct);
            return res != null ? Results.Ok(res) : Results.NotFound();
        })
        .WithName("GetContractBySigningToken")
        .WithSummary("Retrieve contract details and mark viewed for a specific signer token");

        group.MapPost("/signing/{token}/otp/send", async (string token, SendOtpEndpointRequest req, IMultiPartySigningService service, CancellationToken ct) =>
        {
            var res = await service.SendSignerOtpAsync(token, req.Destination, ct);
            return Results.Ok(res);
        })
        .WithName("SendSignerOtp")
        .WithSummary("Generate and transmit 6-digit cryptographic verification code to signer");

        group.MapPost("/signing/{token}/otp/verify", async (string token, VerifyOtpEndpointRequest req, IMultiPartySigningService service, CancellationToken ct) =>
        {
            var res = await service.VerifySignerOtpAsync(token, req.OtpCode, ct);
            return Results.Ok(res);
        })
        .WithName("VerifySignerOtp")
        .WithSummary("Verify signer OTP code before allowing document review/signing");

        group.MapPost("/signing/{token}/sign", async (string token, SGrowing.Api.Modules.CRM.ContractSigning.SubmitSignatureRequest req, IMultiPartySigningService service, CancellationToken ct) =>
        {
            var res = await service.SubmitSignerSignatureAsync(token, req, ct);
            return Results.Ok(res);
        })
        .WithName("SubmitSignerSignature")
        .WithSummary("Submit signature and completed PDF coordinate fields, advancing signing workflow");

        return app;
    }
}
