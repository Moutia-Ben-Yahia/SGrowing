using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.VerifiableCredentials;

public static class VerifiableCredentialsEndpoints
{
    public static IServiceCollection AddVerifiableCredentialsServices(this IServiceCollection services)
    {
        services.AddSingleton<DidDocumentResolverEngine>();
        services.AddSingleton<VerifiableCredentialIssuerAndValidator>();
        services.AddSingleton<VendorComplianceVerificationGateway>();
        return services;
    }

    public static IEndpointRouteBuilder MapVerifiableCredentialsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/verifiable-credentials")
            .WithTags("Decentralized Identity (W3C DID) & Verifiable Credentials (VC)");

        group.MapPost("/did/generate", (DidDocumentResolverEngine resolver) =>
        {
            var (did, privKey, pubKey) = resolver.GenerateDidKey();
            return Results.Ok(new
            {
                Did = did,
                PrivateKeyDerHex = Convert.ToHexString(privKey).ToLowerInvariant(),
                PublicKeyDerHex = Convert.ToHexString(pubKey).ToLowerInvariant()
            });
        });

        group.MapGet("/did/resolve/{did}", (string did, DidDocumentResolverEngine resolver) =>
        {
            try
            {
                var doc = resolver.Resolve(did);
                return Results.Ok(doc);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        group.MapPost("/credentials/issue", (
            IssueCredentialRequest request,
            VerifiableCredentialIssuerAndValidator issuer) =>
        {
            var vc = issuer.IssueCredential(request);
            return Results.Ok(vc);
        });

        group.MapPost("/credentials/verify", (
            VerifiableCredential credential,
            VerifiableCredentialIssuerAndValidator validator) =>
        {
            var result = validator.VerifyCredential(credential);
            return Results.Ok(result);
        });

        group.MapPost("/compliance/assess", (
            AssessComplianceApiRequest request,
            VendorComplianceVerificationGateway gateway) =>
        {
            var report = gateway.AssessVendorCompliance(
                request.VendorDid,
                request.VendorLegalName,
                request.RequiredCertifications,
                request.PresentedCredentials);

            return Results.Ok(report);
        });

        return app;
    }
}

public sealed record AssessComplianceApiRequest(
    string VendorDid,
    string VendorLegalName,
    IReadOnlyList<CredentialComplianceType> RequiredCertifications,
    IReadOnlyList<VerifiableCredential> PresentedCredentials
);
