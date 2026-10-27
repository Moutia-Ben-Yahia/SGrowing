using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.CRM.Import;

public class CsvImportService(SGrowingDbContext db) : ICsvImportService
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public CsvImportPreviewResult PreviewCsv(string csvContent, CsvImportOptions? options = null)
    {
        var opt = options ?? new CsvImportOptions();
        var rows = ParseCsvLines(csvContent, opt.Delimiter);

        if (rows.Count == 0)
        {
            return new CsvImportPreviewResult(0, [], [], []);
        }

        var headers = rows[0];
        var sampleRows = new List<Dictionary<string, string>>();
        var previewErrors = new List<CsvRowValidationIssue>();

        int dataRowsCount = rows.Count - 1;
        int previewLimit = Math.Min(dataRowsCount, 5);

        for (int i = 1; i <= previewLimit; i++)
        {
            var row = rows[i];
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int col = 0; col < headers.Count; col++)
            {
                var val = col < row.Count ? row[col] : string.Empty;
                dict[headers[col]] = val;
            }
            sampleRows.Add(dict);
        }

        // Quick scan first 50 rows for validation issues
        for (int i = 1; i < Math.Min(rows.Count, 50); i++)
        {
            var row = rows[i];
            if (row.Count == 0 || (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])))
            {
                continue;
            }

            if (row.Count != headers.Count)
            {
                previewErrors.Add(new CsvRowValidationIssue(i + 1, null, "ColumnMismatch", $"Row has {row.Count} columns, expected {headers.Count}."));
            }
        }

        return new CsvImportPreviewResult(
            TotalRows: dataRowsCount,
            DetectedHeaders: headers,
            SampleRows: sampleRows,
            PreviewErrors: previewErrors
        );
    }

    public async Task<CsvImportExecutionResult> ImportContactsAsync(
        Guid tenantId,
        string csvContent,
        CsvImportMapping mapping,
        CsvImportOptions options,
        CancellationToken ct = default)
    {
        var rows = ParseCsvLines(csvContent, options.Delimiter);
        if (rows.Count <= 1)
        {
            return new CsvImportExecutionResult(
                TotalProcessed: 0,
                InsertedCount: 0,
                UpdatedCount: 0,
                SkippedDuplicatesCount: 0,
                ErrorCount: 0,
                WasRolledBack: false,
                StatusSummary: "No data rows found to import.",
                Errors: []
            );
        }

        var headers = rows[0];
        var headerIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            headerIndexMap[headers[i].Trim()] = i;
        }

        if (!headerIndexMap.TryGetValue(mapping.EmailColumn.Trim(), out int emailColIdx))
        {
            throw new ArgumentException($"Mapped email column '{mapping.EmailColumn}' was not found in CSV headers.");
        }

        int? firstColIdx = GetColumnIndex(headerIndexMap, mapping.FirstNameColumn);
        int? lastColIdx = GetColumnIndex(headerIndexMap, mapping.LastNameColumn);
        int? phoneColIdx = GetColumnIndex(headerIndexMap, mapping.PhoneColumn);
        int? compColIdx = GetColumnIndex(headerIndexMap, mapping.CompanyColumn);
        int? taxColIdx = GetColumnIndex(headerIndexMap, mapping.TaxIdColumn);
        int? titleColIdx = GetColumnIndex(headerIndexMap, mapping.TitleColumn);
        int? tagsColIdx = GetColumnIndex(headerIndexMap, mapping.TagsColumn);

        // Preload existing contacts for this tenant to fast-match duplicates by Email, Phone, or Tax ID
        var existingContacts = await db.Contacts
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(ct);

        var existingByEmail = existingContacts
            .Where(c => !string.IsNullOrEmpty(c.Email))
            .ToDictionary(c => c.Email!.ToLowerInvariant(), c => c, StringComparer.OrdinalIgnoreCase);

        var existingByPhone = existingContacts
            .Where(c => !string.IsNullOrEmpty(c.Phone))
            .ToDictionary(c => NormalizePhone(c.Phone!), c => c, StringComparer.OrdinalIgnoreCase);

        var existingByTaxId = new Dictionary<string, Contact>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in existingContacts)
        {
            var cf = c.GetCustomFields();
            if (cf != null && cf.TryGetValue("taxId", out var tVal) && tVal != null)
            {
                var s = tVal.ToString()?.Trim();
                if (!string.IsNullOrEmpty(s)) existingByTaxId[s] = c;
            }
        }

        var seenEmailsInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPhonesInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTaxIdsInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int inserted = 0;
        int updated = 0;
        int skipped = 0;
        var errors = new List<CsvRowValidationIssue>();
        var contactsToInsert = new List<Contact>();

        // Collect non-empty data rows
        var dataRows = new List<(int RowNum, List<string> Row)>();
        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count == 0 || (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])))
            {
                continue;
            }
            dataRows.Add((r + 1, row));
        }

        int totalDataRows = dataRows.Count;

        foreach (var (rowNum, row) in dataRows)
        {
            string email = (emailColIdx < row.Count ? row[emailColIdx] : string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            {
                errors.Add(new CsvRowValidationIssue(rowNum, email, "Email", "Invalid or missing email address format."));
                continue;
            }

            string firstName = firstColIdx.HasValue && firstColIdx.Value < row.Count ? row[firstColIdx.Value].Trim() : string.Empty;
            string lastName = lastColIdx.HasValue && lastColIdx.Value < row.Count ? row[lastColIdx.Value].Trim() : string.Empty;
            string phone = phoneColIdx.HasValue && phoneColIdx.Value < row.Count ? row[phoneColIdx.Value].Trim() : string.Empty;
            string companyName = compColIdx.HasValue && compColIdx.Value < row.Count ? row[compColIdx.Value].Trim() : string.Empty;
            string taxId = taxColIdx.HasValue && taxColIdx.Value < row.Count ? row[taxColIdx.Value].Trim() : string.Empty;
            string title = titleColIdx.HasValue && titleColIdx.Value < row.Count ? row[titleColIdx.Value].Trim() : string.Empty;
            string tagsRaw = tagsColIdx.HasValue && tagsColIdx.Value < row.Count ? row[tagsColIdx.Value].Trim() : string.Empty;

            var tagList = !string.IsNullOrWhiteSpace(tagsRaw)
                ? tagsRaw.Split(new[] { ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : new List<string>();

            // In-batch duplicate check across email, phone, and tax ID
            bool isDuplicateInBatch = seenEmailsInBatch.Contains(email) ||
                (!string.IsNullOrEmpty(phone) && seenPhonesInBatch.Contains(NormalizePhone(phone))) ||
                (!string.IsNullOrEmpty(taxId) && seenTaxIdsInBatch.Contains(taxId.Trim()));

            if (isDuplicateInBatch && options.MergeStrategy == DuplicateMergeStrategy.SkipDuplicate)
            {
                skipped++;
                continue;
            }

            seenEmailsInBatch.Add(email);
            if (!string.IsNullOrEmpty(phone)) seenPhonesInBatch.Add(NormalizePhone(phone));
            if (!string.IsNullOrEmpty(taxId)) seenTaxIdsInBatch.Add(taxId.Trim());

            // Duplicate detection heuristic: check email then phone then tax ID
            Contact? existing = null;
            if (existingByEmail.TryGetValue(email, out var byEmail))
            {
                existing = byEmail;
            }
            else if (!string.IsNullOrEmpty(phone) && existingByPhone.TryGetValue(NormalizePhone(phone), out var byPhone))
            {
                existing = byPhone;
            }
            else if (!string.IsNullOrEmpty(taxId) && existingByTaxId.TryGetValue(taxId.Trim(), out var byTax))
            {
                existing = byTax;
            }

            if (existing != null)
            {
                if (options.MergeStrategy == DuplicateMergeStrategy.SkipDuplicate)
                {
                    skipped++;
                    continue;
                }

                if (options.MergeStrategy == DuplicateMergeStrategy.OverwriteExisting)
                {
                    if (!string.IsNullOrEmpty(firstName)) existing.FirstName = firstName;
                    if (!string.IsNullOrEmpty(lastName)) existing.LastName = lastName;
                    if (!string.IsNullOrEmpty(phone)) existing.Phone = phone;
                    if (tagList.Count > 0) existing.Tags = tagList;
                    if (!string.IsNullOrEmpty(companyName) || !string.IsNullOrEmpty(taxId))
                    {
                        var cf = existing.GetCustomFields();
                        if (!string.IsNullOrEmpty(companyName)) cf["company"] = companyName;
                        if (!string.IsNullOrEmpty(taxId)) cf["taxId"] = taxId;
                        existing.CustomFieldsJson = System.Text.Json.JsonSerializer.Serialize(cf);
                    }
                    updated++;
                }
                else if (options.MergeStrategy == DuplicateMergeStrategy.UpdateBlankFieldsOnly)
                {
                    if (string.IsNullOrEmpty(existing.FirstName) && !string.IsNullOrEmpty(firstName)) existing.FirstName = firstName;
                    if (string.IsNullOrEmpty(existing.LastName) && !string.IsNullOrEmpty(lastName)) existing.LastName = lastName;
                    if (string.IsNullOrEmpty(existing.Phone) && !string.IsNullOrEmpty(phone)) existing.Phone = phone;
                    if ((existing.Tags == null || existing.Tags.Count == 0) && tagList.Count > 0) existing.Tags = tagList;
                    if (!string.IsNullOrEmpty(companyName) || !string.IsNullOrEmpty(taxId))
                    {
                        var cf = existing.GetCustomFields();
                        if (!cf.ContainsKey("company") && !string.IsNullOrEmpty(companyName)) cf["company"] = companyName;
                        if (!cf.ContainsKey("taxId") && !string.IsNullOrEmpty(taxId)) cf["taxId"] = taxId;
                        existing.CustomFieldsJson = System.Text.Json.JsonSerializer.Serialize(cf);
                    }
                    updated++;
                }
            }
            else
            {
                var customFields = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(companyName)) customFields["company"] = companyName;
                if (!string.IsNullOrEmpty(taxId)) customFields["taxId"] = taxId;

                var newContact = Contact.Create(
                    tenantId,
                    firstName: string.IsNullOrEmpty(firstName) ? email.Split('@')[0] : firstName,
                    lastName: lastName,
                    email: email,
                    phone: string.IsNullOrEmpty(phone) ? null : phone,
                    stage: LifecycleStage.Lead,
                    customFields: customFields.Count > 0 ? customFields : null,
                    tags: tagList
                );
                contactsToInsert.Add(newContact);
                existingByEmail[email] = newContact;
                if (!string.IsNullOrEmpty(phone)) existingByPhone[NormalizePhone(phone)] = newContact;
                if (!string.IsNullOrEmpty(taxId)) existingByTaxId[taxId.Trim()] = newContact;
                inserted++;
            }
        }

        double errorPct = totalDataRows > 0 ? (double)errors.Count / totalDataRows * 100.0 : 0.0;
        bool shouldRollback = options.RollbackOnError && (errorPct > options.MaxErrorPercentageTolerance);

        if (shouldRollback)
        {
            // Clear change tracker to discard any in-memory modified existing entity states
            db.ChangeTracker.Clear();
            return new CsvImportExecutionResult(
                TotalProcessed: totalDataRows,
                InsertedCount: 0,
                UpdatedCount: 0,
                SkippedDuplicatesCount: 0,
                ErrorCount: errors.Count,
                WasRolledBack: true,
                StatusSummary: $"Import aborted and rolled back: Error rate ({errorPct:F1}%) exceeded tolerance ({options.MaxErrorPercentageTolerance:F1}%).",
                Errors: errors
            );
        }

        if (contactsToInsert.Count > 0)
        {
            await db.Contacts.AddRangeAsync(contactsToInsert, ct);
        }

        await db.SaveChangesAsync(ct);

        return new CsvImportExecutionResult(
            TotalProcessed: totalDataRows,
            InsertedCount: inserted,
            UpdatedCount: updated,
            SkippedDuplicatesCount: skipped,
            ErrorCount: errors.Count,
            WasRolledBack: false,
            StatusSummary: $"Import completed successfully. Inserted: {inserted}, Updated: {updated}, Skipped: {skipped}, Errors: {errors.Count}.",
            Errors: errors
        );
    }

    private static int? GetColumnIndex(Dictionary<string, int> map, string? colName)
    {
        if (string.IsNullOrWhiteSpace(colName)) return null;
        return map.TryGetValue(colName.Trim(), out int idx) ? idx : null;
    }

    private static string NormalizePhone(string phone)
    {
        return Regex.Replace(phone, @"[^\d+]", "");
    }

    public static List<List<string>> ParseCsvLines(string csv, char delimiter)
    {
        var result = new List<List<string>>();
        if (string.IsNullOrEmpty(csv)) return result;

        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;
        int i = 0;

        // Skip UTF-8 BOM if present
        if (csv.Length > 0 && csv[0] == '\uFEFF') i++;

        while (i < csv.Length)
        {
            char c = csv[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Check for escaped quote ("")
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i += 2;
                        continue;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                        continue;
                    }
                }
                else
                {
                    currentField.Append(c);
                    i++;
                    continue;
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                    continue;
                }
                else if (c == delimiter)
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    i++;
                    continue;
                }
                else if (c == '\r')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    result.Add(currentRow);
                    currentRow = new List<string>();
                    i++;
                    continue;
                }
                else if (c == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    result.Add(currentRow);
                    currentRow = new List<string>();
                    i++;
                    continue;
                }
                else
                {
                    currentField.Append(c);
                    i++;
                    continue;
                }
            }
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            result.Add(currentRow);
        }

        return result;
    }
}
