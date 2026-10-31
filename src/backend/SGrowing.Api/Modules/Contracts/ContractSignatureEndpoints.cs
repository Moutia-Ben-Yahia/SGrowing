using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Contracts;

public static class ContractSignatureEndpoints
{
    public static IEndpointRouteBuilder MapContractSignatureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/contracts/e-sign")
            .WithTags("Interactive Multi-Signer Contract E-Signature Engine");

        // 1. List all contracts with signing status
        group.MapGet("/", async (IContractElectronicSignatureService service) =>
        {
            var contracts = await service.GetAllContractsAsync();
            return Results.Ok(contracts);
        })
        .WithName("ListContracts")
        .WithSummary("List all multi-signer contracts and their execution state");

        // 2. Get contract detail with fields and signers
        group.MapGet("/{id:guid}", async (Guid id, IContractElectronicSignatureService service) =>
        {
            var contract = await service.GetContractByIdAsync(id);
            if (contract == null)
            {
                return Results.NotFound(new { error = $"Contract {id} not found." });
            }
            return Results.Ok(contract);
        })
        .WithName("GetContractDetail")
        .WithSummary("Get full contract envelope with pages, signers, fields, and audit log");

        // Lookup by token
        group.MapGet("/token/{token}", async (string token, IContractElectronicSignatureService service) =>
        {
            var contract = await service.GetContractBySignerTokenAsync(token);
            if (contract == null)
            {
                return Results.NotFound(new { error = "No contract found matching the provided signer authorization token." });
            }
            return Results.Ok(contract);
        })
        .WithName("GetContractByToken")
        .WithSummary("Get contract associated with a private signer execution token");

        // 3. Create a new multi-signer contract with fields
        group.MapPost("/", async (
            [FromBody] CreateContractRequest request,
            IContractElectronicSignatureService service) =>
        {
            try
            {
                var created = await service.CreateContractAsync(request);
                return Results.Created($"/api/v1/contracts/e-sign/{created.Id}", created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateContract")
        .WithSummary("Create a new multi-signer agreement with sequential signers and visual fields");

        // 4. Submit a digital signature for a specific signer token
        group.MapPost("/{id:guid}/sign", async (
            Guid id,
            [FromBody] SubmitSignatureRequest request,
            HttpContext context,
            IContractElectronicSignatureService service) =>
        {
            var resolvedIp = !string.IsNullOrWhiteSpace(request.IpAddress)
                ? request.IpAddress
                : context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var resolvedUserAgent = !string.IsNullOrWhiteSpace(request.UserAgent)
                ? request.UserAgent
                : context.Request.Headers.UserAgent.ToString();

            var enrichedRequest = request with
            {
                IpAddress = resolvedIp,
                UserAgent = resolvedUserAgent
            };

            var result = await service.SubmitSignatureAsync(id, enrichedRequest);
            if (!result.Success)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        })
        .WithName("SubmitSignature")
        .WithSummary("Submit a digital signature (typed or drawn vector) for the active sequential signer");

        // 5. Get the cryptographic completion audit certificate
        group.MapGet("/{id:guid}/certificate", async (Guid id, IContractElectronicSignatureService service) =>
        {
            var cert = await service.GetAuditCertificateAsync(id);
            if (cert == null)
            {
                return Results.NotFound(new { error = $"Contract {id} not found." });
            }
            return Results.Ok(cert);
        })
        .WithName("GetAuditCertificate")
        .WithSummary("Retrieve the cryptographic SHA-256 completion audit certificate");

        // 6. Decline to sign with reason
        group.MapPost("/{id:guid}/decline", async (
            Guid id,
            [FromBody] DeclineSignatureRequest request,
            HttpContext context,
            IContractElectronicSignatureService service) =>
        {
            var resolvedIp = !string.IsNullOrWhiteSpace(request.IpAddress)
                ? request.IpAddress
                : context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var resolvedUserAgent = !string.IsNullOrWhiteSpace(request.UserAgent)
                ? request.UserAgent
                : context.Request.Headers.UserAgent.ToString();

            var enrichedRequest = request with
            {
                IpAddress = resolvedIp,
                UserAgent = resolvedUserAgent
            };

            var result = await service.DeclineSignatureAsync(id, enrichedRequest);
            if (!result.Success)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        })
        .WithName("DeclineSignature")
        .WithSummary("Decline to sign contract and provide audit reason");

        // 7. Verify SHA-256 document tamper status
        group.MapPost("/{id:guid}/verify-integrity", async (Guid id, IContractElectronicSignatureService service) =>
        {
            var result = await service.VerifyDocumentIntegrityAsync(id);
            return Results.Ok(result);
        })
        .WithName("VerifyDocumentIntegrity")
        .WithSummary("Verify SHA-256 cryptographic seal against document, signature, and audit log tamper");

        return app;
    }
}
