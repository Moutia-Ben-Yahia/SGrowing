using System.Text;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Conversations.Mime;

public class MimeEmailParserService : IMimeEmailParserService
{
    private static readonly Regex QuotedHeaderRegex = new(
        @"(^On\s+.*wrote:)|(^From:\s+.*)|(^-----Original Message-----)|(^-+\s*Forwarded message\s*-+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex SignatureRegex = new(
        @"(\r?\n--\s*\r?\n.*)|(\r?\nSent from my (iPhone|Android|iPad|Galaxy).*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public ParsedMimeEmail ParseRawMime(string rawMimeContent)
    {
        if (string.IsNullOrWhiteSpace(rawMimeContent))
        {
            return new ParsedMimeEmail(
                Subject: string.Empty,
                From: string.Empty,
                To: string.Empty,
                Cc: null,
                MessageId: null,
                InReplyTo: null,
                References: null,
                DateUtc: DateTime.UtcNow,
                ContentType: "text/plain",
                TextBody: string.Empty,
                CleanTextBody: string.Empty,
                HtmlBody: string.Empty,
                Attachments: []
            );
        }

        // Split top headers from body
        var headerBodySplit = rawMimeContent.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (headerBodySplit < 0)
        {
            headerBodySplit = rawMimeContent.IndexOf("\n\n", StringComparison.Ordinal);
        }

        string rawHeaders = headerBodySplit >= 0 ? rawMimeContent[..headerBodySplit] : rawMimeContent;
        string rawBody = headerBodySplit >= 0 ? rawMimeContent[(headerBodySplit + (rawMimeContent.Contains("\r\n\r\n") ? 4 : 2))..] : string.Empty;

        var headers = ParseHeaders(rawHeaders);

        var subject = headers.GetValueOrDefault("Subject", string.Empty);
        var from = headers.GetValueOrDefault("From", string.Empty);
        var to = headers.GetValueOrDefault("To", string.Empty);
        var cc = headers.GetValueOrDefault("Cc");
        var messageId = headers.GetValueOrDefault("Message-ID") ?? headers.GetValueOrDefault("Message-Id");
        var inReplyTo = headers.GetValueOrDefault("In-Reply-To");
        var references = headers.GetValueOrDefault("References");
        var contentType = headers.GetValueOrDefault("Content-Type", "text/plain");

        DateTime? dateUtc = null;
        if (headers.TryGetValue("Date", out var dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
        {
            dateUtc = parsedDate.ToUniversalTime();
        }

        string textBody = string.Empty;
        string htmlBody = string.Empty;
        var attachments = new List<EmailAttachmentDto>();

        // Check if multipart
        var boundaryMatch = Regex.Match(contentType, @"boundary=""?([^"";\s]+)""?", RegexOptions.IgnoreCase);
        if (boundaryMatch.Success)
        {
            var boundary = boundaryMatch.Groups[1].Value;
            ParseMultipart(rawBody, boundary, ref textBody, ref htmlBody, attachments);
        }
        else if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            htmlBody = rawBody;
            textBody = StripHtmlTags(rawBody);
        }
        else
        {
            textBody = rawBody;
        }

        var cleanText = CleanQuotedHistory(textBody);
        cleanText = CleanSignature(cleanText);

        return new ParsedMimeEmail(
            Subject: subject,
            From: from,
            To: to,
            Cc: cc,
            MessageId: messageId,
            InReplyTo: inReplyTo,
            References: references,
            DateUtc: dateUtc ?? DateTime.UtcNow,
            ContentType: contentType,
            TextBody: textBody.Trim(),
            CleanTextBody: cleanText.Trim(),
            HtmlBody: htmlBody.Trim(),
            Attachments: attachments
        );
    }

    public string CleanQuotedHistory(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        // Split into lines
        var lines = body.Split(['\r', '\n'], StringSplitOptions.None);
        var result = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Stop if quote header encountered
            if (QuotedHeaderRegex.IsMatch(line))
            {
                break;
            }

            // Skip lines that begin with quote mark '>'
            if (line.TrimStart().StartsWith('>'))
            {
                continue;
            }

            result.AppendLine(line);
        }

        return result.ToString().TrimEnd();
    }

    public string CleanSignature(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        return SignatureRegex.Replace(body, string.Empty).TrimEnd();
    }

    private static Dictionary<string, string> ParseHeaders(string rawHeaders)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = rawHeaders.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        string? currentKey = null;
        var currentValue = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.StartsWith(' ') || line.StartsWith('\t'))
            {
                // Header continuation line (folding)
                currentValue.Append(' ').Append(line.Trim());
            }
            else
            {
                if (currentKey != null)
                {
                    headers[currentKey] = currentValue.ToString().Trim();
                }

                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    currentKey = line[..colonIdx].Trim();
                    currentValue.Clear();
                    currentValue.Append(line[(colonIdx + 1)..].Trim());
                }
            }
        }

        if (currentKey != null)
        {
            headers[currentKey] = currentValue.ToString().Trim();
        }

        return headers;
    }

    private static void ParseMultipart(
        string rawBody,
        string boundary,
        ref string textBody,
        ref string htmlBody,
        List<EmailAttachmentDto> attachments)
    {
        var delimiter = "--" + boundary;
        var parts = rawBody.Split([delimiter], StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            if (trimmedPart.StartsWith("--") || string.IsNullOrWhiteSpace(trimmedPart))
            {
                continue; // closing boundary or blank
            }

            var partSplit = trimmedPart.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (partSplit < 0) partSplit = trimmedPart.IndexOf("\n\n", StringComparison.Ordinal);

            if (partSplit < 0) continue;

            var partHeadersRaw = trimmedPart[..partSplit];
            var partBody = trimmedPart[(partSplit + (trimmedPart.Contains("\r\n\r\n") ? 4 : 2))..];
            var partHeaders = ParseHeaders(partHeadersRaw);

            var partType = partHeaders.GetValueOrDefault("Content-Type", "text/plain");
            var partDisp = partHeaders.GetValueOrDefault("Content-Disposition", "");
            var contentId = partHeaders.GetValueOrDefault("Content-ID", "").Trim('<', '>');

            // Check if sub-boundary exists (e.g. multipart/alternative inside multipart/mixed)
            var subMatch = Regex.Match(partType, @"boundary=""?([^"";\s]+)""?", RegexOptions.IgnoreCase);
            if (subMatch.Success)
            {
                ParseMultipart(partBody, subMatch.Groups[1].Value, ref textBody, ref htmlBody, attachments);
                continue;
            }

            // Check if attachment
            bool isAttachment = partDisp.Contains("attachment", StringComparison.OrdinalIgnoreCase) ||
                                !string.IsNullOrEmpty(contentId) ||
                                partDisp.Contains("filename", StringComparison.OrdinalIgnoreCase);

            if (isAttachment)
            {
                var filenameMatch = Regex.Match(partDisp, @"filename=""?([^"";\r\n]+)""?", RegexOptions.IgnoreCase);
                var filename = filenameMatch.Success ? filenameMatch.Groups[1].Value : "attachment.bin";
                bool isInline = partDisp.Contains("inline", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(contentId);

                attachments.Add(new EmailAttachmentDto(
                    FileName: filename,
                    ContentType: partType.Split(';')[0].Trim(),
                    ContentId: contentId,
                    IsInline: isInline,
                    SizeBytes: partBody.Length,
                    Base64Data: partBody.Length < 100_000 ? partBody.Trim() : null
                ));
            }
            else if (partType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                htmlBody = partBody.Trim();
            }
            else if (partType.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
            {
                textBody = partBody.Trim();
            }
        }
    }

    private static string StripHtmlTags(string html)
    {
        return Regex.Replace(html, "<.*?>", string.Empty);
    }
}
