using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Documents.Idp;

/// <summary>
/// Service contract for multi-modal OCR ingestion, spatial token alignment,
/// key-value extraction, and tabular grid recognition for invoices.
/// </summary>
public interface IDocumentIntelligenceParserService
{
    /// <summary>
    /// Ingests raw document data or OCR bounding boxes, performs spatial analysis,
    /// extracts structured invoice data, and computes document cryptographic hash.
    /// </summary>
    Task<ParseDocumentResponse> ParseInvoiceDocumentAsync(
        ParseDocumentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes lowercase SHA-256 hex digest of raw binary data.
    /// </summary>
    string ComputeDocumentSha256(byte[] data);

    /// <summary>
    /// Computes lowercase SHA-256 hex digest of textual content.
    /// </summary>
    string ComputeDocumentSha256(string text);

    /// <summary>
    /// Computes lowercase SHA-256 hex digest of OCR bounding box tokens.
    /// </summary>
    string ComputeDocumentSha256(IEnumerable<OcrBoundingBox> tokens);

    /// <summary>
    /// Organizes planar OCR coordinate tokens into structured tabular rows with spatial clustering.
    /// </summary>
    IReadOnlyList<ExtractedLineItem> ExtractTabularLineItems(
        IReadOnlyList<OcrBoundingBox> tokens,
        double rowToleranceY = 12.0);
}

/// <summary>
/// Production implementation of intelligent document processing parser with spatial token clustering,
/// multi-lingual pattern matching (French/Algerian/English), and tabular invoice extraction.
/// </summary>
public partial class DocumentIntelligenceParserService : IDocumentIntelligenceParserService
{
    #region Regex Definitions

    private static readonly Regex VendorLabelRegex = new(
        @"(?:Fournisseur|Vendor|Société|Supplier|Company)\s*[:#]?\s*(.+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NifRegex = new(
        @"(?:NIF|Tax\s*ID|Identifiant\s*Fiscal|CIF)\s*[:#]?\s*([0-9A-Za-z\-_]{8,25})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RcRegex = new(
        @"(?:RC|Registre\s*(?:du)?\s*Commerce)\s*[:#]?\s*([0-9A-Za-z\/\-_]{6,25})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VatRegex = new(
        @"(?:TVA\s*Intra(?:communautaire)?|VAT\s*No|VAT\s*ID|VAT)\s*[:#]?\s*([A-Za-z]{0,2}[0-9A-Za-z]{8,15})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InvoiceNumRegex = new(
        @"(?:Facture\s*(?:N°|no|numéro)?|Invoice\s*(?:#|no|number|num)?)\s*[:#]?\s*([A-Za-z0-9\-_/]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DateRegex = new(
        @"(?:Date(?:\s+de\s+facture)?|Invoice\s*Date|Émise\s+le)\s*[:]?\s*(\d{1,4}[/\-\.]\d{1,2}[/\-\.]\d{1,4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RibRegex = new(
        @"(?:RIB|IBAN|Compte\s*Bancaire|Bank\s*Account)\s*[:]?\s*([A-Za-z0-9 \-]{10,40})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SubtotalRegex = new(
        @"(?:Total\s*HT|Sous[\-\s]total|Subtotal|Montant\s*HT)\s*[:]?\s*([\d\s,\.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TaxRegex = new(
        @"(?:Total\s*TVA|Montant\s*TVA|TVA\s*(?:\(\s*\d+%\s*\))?|Tax\s*Amount)\s*[:]?\s*([\d\s,\.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TotalRegex = new(
        @"(?:Total\s*TTC|Net\s*(?:à|a)\s*payer|Grand\s*Total|Total\s*Amount|Montant\s*Total)\s*[:]?\s*([\d\s,\.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    #endregion

    /// <inheritdoc />
    public Task<ParseDocumentResponse> ParseInvoiceDocumentAsync(
        ParseDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // 1. Calculate Document Hash
            var sha256 = DetermineDocumentHash(request);

            // 2. Prepare tokens and textual representation
            var tokens = request.Tokens ?? [];
            var text = request.RawText ?? BuildTextFromTokens(tokens);

            // If raw bytes are provided and no text/tokens, convert ASCII/UTF8 if readable
            if (string.IsNullOrWhiteSpace(text) && request.RawData is { Length: > 0 })
            {
                text = Encoding.UTF8.GetString(request.RawData);
            }

            // 3. Extract Tabular Line Items
            IReadOnlyList<ExtractedLineItem> lineItems = [];
            if (tokens.Count > 0)
            {
                lineItems = ExtractTabularLineItems(tokens);
            }

            if (lineItems.Count == 0 && !string.IsNullOrWhiteSpace(text))
            {
                lineItems = ExtractLineItemsFromText(text);
            }

            // 4. Extract Key Metadata Fields
            var vendorName = ExtractVendorName(tokens, text);
            var vendorTaxId = ExtractVendorTaxId(text);
            var invoiceNumber = ExtractInvoiceNumber(tokens, text, request.DocumentId);
            var invoiceDate = ExtractInvoiceDate(text);
            var currency = ExtractCurrency(text);
            var bankRib = ExtractBankRib(text);

            // 5. Extract Totals with Mathematical Cross-Verification
            var subtotal = ExtractAmount(SubtotalRegex, text);
            var taxAmount = ExtractAmount(TaxRegex, text);
            var totalAmount = ExtractAmount(TotalRegex, text);

            // Recalculate / reconcile totals from line items if missing or zero
            if (lineItems.Count > 0)
            {
                var sumLineTotals = lineItems.Sum(li => li.LineTotal);
                if (subtotal == 0m)
                {
                    subtotal = sumLineTotals;
                }

                if (taxAmount == 0m)
                {
                    taxAmount = lineItems.Sum(li =>
                    {
                        var rate = li.TaxRate > 1.0m ? li.TaxRate / 100.0m : li.TaxRate;
                        return Math.Round(li.LineTotal * rate, 2);
                    });
                }

                if (totalAmount == 0m)
                {
                    totalAmount = subtotal + taxAmount;
                }
            }

            // Fallback total check
            if (totalAmount == 0m && subtotal > 0m)
            {
                totalAmount = subtotal + taxAmount;
            }

            // 6. Calculate Confidence Score
            var confidenceScore = CalculateConfidenceScore(
                tokens,
                vendorName,
                vendorTaxId,
                invoiceNumber,
                invoiceDate,
                totalAmount,
                lineItems.Count);

            var extractedData = new ExtractedInvoiceData(
                DocumentId: request.DocumentId,
                VendorName: vendorName,
                VendorTaxId: vendorTaxId,
                InvoiceNumber: invoiceNumber,
                InvoiceDate: invoiceDate,
                Currency: currency,
                LineItems: lineItems,
                Subtotal: subtotal,
                TaxAmount: taxAmount,
                TotalAmount: totalAmount,
                BankRib: bankRib,
                ConfidenceScore: confidenceScore,
                Sha256Hash: sha256);

            return Task.FromResult(new ParseDocumentResponse(
                Success: true,
                ExtractedData: extractedData));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ParseDocumentResponse(
                Success: false,
                ExtractedData: null,
                ErrorMessage: $"Failed to parse document: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public string ComputeDocumentSha256(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }

    /// <inheritdoc />
    public string ComputeDocumentSha256(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ComputeDocumentSha256(Encoding.UTF8.GetBytes(text));
    }

    /// <inheritdoc />
    public string ComputeDocumentSha256(IEnumerable<OcrBoundingBox> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var sb = new StringBuilder();
        foreach (var t in tokens.OrderBy(t => t.Y).ThenBy(t => t.X))
        {
            sb.Append($"{t.X:F1},{t.Y:F1}:{t.Text};");
        }
        return ComputeDocumentSha256(sb.ToString());
    }

    /// <inheritdoc />
    public IReadOnlyList<ExtractedLineItem> ExtractTabularLineItems(
        IReadOnlyList<OcrBoundingBox> tokens,
        double rowToleranceY = 12.0)
    {
        if (tokens.Count == 0) return [];

        // 1. Group tokens into horizontal rows based on vertical proximity
        var sortedTokens = tokens.OrderBy(t => t.Y).ThenBy(t => t.X).ToList();
        var rows = new List<List<OcrBoundingBox>>();

        foreach (var token in sortedTokens)
        {
            var matchedRow = rows.FirstOrDefault(r =>
            {
                var avgY = r.Average(t => t.CenterY);
                return Math.Abs(token.CenterY - avgY) <= rowToleranceY;
            });

            if (matchedRow is not null)
            {
                matchedRow.Add(token);
            }
            else
            {
                rows.Add([token]);
            }
        }

        // Sort tokens within each row left to right
        foreach (var r in rows)
        {
            r.Sort((a, b) => a.X.CompareTo(b.X));
        }

        // 2. Identify Table Header Boundaries
        int headerIndex = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            var rowText = string.Join(" ", rows[i].Select(t => t.Text)).ToLowerInvariant();
            if ((rowText.Contains("sku") || rowText.Contains("ref") || rowText.Contains("item") || rowText.Contains("code")) &&
                (rowText.Contains("desc") || rowText.Contains("désign") || rowText.Contains("design") || rowText.Contains("article") || rowText.Contains("qty") || rowText.Contains("quant")))
            {
                headerIndex = i;
                break;
            }
        }

        // Identify table end boundary (totals section)
        int footerIndex = rows.Count;
        for (int i = Math.Max(0, headerIndex + 1); i < rows.Count; i++)
        {
            var rowText = string.Join(" ", rows[i].Select(t => t.Text)).ToLowerInvariant();
            if (rowText.Contains("total ht") || rowText.Contains("sous-total") ||
                rowText.Contains("total ttc") || rowText.Contains("net à payer") ||
                rowText.Contains("tva") || rowText.Contains("subtotal"))
            {
                footerIndex = i;
                break;
            }
        }

        var startIdx = headerIndex >= 0 ? headerIndex + 1 : 0;
        var lineItems = new List<ExtractedLineItem>();
        int lineCounter = 1;

        for (int i = startIdx; i < footerIndex; i++)
        {
            var row = rows[i];
            if (row.Count < 2) continue; // Skip orphan tokens

            var parsed = ParseTableRow(row, lineCounter);
            if (parsed is not null)
            {
                lineItems.Add(parsed);
                lineCounter++;
            }
        }

        return lineItems;
    }

    #region Helper Parsing Methods

    private string DetermineDocumentHash(ParseDocumentRequest request)
    {
        if (request.RawData is { Length: > 0 })
        {
            return ComputeDocumentSha256(request.RawData);
        }

        if (!string.IsNullOrWhiteSpace(request.RawText))
        {
            return ComputeDocumentSha256(request.RawText);
        }

        if (request.Tokens is { Count: > 0 })
        {
            return ComputeDocumentSha256(request.Tokens);
        }

        return ComputeDocumentSha256(request.DocumentId);
    }

    private static string BuildTextFromTokens(IReadOnlyList<OcrBoundingBox> tokens)
    {
        if (tokens.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var ordered = tokens.OrderBy(t => t.Y).ThenBy(t => t.X).ToList();

        double lastY = -1;
        foreach (var token in ordered)
        {
            if (lastY >= 0 && Math.Abs(token.CenterY - lastY) > 12.0)
            {
                sb.AppendLine();
            }
            else if (lastY >= 0)
            {
                sb.Append(' ');
            }

            sb.Append(token.Text);
            lastY = token.CenterY;
        }

        return sb.ToString();
    }

    private static string ExtractVendorName(IReadOnlyList<OcrBoundingBox> tokens, string text)
    {
        // 1. Try regex pattern in text
        var match = VendorLabelRegex.Match(text);
        if (match.Success)
        {
            var val = match.Groups[1].Value.Trim();
            var lineEnd = val.IndexOfAny(['\r', '\n']);
            if (lineEnd > 0) val = val[..lineEnd].Trim();
            if (!string.IsNullOrWhiteSpace(val)) return val;
        }

        // 2. Check tokens near header (Y < 120)
        var topTokens = tokens.Where(t => t.Y < 120).OrderBy(t => t.Y).ThenBy(t => t.X).ToList();
        if (topTokens.Count > 0)
        {
            var topText = string.Join(" ", topTokens.Select(t => t.Text)).Trim();
            if (!topText.StartsWith("FACTURE", StringComparison.OrdinalIgnoreCase) &&
                !topText.StartsWith("INVOICE", StringComparison.OrdinalIgnoreCase) &&
                topText.Length > 2)
            {
                return topText;
            }
        }

        // 3. Fallback to first line in text
        var firstLine = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstLine) && firstLine.Length < 60)
        {
            return firstLine.Trim();
        }

        return "UNKNOWN_VENDOR";
    }

    private static string ExtractVendorTaxId(string text)
    {
        var nifMatch = NifRegex.Match(text);
        var rcMatch = RcRegex.Match(text);
        var vatMatch = VatRegex.Match(text);

        if (nifMatch.Success && rcMatch.Success)
        {
            return $"NIF: {nifMatch.Groups[1].Value.Trim()} / RC: {rcMatch.Groups[1].Value.Trim()}";
        }

        if (nifMatch.Success) return nifMatch.Groups[1].Value.Trim();
        if (rcMatch.Success) return rcMatch.Groups[1].Value.Trim();
        if (vatMatch.Success) return vatMatch.Groups[1].Value.Trim();

        return "N/A";
    }

    private static string ExtractInvoiceNumber(
        IReadOnlyList<OcrBoundingBox> tokens,
        string text,
        string fallbackId)
    {
        var match = InvoiceNumRegex.Match(text);
        if (match.Success)
        {
            var val = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(val)) return val;
        }

        // Check token text directly
        foreach (var t in tokens)
        {
            var tMatch = InvoiceNumRegex.Match(t.Text);
            if (tMatch.Success)
            {
                return tMatch.Groups[1].Value.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(fallbackId) ? $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}" : fallbackId;
    }

    private static DateTime ExtractInvoiceDate(string text)
    {
        var match = DateRegex.Match(text);
        if (match.Success)
        {
            var dateStr = match.Groups[1].Value.Trim();
            string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "MM/dd/yyyy", "yyyy/MM/dd", "dd.MM.yyyy"];
            if (DateTime.TryParseExact(dateStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            if (DateTime.TryParse(dateStr, out var fallbackDt))
            {
                return DateTime.SpecifyKind(fallbackDt, DateTimeKind.Utc);
            }
        }

        return DateTime.UtcNow.Date;
    }

    private static string ExtractCurrency(string text)
    {
        if (text.Contains("DZD", StringComparison.OrdinalIgnoreCase) || text.Contains("DA", StringComparison.OrdinalIgnoreCase))
            return "DZD";
        if (text.Contains("EUR", StringComparison.OrdinalIgnoreCase) || text.Contains('€'))
            return "EUR";
        if (text.Contains("USD", StringComparison.OrdinalIgnoreCase) || text.Contains('$'))
            return "USD";
        if (text.Contains("GBP", StringComparison.OrdinalIgnoreCase) || text.Contains('£'))
            return "GBP";
        if (text.Contains("MAD", StringComparison.OrdinalIgnoreCase))
            return "MAD";
        if (text.Contains("TND", StringComparison.OrdinalIgnoreCase))
            return "TND";

        return "DZD";
    }

    private static string ExtractBankRib(string text)
    {
        var match = RibRegex.Match(text);
        if (match.Success)
        {
            var raw = match.Groups[1].Value;
            var lineEnd = raw.IndexOfAny(['\r', '\n']);
            if (lineEnd >= 0) raw = raw[..lineEnd];
            return raw.Replace(" ", "").Replace("-", "").Trim();
        }

        return "N/A";
    }

    private static decimal ExtractAmount(Regex regex, string text)
    {
        var match = regex.Match(text);
        if (match.Success)
        {
            var rawVal = match.Groups[1].Value.Trim();
            return ParseDecimalFlexible(rawVal);
        }

        return 0m;
    }

    private static decimal ParseDecimalFlexible(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return 0m;

        // Strip currency symbols and extraneous chars except digits, comma, period
        var cleaned = Regex.Replace(input, @"[^\d,.\-]", "").Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) return 0m;

        // If contains both '.' and ',', determine decimal separator
        if (cleaned.Contains('.') && cleaned.Contains(','))
        {
            if (cleaned.LastIndexOf(',') > cleaned.LastIndexOf('.'))
            {
                // French format: 1.250,50 -> 1250.50
                cleaned = cleaned.Replace(".", "").Replace(',', '.');
            }
            else
            {
                // US format: 1,250.50 -> 1250.50
                cleaned = cleaned.Replace(",", "");
            }
        }
        else if (cleaned.Contains(','))
        {
            cleaned = cleaned.Replace(',', '.');
        }

        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return 0m;
    }

    private static ExtractedLineItem? ParseTableRow(List<OcrBoundingBox> row, int lineCounter)
    {
        // Require at least a description/sku and numeric values
        var combinedText = string.Join(" | ", row.Select(t => t.Text));

        // Find numeric tokens in row (purely numeric, currency, or percentage tokens)
        var numericTokens = new List<(OcrBoundingBox Box, decimal Value)>();
        foreach (var token in row)
        {
            var trimmed = token.Text.Trim();
            if (Regex.IsMatch(trimmed, @"^[\$€£]?\s*[\d,.\-]+\s*[%]?$"))
            {
                var val = ParseDecimalFlexible(token.Text);
                if (val > 0)
                {
                    numericTokens.Add((token, val));
                }
            }
        }

        if (numericTokens.Count < 2) return null;

        // SKU candidate: first alphanumeric token with digits and letters, or first token
        var firstToken = row[0];
        string sku;
        string description;

        if (row.Count >= 3 && (firstToken.Text.Contains('-') || firstToken.Text.Any(char.IsDigit)))
        {
            sku = firstToken.Text;
            description = string.Join(" ", row.Skip(1).TakeWhile(t => !numericTokens.Any(n => n.Box == t)).Select(t => t.Text));
        }
        else
        {
            sku = $"SKU-{lineCounter:D3}";
            description = string.Join(" ", row.TakeWhile(t => !numericTokens.Any(n => n.Box == t)).Select(t => t.Text));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            description = $"Line item {lineCounter}";
        }

        // Quantities and prices
        decimal quantity = 1m;
        decimal unitPrice = 0m;
        decimal lineTotal = 0m;
        decimal taxRate = 0.19m;

        if (numericTokens.Count >= 3)
        {
            quantity = numericTokens[0].Value;
            unitPrice = numericTokens[1].Value;
            lineTotal = numericTokens[2].Value;

            if (numericTokens.Count >= 4)
            {
                taxRate = numericTokens[3].Value;
                if (taxRate > 1.0m) taxRate /= 100.0m;
            }
        }
        else if (numericTokens.Count == 2)
        {
            quantity = numericTokens[0].Value;
            unitPrice = numericTokens[1].Value;
            lineTotal = quantity * unitPrice;
        }

        var boundingBox = new OcrBoundingBox(
            X: row.Min(t => t.X),
            Y: row.Min(t => t.Y),
            Width: row.Max(t => t.Right) - row.Min(t => t.X),
            Height: row.Max(t => t.Bottom) - row.Min(t => t.Y),
            Text: combinedText,
            Confidence: row.Average(t => t.Confidence));

        return new ExtractedLineItem(
            LineNumber: lineCounter,
            Sku: sku,
            Description: description,
            Quantity: quantity,
            UnitPrice: unitPrice,
            LineTotal: lineTotal,
            TaxRate: taxRate,
            Box: boundingBox);
    }

    private static IReadOnlyList<ExtractedLineItem> ExtractLineItemsFromText(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var items = new List<ExtractedLineItem>();
        int lineCounter = 1;

        // Pattern matching for line items: SKU/Code Description Qty Price Total (optional Tax)
        var rowPattern = new Regex(
            @"^\s*(?:\|\s*)?([A-Za-z0-9\-_]{2,20})\s*(?:\|\s*)?([^|0-9]+?)\s*(?:\|\s*)?([\d,\.]+)\s*(?:\|\s*)?([\d,\.]+)\s*(?:\|\s*)?([\d,\.]+)(?:\s*(?:\|\s*)?([\d,\.]+%?))?\s*(?:\|)?$",
            RegexOptions.Compiled);

        foreach (var line in lines)
        {
            var match = rowPattern.Match(line);
            if (match.Success)
            {
                var sku = match.Groups[1].Value.Trim();
                var desc = match.Groups[2].Value.Trim();
                var qty = ParseDecimalFlexible(match.Groups[3].Value);
                var unitPrice = ParseDecimalFlexible(match.Groups[4].Value);
                var lineTotal = ParseDecimalFlexible(match.Groups[5].Value);
                var taxRate = 0.19m;

                if (match.Groups[6].Success && !string.IsNullOrWhiteSpace(match.Groups[6].Value))
                {
                    taxRate = ParseDecimalFlexible(match.Groups[6].Value.Replace("%", ""));
                    if (taxRate > 1.0m) taxRate /= 100.0m;
                }

                if (qty > 0 && unitPrice > 0)
                {
                    if (lineTotal == 0m) lineTotal = qty * unitPrice;
                    items.Add(new ExtractedLineItem(
                        LineNumber: lineCounter++,
                        Sku: sku,
                        Description: desc,
                        Quantity: qty,
                        UnitPrice: unitPrice,
                        LineTotal: lineTotal,
                        TaxRate: taxRate));
                }
            }
        }

        return items;
    }

    private static double CalculateConfidenceScore(
        IReadOnlyList<OcrBoundingBox> tokens,
        string vendorName,
        string vendorTaxId,
        string invoiceNumber,
        DateTime invoiceDate,
        decimal totalAmount,
        int lineItemsCount)
    {
        double baseConfidence = tokens.Count > 0 ? tokens.Average(t => t.Confidence) : 0.90;

        double completenessScore = 0.0;
        if (!string.IsNullOrWhiteSpace(vendorName) && vendorName != "UNKNOWN_VENDOR") completenessScore += 0.20;
        if (!string.IsNullOrWhiteSpace(vendorTaxId) && vendorTaxId != "N/A") completenessScore += 0.20;
        if (!string.IsNullOrWhiteSpace(invoiceNumber)) completenessScore += 0.20;
        if (invoiceDate != default) completenessScore += 0.10;
        if (totalAmount > 0) completenessScore += 0.15;
        if (lineItemsCount > 0) completenessScore += 0.15;

        var finalScore = (baseConfidence * 0.5) + (completenessScore * 0.5);
        return Math.Round(Math.Clamp(finalScore, 0.0, 1.0), 4);
    }

    #endregion
}
