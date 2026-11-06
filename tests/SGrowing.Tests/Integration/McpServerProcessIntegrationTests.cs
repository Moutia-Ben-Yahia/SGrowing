using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace SGrowing.Tests.Integration;

public class McpServerProcessIntegrationTests
{
    [Fact]
    public async Task McpServer_StdioTransport_ExecutesHandshakeAndToolCallsCleanly()
    {
        // 1. Locate the McpServer binary
        var baseDir = AppContext.BaseDirectory;
        // Typical path: tests/SGrowing.Tests/bin/Debug/net10.0
        var solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var binaryPath = Path.Combine(solutionRoot, "src", "backend", "SGrowing.McpServer", "bin", "Debug", "net10.0", "SGrowing.McpServer.exe");

        if (!File.Exists(binaryPath))
        {
            // Fallback for dll on non-Windows or direct runner
            binaryPath = Path.Combine(solutionRoot, "src", "backend", "SGrowing.McpServer", "bin", "Debug", "net10.0", "SGrowing.McpServer.dll");
        }

        var isDll = binaryPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo
        {
            FileName = isDll ? "dotnet" : binaryPath,
            Arguments = isDll ? $"\"{binaryPath}\" --stdio" : "--stdio",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start().Should().BeTrue();

        using var writer = process.StandardInput;
        using var reader = process.StandardOutput;

        // 2. Send initialize
        await writer.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}""");
        await writer.FlushAsync();

        var initLine = await reader.ReadLineAsync();
        initLine.Should().NotBeNullOrEmpty();
        var initJson = JsonNode.Parse(initLine!);
        initJson!["id"]?.GetValue<int>().Should().Be(1);
        initJson["result"]?["serverInfo"]?["name"]?.GetValue<string>().Should().Be("SGrowing.McpServer");

        // 3. Send notifications/initialized (no response expected)
        await writer.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        await writer.FlushAsync();

        // 4. Send ping
        await writer.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"ping"}""");
        await writer.FlushAsync();

        var pingLine = await reader.ReadLineAsync();
        pingLine.Should().NotBeNullOrEmpty();
        var pingJson = JsonNode.Parse(pingLine!);
        pingJson!["id"]?.GetValue<int>().Should().Be(2);

        // 5. Send tools/list
        await writer.WriteLineAsync("""{"jsonrpc":"2.0","id":3,"method":"tools/list"}""");
        await writer.FlushAsync();

        var toolsLine = await reader.ReadLineAsync();
        toolsLine.Should().NotBeNullOrEmpty();
        var toolsJson = JsonNode.Parse(toolsLine!);
        var tools = toolsJson!["result"]?["tools"]?.AsArray();
        tools.Should().NotBeNull();
        tools!.Count.Should().Be(20);

        // 6. Send tools/call for analytics_get_realtime_roas
        await writer.WriteLineAsync("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"analytics_get_realtime_roas","arguments":{"channel":"meta","daysBack":7}}}""");
        await writer.FlushAsync();

        var callLine = await reader.ReadLineAsync();
        callLine.Should().NotBeNullOrEmpty();
        var callJson = JsonNode.Parse(callLine!);
        callJson!["id"]?.GetValue<int>().Should().Be(4);
        callJson["result"]?["isError"]?.GetValue<bool>().Should().BeFalse();
        var text = callJson["result"]?["content"]?[0]?["text"]?.GetValue<string>();
        text.Should().Contain("meta");
        text.Should().Contain("roas");

        // 7. Close stdin to signal graceful exit
        writer.Close();

        await process.WaitForExitAsync();
        process.HasExited.Should().BeTrue();
        process.ExitCode.Should().Be(0);
    }
}
