using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SGrowing.Worker.Services.Dispatchers;

public class SmtpEmailDispatcher : ISmtpEmailDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailDispatcher> _logger;

    public SmtpEmailDispatcher(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SmtpEmailDispatcher> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<EmailDispatchResult> SendEmailAsync(
        Guid tenantId,
        string recipientEmail,
        string subject,
        string body,
        string? senderEmail = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains('@'))
        {
            _logger.LogWarning("Invalid recipient email '{Recipient}' for tenant {TenantId}.", recipientEmail, tenantId);
            return new EmailDispatchResult(false, null, "Invalid or missing recipient email address.");
        }

        var resendApiKey = _configuration["Outbound:Email:ResendApiKey"];
        var smtpHost = _configuration["Outbound:Email:SmtpHost"];
        var smtpPortStr = _configuration["Outbound:Email:SmtpPort"];
        var smtpUsername = _configuration["Outbound:Email:SmtpUsername"];
        var smtpPassword = _configuration["Outbound:Email:SmtpPassword"];

        // 1. Resend API HTTP dispatch if API key provided and not a dummy/mock test key
        if (!string.IsNullOrWhiteSpace(resendApiKey) &&
            !resendApiKey.StartsWith("mock_", StringComparison.OrdinalIgnoreCase) &&
            !resendApiKey.StartsWith("test_", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var fromAddress = !string.IsNullOrWhiteSpace(senderEmail) ? senderEmail : "notifications@sgrowing.io";
                var payload = new
                {
                    from = fromAddress,
                    to = new[] { recipientEmail },
                    subject = subject,
                    html = body
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resendApiKey);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    var id = doc.RootElement.TryGetProperty("id", out var idProp)
                        ? idProp.GetString()
                        : $"resend_{Guid.NewGuid():N}";

                    _logger.LogInformation("Email dispatched via Resend to {Recipient} (Id: {Id})", recipientEmail, id);
                    return new EmailDispatchResult(true, id, null);
                }

                _logger.LogError("Resend API failed ({StatusCode}): {Response}", response.StatusCode, responseContent);
                return new EmailDispatchResult(false, null, $"Resend API error ({response.StatusCode}): {responseContent}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while dispatching email via Resend to {Recipient}", recipientEmail);
                return new EmailDispatchResult(false, null, ex.Message);
            }
        }

        // 2. SMTP dispatch if SmtpHost configured and not dummy/mock
        if (!string.IsNullOrWhiteSpace(smtpHost) &&
            !smtpHost.StartsWith("mock_", StringComparison.OrdinalIgnoreCase) &&
            !smtpHost.StartsWith("test_", StringComparison.OrdinalIgnoreCase) &&
            !smtpHost.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                int smtpPort = int.TryParse(smtpPortStr, out var parsedPort) ? parsedPort : 587;
                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = smtpPort == 465 || smtpPort == 587,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                if (!string.IsNullOrWhiteSpace(smtpUsername) && !string.IsNullOrWhiteSpace(smtpPassword))
                {
                    smtpClient.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                }

                var fromAddress = !string.IsNullOrWhiteSpace(senderEmail) ? senderEmail : "noreply@sgrowing.io";
                using var message = new MailMessage(fromAddress, recipientEmail, subject, body)
                {
                    IsBodyHtml = true
                };

                await smtpClient.SendMailAsync(message, cancellationToken);

                var messageId = $"smtp_{Guid.NewGuid():N}";
                _logger.LogInformation("Email dispatched via SMTP ({Host}:{Port}) to {Recipient} (Id: {Id})",
                    smtpHost, smtpPort, recipientEmail, messageId);

                return new EmailDispatchResult(true, messageId, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while dispatching email via SMTP to {Recipient}", recipientEmail);
                return new EmailDispatchResult(false, null, ex.Message);
            }
        }

        // 3. Graceful fallback for local development & test runs when credentials are not configured
        var simId = $"sim_email_{Guid.NewGuid():N}";
        _logger.LogInformation("No live email credentials configured. Simulating email dispatch to {Recipient} [Subject: '{Subject}', SimId: {SimId}]",
            recipientEmail, subject, simId);

        return new EmailDispatchResult(true, simId, null, IsSimulated: true);
    }
}
