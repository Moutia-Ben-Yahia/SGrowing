namespace SGrowing.Api.Modules.Analytics.SessionReplay;

/// <summary>
/// Privacy and PII scrubbing vault service compliant with GDPR Article 25 and Algerian Law 18-07.
/// </summary>
public interface IPiiScrubbingVaultService
{
    /// <summary>
    /// Scrubs and sanitizes an entire DOM mutation batch, masking PII and stripping executable scripts.
    /// </summary>
    DomMutationBatch ScrubMutationBatch(DomMutationBatch batch);

    /// <summary>
    /// Scans a text payload for credit cards (Luhn-checked), emails, phones, NIN, and secrets, returning masked text.
    /// </summary>
    string ScrubTextPayload(string text);

    /// <summary>
    /// Evaluates if an element or attribute denotes a sensitive privacy field.
    /// </summary>
    bool IsSensitiveField(string fieldName, string? inputType = null);
}
