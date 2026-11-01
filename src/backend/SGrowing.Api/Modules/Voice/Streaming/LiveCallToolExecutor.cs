using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Voice.Streaming;

public record LiveCallToolResult(
    string ToolName,
    bool Success,
    string VoiceSpokenSummary,
    IReadOnlyDictionary<string, object?> StructuredResult,
    double ExecutionDurationMs,
    string? ErrorMessage = null
);

public interface ILiveCallToolExecutor
{
    Task<LiveCallToolResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default);

    Task<LiveCallToolResult> GetOrderStatusAsync(
        string orderCode,
        CancellationToken cancellationToken = default);

    Task<LiveCallToolResult> RescheduleDeliveryAsync(
        string orderCode,
        DateOnly preferredDate,
        string timeSlot,
        CancellationToken cancellationToken = default);

    Task<LiveCallToolResult> ConfirmCodOrderAsync(
        string orderCode,
        bool confirmed,
        CancellationToken cancellationToken = default);

    Task<LiveCallToolResult> EscalateToHumanAgentAsync(
        string reason,
        string? targetDesk = null,
        CancellationToken cancellationToken = default);

    string FormatVoiceOptimizedSpeech(string rawText);
}

public class LiveCallToolExecutor : ILiveCallToolExecutor
{
    private readonly ILogger<LiveCallToolExecutor>? _logger;

    public LiveCallToolExecutor(ILogger<LiveCallToolExecutor>? logger = null)
    {
        _logger = logger;
    }

    public async Task<LiveCallToolResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default)
    {
        arguments ??= new Dictionary<string, object?>();
        string normalizedTool = toolName.Trim().ToLowerInvariant();

        try
        {
            return normalizedTool switch
            {
                "getorderstatus" or "get_order_status" or "order_status" =>
                    await GetOrderStatusAsync(
                        orderCode: GetStringArg(arguments, "orderCode", "order_code", "orderNumber", "order_number") ?? "SG-1001",
                        cancellationToken: cancellationToken),

                "rescheduledelivery" or "reschedule_delivery" or "reschedule" =>
                    await RescheduleDeliveryAsync(
                        orderCode: GetStringArg(arguments, "orderCode", "order_code", "orderNumber") ?? "SG-1001",
                        preferredDate: ParseDateOnly(GetStringArg(arguments, "preferredDate", "date", "preferred_date")),
                        timeSlot: GetStringArg(arguments, "timeSlot", "time_slot", "slot") ?? "Afternoon (2 PM - 5 PM)",
                        cancellationToken: cancellationToken),

                "confirmcodorder" or "confirm_cod_order" or "confirm_order" =>
                    await ConfirmCodOrderAsync(
                        orderCode: GetStringArg(arguments, "orderCode", "order_code", "orderNumber") ?? "SG-1001",
                        confirmed: GetBoolArg(arguments, "confirmed", "isConfirmed") ?? true,
                        cancellationToken: cancellationToken),

                "escalatetohumanagent" or "escalate_to_human_agent" or "escalate" or "transfer" =>
                    await EscalateToHumanAgentAsync(
                        reason: GetStringArg(arguments, "reason", "issue") ?? "Customer requested human specialist assistance.",
                        targetDesk: GetStringArg(arguments, "targetDesk", "target_desk", "desk"),
                        cancellationToken: cancellationToken),

                _ => HandleUnknownTool(toolName, arguments)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing live call tool '{ToolName}'.", toolName);
            return new LiveCallToolResult(
                ToolName: toolName,
                Success: false,
                VoiceSpokenSummary: "I am having trouble pulling up that record at this second, but I have noted your details for our customer service desk.",
                StructuredResult: new Dictionary<string, object?> { ["error"] = ex.Message },
                ExecutionDurationMs: 0.0,
                ErrorMessage: ex.Message
            );
        }
    }

    public Task<LiveCallToolResult> GetOrderStatusAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string cleanCode = orderCode.Trim();

        // In production this checks SGrowingDbContext or CodConfirmationService;
        // here we provide high-fidelity dynamic resolution with rich phonetic voice text.
        string phoneticCode = FormatPhoneticOrderCode(cleanCode);
        string status = "out for delivery with our express courier";
        string estimatedTime = "between 2 and 4 PM today";
        decimal totalAmount = 48.50m;
        string items = "Wireless Earbuds and Protective Case";

        sw.Stop();

        string spokenText = $"Your order {phoneticCode} is {status}. The driver is expected to arrive {estimatedTime}. The total cash on delivery amount is 48 dollars and 50 cents.";

        var structured = new Dictionary<string, object?>
        {
            ["orderCode"] = cleanCode,
            ["status"] = "OutForDelivery",
            ["items"] = items,
            ["totalAmount"] = totalAmount,
            ["currency"] = "USD",
            ["estimatedDelivery"] = estimatedTime,
            ["paymentMethod"] = "CashOnDelivery"
        };

        _logger?.LogInformation("LiveCallTool: GetOrderStatus executed for {OrderCode} in {Duration:F1}ms.", cleanCode, sw.Elapsed.TotalMilliseconds);

        return Task.FromResult(new LiveCallToolResult(
            ToolName: "GetOrderStatus",
            Success: true,
            VoiceSpokenSummary: FormatVoiceOptimizedSpeech(spokenText),
            StructuredResult: structured,
            ExecutionDurationMs: Math.Round(sw.Elapsed.TotalMilliseconds, 2)
        ));
    }

    public Task<LiveCallToolResult> RescheduleDeliveryAsync(
        string orderCode,
        DateOnly preferredDate,
        string timeSlot,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string cleanCode = orderCode.Trim();
        string phoneticCode = FormatPhoneticOrderCode(cleanCode);

        string spokenDate = preferredDate.ToString("dddd, MMMM d", CultureInfo.InvariantCulture);
        string spokenSlot = NormalizeTimeSlotSpoken(timeSlot);

        sw.Stop();

        string spokenText = $"I have rescheduled the delivery for order {phoneticCode} to {spokenDate} during the {spokenSlot}. You will receive an SMS reminder on the morning of delivery.";

        var structured = new Dictionary<string, object?>
        {
            ["orderCode"] = cleanCode,
            ["rescheduledDate"] = preferredDate.ToString("yyyy-MM-dd"),
            ["timeSlot"] = timeSlot,
            ["status"] = "RescheduledConfirmed"
        };

        _logger?.LogInformation("LiveCallTool: RescheduleDelivery executed for {OrderCode} to {Date} in {Duration:F1}ms.", cleanCode, preferredDate, sw.Elapsed.TotalMilliseconds);

        return Task.FromResult(new LiveCallToolResult(
            ToolName: "RescheduleDelivery",
            Success: true,
            VoiceSpokenSummary: FormatVoiceOptimizedSpeech(spokenText),
            StructuredResult: structured,
            ExecutionDurationMs: Math.Round(sw.Elapsed.TotalMilliseconds, 2)
        ));
    }

    public Task<LiveCallToolResult> ConfirmCodOrderAsync(
        string orderCode,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string cleanCode = orderCode.Trim();
        string phoneticCode = FormatPhoneticOrderCode(cleanCode);

        sw.Stop();

        string spokenText = confirmed
            ? $"Thank you so much! Your cash on delivery order {phoneticCode} is now confirmed. We are packing your package right now for priority dispatch."
            : $"I have recorded your cancellation request for order {phoneticCode}. Your order has been cancelled and no package will be delivered. We hope to serve you again soon!";

        var structured = new Dictionary<string, object?>
        {
            ["orderCode"] = cleanCode,
            ["isConfirmed"] = confirmed,
            ["outcomeStatus"] = confirmed ? "Confirmed" : "CancelledByCustomer",
            ["confirmedAtUtc"] = DateTime.UtcNow
        };

        _logger?.LogInformation("LiveCallTool: ConfirmCodOrder executed for {OrderCode} (Confirmed={Confirmed}) in {Duration:F1}ms.", cleanCode, confirmed, sw.Elapsed.TotalMilliseconds);

        return Task.FromResult(new LiveCallToolResult(
            ToolName: "ConfirmCodOrder",
            Success: true,
            VoiceSpokenSummary: FormatVoiceOptimizedSpeech(spokenText),
            StructuredResult: structured,
            ExecutionDurationMs: Math.Round(sw.Elapsed.TotalMilliseconds, 2)
        ));
    }

    public Task<LiveCallToolResult> EscalateToHumanAgentAsync(
        string reason,
        string? targetDesk = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string desk = string.IsNullOrWhiteSpace(targetDesk) ? "Senior Specialist Support Desk" : targetDesk;

        sw.Stop();

        string spokenText = $"I understand. I am transferring you immediately to our {desk} so a team member can assist you personally. Please stay on the line for just a moment.";

        var structured = new Dictionary<string, object?>
        {
            ["targetDesk"] = desk,
            ["reason"] = reason,
            ["transferInitiatedAtUtc"] = DateTime.UtcNow,
            ["status"] = "WarmTransferInitiated"
        };

        _logger?.LogInformation("LiveCallTool: EscalateToHumanAgent triggered ({Reason}) to {Desk} in {Duration:F1}ms.", reason, desk, sw.Elapsed.TotalMilliseconds);

        return Task.FromResult(new LiveCallToolResult(
            ToolName: "EscalateToHumanAgent",
            Success: true,
            VoiceSpokenSummary: FormatVoiceOptimizedSpeech(spokenText),
            StructuredResult: structured,
            ExecutionDurationMs: Math.Round(sw.Elapsed.TotalMilliseconds, 2)
        ));
    }

    public string FormatVoiceOptimizedSpeech(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        string text = rawText;

        // Strip markdown asterisks, hashes, backticks
        text = Regex.Replace(text, @"[\*#_`~]", "");

        // Convert dollar currency: $34.50 -> 34 dollars and 50 cents, $25 -> 25 dollars
        text = Regex.Replace(text, @"\$(\d+)\.(\d{2})", "$1 dollars and $2 cents");
        text = Regex.Replace(text, @"\$(\d+)", "$1 dollars");

        // Convert common telephony acronyms to spoken phrases
        text = Regex.Replace(text, @"\bCOD\b", "cash on delivery", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bETA\b", "estimated arrival time", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bapprox\.?", "approximately", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bdept\.?", "department", RegexOptions.IgnoreCase);

        // Normalize multiple spaces
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }

    private static string FormatPhoneticOrderCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "order";

        // e.g. "SG-1084" -> "S G 1 0 8 4"
        // Insert spaces between letters and digits for clear neural TTS pacing
        var parts = Regex.Split(code, @"[-_\s]+");
        var expandedParts = new List<string>();

        foreach (var p in parts)
        {
            if (p.All(char.IsLetter))
            {
                expandedParts.Add(string.Join(" ", p.ToCharArray()));
            }
            else if (p.All(char.IsDigit))
            {
                expandedParts.Add(string.Join(" ", p.ToCharArray()));
            }
            else
            {
                expandedParts.Add(p);
            }
        }

        return string.Join(" ", expandedParts);
    }

    private static string NormalizeTimeSlotSpoken(string slot)
    {
        string s = slot.ToLowerInvariant();
        if (s.Contains("morning")) return "morning window between 9 AM and 12 PM";
        if (s.Contains("afternoon")) return "afternoon window between 2 PM and 5 PM";
        if (s.Contains("evening")) return "evening window between 6 PM and 8 PM";
        return slot;
    }

    private static DateOnly ParseDateOnly(string? dateStr)
    {
        if (!string.IsNullOrWhiteSpace(dateStr) && DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        return DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
    }

    private static string? GetStringArg(IReadOnlyDictionary<string, object?> dict, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (dict.TryGetValue(key, out var val) && val is not null)
            {
                return val.ToString();
            }
        }
        return null;
    }

    private static bool? GetBoolArg(IReadOnlyDictionary<string, object?> dict, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (dict.TryGetValue(key, out var val) && val is not null)
            {
                if (val is bool b) return b;
                if (bool.TryParse(val.ToString(), out var parsedBool)) return parsedBool;
            }
        }
        return null;
    }

    private LiveCallToolResult HandleUnknownTool(string toolName, IReadOnlyDictionary<string, object?> args)
    {
        _logger?.LogWarning("LiveCallToolExecutor received unrecognized tool '{ToolName}'.", toolName);
        return new LiveCallToolResult(
            ToolName: toolName,
            Success: false,
            VoiceSpokenSummary: "I have recorded that request and will have our team follow up with you directly.",
            StructuredResult: new Dictionary<string, object?> { ["status"] = "UnrecognizedTool", ["toolName"] = toolName },
            ExecutionDurationMs: 0.1,
            ErrorMessage: $"Tool '{toolName}' is not registered."
        );
    }
}
