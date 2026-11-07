using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SGrowing.Client.Models;

namespace SGrowing.Client;

/// <summary>
/// Official .NET client implementation for the SGrowing Enterprise Growth OS platform.
/// </summary>
public class SGrowingClient : ISGrowingClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SGrowingClientOptions _options;
    private readonly bool _disposeHttpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of <see cref="SGrowingClient"/> with configuration options.
    /// </summary>
    public SGrowingClient(SGrowingClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _disposeHttpClient = true;

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15)
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
            Timeout = options.Timeout
        };

        ConfigureDefaultHeaders(_httpClient, options);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SGrowingClient"/> with an existing HttpClient and options.
    /// Typically used via ASP.NET Core dependency injection with <c>services.AddSGrowingClient</c>.
    /// </summary>
    public SGrowingClient(HttpClient httpClient, IOptions<SGrowingClientOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _disposeHttpClient = false;

        if (_httpClient.BaseAddress == null && !string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }

        ConfigureDefaultHeaders(_httpClient, _options);
    }

    private static void ConfigureDefaultHeaders(HttpClient client, SGrowingClientOptions options)
    {
        if (!client.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Remove("X-Api-Key");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {options.ApiKey}");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", options.ApiKey);
        }

        if (options.TenantId.HasValue)
        {
            client.DefaultRequestHeaders.Remove("X-Tenant-Id");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Tenant-Id", options.TenantId.Value.ToString());
        }

        foreach (var (key, value) in options.CustomHeaders)
        {
            client.DefaultRequestHeaders.Remove(key);
            client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
        }
    }

    // --- AI Autonomous Missions ---

    /// <inheritdoc />
    public async Task<AgentMissionPlan> PlanMissionAsync(
        MissionObjectiveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await SendAsync<AgentMissionPlan>(
            HttpMethod.Post,
            "api/v1/ai/missions/plan",
            request,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgentMissionPlan> StepMissionAsync(
        Guid missionId,
        string? approveTaskId = null,
        CancellationToken cancellationToken = default)
    {
        var path = !string.IsNullOrWhiteSpace(approveTaskId)
            ? $"api/v1/ai/missions/{missionId}/approve/{Uri.EscapeDataString(approveTaskId)}"
            : $"api/v1/ai/missions/{missionId}/step";

        return await SendAsync<AgentMissionPlan>(
            HttpMethod.Post,
            path,
            null,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgentMissionPlan?> GetMissionAsync(
        Guid missionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await SendAsync<AgentMissionPlan>(
                HttpMethod.Get,
                $"api/v1/ai/missions/{missionId}",
                null,
                cancellationToken);
        }
        catch (SGrowingApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // --- E-Commerce Connectors & Marketplace ---

    /// <inheritdoc />
    public async Task<TransformPayloadResult> TransformOrderAsync(
        string connectorId,
        string rawPayload,
        Dictionary<string, string>? customMappings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectorId);
        ArgumentNullException.ThrowIfNull(rawPayload);

        var requestBody = new TransformOrderRequest(rawPayload, customMappings);

        return await SendAsync<TransformPayloadResult>(
            HttpMethod.Post,
            $"api/v1/connectors/marketplace/{Uri.EscapeDataString(connectorId)}/transform-order",
            requestBody,
            cancellationToken);
    }

    // --- Developer Webhooks ---

    /// <inheritdoc />
    public async Task<TestDispatchWebhookResponse> DispatchTestWebhookAsync(
        string eventType,
        object payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(payload);

        if (!_options.TenantId.HasValue)
        {
            throw new InvalidOperationException("TenantId must be configured in SGrowingClientOptions to dispatch test webhooks.");
        }

        var requestBody = new TestDispatchWebhookRequest(_options.TenantId.Value, eventType, payload);

        return await SendAsync<TestDispatchWebhookResponse>(
            HttpMethod.Post,
            "api/developer/webhooks/test-dispatch",
            requestBody,
            cancellationToken);
    }

    /// <inheritdoc />
    public bool VerifyWebhookSignature(
        string rawBody,
        string secret,
        string signatureHeader,
        double toleranceSeconds = 300,
        string? timestampHeader = null)
    {
        return SGrowingWebhookVerifier.Verify(rawBody, secret, signatureHeader, toleranceSeconds, timestampHeader);
    }

    // --- SRE Health Radar & Telemetry ---

    /// <inheritdoc />
    public async Task<SreHealthRadarReport> GetSreHealthRadarAsync(
        int dlqDepth = 0,
        int activeMissions = 0,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/v1/telemetry/sre-radar?dlqDepth={dlqDepth}&activeMissions={activeMissions}";

        return await SendAsync<SreHealthRadarReport>(
            HttpMethod.Get,
            path,
            null,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RecordSyntheticMetricAsync(
        RecordSyntheticMetricRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await SendAsync<JsonElement>(
            HttpMethod.Post,
            "api/v1/telemetry/record-synthetic-metric",
            request,
            cancellationToken);

        return true;
    }

    // --- Internal HTTP Execution ---

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativePath);

        if (content != null)
        {
            var json = JsonSerializer.Serialize(content, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            try
            {
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                    {
                        errorMessage = msgProp.GetString()!;
                    }
                    else if (doc.RootElement.TryGetProperty("error", out var errProp) && errProp.ValueKind == JsonValueKind.String)
                    {
                        errorMessage = errProp.GetString()!;
                    }
                }
            }
            catch
            {
                // Fall back to HTTP status message
            }

            throw new SGrowingApiException(errorMessage, response.StatusCode, responseBody);
        }

        if (typeof(T) == typeof(JsonElement))
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                using var emptyDoc = JsonDocument.Parse("{}");
                return (T)(object)emptyDoc.RootElement.Clone();
            }
            using var doc = JsonDocument.Parse(responseBody);
            return (T)(object)doc.RootElement.Clone();
        }

        var result = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);
        if (result == null)
        {
            throw new SGrowingApiException("Failed to deserialize response payload from server.", response.StatusCode, responseBody);
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
