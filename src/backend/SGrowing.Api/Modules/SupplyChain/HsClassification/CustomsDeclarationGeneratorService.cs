using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.SupplyChain.HsClassification;

/// <summary>
/// Generates standardized Single Administrative Document (SAD / DAU) customs declarations
/// with tamper-evident cryptographic seal hashes.
/// </summary>
public sealed class CustomsDeclarationGeneratorService
{
    public SingleAdministrativeDocument GenerateDeclaration(
        string importerTaxId,
        string exporterName,
        string countryOfExport,
        string countryOfDestination,
        string customsOfficeCode,
        IReadOnlyList<SadLineItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(importerTaxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(exporterName);
        ArgumentNullException.ThrowIfNull(items);

        var declarationId = $"SAD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..22].ToUpperInvariant();

        var totalStatisticalValue = items.Sum(i => i.ItemInvoiceValueEur);
        var totalDutyAndTaxes = items.Sum(i => i.CalculatedDutyEur + i.CalculatedVatEur);
        var now = DateTime.UtcNow;

        var rawPayload = $"{declarationId}|{importerTaxId}|{exporterName}|{countryOfExport}|{countryOfDestination}|{customsOfficeCode}|{totalStatisticalValue}|{totalDutyAndTaxes}|{now:O}";
        var sealHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload))).ToLowerInvariant();

        return new SingleAdministrativeDocument(
            DeclarationId: declarationId,
            ImporterTaxId: importerTaxId,
            ExporterName: exporterName,
            CountryOfExport: countryOfExport,
            CountryOfDestination: countryOfDestination,
            CustomsOfficeCode: customsOfficeCode,
            Items: items,
            TotalStatisticalValueEur: totalStatisticalValue,
            TotalPayableDutyAndTaxesEur: totalDutyAndTaxes,
            DigitalCustomsSealHex: sealHash,
            FiledAtUtc: now
        );
    }
}
