namespace SGrowing.Api.Modules.SEO.Programmatic;

public enum PagePublishStatus
{
    Draft,
    Generated,
    Indexed
}

public record ProgrammaticPage(
    string PageId,
    string Slug,
    string TargetKeyword,
    string Industry,
    string MetaTitle,
    string MetaDescription,
    string H1Title,
    string SchemaJsonLd,
    List<string> InternalLinks,
    int WordCount,
    PagePublishStatus Status,
    DateTime PublishedAtUtc
);

public record BacklinkOpportunity(
    string OpportunityId,
    string TargetDomain,
    int DomainAuthority, // 0 - 100
    string CompetitorUrl,
    string OutreachStatus, // "Identified", "Pitched", "Secured"
    string SuggestedAnchorText
);

public record BatchGeneratePagesRequest(
    string BaseSlugPattern, // e.g. "crm-for-{industry}"
    List<string> Industries,
    string TargetAudience
);

public record IndexNowSubmissionResult(
    string Engine, // "Bing", "Yandex", "IndexNow-Mesh"
    int UrlsSubmitted,
    string ResponseCode, // "202 Accepted"
    DateTime SubmittedAtUtc
);
