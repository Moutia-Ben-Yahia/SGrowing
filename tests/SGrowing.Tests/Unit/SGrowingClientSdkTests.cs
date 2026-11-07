using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGrowing.Client;
using SGrowing.Client.Models;
using Xunit;

namespace SGrowing.Tests.Unit;

public class SGrowingClientSdkTests
{
    private const string TestSecret = "whsec_test_secret_key_abcdef123456";
    private const string SamplePayload = "{\"event\":\"order.created\",\"orderId\":\"ORD-777\"}";

    [Fact]
    public void WebhookVerifier_ComputeSignature_ProducesSha256PrefixedHex()
    {
        var sig = SGrowingWebhookVerifier.ComputeSignature(SamplePayload, TestSecret);

        sig.Should().StartWith("sha256=");
        sig.Length.Should().Be(71); // "sha256=" (7) + 64 hex chars
    }

    [Fact]
    public void WebhookVerifier_Verify_WithValidDirectSignature_ReturnsTrue()
    {
        var sig = SGrowingWebhookVerifier.ComputeSignature(SamplePayload, TestSecret);

        var isValid = SGrowingWebhookVerifier.Verify(SamplePayload, TestSecret, sig);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void WebhookVerifier_Verify_WithValidTimestampBoundSignature_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sig = SGrowingWebhookVerifier.ComputeSignature(SamplePayload, TestSecret, now);

        var isValid = SGrowingWebhookVerifier.Verify(SamplePayload, TestSecret, sig, toleranceSeconds: 300, timestampHeader: now.ToString());

        isValid.Should().BeTrue();
    }

    [Fact]
    public void WebhookVerifier_Verify_WithStructuredHeader_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hex = SGrowingWebhookVerifier.ComputeSignature(SamplePayload, TestSecret, now)["sha256=".Length..];
        var header = $"t={now},v1={hex}";

        var isValid = SGrowingWebhookVerifier.Verify(SamplePayload, TestSecret, header);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void WebhookVerifier_Verify_WithTamperedPayload_ReturnsFalse()
    {
        var sig = SGrowingWebhookVerifier.ComputeSignature(SamplePayload, TestSecret);
        var tampered = "{\"event\":\"order.created\",\"orderId\":\"ORD-TAMPERED\"}";

        var isValid = SGrowingWebhookVerifier.Verify(tampered, TestSecret, sig);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void WebhookVerifier_Verify_WithWrongSecret_ReturnsFalse()
    {
        var sig = SGrowingWebhookVerifier.ComputeSignature(SamplePayload, "wrong_secret");

        var isValid = SGrowingWebhookVerifier.Verify(SamplePayload, TestSecret, sig);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void WebhookVerifier_Verify_WithExpiredTimestamp_ReturnsFalse()
    {
        var expiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 600; // 10 mins ago
        var sig = SGrowingWebhookVerifier.ComputeSignature(SamplePayload, TestSecret, expiredTimestamp);

        // Tolerance is 300 seconds (5 mins)
        var isValid = SGrowingWebhookVerifier.Verify(
            SamplePayload,
            TestSecret,
            sig,
            toleranceSeconds: 300,
            timestampHeader: expiredTimestamp.ToString());

        isValid.Should().BeFalse();
    }

    [Fact]
    public void WebhookVerifier_Verify_WithInvalidHex_ReturnsFalse()
    {
        var isValid = SGrowingWebhookVerifier.Verify(SamplePayload, TestSecret, "sha256=invalid_not_hex_chars");

        isValid.Should().BeFalse();
    }

    [Fact]
    public void WebhookVerifier_Verify_WithEmptyArguments_ReturnsFalse()
    {
        SGrowingWebhookVerifier.Verify("", TestSecret, "sha256=123").Should().BeFalse();
        SGrowingWebhookVerifier.Verify(SamplePayload, "", "sha256=123").Should().BeFalse();
        SGrowingWebhookVerifier.Verify(SamplePayload, TestSecret, "").Should().BeFalse();
    }

    [Fact]
    public async Task Client_PlanMissionAsync_SendsCorrectRequestAndReturnsPlan()
    {
        var expectedPlan = new AgentMissionPlan(
            Guid.NewGuid(),
            "Q4 Black Friday Activation",
            "Drive 4x ROAS",
            MissionStatus.InProgress,
            new List<MissionTaskNode>
            {
                new("t1", "Audience Scoring", "GrowthAnalyst", new List<string>(), TaskExecutionStatus.Succeeded, false),
                new("t2", "Budget Signoff", "CampaignStrategist", new List<string> { "t1" }, TaskExecutionStatus.WaitingApproval, true)
            },
            1500m,
            DateTime.UtcNow);

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(async (req, ct) =>
        {
            capturedRequest = req;
            capturedBody = req.Content != null ? await req.Content.ReadAsStringAsync(ct) : null;
            var json = JsonSerializer.Serialize(expectedPlan);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var options = new SGrowingClientOptions
        {
            BaseUrl = "https://api.test.sgrowing.io",
            ApiKey = "sg_live_testkey_123",
            TenantId = Guid.NewGuid()
        };

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        using var client = new SGrowingClient(httpClient, Options.Create(options));

        var request = new MissionObjectiveRequest(
            "Q4 Black Friday Activation",
            "Drive 4x ROAS",
            1500m,
            "US,SA",
            new List<string> { "CPA < 25" });

        var plan = await client.PlanMissionAsync(request);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/api/v1/ai/missions/plan");
        capturedRequest.Headers.GetValues("Authorization").First().Should().Be("Bearer sg_live_testkey_123");
        capturedRequest.Headers.GetValues("X-Api-Key").First().Should().Be("sg_live_testkey_123");
        capturedRequest.Headers.GetValues("X-Tenant-Id").First().Should().Be(options.TenantId.ToString());
        capturedBody.Should().Contain("Q4 Black Friday Activation");

        plan.Should().NotBeNull();
        plan.MissionId.Should().Be(expectedPlan.MissionId);
        plan.TaskDag.Should().HaveCount(2);
    }

    [Fact]
    public async Task Client_StepMissionAsync_DispatchesStepOrApprovePath()
    {
        var missionId = Guid.NewGuid();
        var expectedPlan = new AgentMissionPlan(
            missionId,
            "Step Mission Test",
            "Goal",
            MissionStatus.InProgress,
            new List<MissionTaskNode>(),
            500m,
            DateTime.UtcNow);

        var pathsCalled = new List<string>();

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            pathsCalled.Add(req.RequestUri!.AbsolutePath);
            var json = JsonSerializer.Serialize(expectedPlan);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var options = new SGrowingClientOptions { BaseUrl = "https://api.test.sgrowing.io" };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        using var client = new SGrowingClient(httpClient, Options.Create(options));

        // Call step without approval task ID
        await client.StepMissionAsync(missionId);
        // Call step with approval task ID
        await client.StepMissionAsync(missionId, "task-approve-42");

        pathsCalled.Should().Contain($"/api/v1/ai/missions/{missionId}/step");
        pathsCalled.Should().Contain($"/api/v1/ai/missions/{missionId}/approve/task-approve-42");
    }

    [Fact]
    public async Task Client_GetMissionAsync_ReturnsPlanWhenFound_AndNullWhenNotFound()
    {
        var missionId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            if (req.RequestUri!.AbsolutePath.Contains(missionId.ToString()))
            {
                var plan = new AgentMissionPlan(missionId, "Found Plan", "Goal", MissionStatus.Completed, new(), 0m, DateTime.UtcNow);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(plan), Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var options = new SGrowingClientOptions { BaseUrl = "https://api.test.sgrowing.io" };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        using var client = new SGrowingClient(httpClient, Options.Create(options));

        var found = await client.GetMissionAsync(missionId);
        found.Should().NotBeNull();
        found!.Title.Should().Be("Found Plan");

        var missing = await client.GetMissionAsync(Guid.NewGuid());
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Client_TransformOrderAsync_CallsConnectorTransformEndpoint()
    {
        var mockResult = new TransformPayloadResult(
            true,
            "Order",
            new CanonicalOrderPayload(
                "SP-999",
                "#999",
                250.0m,
                "USD",
                "customer@sgrowing.io",
                "+1234567890",
                "San Francisco",
                "US",
                new List<CanonicalOrderItem>
                {
                    new("SKU-1", "Pro Growth Plan", 1, 250.0m, 250.0m)
                },
                DateTime.UtcNow));

        HttpRequestMessage? capturedReq = null;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            capturedReq = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(mockResult), Encoding.UTF8, "application/json")
            });
        });

        var options = new SGrowingClientOptions { BaseUrl = "https://api.test.sgrowing.io" };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        using var client = new SGrowingClient(httpClient, Options.Create(options));

        var result = await client.TransformOrderAsync(
            "shopify",
            "{\"id\": 999}",
            new Dictionary<string, string> { ["email"] = "customer.email" });

        capturedReq.Should().NotBeNull();
        capturedReq!.RequestUri!.AbsolutePath.Should().Be("/api/v1/connectors/marketplace/shopify/transform-order");
        result.Success.Should().BeTrue();
        result.TransformedOrder!.ExternalOrderId.Should().Be("SP-999");
    }

    [Fact]
    public async Task Client_DispatchTestWebhookAsync_ValidatesTenantAndDispatches()
    {
        var tenantId = Guid.NewGuid();
        var mockResponse = new TestDispatchWebhookResponse(1, new List<WebhookDeliveryAttemptLog>());

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(mockResponse), Encoding.UTF8, "application/json")
            });
        });

        var optionsWithTenant = new SGrowingClientOptions { BaseUrl = "https://api.test.sgrowing.io", TenantId = tenantId };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(optionsWithTenant.BaseUrl) };
        using var client = new SGrowingClient(httpClient, Options.Create(optionsWithTenant));

        var response = await client.DispatchTestWebhookAsync("order.completed", new { orderId = 123 });
        response.DispatchedCount.Should().Be(1);

        // Without tenantId should throw InvalidOperationException
        var optionsWithoutTenant = new SGrowingClientOptions { BaseUrl = "https://api.test.sgrowing.io", TenantId = null };
        using var httpClient2 = new HttpClient(handler) { BaseAddress = new Uri(optionsWithoutTenant.BaseUrl) };
        using var clientWithoutTenant = new SGrowingClient(httpClient2, Options.Create(optionsWithoutTenant));

        var act = async () => await clientWithoutTenant.DispatchTestWebhookAsync("order.completed", new { orderId = 123 });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Client_GetSreHealthRadarAsync_QueriesRadarEndpoint()
    {
        var mockReport = new SreHealthRadarReport(
            99.9,
            "HEALTHY",
            99.95,
            42.0,
            1200,
            0,
            5,
            0,
            new List<SreAlertDto>(),
            DateTime.UtcNow);

        HttpRequestMessage? capturedReq = null;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            capturedReq = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(mockReport), Encoding.UTF8, "application/json")
            });
        });

        var options = new SGrowingClientOptions { BaseUrl = "https://api.test.sgrowing.io" };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        using var client = new SGrowingClient(httpClient, Options.Create(options));

        var report = await client.GetSreHealthRadarAsync(dlqDepth: 0, activeMissions: 5);

        capturedReq.Should().NotBeNull();
        capturedReq!.RequestUri!.Query.Should().Contain("dlqDepth=0");
        capturedReq.RequestUri.Query.Should().Contain("activeMissions=5");
        report.OverallHealthScore.Should().Be(99.9);
        report.OverallStatus.Should().Be("HEALTHY");
    }

    [Fact]
    public async Task Client_ThrowsSGrowingApiException_OnHttpError()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"Invalid mission parameters\"}", Encoding.UTF8, "application/json")
            });
        });

        var options = new SGrowingClientOptions { BaseUrl = "https://api.test.sgrowing.io" };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        using var client = new SGrowingClient(httpClient, Options.Create(options));

        var act = async () => await client.PlanMissionAsync(new MissionObjectiveRequest("Test", "Goal", 100m, "US"));

        var ex = await act.Should().ThrowAsync<SGrowingApiException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Be("Invalid mission parameters");
    }

    [Fact]
    public void DependencyInjection_AddSGrowingClient_RegistersAndConfiguresClient()
    {
        var services = new ServiceCollection();
        var tenantId = Guid.NewGuid();

        services.AddSGrowingClient(opt =>
        {
            opt.BaseUrl = "https://api.production.sgrowing.io";
            opt.ApiKey = "sg_live_di_key_777";
            opt.TenantId = tenantId;
            opt.Timeout = TimeSpan.FromSeconds(15);
            opt.CustomHeaders["X-Custom-Env"] = "Staging";
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<ISGrowingClient>();

        client.Should().NotBeNull();
        client.Should().BeOfType<SGrowingClient>();
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
