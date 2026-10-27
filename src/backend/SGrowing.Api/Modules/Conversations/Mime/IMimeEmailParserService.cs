namespace SGrowing.Api.Modules.Conversations.Mime;

public record EmailAttachmentDto(
    string FileName,
    string ContentType,
    string ContentId,
    bool IsInline,
    long SizeBytes,
    string? Base64Data
);

public record ParsedMimeEmail(
    string Subject,
    string From,
    string To,
    string? Cc,
    string? MessageId,
    string? InReplyTo,
    string? References,
    DateTime? DateUtc,
    string ContentType,
    string TextBody,
    string CleanTextBody,
    string HtmlBody,
    List<EmailAttachmentDto> Attachments
);

public interface IMimeEmailParserService
{
    ParsedMimeEmail ParseRawMime(string rawMimeContent);
    string CleanQuotedHistory(string body);
    string CleanSignature(string body);
}
