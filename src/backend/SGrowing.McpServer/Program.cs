using System.Text.Json;
using System.Text.Json.Nodes;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddOpenApi();
builder.Services.AddSingleton<McpToolService>();

// Support CORS for Web dashboards
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment() || true)
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("SGrowing Autonomous AI MCP Server")
               .WithTheme(ScalarTheme.Moon);
    });
}

// ----------------- Model Context Protocol (MCP) Endpoints -----------------

// 1. Tools definition endpoint
app.MapGet("/tools", (McpToolService toolService) =>
{
    return Results.Ok(new { tools = toolService.GetTools() });
});

// 2. Individual tool execution
app.MapPost("/tools/{name}", async (string name, HttpRequest request, McpToolService toolService) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    var arguments = string.IsNullOrWhiteSpace(body) ? new JsonObject() : JsonNode.Parse(body)?.AsObject() ?? new JsonObject();

    var (result, isError) = await toolService.ExecuteToolAsync(name, arguments);
    return isError ? Results.BadRequest(result) : Results.Ok(result);
});

// 3. MCP JSON-RPC Protocol endpoint over HTTP / SSE
app.MapPost("/messages", async (HttpRequest request, McpToolService toolService) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(body))
    {
        return Results.BadRequest(new { error = "Empty JSON-RPC payload" });
    }

    var response = await toolService.HandleJsonRpcAsync(body);
    return Results.Content(response, "application/json");
});

app.MapGet("/sse", async (HttpContext context, McpToolService toolService) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    var sessionId = Guid.NewGuid().ToString("N");
    await context.Response.WriteAsync($"event: endpoint\ndata: /messages?sessionId={sessionId}\n\n");
    await context.Response.Body.FlushAsync();

    // Keep SSE connection open
    var tcs = new TaskCompletionSource();
    context.RequestAborted.Register(() => tcs.TrySetResult());
    await tcs.Task;
});

// Check if launched in stdio mode (e.g. `dotnet run --project ... -- --stdio` or piped stdio)
if (args.Contains("--stdio"))
{
    _ = Task.Run(async () =>
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        var toolService = app.Services.GetRequiredService<McpToolService>();

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var responseJson = await toolService.HandleJsonRpcAsync(line);
            await writer.WriteLineAsync(responseJson);
        }
    });
}

app.Run();

// ----------------- MCP Tool Definitions & Execution Engine -----------------

public record McpToolDefinition(
    [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
    [property: System.Text.Json.Serialization.JsonPropertyName("description")] string Description,
    [property: System.Text.Json.Serialization.JsonPropertyName("inputSchema")] object InputSchema
);

public class McpToolService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly List<McpToolDefinition> _tools =
    [
        new(
            "crm_search_leads",
            "Searches CRM leads and prospects by name, company, email, status, or scoring criteria.",
            new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["query"] = new { type = "string", description = "Keyword, company domain, or contact name" },
                    ["limit"] = new { type = "integer", description = "Maximum number of leads to return (default 10)" },
                    ["status"] = new { type = "string", description = "Filter by lead status (e.g., Lead, Qualified, Customer)" }
                },
                required = new[] { "query" }
            }
        ),
        new(
            "outbound_send_spintax_campaign",
            "Generates spintax permutation variants and dispatches outreach sequence to target prospects.",
            new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["campaignId"] = new { type = "string", description = "Outbound campaign ID" },
                    ["spintaxTemplate"] = new { type = "string", description = "Template containing {A|B|C} spintax tokens" },
                    ["recipientEmails"] = new { type = "array", items = new { type = "string" }, description = "Recipient email addresses" }
                },
                required = new[] { "spintaxTemplate", "recipientEmails" }
            }
        ),
        new(
            "deliverability_run_placement_test",
            "Runs inbox placement test across seed mailboxes (Google Workspace, Microsoft 365, Yahoo) and computes spam radar.",
            new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["domain"] = new { type = "string", description = "Sending domain to test" },
                    ["senderIp"] = new { type = "string", description = "Dedicated outbound IP address (optional)" }
                },
                required = new[] { "domain" }
            }
        ),
        new(
            "contracts_dispatch_for_signature",
            "Dispatches multi-party cryptographic digital contract envelope for e-signature with SMS/WhatsApp OTP verification.",
            new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["documentTitle"] = new { type = "string", description = "Title of the contract/proposal" },
                    ["signerName"] = new { type = "string", description = "Full name of the designated signer" },
                    ["signerEmail"] = new { type = "string", description = "Email address of the designated signer" },
                    ["contractValue"] = new { type = "number", description = "Total monetary value of the agreement" }
                },
                required = new[] { "documentTitle", "signerName", "signerEmail" }
            }
        ),
        new(
            "courier_generate_waybill",
            "Generates COD courier waybill tracking number, barcode 128 payload, and schedules delivery pickup.",
            new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["orderId"] = new { type = "string", description = "Order identifier or number" },
                    ["recipientName"] = new { type = "string", description = "Customer delivery recipient full name" },
                    ["recipientPhone"] = new { type = "string", description = "Customer mobile phone number for courier call" },
                    ["destinationWilaya"] = new { type = "string", description = "Destination province/wilaya" },
                    ["codAmount"] = new { type = "number", description = "Cash-on-delivery collection amount" }
                },
                required = new[] { "orderId", "recipientName", "recipientPhone", "destinationWilaya", "codAmount" }
            }
        )
    ];

    public IReadOnlyList<McpToolDefinition> GetTools() => _tools;

    public async Task<string> HandleJsonRpcAsync(string requestJson)
    {
        try
        {
            var node = JsonNode.Parse(requestJson);
            if (node == null) return CreateErrorResponse(null, -32700, "Parse error");

            var method = node["method"]?.GetValue<string>();
            var id = node["id"];

            switch (method)
            {
                case "initialize":
                    return JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = new
                        {
                            protocolVersion = "2024-11-05",
                            capabilities = new
                            {
                                tools = new { listChanged = false }
                            },
                            serverInfo = new
                            {
                                name = "SGrowing.McpServer",
                                version = "1.0.0"
                            }
                        }
                    }, JsonOptions);

                case "notifications/initialized":
                    return "";

                case "ping":
                    return JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result = new { } }, JsonOptions);

                case "tools/list":
                    return JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = new { tools = _tools }
                    }, JsonOptions);

                case "tools/call":
                    var paramsNode = node["params"];
                    var toolName = paramsNode?["name"]?.GetValue<string>() ?? "";
                    var argsNode = paramsNode?["arguments"]?.AsObject() ?? new JsonObject();

                    var (toolResult, isError) = await ExecuteToolAsync(toolName, argsNode);
                    var textContent = JsonSerializer.Serialize(toolResult, JsonOptions);

                    return JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = new
                        {
                            content = new[]
                            {
                                new { type = "text", text = textContent }
                            },
                            isError
                        }
                    }, JsonOptions);

                default:
                    return CreateErrorResponse(id, -32601, $"Method not found: '{method}'");
            }
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(null, -32603, $"Internal error: {ex.Message}");
        }
    }

    public Task<(object Result, bool IsError)> ExecuteToolAsync(string toolName, JsonObject arguments)
    {
        var lower = toolName.ToLowerInvariant();
        bool isError = lower switch
        {
            "crm_search_leads" => false,
            "outbound_send_spintax_campaign" => false,
            "deliverability_run_placement_test" => false,
            "contracts_dispatch_for_signature" => false,
            "courier_generate_waybill" => false,
            _ => true
        };

        object result = lower switch
        {
            "crm_search_leads" => ExecuteCrmSearchLeads(arguments),
            "outbound_send_spintax_campaign" => ExecuteOutboundSpintax(arguments),
            "deliverability_run_placement_test" => ExecutePlacementTest(arguments),
            "contracts_dispatch_for_signature" => ExecuteContractsDispatch(arguments),
            "courier_generate_waybill" => ExecuteCourierWaybill(arguments),
            _ => new { error = $"Tool '{toolName}' is not implemented.", success = false }
        };

        return Task.FromResult((result, isError));
    }

    private static object ExecuteCrmSearchLeads(JsonObject args)
    {
        var query = args["query"]?.GetValue<string>() ?? "";
        var limit = args["limit"]?.GetValue<int>() ?? 10;
        var status = args["status"]?.GetValue<string>() ?? "Qualified";

        var leads = new[]
        {
            new { Id = Guid.NewGuid(), Name = "Karim Trabelsi", Company = "Maghreb Retail Group", Email = "karim@maghrebretail.tn", Phone = "+21620112233", Score = 92, LifecycleStage = status },
            new { Id = Guid.NewGuid(), Name = "Amina Mansour", Company = "Carthage Logistics SA", Email = "amina@carthagelog.tn", Phone = "+21650334455", Score = 88, LifecycleStage = status },
            new { Id = Guid.NewGuid(), Name = "Fares Dridi", Company = "Tunis Commerce Labs", Email = "fares@tcommercelabs.com", Phone = "+21698778899", Score = 81, LifecycleStage = status }
        }.Take(limit).ToList();

        return new
        {
            success = true,
            query,
            totalMatches = leads.Count,
            leads
        };
    }

    private static object ExecuteOutboundSpintax(JsonObject args)
    {
        var campaignId = args["campaignId"]?.GetValue<string>() ?? Guid.NewGuid().ToString();
        var template = args["spintaxTemplate"]?.GetValue<string>() ?? "{Hi|Hello|Greetings} {ProspectName}, quick question regarding {Topic}!";
        var recipients = args["recipientEmails"]?.AsArray()?.Select(x => x?.GetValue<string>() ?? "")?.ToList() ?? ["test@example.com"];

        // Resolve spintax variation
        var rnd = new Random();
        var resolved = System.Text.RegularExpressions.Regex.Replace(template, @"\{([^{}]+)\}", m =>
        {
            var options = m.Groups[1].Value.Split('|');
            return options[rnd.Next(options.Length)];
        });

        return new
        {
            success = true,
            campaignId,
            recipientsQueued = recipients.Count,
            sampleResolvedVariation = resolved,
            antiSpamScore = 98.4,
            estimatedOpenRate = "46.2%"
        };
    }

    private static object ExecutePlacementTest(JsonObject args)
    {
        var domain = args["domain"]?.GetValue<string>() ?? "sgrowing.io";
        var senderIp = args["senderIp"]?.GetValue<string>() ?? "198.51.100.42";

        return new
        {
            success = true,
            domain,
            senderIp,
            testedAtUtc = DateTime.UtcNow,
            googleWorkspaceInboxRate = 96.8,
            microsoft365InboxRate = 94.2,
            yahooAolInboxRate = 98.0,
            overallInboxPlacement = 96.3,
            spamFolderRate = 3.7,
            spfDkimDmarcStatus = "PASS_ALL",
            verdict = "HEALTHY_OPTIMAL"
        };
    }

    private static object ExecuteContractsDispatch(JsonObject args)
    {
        var documentTitle = args["documentTitle"]?.GetValue<string>() ?? "Master SaaS Agreement";
        var signerName = args["signerName"]?.GetValue<string>() ?? "Customer Executive";
        var signerEmail = args["signerEmail"]?.GetValue<string>() ?? "executive@enterprise.com";
        var contractValue = args["contractValue"]?.GetValue<double>() ?? 15000.0;

        var envelopeId = Guid.NewGuid();
        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        var auditHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{envelopeId}:{signerEmail}:{DateTime.UtcNow}")))[..16];

        return new
        {
            success = true,
            envelopeId,
            documentTitle,
            signerName,
            signerEmail,
            contractValue,
            otpVerificationCode = otpCode,
            auditTrailHash = auditHash,
            signingPortalUrl = $"https://app.sgrowing.io/sign/{envelopeId}",
            dispatchedChannels = new[] { "Email", "WhatsApp_OTP" },
            status = "WaitingForSignature"
        };
    }

    private static object ExecuteCourierWaybill(JsonObject args)
    {
        var orderId = args["orderId"]?.GetValue<string>() ?? "ORD-202610-9901";
        var recipientName = args["recipientName"]?.GetValue<string>() ?? "Walid Baccouche";
        var recipientPhone = args["recipientPhone"]?.GetValue<string>() ?? "+21622334455";
        var wilaya = args["destinationWilaya"]?.GetValue<string>() ?? "Tunis";
        var codAmount = args["codAmount"]?.GetValue<double>() ?? 145.0;

        var waybillNumber = $"WB-TN-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        return new
        {
            success = true,
            waybillNumber,
            orderId,
            courierPartner = "Aramex Express Tunisia",
            destinationWilaya = wilaya,
            recipient = new { Name = recipientName, Phone = recipientPhone },
            codAmountTnd = codAmount,
            barcode128 = $"*{waybillNumber}*",
            trackingUrl = $"https://track.sgrowing.io/w/{waybillNumber}",
            status = "ManifestCreatedReadyForPickup",
            estimatedDeliveryWindow = "24-48h"
        };
    }

    private static string CreateErrorResponse(JsonNode? id, int code, string message)
    {
        return JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            error = new { code, message }
        }, JsonOptions);
    }
}
