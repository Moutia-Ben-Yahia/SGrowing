using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace SGrowing.Tests.AI;

public class McpJsonRpcProtocolTests
{
    private readonly McpToolService _mcpService = new();

    [Fact]
    public async Task HandleJsonRpc_Initialize_ShouldReturnServerCapabilities()
    {
        // Arrange
        const string request = """
        {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {}
            }
        }
        """;

        // Act
        var responseJson = await _mcpService.HandleJsonRpcAsync(request);

        // Assert
        responseJson.Should().NotBeNullOrEmpty();
        var node = JsonNode.Parse(responseJson);
        node.Should().NotBeNull();
        node!["id"]?.GetValue<int>().Should().Be(1);
        node["result"]?["protocolVersion"]?.GetValue<string>().Should().Be("2024-11-05");
        node["result"]?["serverInfo"]?["name"]?.GetValue<string>().Should().Be("SGrowing.McpServer");
    }

    [Fact]
    public async Task HandleJsonRpc_ToolsList_ShouldReturnAllFiveTools()
    {
        // Arrange
        const string request = """
        {
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/list"
        }
        """;

        // Act
        var responseJson = await _mcpService.HandleJsonRpcAsync(request);

        // Assert
        var node = JsonNode.Parse(responseJson);
        node.Should().NotBeNull();
        node!["id"]?.GetValue<int>().Should().Be(2);

        var toolsArray = node["result"]?["tools"]?.AsArray();
        toolsArray.Should().NotBeNull();
        toolsArray!.Count.Should().Be(5);

        var names = toolsArray.Select(t => t?["name"]?.GetValue<string>()).ToList();
        names.Should().Contain("crm_search_leads");
        names.Should().Contain("outbound_send_spintax_campaign");
        names.Should().Contain("deliverability_run_placement_test");
        names.Should().Contain("contracts_dispatch_for_signature");
        names.Should().Contain("courier_generate_waybill");
    }

    [Fact]
    public async Task HandleJsonRpc_ToolsCall_CourierWaybill_ShouldExecuteSuccessfully()
    {
        // Arrange
        const string request = """
        {
            "jsonrpc": "2.0",
            "id": 3,
            "method": "tools/call",
            "params": {
                "name": "courier_generate_waybill",
                "arguments": {
                    "orderId": "ORD-E2E-9999",
                    "recipientName": "Sarra Ben Ahmed",
                    "recipientPhone": "+21622334455",
                    "destinationWilaya": "Ariana",
                    "codAmount": 210.5
                }
            }
        }
        """;

        // Act
        var responseJson = await _mcpService.HandleJsonRpcAsync(request);

        // Assert
        var node = JsonNode.Parse(responseJson);
        node.Should().NotBeNull();
        node!["id"]?.GetValue<int>().Should().Be(3);
        node["result"]?["isError"]?.GetValue<bool>().Should().BeFalse();

        var contentText = node["result"]?["content"]?[0]?["text"]?.GetValue<string>();
        contentText.Should().NotBeNullOrEmpty();
        contentText.Should().Contain("WB-TN-");
        contentText.Should().Contain("ORD-E2E-9999");
    }

    [Fact]
    public async Task HandleJsonRpc_UnknownMethod_ShouldReturnMethodNotFound()
    {
        // Arrange
        const string request = """
        {
            "jsonrpc": "2.0",
            "id": 4,
            "method": "non_existent_method"
        }
        """;

        // Act
        var responseJson = await _mcpService.HandleJsonRpcAsync(request);

        // Assert
        var node = JsonNode.Parse(responseJson);
        node.Should().NotBeNull();
        node!["error"]?["code"]?.GetValue<int>().Should().Be(-32601);
        node["error"]?["message"]?.GetValue<string>().Should().Contain("Method not found");
    }

    [Fact]
    public async Task HandleJsonRpc_InvalidJson_ShouldReturnParseError()
    {
        // Arrange
        const string request = "{ not valid json ...";

        // Act
        var responseJson = await _mcpService.HandleJsonRpcAsync(request);

        // Assert
        var node = JsonNode.Parse(responseJson);
        node.Should().NotBeNull();
        node!["error"]?["code"]?.GetValue<int>().Should().Be(-32603);
    }
    
    [Fact]
    public async Task HandleJsonRpc_ToolsCall_UnimplementedTool_ShouldReturnIsErrorTrue()
    {
        // Arrange
        const string request = """
        {
            "jsonrpc": "2.0",
            "id": 5,
            "method": "tools/call",
            "params": {
                "name": "unsupported_tool",
                "arguments": {}
            }
        }
        """;

        // Act
        var responseJson = await _mcpService.HandleJsonRpcAsync(request);

        // Assert
        var node = JsonNode.Parse(responseJson);
        node.Should().NotBeNull();
        node!["id"]?.GetValue<int>().Should().Be(5);
        node["result"]?["isError"]?.GetValue<bool>().Should().BeTrue();
    }
}
