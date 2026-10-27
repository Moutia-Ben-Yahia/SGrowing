using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Modules.Outbound.EmailVerification;
using SGrowing.Api.Modules.Outbound.Enrichment;
using SGrowing.Api.Modules.Outbound.Spintax;

namespace SGrowing.Api.Modules.Outbound;

public record SpintaxPreviewRequest(string Template, string? ProspectIdOrEmail = null, Dictionary<string, string>? Variables = null, int SampleCount = 3);
public record SpintaxPreviewResponse(string DeterministicVariant, List<string> SampleVariants, long TotalPermutations);
public record VerifyEmailRequest(string? Email);

public static class OutboundPillarEndpoints
{
    public static IEndpointRouteBuilder MapOutboundPillarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/outbound")
            .WithTags("Outbound & AI SDR");

        group.MapPost("/spintax/preview", (SpintaxPreviewRequest req, ISpintaxService spintax) =>
        {
            var deterministic = !string.IsNullOrEmpty(req.ProspectIdOrEmail)
                ? spintax.ParseForProspect(req.Template, req.ProspectIdOrEmail, req.Variables)
                : spintax.Parse(req.Template, variables: req.Variables);

            var samples = spintax.GenerateVariants(req.Template, req.SampleCount, req.Variables).ToList();
            var total = spintax.CalculatePermutationsCount(req.Template);

            return Results.Ok(new SpintaxPreviewResponse(deterministic, samples, total));
        })
        .WithName("PreviewSpintax")
        .WithSummary("Generate deterministic variants and calculate permutations for nested spintax templates");

        group.MapPost("/email/verify", async (HttpContext ctx, IEmailVerificationService verifier, CancellationToken ct) =>
        {
            string email = string.Empty;
            if (ctx.Request.Query.TryGetValue("email", out var qVal) && !string.IsNullOrWhiteSpace(qVal))
            {
                email = qVal.ToString();
            }
            else if (ctx.Request.HasJsonContentType())
            {
                var body = await ctx.Request.ReadFromJsonAsync<VerifyEmailRequest>(cancellationToken: ct);
                email = body?.Email ?? string.Empty;
            }

            var res = await verifier.VerifyEmailAsync(email, ct);
            return Results.Ok(res);
        })
        .WithName("VerifyEmailDeliverability")
        .WithSummary("Real-time RFC 5322, MX, disposable, and SMTP handshake verification");

        group.MapPost("/contacts/enrich-and-verify", async (Guid tenantId, ContactEnrichmentRequest request, IContactEnrichmentPipeline pipeline, CancellationToken ct) =>
        {
            var result = await pipeline.EnrichAndVerifyContactAsync(tenantId, request, ct);
            return Results.Ok(result);
        })
        .WithName("EnrichAndVerifyContact")
        .WithSummary("Full contact data enrichment pipeline combining firmographics and deliverability verification");

        return app;
    }
}
