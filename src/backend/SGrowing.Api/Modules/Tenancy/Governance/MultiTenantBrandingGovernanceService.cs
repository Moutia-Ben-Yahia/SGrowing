using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Tenancy.Governance;

public enum ThreatSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public sealed record CssThreat(
    string ThreatType,
    ThreatSeverity Severity,
    string RuleSnippet,
    string Description
);

public sealed record CssSanitizationResult(
    bool IsClean,
    IReadOnlyList<CssThreat> DetectedThreats,
    string SanitizedCss
);

public sealed record ContrastEvaluationResult(
    string ForegroundHex,
    string BackgroundHex,
    double ForegroundLuminance,
    double BackgroundLuminance,
    double ContrastRatio,
    bool PassesAaNormal,
    bool PassesAaLarge,
    bool PassesAaaNormal,
    bool PassesAaaLarge,
    string RecommendedAdjustedColor,
    double AdjustedContrastRatio
);

public interface IMultiTenantBrandingGovernanceService
{
    CssSanitizationResult ScanAndSanitizeCss(string? cssContent);
    double CalculateRelativeLuminance(string hexColor);
    double CalculateContrastRatio(string foregroundHex, string backgroundHex);
    ContrastEvaluationResult EvaluateContrast(string foregroundHex, string backgroundHex, bool isLargeText = false);
    string AutoAdjustToneForContrast(string foregroundHex, string backgroundHex, double targetRatio = 4.5);
}

public sealed class MultiTenantBrandingGovernanceService : IMultiTenantBrandingGovernanceService
{
    private static readonly Regex ExpressionRegex = new(
        @"expression\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JavascriptUriRegex = new(
        @"(javascript|vbscript)\s*:",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BehaviorBindingRegex = new(
        @"(behavior|-moz-binding)\s*:",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ImportRegex = new(
        @"@import\s+[^;]+;",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HtmlTagRegex = new(
        @"<\s*\/?\s*(style|script|svg|iframe|embed|object)[^>]*>|<!--|-->",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExternalUrlRegex = new(
        @"url\s*\(\s*['""]?(https?:|\/\/)[^'"")]+['""]?\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex KeyloggerPatternRegex = new(
        @"input\s*\[\s*value\s*[\^\$\*]?=.*\]\s*\{[^}]*url\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public CssSanitizationResult ScanAndSanitizeCss(string? cssContent)
    {
        if (string.IsNullOrWhiteSpace(cssContent))
        {
            return new CssSanitizationResult(true, [], string.Empty);
        }

        var threats = new List<CssThreat>();
        var sanitized = cssContent;

        // 1. Detect HTML tag escapes (e.g. </style><script>alert(1)</script>)
        var htmlMatches = HtmlTagRegex.Matches(sanitized);
        foreach (Match match in htmlMatches)
        {
            threats.Add(new CssThreat(
                ThreatType: "HtmlTagInjection",
                Severity: ThreatSeverity.Critical,
                RuleSnippet: match.Value,
                Description: "Detected HTML tag or comment inside CSS payload."
            ));
        }
        sanitized = HtmlTagRegex.Replace(sanitized, "/* neutralized-tag */");

        // 2. Detect IE CSS expression() execution
        var exprMatches = ExpressionRegex.Matches(sanitized);
        foreach (Match match in exprMatches)
        {
            threats.Add(new CssThreat(
                ThreatType: "ExpressionExecution",
                Severity: ThreatSeverity.Critical,
                RuleSnippet: match.Value,
                Description: "Dynamic CSS expression() script execution vector detected."
            ));
        }
        sanitized = ExpressionRegex.Replace(sanitized, "/* neutralized-expr */(");

        // 3. Detect javascript: or vbscript: pseudo-protocols
        var jsMatches = JavascriptUriRegex.Matches(sanitized);
        foreach (Match match in jsMatches)
        {
            threats.Add(new CssThreat(
                ThreatType: "ScriptPseudoProtocol",
                Severity: ThreatSeverity.Critical,
                RuleSnippet: match.Value,
                Description: "javascript: or vbscript: execution protocol detected."
            ));
        }
        sanitized = JavascriptUriRegex.Replace(sanitized, "/* blocked-protocol */");

        // 4. Detect CSS behavior: and -moz-binding
        var behaviorMatches = BehaviorBindingRegex.Matches(sanitized);
        foreach (Match match in behaviorMatches)
        {
            threats.Add(new CssThreat(
                ThreatType: "BehaviorBinding",
                Severity: ThreatSeverity.High,
                RuleSnippet: match.Value,
                Description: "Legacy behavior or -moz-binding component injection detected."
            ));
        }
        sanitized = BehaviorBindingRegex.Replace(sanitized, "/* neutralized-binding */");

        // 5. Detect dangerous @import rules
        var importMatches = ImportRegex.Matches(sanitized);
        foreach (Match match in importMatches)
        {
            threats.Add(new CssThreat(
                ThreatType: "MaliciousImport",
                Severity: ThreatSeverity.High,
                RuleSnippet: match.Value,
                Description: "External CSS @import rule detected."
            ));
        }
        sanitized = ImportRegex.Replace(sanitized, "/* neutralized-import */");

        // 6. Detect CSS keylogger selector pattern (input[value^='x'])
        var keyloggerMatches = KeyloggerPatternRegex.Matches(sanitized);
        foreach (Match match in keyloggerMatches)
        {
            threats.Add(new CssThreat(
                ThreatType: "CssKeylogger",
                Severity: ThreatSeverity.Critical,
                RuleSnippet: match.Value,
                Description: "Attribute selector with URL exfiltration (CSS keylogger vector) detected."
            ));
        }

        // 7. Detect external url() exfiltration
        var urlMatches = ExternalUrlRegex.Matches(sanitized);
        foreach (Match match in urlMatches)
        {
            threats.Add(new CssThreat(
                ThreatType: "ExternalUrlExfiltration",
                Severity: ThreatSeverity.Medium,
                RuleSnippet: match.Value,
                Description: "External URL reference in CSS url(...) detected."
            ));
        }
        sanitized = ExternalUrlRegex.Replace(sanitized, "/* neutralized-url */");

        return new CssSanitizationResult(
            IsClean: threats.Count == 0,
            DetectedThreats: threats,
            SanitizedCss: sanitized.Trim()
        );
    }

    /// <summary>
    /// Calculates relative luminance according to the official W3C WCAG 2.2 formula:
    /// L = 0.2126 * R' + 0.7152 * G' + 0.0722 * B'
    /// where C' = (C/255) <= 0.04045 ? (C/255)/12.92 : ((C/255 + 0.055)/1.055)^2.4
    /// </summary>
    public double CalculateRelativeLuminance(string hexColor)
    {
        var (r, g, b) = ParseHexColor(hexColor);

        var rPrime = LinearizeChannel(r);
        var gPrime = LinearizeChannel(g);
        var bPrime = LinearizeChannel(b);

        return 0.2126 * rPrime + 0.7152 * gPrime + 0.0722 * bPrime;
    }

    /// <summary>
    /// Calculates WCAG 2.2 contrast ratio: (L1 + 0.05) / (L2 + 0.05) where L1 >= L2
    /// </summary>
    public double CalculateContrastRatio(string foregroundHex, string backgroundHex)
    {
        var l1 = CalculateRelativeLuminance(foregroundHex);
        var l2 = CalculateRelativeLuminance(backgroundHex);

        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);

        return (lighter + 0.05) / (darker + 0.05);
    }

    public ContrastEvaluationResult EvaluateContrast(string foregroundHex, string backgroundHex, bool isLargeText = false)
    {
        var fgLum = CalculateRelativeLuminance(foregroundHex);
        var bgLum = CalculateRelativeLuminance(backgroundHex);

        var ratio = CalculateContrastRatio(foregroundHex, backgroundHex);

        var passesAaNormal = ratio >= 4.5;
        var passesAaLarge = ratio >= 3.0;
        var passesAaaNormal = ratio >= 7.0;
        var passesAaaLarge = ratio >= 4.5;

        var requiredRatio = isLargeText ? 3.0 : 4.5;
        string recommendedColor = foregroundHex;
        double adjustedRatio = ratio;

        if (ratio < requiredRatio)
        {
            recommendedColor = AutoAdjustToneForContrast(foregroundHex, backgroundHex, requiredRatio);
            adjustedRatio = CalculateContrastRatio(recommendedColor, backgroundHex);
        }

        return new ContrastEvaluationResult(
            ForegroundHex: NormalizeHex(foregroundHex),
            BackgroundHex: NormalizeHex(backgroundHex),
            ForegroundLuminance: Math.Round(fgLum, 5),
            BackgroundLuminance: Math.Round(bgLum, 5),
            ContrastRatio: Math.Round(ratio, 2),
            PassesAaNormal: passesAaNormal,
            PassesAaLarge: passesAaLarge,
            PassesAaaNormal: passesAaaNormal,
            PassesAaaLarge: passesAaaLarge,
            RecommendedAdjustedColor: recommendedColor,
            AdjustedContrastRatio: Math.Round(adjustedRatio, 2)
        );
    }

    public string AutoAdjustToneForContrast(string foregroundHex, string backgroundHex, double targetRatio = 4.5)
    {
        var currentRatio = CalculateContrastRatio(foregroundHex, backgroundHex);
        if (currentRatio >= targetRatio)
        {
            return NormalizeHex(foregroundHex);
        }

        var (r, g, b) = ParseHexColor(foregroundHex);
        var (h, s, l) = RgbToHsl(r, g, b);
        var bgLuminance = CalculateRelativeLuminance(backgroundHex);

        // Determine adjustment direction:
        // If background is light (L >= 0.179), darken the foreground (L -> 0).
        // If background is dark (L < 0.179), lighten the foreground (L -> 1).
        bool shouldDarken = bgLuminance >= 0.179;

        string bestCandidate = foregroundHex;
        double bestRatio = currentRatio;

        if (shouldDarken)
        {
            for (double stepL = l; stepL >= 0.0; stepL -= 0.01)
            {
                var (candR, candG, candB) = HslToRgb(h, s, stepL);
                var candHex = ToHexColor(candR, candG, candB);
                var candRatio = CalculateContrastRatio(candHex, backgroundHex);

                if (candRatio > bestRatio)
                {
                    bestRatio = candRatio;
                    bestCandidate = candHex;
                }

                if (candRatio >= targetRatio)
                {
                    return candHex;
                }
            }
        }
        else
        {
            for (double stepL = l; stepL <= 1.0; stepL += 0.01)
            {
                var (candR, candG, candB) = HslToRgb(h, s, stepL);
                var candHex = ToHexColor(candR, candG, candB);
                var candRatio = CalculateContrastRatio(candHex, backgroundHex);

                if (candRatio > bestRatio)
                {
                    bestRatio = candRatio;
                    bestCandidate = candHex;
                }

                if (candRatio >= targetRatio)
                {
                    return candHex;
                }
            }
        }

        // Fallback: If hue adjustment alone didn't reach target (e.g. background is mid-tone),
        // check pure black or pure white
        var blackRatio = CalculateContrastRatio("#000000", backgroundHex);
        var whiteRatio = CalculateContrastRatio("#FFFFFF", backgroundHex);

        if (blackRatio >= targetRatio && blackRatio > whiteRatio)
            return "#000000";
        if (whiteRatio >= targetRatio)
            return "#FFFFFF";

        return bestCandidate;
    }

    private static double LinearizeChannel(int channel8Bit)
    {
        var sRgb = channel8Bit / 255.0;
        return sRgb <= 0.04045
            ? sRgb / 12.92
            : Math.Pow((sRgb + 0.055) / 1.055, 2.4);
    }

    private static (int R, int G, int B) ParseHexColor(string hex)
    {
        var clean = hex.Trim().TrimStart('#');
        if (clean.Length == 3)
        {
            // Expand 3-digit hex (#RGB -> #RRGGBB)
            clean = $"{clean[0]}{clean[0]}{clean[1]}{clean[1]}{clean[2]}{clean[2]}";
        }

        if (clean.Length != 6 || !uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var val))
        {
            throw new ArgumentException($"Invalid hex color code: '{hex}'");
        }

        var r = (int)((val >> 16) & 0xFF);
        var g = (int)((val >> 8) & 0xFF);
        var b = (int)(val & 0xFF);

        return (r, g, b);
    }

    private static string NormalizeHex(string hex)
    {
        var (r, g, b) = ParseHexColor(hex);
        return ToHexColor(r, g, b);
    }

    private static string ToHexColor(int r, int g, int b) =>
        $"#{Math.Clamp(r, 0, 255):X2}{Math.Clamp(g, 0, 255):X2}{Math.Clamp(b, 0, 255):X2}";

    public static (double H, double S, double L) RgbToHsl(int r, int g, int b)
    {
        var rd = r / 255.0;
        var gd = g / 255.0;
        var bd = b / 255.0;

        var max = Math.Max(rd, Math.Max(gd, bd));
        var min = Math.Min(rd, Math.Min(gd, bd));
        var l = (max + min) / 2.0;

        if (Math.Abs(max - min) < 0.00001)
        {
            return (0.0, 0.0, l);
        }

        var d = max - min;
        var s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

        double h;
        if (Math.Abs(max - rd) < 0.00001)
            h = (gd - bd) / d + (gd < bd ? 6.0 : 0.0);
        else if (Math.Abs(max - gd) < 0.00001)
            h = (bd - rd) / d + 2.0;
        else
            h = (rd - gd) / d + 4.0;

        h /= 6.0;

        return (h * 360.0, s, l);
    }

    public static (int R, int G, int B) HslToRgb(double h, double s, double l)
    {
        if (Math.Abs(s) < 0.00001)
        {
            var gray = (int)Math.Round(l * 255.0);
            return (gray, gray, gray);
        }

        var q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
        var p = 2.0 * l - q;
        var hk = (h % 360.0) / 360.0;
        if (hk < 0) hk += 1.0;

        var r = HueToRgb(p, q, hk + 1.0 / 3.0);
        var g = HueToRgb(p, q, hk);
        var b = HueToRgb(p, q, hk - 1.0 / 3.0);

        return ((int)Math.Round(r * 255.0), (int)Math.Round(g * 255.0), (int)Math.Round(b * 255.0));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1.0;
        if (t > 1) t -= 1.0;
        if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        return p;
    }
}
