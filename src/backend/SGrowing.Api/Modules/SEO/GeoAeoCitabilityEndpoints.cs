using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.SEO;

public class GeoAeoCitabilityEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/seo/geo")
            .WithTags("Generative Engine Optimization (GEO/AEO)");

        group.MapPost("/audit-citability", ([FromBody] GeoCitabilityAuditRequest request, IGeoAeoCitabilityService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(new { error = "Content is required for citability audit." });
            }

            var result = service.AuditCitability(request);
            return Results.Ok(result);
        })
        .WithName("AuditGeoCitability")
        .WithSummary("Audits text for SearchGPT, Perplexity, and Google AI Overviews citability metrics.");

        group.MapPost("/generate-llms-txt", ([FromBody] LlmsTxtGenerationRequest request, IGeoAeoCitabilityService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.BrandName) || string.IsNullOrWhiteSpace(request.PrimaryDomain))
            {
                return Results.BadRequest(new { error = "BrandName and PrimaryDomain are required." });
            }

            var result = service.GenerateLlmsTxt(request);
            return Results.Ok(result);
        })
        .WithName("GenerateLlmsTxt")
        .WithSummary("Generates standard /llms.txt and /llms-full.txt discovery files for AI crawler agents.");

        // Root /llms.txt endpoint for AI agents
        app.MapGet("/llms.txt", (IGeoAeoCitabilityService service) =>
        {
            var req = new LlmsTxtGenerationRequest(
                BrandName: "SGrowing",
                BrandTagline: "The AI-Native Growth Operating System for High-Velocity Modern Teams",
                PrimaryDomain: "https://sgrowing.io",
                CoreCapabilities: new List<string>
                {
                    "Autonomous Multi-Agent Swarms with Cryptographic HITL Approval Gates",
                    "Partitioned Event Streaming Mesh & ClickHouse OLAP Sub-Second Analytics",
                    "Omnichannel Inbound & Outbound Communication (WhatsApp, SMS, Twilio WebRTC)",
                    "ISO 32000-1 PAdES-B-LTV Digital Contract Vault with S3 Cold Storage",
                    "Multi-Tenant PostgreSQL 17 with 12 Schemas and Kernel Row-Level Security"
                },
                KeySections: new List<LlmsSectionDto>
                {
                    new("API Documentation", "/scalar/v1", "Complete OpenAPI Scalar interactive documentation"),
                    new("MCP Server", "/mcp", "17 Enterprise Model Context Protocol tools for Claude Desktop & Cursor"),
                    new("Architecture Runbook", "/docs/runbook", "Production operations and disaster recovery procedures")
                }
            );

            var result = service.GenerateLlmsTxt(req);
            return Results.Text(result.LlmsTxt, "text/plain");
        })
        .WithName("GetPlatformLlmsTxt")
        .WithSummary("Serves standard /llms.txt for AI agents and LLM crawlers");
    }
}
