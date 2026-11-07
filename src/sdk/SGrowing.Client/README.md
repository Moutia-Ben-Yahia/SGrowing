# SGrowing.Client

Official .NET 10 Client SDK for the **SGrowing Enterprise Growth OS** platform.

Provides strongly typed, high-performance HTTP client implementations and cryptographic webhook verification for:
- **Autonomous AI Missions & Swarms**: Strategic goal decomposition, DAG orchestration, and human-in-the-loop task sign-off.
- **E-Commerce Marketplace Connectors**: Omnichannel schema normalization for Shopify, Salla, WooCommerce, and Zid orders.
- **Developer Webhooks**: Synthetic event dispatch and timing-safe constant-time HMAC-SHA256 signature verification (`CryptographicOperations.FixedTimeEquals`).
- **SRE Health Radar & Telemetry**: SLO error budgets, p99 latencies, clickstream event drain metrics, and distributed tracing.

## Installation

```shell
dotnet add package SGrowing.Client
```

## Quick Start (Dependency Injection)

In `Program.cs`:

```csharp
using SGrowing.Client;

builder.Services.AddSGrowingClient(options =>
{
    options.BaseUrl = "https://api.sgrowing.io";
    options.ApiKey = builder.Configuration["SGrowing:ApiKey"];
    options.TenantId = Guid.Parse(builder.Configuration["SGrowing:TenantId"]!);
    options.Timeout = TimeSpan.FromSeconds(30);
});
```

Inject and consume `ISGrowingClient`:

```csharp
public class GrowthCampaignManager
{
    private readonly ISGrowingClient _client;

    public GrowthCampaignManager(ISGrowingClient client)
    {
        _client = client;
    }

    public async Task LaunchMissionAsync()
    {
        // 1. Plan AI Mission
        var plan = await _client.PlanMissionAsync(new MissionObjectiveRequest(
            Title: "Black Friday Flash Sale",
            StrategicGoal: "Drive 4x ROAS on dynamic retargeting",
            BudgetLimit: 10000m,
            TargetGeo: "SA,UAE"
        ));

        // 2. Advance Mission Execution
        var updated = await _client.StepMissionAsync(plan.MissionId);

        // 3. Inspect SRE Health Radar
        var radar = await _client.GetSreHealthRadarAsync();
        Console.WriteLine($"Platform Health Score: {radar.OverallHealthScore} (Status: {radar.OverallStatus})");
    }
}
```

## Standalone Usage

```csharp
using SGrowing.Client;
using SGrowing.Client.Models;

var client = new SGrowingClient(new SGrowingClientOptions
{
    BaseUrl = "https://api.sgrowing.io",
    ApiKey = "sg_live_...",
    TenantId = Guid.NewGuid()
});

var result = await client.TransformOrderAsync("shopify", rawJsonPayload);
```

## Webhook Signature Verification

```csharp
var isValid = SGrowingWebhookVerifier.Verify(
    rawBody: requestBodyString,
    secret: Environment.GetEnvironmentVariable("WEBHOOK_SECRET")!,
    signatureHeader: Request.Headers["X-SG-Signature"]!,
    toleranceSeconds: 300
);
```

## License

MIT
