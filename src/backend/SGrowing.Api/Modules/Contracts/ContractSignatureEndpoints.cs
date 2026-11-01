using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Contracts;

public static class ContractSignatureEndpoints
{
    public static IEndpointRouteBuilder MapContractSignatureEndpoints(this IEndpointRouteBuilder app)
    {
        // -------------------------------------------------------------
        // Group A: Legacy / Direct E-Sign Interactive Endpoints
        // -------------------------------------------------------------
        var esignGroup = app.MapGroup("/api/v1/contracts/e-sign")
            .WithTags("Interactive Multi-Signer Contract E-Signature Engine");

        // 1. List all contracts with signing status
        esignGroup.MapGet("/", async (IContractElectronicSignatureService service) =>
        {
            var contracts = await service.GetAllContractsAsync();
            return Results.Ok(contracts);
        })
        .WithName("ListContracts")
        .WithSummary("List all multi-signer contracts and their execution state");

        // 2. Get contract detail with fields and signers
        esignGroup.MapGet("/{id:guid}", async (Guid id, IContractElectronicSignatureService service) =>
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
        esignGroup.MapGet("/token/{token}", async (string token, IContractElectronicSignatureService service) =>
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
        esignGroup.MapPost("/", async (
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
        esignGroup.MapPost("/{id:guid}/sign", async (
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
        esignGroup.MapGet("/{id:guid}/certificate", async (Guid id, IContractElectronicSignatureService service) =>
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
        esignGroup.MapPost("/{id:guid}/decline", async (
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
        esignGroup.MapPost("/{id:guid}/verify-integrity", async (Guid id, IContractElectronicSignatureService service) =>
        {
            var result = await service.VerifyDocumentIntegrityAsync(id);
            return Results.Ok(result);
        })
        .WithName("VerifyDocumentIntegrity")
        .WithSummary("Verify SHA-256 cryptographic seal against document, signature, and audit log tamper");

        // -------------------------------------------------------------
        // Group B: Multi-Party Signing Workflow Engine
        // -------------------------------------------------------------
        var workflowGroup = app.MapGroup("/api/v1/contracts/workflow")
            .WithTags("Multi-Party Signing Workflow Engine");

        // POST /api/v1/contracts/workflow/create-multiparty
        workflowGroup.MapPost("/create-multiparty", async (
            [FromBody] CreateMultiPartyWorkflowRequest request,
            IMultiPartySigningWorkflowService service) =>
        {
            try
            {
                var workflow = await service.CreateWorkflowAsync(request);
                return Results.Created($"/api/v1/contracts/workflow/{workflow.Id}", workflow);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateMultiPartyWorkflow")
        .WithSummary("Create a multi-party signing workflow with Sequential or Parallel order and HMAC-SHA256 tokens");

        // POST /api/v1/contracts/workflow/submit-signature
        workflowGroup.MapPost("/submit-signature", async (
            [FromBody] SubmitWorkflowSignatureApiRequest apiRequest,
            HttpContext context,
            IMultiPartySigningWorkflowService service) =>
        {
            var resolvedIp = !string.IsNullOrWhiteSpace(apiRequest.IpAddress)
                ? apiRequest.IpAddress
                : context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var resolvedUserAgent = !string.IsNullOrWhiteSpace(apiRequest.UserAgent)
                ? apiRequest.UserAgent
                : context.Request.Headers.UserAgent.ToString();

            var req = new SubmitMultiPartySignatureRequest(
                apiRequest.SignerToken,
                apiRequest.SignatureType,
                apiRequest.SignatureData,
                apiRequest.IsPartialSignature,
                resolvedIp,
                resolvedUserAgent,
                apiRequest.Geolocation
            );

            var result = await service.SubmitSignatureAsync(apiRequest.TenantId, apiRequest.ContractId, req);
            if (!result.Success)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        })
        .WithName("SubmitMultiPartySignature")
        .WithSummary("Submit digital signature for multi-party sequential or parallel workflow");

        // GET /api/v1/contracts/workflow/{contractId}
        workflowGroup.MapGet("/{contractId:guid}", async (
            Guid contractId,
            [FromQuery] Guid? tenantId,
            IMultiPartySigningWorkflowService service) =>
        {
            var effectiveTenantId = tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var workflow = await service.GetWorkflowAsync(effectiveTenantId, contractId);
            if (workflow == null)
            {
                return Results.NotFound(new { error = $"Workflow {contractId} not found." });
            }
            return Results.Ok(workflow);
        })
        .WithName("GetMultiPartyWorkflow")
        .WithSummary("Get multi-party signing envelope status, signers, and audit log");

        // -------------------------------------------------------------
        // Group C: Cryptographic Document Sealing & Merkle Verification
        // -------------------------------------------------------------
        var sealingGroup = app.MapGroup("/api/v1/contracts/sealing")
            .WithTags("Cryptographic Document Sealing & Integrity");

        // POST /api/v1/contracts/sealing/seal
        sealingGroup.MapPost("/seal", async (
            [FromBody] SealContractApiRequest request,
            ICryptographicDocumentSealingService service) =>
        {
            try
            {
                var certificate = await service.SealContractAsync(request.TenantId, request.ContractId, request.Proofs);
                return Results.Ok(certificate);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SealContract")
        .WithSummary("Seal completed contract with Merkle tree, SHA-256 leaves, and Ed25519/ECDsa asymmetric signature");

        // GET /api/v1/contracts/sealing/verify/{contractId}
        sealingGroup.MapGet("/verify/{contractId:guid}", async (
            Guid contractId,
            [FromQuery] Guid? tenantId,
            [FromQuery] string? expectedHash,
            ICryptographicDocumentSealingService service) =>
        {
            var effectiveTenantId = tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var cert = await service.GetCertificateAsync(effectiveTenantId, contractId);

            if (cert == null)
            {
                return Results.NotFound(new { error = $"No cryptographic seal record found for contract {contractId}." });
            }

            var hashToVerify = !string.IsNullOrWhiteSpace(expectedHash) ? expectedHash : cert.DocumentSha256;
            var docBytes = System.Text.Encoding.UTF8.GetBytes($"DOC:{effectiveTenantId}:{contractId}:{cert.DocumentTitle}:{cert.MerkleRootHash}");
            var verification = await service.VerifyContractIntegrityDetailedAsync(effectiveTenantId, contractId, docBytes, hashToVerify);

            return Results.Ok(new
            {
                Certificate = cert,
                Verification = verification
            });
        })
        .WithName("VerifyContractSealing")
        .WithSummary("Cryptographically verify contract execution certificate and Merkle root integrity");

        // -------------------------------------------------------------
        // Group D: Document Viewer Telemetry & Engagement Heatmaps
        // -------------------------------------------------------------
        var telemetryGroup = app.MapGroup("/api/v1/contracts/telemetry")
            .WithTags("Document Viewer Telemetry & Engagement Heatmap");

        // POST /api/v1/contracts/telemetry/record-view
        telemetryGroup.MapPost("/record-view", async (
            [FromBody] RecordPageViewRequest request,
            HttpContext context,
            IDocumentViewerTelemetryAndHeatmapService service) =>
        {
            var resolvedIp = !string.IsNullOrWhiteSpace(request.ViewerIp)
                ? request.ViewerIp
                : context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var enriched = request with
            {
                ViewerIp = resolvedIp
            };

            var record = await service.RecordPageViewAsync(enriched);
            return Results.Ok(record);
        })
        .WithName("RecordContractPageView")
        .WithSummary("Record page-by-page viewing events (dwell time, scroll depth, zoom level, viewer IP)");

        // GET /api/v1/contracts/telemetry/heatmap/{contractId}
        telemetryGroup.MapGet("/heatmap/{contractId:guid}", async (
            Guid contractId,
            [FromQuery] Guid? tenantId,
            IDocumentViewerTelemetryAndHeatmapService service) =>
        {
            var effectiveTenantId = tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var heatmap = await service.GetContractHeatmapAsync(effectiveTenantId, contractId);
            return Results.Ok(heatmap);
        })
        .WithName("GetContractHeatmap")
        .WithSummary("Get aggregated page dwell time heatmap and attention share for sales intelligence");

        return app;
    }
}

public record SubmitWorkflowSignatureApiRequest(
    Guid TenantId,
    Guid ContractId,
    string SignerToken,
    string SignatureType,
    string SignatureData,
    bool IsPartialSignature = false,
    string? IpAddress = null,
    string? UserAgent = null,
    string? Geolocation = null
);

public record SealContractApiRequest(
    Guid TenantId,
    Guid ContractId,
    List<SignerExecutionProof> Proofs
);
