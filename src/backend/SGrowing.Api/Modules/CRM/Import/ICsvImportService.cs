namespace SGrowing.Api.Modules.CRM.Import;

public enum DuplicateMergeStrategy
{
    SkipDuplicate,
    OverwriteExisting,
    UpdateBlankFieldsOnly
}

public record CsvImportMapping(
    string EmailColumn,
    string? FirstNameColumn = null,
    string? LastNameColumn = null,
    string? PhoneColumn = null,
    string? CompanyColumn = null,
    string? TaxIdColumn = null,
    string? TitleColumn = null,
    string? TagsColumn = null
);

public record CsvImportOptions(
    char Delimiter = ',',
    bool HasHeader = true,
    DuplicateMergeStrategy MergeStrategy = DuplicateMergeStrategy.UpdateBlankFieldsOnly,
    int BatchSize = 250,
    bool RollbackOnError = true,
    double MaxErrorPercentageTolerance = 10.0 // Abort & rollback if > 10% errors
);

public record CsvRowValidationIssue(
    int RowNumber,
    string? Email,
    string FieldName,
    string ErrorMessage
);

public record CsvImportPreviewResult(
    int TotalRows,
    List<string> DetectedHeaders,
    List<Dictionary<string, string>> SampleRows,
    List<CsvRowValidationIssue> PreviewErrors
);

public record CsvImportExecutionResult(
    int TotalProcessed,
    int InsertedCount,
    int UpdatedCount,
    int SkippedDuplicatesCount,
    int ErrorCount,
    bool WasRolledBack,
    string StatusSummary,
    List<CsvRowValidationIssue> Errors
);

public interface ICsvImportService
{
    CsvImportPreviewResult PreviewCsv(string csvContent, CsvImportOptions? options = null);
    Task<CsvImportExecutionResult> ImportContactsAsync(
        Guid tenantId,
        string csvContent,
        CsvImportMapping mapping,
        CsvImportOptions options,
        CancellationToken ct = default);
}
