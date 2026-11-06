using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SGrowing.McpServer.Services;
using Xunit;

namespace SGrowing.Tests.Unit;

public class McpEnterpriseToolsTests
{
    private readonly McpToolService _mcpService = new();

    [Fact]
    public void GetTools_ContainsAllEnterpriseTools()
    {
        var tools = _mcpService.GetTools();

        Assert.Contains(tools, t => t.Name == "analytics_get_realtime_roas");
        Assert.Contains(tools, t => t.Name == "analytics_get_funnel_conversion");
        Assert.Contains(tools, t => t.Name == "courier_reconcile_remittance");
        Assert.Contains(tools, t => t.Name == "campaign_audit_and_rebalance");
        Assert.Contains(tools, t => t.Name == "document_vault_verify_seal");
        Assert.Contains(tools, t => t.Name == "document_mirror_to_s3");
        Assert.Contains(tools, t => t.Name == "seo_audit_geo_aeo_citability");
        Assert.Contains(tools, t => t.Name == "content_generate_brief");
        Assert.Contains(tools, t => t.Name == "content_grade_draft");
    }

    [Fact]
    public async Task HandleJsonRpcAsync_AnalyticsRealtimeRoas_ReturnsValidResult()
    {
        var request = """
        {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "tools/call",
            "params": {
                "name": "analytics_get_realtime_roas",
                "arguments": {
                    "channel": "meta",
                    "daysBack": 14
                }
            }
        }
        """;

        var responseJson = await _mcpService.HandleJsonRpcAsync(request);
        using var doc = JsonDocument.Parse(responseJson);

        var result = doc.RootElement.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());

        var contentArray = result.GetProperty("content");
        var text = contentArray[0].GetProperty("text").GetString()!;

        using var innerDoc = JsonDocument.Parse(text);
        Assert.True(innerDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(innerDoc.RootElement.TryGetProperty("roas", out _));
        Assert.True(innerDoc.RootElement.TryGetProperty("totalSpend", out _));
        Assert.True(innerDoc.RootElement.TryGetProperty("totalRevenue", out _));
    }

    [Fact]
    public async Task HandleJsonRpcAsync_CourierReconcileRemittance_ReturnsSettlement()
    {
        var request = """
        {
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/call",
            "params": {
                "name": "courier_reconcile_remittance",
                "arguments": {
                    "courierProvider": "Yalidine",
                    "statementId": "STMT-2026-TEST",
                    "declaredGross": 25000.0,
                    "declaredFees": 3750.0
                }
            }
        }
        """;

        var responseJson = await _mcpService.HandleJsonRpcAsync(request);
        using var doc = JsonDocument.Parse(responseJson);

        var result = doc.RootElement.GetProperty("result");
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;

        using var innerDoc = JsonDocument.Parse(text);
        Assert.True(innerDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("STMT-2026-TEST", innerDoc.RootElement.GetProperty("statementId").GetString());
        Assert.Equal("Yalidine", innerDoc.RootElement.GetProperty("courierProvider").GetString());
        Assert.Equal(21250.0, innerDoc.RootElement.GetProperty("netPayable").GetDouble(), 1);
    }

    [Fact]
    public async Task HandleJsonRpcAsync_DocumentVerifySeal_ReturnsVerification()
    {
        var request = """
        {
            "jsonrpc": "2.0",
            "id": 3,
            "method": "tools/call",
            "params": {
                "name": "document_vault_verify_seal",
                "arguments": {
                    "sealId": "00000000-0000-0000-0000-000000000001"
                }
            }
        }
        """;

        var responseJson = await _mcpService.HandleJsonRpcAsync(request);
        using var doc = JsonDocument.Parse(responseJson);

        var result = doc.RootElement.GetProperty("result");
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;

        using var innerDoc = JsonDocument.Parse(text);
        Assert.True(innerDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(innerDoc.RootElement.GetProperty("isTamperEvident").GetBoolean());
    }

    [Fact]
    public async Task HandleJsonRpcAsync_SeoAuditGeoCitability_ReturnsValidResult()
    {
        var request = """
        {
            "jsonrpc": "2.0",
            "id": 4,
            "method": "tools/call",
            "params": {
                "name": "seo_audit_geo_aeo_citability",
                "arguments": {
                    "targetQuery": "What is SGrowing OS?",
                    "content": "SGrowing is an autonomous operating system that provides CRM and SEO features."
                }
            }
        }
        """;

        var responseJson = await _mcpService.HandleJsonRpcAsync(request);
        using var doc = JsonDocument.Parse(responseJson);

        var result = doc.RootElement.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        using var innerDoc = JsonDocument.Parse(text);
        Assert.True(innerDoc.RootElement.GetProperty("overallCitabilityScore").GetDouble() > 0);
    }

    [Fact]
    public async Task HandleJsonRpcAsync_ContentGenerateBrief_ReturnsValidResult()
    {
        var request = """
        {
            "jsonrpc": "2.0",
            "id": 5,
            "method": "tools/call",
            "params": {
                "name": "content_generate_brief",
                "arguments": {
                    "primaryKeyword": "dynamic pricing optimization",
                    "desiredWordCount": 1800
                }
            }
        }
        """;

        var responseJson = await _mcpService.HandleJsonRpcAsync(request);
        using var doc = JsonDocument.Parse(responseJson);

        var result = doc.RootElement.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        using var innerDoc = JsonDocument.Parse(text);
        Assert.Equal("dynamic pricing optimization", innerDoc.RootElement.GetProperty("primaryKeyword").GetString());
        Assert.Equal(1800, innerDoc.RootElement.GetProperty("targetWordCountMin").GetInt32());
    }

    [Fact]
    public async Task HandleJsonRpcAsync_ContentGradeDraft_ReturnsValidResult()
    {
        var request = """
        {
            "jsonrpc": "2.0",
            "id": 6,
            "method": "tools/call",
            "params": {
                "name": "content_grade_draft",
                "arguments": {
                    "primaryKeyword": "revenue analytics",
                    "draftText": "Revenue analytics is the systematic computational analysis of business revenue streams."
                }
            }
        }
        """;

        var responseJson = await _mcpService.HandleJsonRpcAsync(request);
        using var doc = JsonDocument.Parse(responseJson);

        var result = doc.RootElement.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        using var innerDoc = JsonDocument.Parse(text);
        Assert.True(innerDoc.RootElement.GetProperty("totalWordCount").GetInt32() > 0);
        Assert.NotNull(innerDoc.RootElement.GetProperty("grade").GetString());
    }
}
