using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SGrowing.Api.Modules.SupplyChain.Customs;

/// <summary>
/// Autonomous digital customs declaration, cryptographic signing, and border verification service.
/// Thread-safe implementation utilizing modern C# 14 / .NET 10 System.Threading.Lock.
/// </summary>
public class DigitalCustomsDeclarationService(IDynamicCustomsTariffService tariffService) : IDigitalCustomsDeclarationService
{
    private readonly IDynamicCustomsTariffService _tariffService = tariffService ?? throw new ArgumentNullException(nameof(tariffService));
    private readonly Lock _lock = new();
    private readonly Dictionary<string, DigitalDeclaration> _declarations = new(StringComparer.OrdinalIgnoreCase);

    public const string DefaultCustomsSigningKey = "SGrowing_Customs_Secret_Key_Default_2026";

    /// <inheritdoc />
    public DigitalDeclaration CreateDeclaration(CreateDeclarationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new ArgumentException("A customs declaration must contain at least one commercial item.", nameof(request));
        }

        var declarationId = GenerateDeclarationId(request.Type, request.DestinationCountry);
        var issuedAt = DateTime.UtcNow;
        var validityDays = request.ValidityDays > 0 ? request.ValidityDays : 30;
        var expiresAt = issuedAt.AddDays(validityDays);

        // Calculate Totals and Landed Cost across items
        decimal totalFob = 0m;
        decimal totalCif = 0m;
        decimal totalDuty = 0m;
        decimal totalVat = 0m;
        decimal totalExcise = 0m;
        decimal totalCustomsFees = 0m;
        decimal totalLandedCost = 0m;

        var itemsList = new List<DeclarationItem>(request.Items.Count);
        var totalInputFob = request.Items.Sum(i => i.Quantity * i.UnitPriceFob);
        if (totalInputFob <= 0)
        {
            totalInputFob = 1.0m; // avoid division by zero
        }

        for (var idx = 0; idx < request.Items.Count; idx++)
        {
            var item = request.Items[idx];
            var itemFob = Math.Round(item.Quantity * item.UnitPriceFob, 2, MidpointRounding.AwayFromZero);
            var fobRatio = itemFob / totalInputFob;

            var allocatedFreight = Math.Round(request.Freight * fobRatio, 2, MidpointRounding.AwayFromZero);
            var allocatedInsurance = Math.Round(request.Insurance * fobRatio, 2, MidpointRounding.AwayFromZero);
            var itemCif = itemFob + allocatedFreight + allocatedInsurance;

            var tariffRequest = new TariffCalculationRequest(
                FobPrice: itemFob,
                InternationalFreight: allocatedFreight,
                MarineInsurance: allocatedInsurance,
                DestinationCountry: request.DestinationCountry,
                OriginCountry: request.OriginCountry,
                HsCode: item.HsCode,
                HasCertificateOfOrigin: request.Type == CustomsDeclarationType.Eur1CertificateOfOrigin || request.Type == CustomsDeclarationType.SingleAdministrativeDocument,
                IsCommercial: true);

            var tariffCalc = _tariffService.CalculateLandedTariff(tariffRequest);

            totalFob += itemFob;
            totalCif += tariffCalc.CifValue;
            totalDuty += tariffCalc.DutyAmount + tariffCalc.DapsAmount;
            totalVat += tariffCalc.VatAmount;
            totalExcise += tariffCalc.ExciseAmount;
            totalCustomsFees += tariffCalc.CustomsFeeTotal;
            totalLandedCost += tariffCalc.TotalLandedCost;

            itemsList.Add(item with
            {
                TotalFob = itemFob,
                CifValue = itemCif
            });
        }

        // Canonical items representation for SHA-256 hashing
        var canonicalItemsString = string.Join(";", itemsList
            .OrderBy(i => i.ItemId, StringComparer.Ordinal)
            .Select(i => $"{i.ItemId}:{i.HsCode}:{i.Quantity}:{i.UnitPriceFob:F2}:{i.GrossWeightKg:F2}:{i.CountryOfOrigin}"));
        var itemsHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalItemsString)));

        // Primary HS Code for declaration
        var primaryHsCode = itemsList[0].HsCode;
        var effectiveSecret = !string.IsNullOrWhiteSpace(request.SecretSigningKey)
            ? request.SecretSigningKey
            : DefaultCustomsSigningKey;

        // Canonical string for HMAC-SHA256 signature
        var canonicalSignaturePayload = BuildCanonicalPayload(
            declarationId: declarationId,
            type: request.Type.ToString(),
            hsCode: primaryHsCode,
            landedValue: totalLandedCost,
            currency: request.Currency,
            declarant: request.Declarant.Name,
            destination: request.DestinationCountry,
            itemsHash: itemsHash,
            issuedAtUtc: issuedAt.ToString("O"),
            expiresAtUtc: expiresAt.ToString("O"));

        var signatureBytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(effectiveSecret),
            Encoding.UTF8.GetBytes(canonicalSignaturePayload));
        var hmacSignature = Convert.ToHexStringLower(signatureBytes);

        // Generate QR code payload (Base64-encoded JSON)
        var qrObject = new JsonObject
        {
            ["id"] = declarationId,
            ["type"] = request.Type.ToString(),
            ["hs"] = primaryHsCode,
            ["val"] = totalLandedCost,
            ["cur"] = request.Currency,
            ["dec"] = request.Declarant.Name,
            ["dest"] = request.DestinationCountry,
            ["ts"] = issuedAt.ToString("O"),
            ["exp"] = expiresAt.ToString("O"),
            ["ihash"] = itemsHash,
            ["sig"] = hmacSignature
        };
        var qrJson = qrObject.ToJsonString();
        var qrPayloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(qrJson));

        // Generate type-specific customs metadata
        var metadata = GenerateDocumentMetadata(request, declarationId, expiresAt, primaryHsCode, totalDuty, totalVat);

        var declaration = new DigitalDeclaration(
            DeclarationId: declarationId,
            Type: request.Type,
            Status: DeclarationStatus.Approved,
            Declarant: request.Declarant,
            Consignee: request.Consignee,
            OriginCountry: request.OriginCountry,
            DestinationCountry: request.DestinationCountry,
            Items: itemsList,
            TotalFob: totalFob,
            TotalCif: totalCif,
            TotalDuty: totalDuty,
            TotalVat: totalVat,
            TotalExcise: totalExcise,
            TotalCustomsFees: totalCustomsFees,
            TotalLandedCost: totalLandedCost,
            Currency: request.Currency,
            TransportMode: request.TransportMode ?? "Maritime",
            BillOfLading: request.BillOfLadingOrAirwayBill,
            ItemsHashSha256: itemsHash,
            HmacSignature: hmacSignature,
            QrPayload: qrPayloadBase64,
            IssuedAtUtc: issuedAt,
            ExpiresAtUtc: expiresAt,
            Metadata: metadata);

        lock (_lock)
        {
            _declarations[declarationId] = declaration;
        }

        return declaration;
    }

    /// <inheritdoc />
    public DigitalDeclaration? GetDeclaration(string declarationId)
    {
        if (string.IsNullOrWhiteSpace(declarationId))
        {
            return null;
        }

        lock (_lock)
        {
            return _declarations.GetValueOrDefault(declarationId);
        }
    }

    /// <inheritdoc />
    public DeclarationVerificationResult VerifyDeclaration(string declarationId, string signature, string? secretKey = null)
    {
        if (string.IsNullOrWhiteSpace(declarationId) || string.IsNullOrWhiteSpace(signature))
        {
            return new DeclarationVerificationResult(false, declarationId, "InvalidParameters", "Declaration ID and signature cannot be empty.");
        }

        DigitalDeclaration? declaration;
        lock (_lock)
        {
            declaration = _declarations.GetValueOrDefault(declarationId);
        }

        if (declaration is null)
        {
            return new DeclarationVerificationResult(false, declarationId, "NotFound", $"Declaration '{declarationId}' was not found in customs registry.");
        }

        if (declaration.Status == DeclarationStatus.Revoked)
        {
            return new DeclarationVerificationResult(false, declarationId, "Revoked", $"Declaration '{declarationId}' has been revoked. Reason: {declaration.RevocationReason}");
        }

        if (DateTime.UtcNow > declaration.ExpiresAtUtc)
        {
            return new DeclarationVerificationResult(false, declarationId, "Expired", $"Declaration '{declarationId}' expired on {declaration.ExpiresAtUtc:O}.");
        }

        var effectiveSecret = secretKey ?? DefaultCustomsSigningKey;
        var canonical = BuildCanonicalPayload(
            declarationId: declaration.DeclarationId,
            type: declaration.Type.ToString(),
            hsCode: declaration.Items[0].HsCode,
            landedValue: declaration.TotalLandedCost,
            currency: declaration.Currency,
            declarant: declaration.Declarant.Name,
            destination: declaration.DestinationCountry,
            itemsHash: declaration.ItemsHashSha256,
            issuedAtUtc: declaration.IssuedAtUtc.ToString("O"),
            expiresAtUtc: declaration.ExpiresAtUtc.ToString("O"));

        var expectedSignatureBytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(effectiveSecret),
            Encoding.UTF8.GetBytes(canonical));
        var expectedSignature = Convert.ToHexStringLower(expectedSignatureBytes);

        var match = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expectedSignature.ToLowerInvariant()));

        if (!match)
        {
            return new DeclarationVerificationResult(false, declarationId, "TamperedSignature", "Cryptographic HMAC signature does not match declaration payload.");
        }

        return new DeclarationVerificationResult(true, declarationId, "Verified", "Declaration cryptographic signature is authentic and verified.");
    }

    /// <inheritdoc />
    public bool RevokeDeclaration(string declarationId, string reason)
    {
        if (string.IsNullOrWhiteSpace(declarationId))
        {
            return false;
        }

        lock (_lock)
        {
            if (!_declarations.TryGetValue(declarationId, out var existing))
            {
                return false;
            }

            var updated = existing with
            {
                Status = DeclarationStatus.Revoked,
                RevokedAtUtc = DateTime.UtcNow,
                RevocationReason = reason
            };

            _declarations[declarationId] = updated;
            return true;
        }
    }

    /// <inheritdoc />
    public BorderVerificationResult VerifyDeclarationQr(string qrPayload, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(qrPayload) || string.IsNullOrWhiteSpace(secretKey))
        {
            return new BorderVerificationResult(
                IsValid: false,
                DeclarationId: string.Empty,
                Type: null,
                DeclarantName: null,
                DestinationCountry: null,
                LandedValue: null,
                Currency: null,
                IssuedAtUtc: null,
                ExpiresAtUtc: null,
                IsExpired: false,
                IsRevoked: false,
                RevocationReason: null,
                VerificationMessage: "QR payload and customs secret key are required.");
        }

        try
        {
            // Decode payload: if Base64, decode to JSON string; otherwise parse as JSON
            string jsonString;
            try
            {
                var bytes = Convert.FromBase64String(qrPayload);
                jsonString = Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                jsonString = qrPayload;
            }

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var id = root.GetProperty("id").GetString() ?? string.Empty;
            var typeString = root.GetProperty("type").GetString() ?? string.Empty;
            var hs = root.GetProperty("hs").GetString() ?? string.Empty;
            var val = root.GetProperty("val").GetDecimal();
            var cur = root.GetProperty("cur").GetString() ?? "USD";
            var dec = root.GetProperty("dec").GetString() ?? string.Empty;
            var dest = root.GetProperty("dest").GetString() ?? string.Empty;
            var tsString = root.GetProperty("ts").GetString() ?? string.Empty;
            var expString = root.GetProperty("exp").GetString() ?? string.Empty;
            var ihash = root.GetProperty("ihash").GetString() ?? string.Empty;
            var sig = root.GetProperty("sig").GetString() ?? string.Empty;

            DateTime.TryParse(tsString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var issuedAt);
            DateTime.TryParse(expString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt);
            Enum.TryParse<CustomsDeclarationType>(typeString, true, out var declarationType);

            // 1. Recompute cryptographic signature over canonical payload
            var canonical = BuildCanonicalPayload(
                declarationId: id,
                type: typeString,
                hsCode: hs,
                landedValue: val,
                currency: cur,
                declarant: dec,
                destination: dest,
                itemsHash: ihash,
                issuedAtUtc: tsString,
                expiresAtUtc: expString);

            var expectedBytes = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secretKey),
                Encoding.UTF8.GetBytes(canonical));
            var expectedHex = Convert.ToHexStringLower(expectedBytes);

            var signatureMatches = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(sig.ToLowerInvariant()),
                Encoding.UTF8.GetBytes(expectedHex.ToLowerInvariant()));

            if (!signatureMatches)
            {
                return new BorderVerificationResult(
                    IsValid: false,
                    DeclarationId: id,
                    Type: declarationType,
                    DeclarantName: dec,
                    DestinationCountry: dest,
                    LandedValue: val,
                    Currency: cur,
                    IssuedAtUtc: issuedAt,
                    ExpiresAtUtc: expiresAt,
                    IsExpired: false,
                    IsRevoked: false,
                    RevocationReason: null,
                    VerificationMessage: "TamperedPayload: Cryptographic signature mismatch. Border clearance rejected.");
            }

            // 2. Check Expiration
            var isExpired = DateTime.UtcNow > expiresAt;
            if (isExpired)
            {
                return new BorderVerificationResult(
                    IsValid: false,
                    DeclarationId: id,
                    Type: declarationType,
                    DeclarantName: dec,
                    DestinationCountry: dest,
                    LandedValue: val,
                    Currency: cur,
                    IssuedAtUtc: issuedAt,
                    ExpiresAtUtc: expiresAt,
                    IsExpired: true,
                    IsRevoked: false,
                    RevocationReason: null,
                    VerificationMessage: $"DeclarationExpired: Customs declaration expired on {expiresAt:O}. Border clearance denied.");
            }

            // 3. Check Revocation against internal state
            bool isRevoked = false;
            string? revocationReason = null;

            lock (_lock)
            {
                if (_declarations.TryGetValue(id, out var stored))
                {
                    if (stored.Status == DeclarationStatus.Revoked)
                    {
                        isRevoked = true;
                        revocationReason = stored.RevocationReason;
                    }
                }
            }

            if (isRevoked)
            {
                return new BorderVerificationResult(
                    IsValid: false,
                    DeclarationId: id,
                    Type: declarationType,
                    DeclarantName: dec,
                    DestinationCountry: dest,
                    LandedValue: val,
                    Currency: cur,
                    IssuedAtUtc: issuedAt,
                    ExpiresAtUtc: expiresAt,
                    IsExpired: false,
                    IsRevoked: true,
                    RevocationReason: revocationReason,
                    VerificationMessage: $"DeclarationRevoked: Customs declaration was revoked. Reason: {revocationReason}. Border clearance denied.");
            }

            return new BorderVerificationResult(
                IsValid: true,
                DeclarationId: id,
                Type: declarationType,
                DeclarantName: dec,
                DestinationCountry: dest,
                LandedValue: val,
                Currency: cur,
                IssuedAtUtc: issuedAt,
                ExpiresAtUtc: expiresAt,
                IsExpired: false,
                IsRevoked: false,
                RevocationReason: null,
                VerificationMessage: "DeclarationVerified: Authentic digital customs declaration verified. Border transit authorized.");
        }
        catch (Exception ex)
        {
            return new BorderVerificationResult(
                IsValid: false,
                DeclarationId: string.Empty,
                Type: null,
                DeclarantName: null,
                DestinationCountry: null,
                LandedValue: null,
                Currency: null,
                IssuedAtUtc: null,
                ExpiresAtUtc: null,
                IsExpired: false,
                IsRevoked: false,
                RevocationReason: null,
                VerificationMessage: $"MalformedPayload: Failed to parse customs QR payload: {ex.Message}");
        }
    }

    #region Helper Methods

    private static string GenerateDeclarationId(CustomsDeclarationType type, string destinationCountry)
    {
        var prefix = type switch
        {
            CustomsDeclarationType.Eur1CertificateOfOrigin => "EUR1",
            CustomsDeclarationType.AtaCarnet => "ATA",
            _ => "DAU"
        };
        var dest = destinationCountry.ToUpperInvariant().Trim();
        var unique = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"{prefix}-{dest}-{unique}";
    }

    private static string BuildCanonicalPayload(
        string declarationId,
        string type,
        string hsCode,
        decimal landedValue,
        string currency,
        string declarant,
        string destination,
        string itemsHash,
        string issuedAtUtc,
        string expiresAtUtc)
    {
        return $"{declarationId}:{type}:{hsCode}:{landedValue:F2}:{currency}:{declarant}:{destination}:{itemsHash}:{issuedAtUtc}:{expiresAtUtc}";
    }

    private static Dictionary<string, string> GenerateDocumentMetadata(
        CreateDeclarationRequest request,
        string declarationId,
        DateTime expiresAt,
        string primaryHsCode,
        decimal totalDuty,
        decimal totalVat)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        switch (request.Type)
        {
            case CustomsDeclarationType.SingleAdministrativeDocument:
                meta["DocumentTitle"] = "Document Administratif Unique (DAU) / Single Administrative Document";
                meta["Box1_DeclarationType"] = "IM / 40 00 (Commercial Import for Home Consumption)";
                meta["Box14_Declarant"] = $"{request.Declarant.Name} ({request.Declarant.TaxIdOrEori})";
                meta["Box22_Currency"] = request.Currency;
                meta["Box33_TariffCode"] = primaryHsCode;
                meta["Box47_DutyCalculation"] = $"Duty: {totalDuty:F2} {request.Currency}, VAT: {totalVat:F2} {request.Currency}";
                meta["ElectronicCustomsManifest"] = $"SIDONIA/ALCES-{declarationId}";
                break;

            case CustomsDeclarationType.Eur1CertificateOfOrigin:
                meta["DocumentTitle"] = "EUR.1 Movement Certificate - Proof of Preferential Origin";
                meta["Box1_Exporter"] = $"{request.Declarant.Name}, {request.Declarant.Address}";
                meta["Box2_PreferentialAgreement"] = $"EU-Algeria Association Agreement (Preferential Tariff Corridor)";
                meta["Box4_CountryOfOrigin"] = request.OriginCountry;
                meta["Box5_CountryOfDestination"] = request.DestinationCountry;
                meta["Box8_CommodityCode"] = primaryHsCode;
                meta["Box11_CustomsEndorsement"] = $"Verified by Customs Office, Digital Stamp: {declarationId}";
                break;

            case CustomsDeclarationType.AtaCarnet:
                meta["DocumentTitle"] = "ATA Carnet - Admission Temporaire / Temporary Admission";
                meta["CarnetNumber"] = $"ATA-{declarationId}";
                meta["GuaranteeingAssociation"] = "International Chamber of Commerce (ICC) / World Chambers Federation";
                meta["IntendedUse"] = "Commercial samples, exhibition display, and professional tools";
                meta["TemporaryAdmissionExpiry"] = expiresAt.ToString("O");
                meta["DutySuspensionGuarantee"] = "Full import duties and taxes suspended under ICC guarantee chain.";
                break;
        }

        return meta;
    }

    #endregion
}
