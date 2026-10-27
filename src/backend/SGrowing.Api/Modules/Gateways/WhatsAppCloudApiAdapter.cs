using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SGrowing.Api.Modules.Gateways;

public interface IWhatsAppCloudApiClient
{
    bool VerifyWebhookSignature(string payloadBody, string? signatureHeader, string appSecret);
    Task<SendWhatsAppMessageResultDto> SendTemplateMessageAsync(string phoneNumberId, string accessToken, string recipientPhone, string templateName, string languageCode, Dictionary<string, string> parameters, CancellationToken ct = default);
    Task<SendWhatsAppMessageResultDto> SendTextMessageAsync(string phoneNumberId, string accessToken, string recipientPhone, string message, CancellationToken ct = default);
}

public class WhatsAppCloudApiAdapter : IWhatsAppCloudApiClient
{
    private readonly HttpClient _httpClient;

    public WhatsAppCloudApiAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool VerifyWebhookSignature(string payloadBody, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || !signatureHeader.StartsWith("sha256="))
        {
            return false;
        }

        var expectedHash = signatureHeader.Substring("sha256=".Length);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var computedHashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBody));
        var computedHash = Convert.ToHexString(computedHashBytes).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(computedHash)
        );
    }

    public async Task<SendWhatsAppMessageResultDto> SendTemplateMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhone,
        string templateName,
        string languageCode,
        Dictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var url = $"https://graph.facebook.com/v21.0/{phoneNumberId}/messages";
        var payload = new
        {
            messaging_product = "whatsapp",
            to = recipientPhone,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = parameters.Select(p => new { type = "text", text = p.Value }).ToArray()
                    }
                }
            }
        };

        var jsonBody = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // If in offline / test mode or token is dummy, return successful simulated payload
        if (string.IsNullOrEmpty(accessToken) || accessToken.StartsWith("mock_") || accessToken.StartsWith("test_"))
        {
            return new SendWhatsAppMessageResultDto(
                $"WAMID-{Guid.NewGuid():N}",
                "accepted",
                recipientPhone,
                DateTime.UtcNow
            );
        }

        var response = await _httpClient.SendAsync(request, ct);
        var responseString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"WhatsApp Cloud API error ({response.StatusCode}): {responseString}");
        }

        using var doc = JsonDocument.Parse(responseString);
        var msgId = doc.RootElement.GetProperty("messages")[0].GetProperty("id").GetString() ?? $"WAMID-{Guid.NewGuid():N}";

        return new SendWhatsAppMessageResultDto(
            msgId,
            "accepted",
            recipientPhone,
            DateTime.UtcNow
        );
    }

    public async Task<SendWhatsAppMessageResultDto> SendTextMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhone,
        string message,
        CancellationToken ct = default)
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

        var jsonBody = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        if (string.IsNullOrEmpty(accessToken) || accessToken.StartsWith("mock_") || accessToken.StartsWith("test_"))
        {
            return new SendWhatsAppMessageResultDto(
                $"WAMID-TXT-{Guid.NewGuid():N}",
                "accepted",
                recipientPhone,
                DateTime.UtcNow
            );
        }

        var response = await _httpClient.SendAsync(request, ct);
        var responseString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"WhatsApp Cloud API error ({response.StatusCode}): {responseString}");
        }

        using var doc = JsonDocument.Parse(responseString);
        var msgId = doc.RootElement.GetProperty("messages")[0].GetProperty("id").GetString() ?? $"WAMID-TXT-{Guid.NewGuid():N}";

        return new SendWhatsAppMessageResultDto(
            msgId,
            "accepted",
            recipientPhone,
            DateTime.UtcNow
        );
    }
}
