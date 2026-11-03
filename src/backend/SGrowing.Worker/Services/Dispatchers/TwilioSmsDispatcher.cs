using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SGrowing.Worker.Services.Dispatchers;

public class TwilioSmsDispatcher : ITwilioSmsDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TwilioSmsDispatcher> _logger;

    public TwilioSmsDispatcher(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TwilioSmsDispatcher> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<SmsDispatchResult> SendSmsAsync(
        Guid tenantId,
        string recipientPhone,
        string message,
        string? fromNumber = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientPhone))
        {
            _logger.LogWarning("Missing recipient phone number for tenant {TenantId}.", tenantId);
            return new SmsDispatchResult(false, null, "Missing recipient phone number.");
        }

        var accountSid = _configuration["Outbound:Twilio:AccountSid"];
        var authToken = _configuration["Outbound:Twilio:AuthToken"];
        var configFrom = _configuration["Outbound:Twilio:FromNumber"];
        var senderNumber = !string.IsNullOrWhiteSpace(fromNumber) ? fromNumber : configFrom;

        // Graceful fallback for local dev / testing if credentials missing or mock
        if (string.IsNullOrWhiteSpace(accountSid) ||
            string.IsNullOrWhiteSpace(authToken) ||
            accountSid.StartsWith("mock_", StringComparison.OrdinalIgnoreCase) ||
            accountSid.StartsWith("test_", StringComparison.OrdinalIgnoreCase))
        {
            var simSid = $"SM{Guid.NewGuid():N}";
            _logger.LogInformation("Twilio credentials not configured. Simulating SMS dispatch to {Phone} (SimSid: {SimSid})",
                recipientPhone, simSid);
            return new SmsDispatchResult(true, simSid, null, IsSimulated: true);
        }

        try
        {
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);

            var basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

            var formParams = new List<KeyValuePair<string, string>>
            {
                new("To", recipientPhone),
                new("From", !string.IsNullOrWhiteSpace(senderNumber) ? senderNumber : "+15005550006"),
                new("Body", message)
            };
            request.Content = new FormUrlEncodedContent(formParams);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                var sid = doc.RootElement.TryGetProperty("sid", out var sidProp)
                    ? sidProp.GetString()
                    : $"SM{Guid.NewGuid():N}";

                _logger.LogInformation("SMS dispatched via Twilio to {Phone} (Sid: {Sid})", recipientPhone, sid);
                return new SmsDispatchResult(true, sid, null);
            }

            _logger.LogError("Twilio SMS send failed ({StatusCode}): {Body}", response.StatusCode, responseBody);
            return new SmsDispatchResult(false, null, $"Twilio error ({response.StatusCode}): {responseBody}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending SMS via Twilio to {Phone}", recipientPhone);
            return new SmsDispatchResult(false, null, ex.Message);
        }
    }
}
