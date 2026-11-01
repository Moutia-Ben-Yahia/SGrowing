using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.CRM.CustomObjects;

#region Validation Models

public record SchemaValidationError(
    string Field,
    string Message,
    string ErrorCode
);

public record SchemaValidationResult(
    bool IsValid,
    List<SchemaValidationError> Errors
)
{
    public static SchemaValidationResult Success() => new(true, []);
    public static SchemaValidationResult Failed(List<SchemaValidationError> errors) => new(false, errors);
}

public class CustomObjectValidationException(string message, IReadOnlyList<SchemaValidationError> errors)
    : Exception(message)
{
    public IReadOnlyList<SchemaValidationError> Errors { get; } = errors;
}

#endregion

#region Validator Interface & Implementation

public interface IEavDynamicSchemaValidator
{
    SchemaValidationResult Validate(CustomObjectSchemaRecord schema, IDictionary<string, object?> data);
    Dictionary<string, object?> NormalizeData(IDictionary<string, object?> data);
}

public class EavDynamicSchemaValidator : IEavDynamicSchemaValidator
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Dictionary<string, object?> NormalizeData(IDictionary<string, object?> data)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (data == null) return normalized;

        foreach (var (key, value) in data)
        {
            var cleanKey = key.Trim().ToLowerInvariant().Replace(" ", "_");
            normalized[cleanKey] = NormalizeValue(value);
        }

        return normalized;
    }

    public static object? NormalizeValue(object? val)
    {
        if (val is null) return null;

        if (val is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number => je.TryGetInt64(out var l)
                    ? (decimal)l
                    : (je.TryGetDecimal(out var d) ? d : (decimal)je.GetDouble()),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.Array => je.EnumerateArray().Select(x => NormalizeValue(x)).ToList(),
                JsonValueKind.Object => je.EnumerateObject().ToDictionary(
                    p => p.Name.Trim().ToLowerInvariant().Replace(" ", "_"),
                    p => NormalizeValue(p.Value),
                    StringComparer.OrdinalIgnoreCase),
                _ => je.ToString()
            };
        }

        if (val is IEnumerable enumerable && val is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(NormalizeValue(item));
            }
            return list;
        }

        return val;
    }

    public SchemaValidationResult Validate(CustomObjectSchemaRecord schema, IDictionary<string, object?> data)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(data);

        var errors = new List<SchemaValidationError>();
        var normalizedData = NormalizeData(data);

        foreach (var field in schema.Fields)
        {
            var fieldKey = field.Name.Trim().ToLowerInvariant().Replace(" ", "_");
            var hasKey = normalizedData.TryGetValue(fieldKey, out var val);

            var isNullOrEmpty = val is null
                || (val is string s && string.IsNullOrWhiteSpace(s))
                || (val is IList list && list.Count == 0);

            // 1. Required field enforcement
            if (field.IsRequired && (!hasKey || isNullOrEmpty))
            {
                errors.Add(new SchemaValidationError(
                    field.Name,
                    $"Field '{field.Label}' ({field.Name}) is required and cannot be empty.",
                    "REQUIRED_FIELD_MISSING"));
                continue;
            }

            // If field is optional and absent/null, skip further type checks
            if (!hasKey || val is null)
            {
                continue;
            }

            // 2. Type checks & constraints
            switch (field.Type)
            {
                case CustomObjectFieldType.Text:
                    if (val is not string && val is not ValueType)
                    {
                        errors.Add(new SchemaValidationError(
                            field.Name,
                            $"Field '{field.Label}' must be text.",
                            "INVALID_TEXT_TYPE"));
                    }
                    break;

                case CustomObjectFieldType.Number:
                    if (!TryConvertToDecimal(val, out var numVal))
                    {
                        errors.Add(new SchemaValidationError(
                            field.Name,
                            $"Field '{field.Label}' must be a valid number.",
                            "INVALID_NUMBER_TYPE"));
                    }
                    else
                    {
                        if (field.Min.HasValue && numVal < field.Min.Value)
                        {
                            errors.Add(new SchemaValidationError(
                                field.Name,
                                $"Field '{field.Label}' must be at least {field.Min.Value}.",
                                "NUMBER_MIN_VIOLATION"));
                        }
                        if (field.Max.HasValue && numVal > field.Max.Value)
                        {
                            errors.Add(new SchemaValidationError(
                                field.Name,
                                $"Field '{field.Label}' cannot exceed {field.Max.Value}.",
                                "NUMBER_MAX_VIOLATION"));
                        }
                    }
                    break;

                case CustomObjectFieldType.Currency:
                    if (!TryConvertToDecimal(val, out var currVal))
                    {
                        errors.Add(new SchemaValidationError(
                            field.Name,
                            $"Field '{field.Label}' must be a valid numeric currency amount.",
                            "INVALID_CURRENCY_TYPE"));
                    }
                    else
                    {
                        if (field.Min.HasValue && currVal < field.Min.Value)
                        {
                            errors.Add(new SchemaValidationError(
                                field.Name,
                                $"Field '{field.Label}' currency amount must be at least {field.Min.Value}.",
                                "CURRENCY_MIN_VIOLATION"));
                        }
                        if (field.Max.HasValue && currVal > field.Max.Value)
                        {
                            errors.Add(new SchemaValidationError(
                                field.Name,
                                $"Field '{field.Label}' currency amount cannot exceed {field.Max.Value}.",
                                "CURRENCY_MAX_VIOLATION"));
                        }
                    }
                    break;

                case CustomObjectFieldType.Boolean:
                    if (!TryConvertToBool(val, out _))
                    {
                        errors.Add(new SchemaValidationError(
                            field.Name,
                            $"Field '{field.Label}' must be a valid boolean.",
                            "INVALID_BOOLEAN_TYPE"));
                    }
                    break;

                case CustomObjectFieldType.Date:
                    if (!TryConvertToDateTime(val, out _))
                    {
                        errors.Add(new SchemaValidationError(
                            field.Name,
                            $"Field '{field.Label}' must be a valid date or datetime.",
                            "INVALID_DATE_TYPE"));
                    }
                    break;

                case CustomObjectFieldType.Email:
                    var emailStr = val.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(emailStr) || !EmailRegex.IsMatch(emailStr))
                    {
                        errors.Add(new SchemaValidationError(
                            field.Name,
                            $"Field '{field.Label}' must be a valid email address.",
                            "INVALID_EMAIL_FORMAT"));
                    }
                    break;

                case CustomObjectFieldType.Select:
                    var optionStr = val.ToString()?.Trim() ?? string.Empty;
                    var allowedOptions = field.Options ?? [];
                    if (allowedOptions.Count > 0 && !allowedOptions.Any(o => o.Equals(optionStr, StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add(new SchemaValidationError(
                            field.Name,
                            $"Option '{optionStr}' is not valid for '{field.Label}'. Allowed options: [{string.Join(", ", allowedOptions)}].",
                            "INVALID_SELECT_OPTION"));
                    }
                    break;

                case CustomObjectFieldType.MultiSelect:
                    if (!TryConvertToStringList(val, out var selectedList))
                    {
                        errors.Add(new SchemaValidationError(
                            field.Name,
                            $"Field '{field.Label}' must be a list of selected options.",
                            "INVALID_MULTISELECT_TYPE"));
                    }
                    else
                    {
                        var multiselectOptions = field.Options ?? [];
                        foreach (var item in selectedList)
                        {
                            if (multiselectOptions.Count > 0 && !multiselectOptions.Any(o => o.Equals(item, StringComparison.OrdinalIgnoreCase)))
                            {
                                errors.Add(new SchemaValidationError(
                                    field.Name,
                                    $"Option '{item}' is not valid for multiselect field '{field.Label}'. Allowed options: [{string.Join(", ", multiselectOptions)}].",
                                    "INVALID_MULTISELECT_OPTION"));
                            }
                        }
                    }
                    break;
            }

            // 3. Validation Regex pattern enforcement
            if (!string.IsNullOrWhiteSpace(field.ValidationRegex))
            {
                var strVal = val.ToString() ?? string.Empty;
                try
                {
                    if (!Regex.IsMatch(strVal, field.ValidationRegex))
                    {
                        errors.Add(new SchemaValidationError(
                            field.Name,
                            $"Field '{field.Label}' with value '{strVal}' does not match required regex pattern '{field.ValidationRegex}'.",
                            "REGEX_PATTERN_MISMATCH"));
                    }
                }
                catch (ArgumentException)
                {
                    // If the schema's regex was malformed, fail gracefully or record error
                    errors.Add(new SchemaValidationError(
                        field.Name,
                        $"Schema configuration error: invalid regex pattern '{field.ValidationRegex}'.",
                        "SCHEMA_REGEX_INVALID"));
                }
            }
        }

        return errors.Count == 0
            ? SchemaValidationResult.Success()
            : SchemaValidationResult.Failed(errors);
    }

    private static bool TryConvertToDecimal(object val, out decimal result)
    {
        result = 0m;
        if (val is decimal d) { result = d; return true; }
        if (val is int i) { result = i; return true; }
        if (val is long l) { result = l; return true; }
        if (val is double dbl) { result = (decimal)dbl; return true; }
        if (val is float f) { result = (decimal)f; return true; }
        if (val is short s) { result = s; return true; }
        if (val is byte b) { result = b; return true; }

        var str = val?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return false;

        return decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryConvertToBool(object val, out bool result)
    {
        result = false;
        if (val is bool b) { result = b; return true; }

        var str = val?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return false;

        if (bool.TryParse(str, out result)) return true;

        if (str.Equals("1", StringComparison.OrdinalIgnoreCase) || str.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (str.Equals("0", StringComparison.OrdinalIgnoreCase) || str.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        return false;
    }

    private static bool TryConvertToDateTime(object val, out DateTime result)
    {
        result = default;
        if (val is DateTime dt) { result = dt; return true; }
        if (val is DateTimeOffset dto) { result = dto.UtcDateTime; return true; }
        if (val is DateOnly dOnly) { result = dOnly.ToDateTime(TimeOnly.MinValue); return true; }

        var str = val?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return false;

        return DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result);
    }

    private static bool TryConvertToStringList(object val, out List<string> result)
    {
        result = [];

        if (val is IEnumerable<string> stringEnumerable)
        {
            result = stringEnumerable.ToList();
            return true;
        }

        if (val is IEnumerable enumerable && val is not string)
        {
            result = [];
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    result.Add(item.ToString()?.Trim() ?? string.Empty);
                }
            }
            return true;
        }

        return false;
    }
}

#endregion
