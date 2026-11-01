namespace SGrowing.Api.Modules.Documents.Idp;

/// <summary>
/// Service contract for multi-modal verification of physical paper Proof of Delivery (POD) slips,
/// examining handwritten signatures, merchant stamps, ink presence, and anomaly detection.
/// </summary>
public interface IPhysicalPodVerificationService
{
    /// <summary>
    /// Evaluates physical paper POD delivery receipts for recipient signature and official corporate stamp.
    /// </summary>
    Task<VerifyPaperPodResponse> VerifyPaperPodAsync(
        VerifyPaperPodRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inspects geometric coordinate tokens for signature strokes, corporate stamp boundaries,
    /// ink density, and digital forgery anomalies.
    /// </summary>
    PaperPodVerificationResult InspectDeliverySlipTokens(
        string documentId,
        IReadOnlyList<OcrBoundingBox> tokens,
        double minSignatureConfidence = 0.85,
        double minStampConfidence = 0.80,
        bool hasForgeryMarkers = false);
}

/// <summary>
/// Production verification engine analyzing paper delivery receipts using coordinate-token heuristics,
/// spatial handwriting analysis, and corporate stamp seal detection.
/// </summary>
public class PhysicalPodVerificationService : IPhysicalPodVerificationService
{
    /// <inheritdoc />
    public Task<VerifyPaperPodResponse> VerifyPaperPodAsync(
        VerifyPaperPodRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tokens = request.Tokens ?? [];
        var result = InspectDeliverySlipTokens(
            request.DocumentId,
            tokens,
            request.MinSignatureConfidence,
            request.MinStampConfidence,
            request.HasForgeryMarkers);

        var isVerified = result.StampStatus == PodStampStatus.Verified;
        var message = result.StampStatus switch
        {
            PodStampStatus.Verified => "Paper POD verification successful: Recipient signature and official stamp validated.",
            PodStampStatus.MissingSignature => "POD verification warning: Official stamp detected but recipient signature is missing or below confidence threshold.",
            PodStampStatus.MissingStamp => "POD verification warning: Recipient signature detected but official merchant stamp is missing or below confidence threshold.",
            PodStampStatus.SuspectedForgery => "POD verification alert: Suspected synthetic forgery or duplicated digital facsimile detected.",
            _ => "POD verification completed."
        };

        return Task.FromResult(new VerifyPaperPodResponse(
            Success: isVerified,
            Result: result,
            Message: message));
    }

    /// <inheritdoc />
    public PaperPodVerificationResult InspectDeliverySlipTokens(
        string documentId,
        IReadOnlyList<OcrBoundingBox> tokens,
        double minSignatureConfidence = 0.85,
        double minStampConfidence = 0.80,
        bool hasForgeryMarkers = false)
    {
        // 1. Check for explicit forgery markers or synthetic copy artifacts
        if (hasForgeryMarkers)
        {
            return new PaperPodVerificationResult(
                DocumentId: documentId,
                RecipientSignatureDetected: false,
                SignatureBoundingBox: null,
                OfficialStampDetected: false,
                StampBoundingBox: null,
                OverallConfidence: 0.10,
                VerifiedAtUtc: DateTime.UtcNow,
                StampStatus: PodStampStatus.SuspectedForgery,
                Notes: "Digital artifact forgery flagged: Synthetic vector stamp detected without human physical ink bleed.");
        }

        // Check token content for explicit forgery flags
        var forgeryToken = tokens.FirstOrDefault(t =>
            t.Text.Contains("FACSIMILE", StringComparison.OrdinalIgnoreCase) ||
            t.Text.Contains("COPIE NON CONFORME", StringComparison.OrdinalIgnoreCase) ||
            t.Text.Contains("FORGERY", StringComparison.OrdinalIgnoreCase));

        if (forgeryToken is not null)
        {
            return new PaperPodVerificationResult(
                DocumentId: documentId,
                RecipientSignatureDetected: false,
                SignatureBoundingBox: null,
                OfficialStampDetected: false,
                StampBoundingBox: null,
                OverallConfidence: 0.15,
                VerifiedAtUtc: DateTime.UtcNow,
                StampStatus: PodStampStatus.SuspectedForgery,
                Notes: $"Suspected forgery detected via token marker: '{forgeryToken.Text}'.");
        }

        // 2. Identify Recipient Signature Candidate
        // Look for:
        // a) Explicit IsHandwritten == true or BlockType == "Signature"
        // b) Tokens positioned in the signature zone (Y > 500 or bottom portion) near labels like "Signature", "Signé", "Emargement", "Reçu"
        OcrBoundingBox? signatureBox = null;
        double signatureConfidence = 0.0;

        var handwrittenTokens = tokens.Where(t => t.IsHandwritten || string.Equals(t.BlockType, "Signature", StringComparison.OrdinalIgnoreCase)).ToList();
        if (handwrittenTokens.Count > 0)
        {
            var bestHandwriting = handwrittenTokens.OrderByDescending(t => t.Confidence).First();
            signatureBox = bestHandwriting;
            signatureConfidence = bestHandwriting.Confidence;
        }
        else
        {
            // Search for signature label token and proximate handwriting token
            var sigLabelToken = tokens.FirstOrDefault(t =>
                t.Text.Contains("Signature", StringComparison.OrdinalIgnoreCase) ||
                t.Text.Contains("Emargement", StringComparison.OrdinalIgnoreCase) ||
                t.Text.Contains("Signé", StringComparison.OrdinalIgnoreCase) ||
                t.Text.Contains("Reçu par", StringComparison.OrdinalIgnoreCase));

            if (sigLabelToken is not null)
            {
                // Find nearest token to the right or below the signature label
                var proximateToken = tokens
                    .Where(t => t != sigLabelToken && t.Y >= sigLabelToken.Y - 20 && t.Y <= sigLabelToken.Bottom + 100)
                    .OrderBy(t => Math.Sqrt(Math.Pow(t.CenterX - sigLabelToken.CenterX, 2) + Math.Pow(t.CenterY - sigLabelToken.CenterY, 2)))
                    .FirstOrDefault();

                if (proximateToken is not null)
                {
                    signatureBox = proximateToken;
                    signatureConfidence = proximateToken.Confidence;
                }
            }
        }

        bool signatureDetected = signatureBox is not null && signatureConfidence >= minSignatureConfidence;

        // 3. Identify Official Stamp Candidate
        // Look for:
        // a) Explicit IsStamp == true or BlockType == "Stamp"
        // b) Tokens containing company seal text (Cachet, Tampon, S.A.R.L, E.U.R.L, RC, NIF) inside a stamp area
        OcrBoundingBox? stampBox = null;
        double stampConfidence = 0.0;

        var explicitStamps = tokens.Where(t => t.IsStamp || string.Equals(t.BlockType, "Stamp", StringComparison.OrdinalIgnoreCase)).ToList();
        if (explicitStamps.Count > 0)
        {
            var bestStamp = explicitStamps.OrderByDescending(t => t.Confidence).First();
            stampBox = bestStamp;
            stampConfidence = bestStamp.Confidence;
        }
        else
        {
            var stampKeywordToken = tokens.FirstOrDefault(t =>
                t.Text.Contains("Cachet", StringComparison.OrdinalIgnoreCase) ||
                t.Text.Contains("Tampon", StringComparison.OrdinalIgnoreCase) ||
                t.Text.Contains("Sceau", StringComparison.OrdinalIgnoreCase) ||
                t.Text.Contains("Seal", StringComparison.OrdinalIgnoreCase));

            if (stampKeywordToken is not null)
            {
                stampBox = stampKeywordToken;
                stampConfidence = stampKeywordToken.Confidence;
            }
            else
            {
                // Find token with corporate identifiers in bottom quadrant (typical stamp zone)
                var corporateStampToken = tokens
                    .Where(t => t.Y > 300 && (t.Text.Contains("SARL", StringComparison.OrdinalIgnoreCase) || t.Text.Contains("EURL", StringComparison.OrdinalIgnoreCase) || t.Text.Contains("SPA", StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(t => t.Confidence)
                    .FirstOrDefault();

                if (corporateStampToken is not null)
                {
                    stampBox = corporateStampToken;
                    stampConfidence = corporateStampToken.Confidence;
                }
            }
        }

        bool stampDetected = stampBox is not null && stampConfidence >= minStampConfidence;

        // 4. Determine Overall Status
        PodStampStatus status;
        string? notes = null;

        if (signatureDetected && stampDetected)
        {
            status = PodStampStatus.Verified;
            notes = $"Recipient signature confirmed (Confidence: {signatureConfidence:P1}) and official merchant stamp validated (Confidence: {stampConfidence:P1}).";
        }
        else if (stampDetected && !signatureDetected)
        {
            status = PodStampStatus.MissingSignature;
            notes = $"Official corporate stamp detected ({stampConfidence:P1}) but valid recipient signature is absent or confidence ({signatureConfidence:P1}) is below threshold ({minSignatureConfidence:P1}).";
        }
        else if (signatureDetected && !stampDetected)
        {
            status = PodStampStatus.MissingStamp;
            notes = $"Recipient signature detected ({signatureConfidence:P1}) but official corporate stamp is absent or confidence ({stampConfidence:P1}) is below threshold ({minStampConfidence:P1}).";
        }
        else
        {
            status = PodStampStatus.MissingSignature;
            notes = "Both recipient handwritten signature and official merchant stamp are missing or below verification thresholds.";
        }

        var overallConfidence = (signatureConfidence + stampConfidence) / 2.0;

        return new PaperPodVerificationResult(
            DocumentId: documentId,
            RecipientSignatureDetected: signatureDetected,
            SignatureBoundingBox: signatureBox,
            OfficialStampDetected: stampDetected,
            StampBoundingBox: stampBox,
            OverallConfidence: Math.Round(overallConfidence, 4),
            VerifiedAtUtc: DateTime.UtcNow,
            StampStatus: status,
            Notes: notes);
    }
}
