using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SGrowing.Worker.Services.Dispatchers;

public class MetaWhatsAppDispatcher : IMetaWhatsAppDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MetaWhatsAppDispatcher> _logger;

    public MetaWhatsAppDispatcher(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MetaWhatsAppDispatcher> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<WhatsAppDispatchResult> SendTextMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientPhone))
        {
            _logger.LogWarning("Missing recipient phone number for WhatsApp message (Tenant {TenantId}).", tenantId);
            return new WhatsAppDispatchResult(false, null, "Missing recipient phone number.");
        }

        var accessToken = _configuration["Outbound:WhatsApp:AccessToken"];
        var phoneNumberId = _configuration["Outbound:WhatsApp:PhoneNumberId"];

        if (string.IsNullOrWhiteSpace(accessToken) ||
            string.IsNullOrWhiteSpace(phoneNumberId) ||
            accessToken.StartsWith("mock_", StringComparison.OrdinalIgnoreCase) ||
            accessToken.StartsWith("test_", StringComparison.OrdinalIgnoreCase))
        {
            var simId = $"wamid.{Guid.NewGuid():N}";
            _logger.LogInformation("Meta WhatsApp credentials not configured. Simulating text dispatch to {Phone} (SimId: {SimId})",
                recipientPhone, simId);
            return new WhatsAppDispatchResult(true, simId, null, IsSimulated: true);
        }

        try
        {
            var url = $"https://graph.facebook.com/v21.0/{phoneNumberId}/messages";
            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = recipientPhone,
                type = "text",
                text = new { preview_url = false, body = message }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                var id = doc.RootElement.TryGetProperty("messages", out var messagesArr) && messagesArr.GetArrayLength() > 0
                    ? messagesArr[0].GetProperty("id").GetString()
                    : $"wamid.{Guid.NewGuid():N}";

                _logger.LogInformation("WhatsApp text message sent via Meta Cloud API to {Phone} (Id: {Id})", recipientPhone, id);
                return new WhatsAppDispatchResult(true, id, null);
            }

            _logger.LogError("Meta Cloud API failed ({StatusCode}): {Body}", response.StatusCode, responseBody);
            return new WhatsAppDispatchResult(false, null, $"Meta API error ({response.StatusCode}): {responseBody}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending WhatsApp text message to {Phone}", recipientPhone);
            return new WhatsAppDispatchResult(false, null, ex.Message);
        }
    }

    public async Task<WhatsAppDispatchResult> SendTemplateMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string templateName,
        string? languageCode = "en_US",
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientPhone))
        {
            _logger.LogWarning("Missing recipient phone number for WhatsApp template (Tenant {TenantId}).", tenantId);
            return new WhatsAppDispatchResult(false, null, "Missing recipient phone number.");
        }

        var accessToken = _configuration["Outbound:WhatsApp:AccessToken"];
        var phoneNumberId = _configuration["Outbound:WhatsApp:PhoneNumberId"];

        if (string.IsNullOrWhiteSpace(accessToken) ||
            string.IsNullOrWhiteSpace(phoneNumberId) ||
            accessToken.StartsWith("mock_", StringComparison.OrdinalIgnoreCase) ||
            accessToken.StartsWith("test_", StringComparison.OrdinalIgnoreCase))
        {
            var simId = $"wamid.{Guid.NewGuid():N}";
            _logger.LogInformation("Meta WhatsApp credentials not configured. Simulating template dispatch to {Phone} [Template: {Template}, SimId: {SimId}]",
                recipientPhone, templateName, simId);
            return new WhatsAppDispatchResult(true, simId, null, IsSimulated: true);
        }

        try
        {
            var url = $"https://graph.facebook.com/v21.0/{phoneNumberId}/messages";

            object templateObject;
            if (parameters != null && parameters.Count > 0)
            {
                var parameterList = parameters.Select(p => new { type = "text", text = p.Value }).ToArray();
                templateObject = new
                {
                    name = templateName,
                    language = new { code = languageCode ?? "en_US" },
                    components = new[]
                    {
                        new
                        {
                            type = "body",
                            parameters = parameterList
                        }
                    }
                };
            }
            else
            {
                templateObject = new
                {
                    name = templateName,
                    language = new { code = languageCode ?? "en_US" }
                };
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = recipientPhone,
                type = "template",
                template = templateObject
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                var id = doc.RootElement.TryGetProperty("messages", out var messagesArr) && messagesArr.GetArrayLength() > 0
                    ? messagesArr[0].GetProperty("id").GetString()
                    : $"wamid.{Guid.NewGuid():N}";

                _logger.LogInformation("WhatsApp template '{Template}' sent via Meta Cloud API to {Phone} (Id: {Id})",
                    templateName, recipientPhone, id);
                return new WhatsAppDispatchResult(true, id, null);
            }

            _logger.LogError("Meta Cloud API template send failed ({StatusCode}): {Body}", response.StatusCode, responseBody);
            return new WhatsAppDispatchResult(false, null, $"Meta API error ({response.StatusCode}): {responseBody}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending WhatsApp template message to {Phone}", recipientPhone);
            return new WhatsAppDispatchResult(false, null, ex.Message);
        }
    }
}
