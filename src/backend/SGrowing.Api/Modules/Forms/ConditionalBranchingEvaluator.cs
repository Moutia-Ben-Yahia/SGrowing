using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace SGrowing.Api.Modules.Forms;

public enum RuleOperator
{
    Equals,
    NotEquals,
    Contains,
    GreaterThan,
    LessThan,
    In,
    IsEmpty,
    IsNotEmpty
}

public enum LogicalOperator
{
    And,
    Or
}

public enum FieldAction
{
    Show,
    Hide,
    Require,
    Optional,
    Enable,
    Disable
}

public class ConditionRule
{
    public string FieldName { get; set; } = string.Empty;
    public RuleOperator Operator { get; set; } = RuleOperator.Equals;
    public object? Value { get; set; }
}

public class RuleGroup
{
    public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.And;
    public List<ConditionRule> Rules { get; set; } = [];
    public List<RuleGroup>? NestedGroups { get; set; }
}

public class FieldBranchingRule
{
    public string TargetField { get; set; } = string.Empty;
    public RuleGroup Condition { get; set; } = new();
    public FieldAction Action { get; set; } = FieldAction.Show;
    public FieldAction? ElseAction { get; set; }
}

public class BranchingEvaluationRequest
{
    public List<FormFieldDefinition> Fields { get; set; } = [];
    public List<FieldBranchingRule> Rules { get; set; } = [];
    public Dictionary<string, object?> CurrentAnswers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class FieldEvaluationState
{
    public string FieldName { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public bool IsRequired { get; set; } = false;
    public bool IsDisabled { get; set; } = false;
}

public class BranchingEvaluationResponse
{
    public Dictionary<string, FieldEvaluationState> FieldStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> VisibleFields => FieldStates.Values.Where(f => f.IsVisible).Select(f => f.FieldName).ToList();
    public List<string> HiddenFields => FieldStates.Values.Where(f => !f.IsVisible).Select(f => f.FieldName).ToList();
    public List<string> RequiredFields => FieldStates.Values.Where(f => f.IsRequired).Select(f => f.FieldName).ToList();
    public List<string> DisabledFields => FieldStates.Values.Where(f => f.IsDisabled).Select(f => f.FieldName).ToList();
}

public interface IConditionalBranchingEvaluator
{
    BranchingEvaluationResponse Evaluate(BranchingEvaluationRequest request);
    bool EvaluateRuleGroup(RuleGroup group, IReadOnlyDictionary<string, object?> answers);
    bool EvaluateRule(ConditionRule rule, IReadOnlyDictionary<string, object?> answers);
}

public class ConditionalBranchingEvaluator : IConditionalBranchingEvaluator
{
    public BranchingEvaluationResponse Evaluate(BranchingEvaluationRequest request)
    {
        var response = new BranchingEvaluationResponse();

        // 1. Initialize field states from form field definitions
        foreach (var field in request.Fields)
        {
            response.FieldStates[field.Name] = new FieldEvaluationState
            {
                FieldName = field.Name,
                IsVisible = true,
                IsRequired = field.IsRequired,
                IsDisabled = false
            };
        }

        // Also ensure any target fields in rules that weren't in Fields exist
        foreach (var rule in request.Rules)
        {
            if (!response.FieldStates.ContainsKey(rule.TargetField))
            {
                response.FieldStates[rule.TargetField] = new FieldEvaluationState
                {
                    FieldName = rule.TargetField,
                    IsVisible = true,
                    IsRequired = false,
                    IsDisabled = false
                };
            }
        }

        // Case-insensitive dictionary for answers
        var answers = new Dictionary<string, object?>(request.CurrentAnswers, StringComparer.OrdinalIgnoreCase);

        // 2. Evaluate each branching rule
        foreach (var rule in request.Rules)
        {
            if (!response.FieldStates.TryGetValue(rule.TargetField, out var state))
            {
                continue;
            }

            bool conditionMet = EvaluateRuleGroup(rule.Condition, answers);

            if (conditionMet)
            {
                ApplyAction(state, rule.Action);
            }
            else if (rule.ElseAction.HasValue)
            {
                ApplyAction(state, rule.ElseAction.Value);
            }
        }

        return response;
    }

    private static void ApplyAction(FieldEvaluationState state, FieldAction action)
    {
        switch (action)
        {
            case FieldAction.Show:
                state.IsVisible = true;
                break;
            case FieldAction.Hide:
                state.IsVisible = false;
                break;
            case FieldAction.Require:
                state.IsRequired = true;
                break;
            case FieldAction.Optional:
                state.IsRequired = false;
                break;
            case FieldAction.Enable:
                state.IsDisabled = false;
                break;
            case FieldAction.Disable:
                state.IsDisabled = true;
                break;
        }
    }

    public bool EvaluateRuleGroup(RuleGroup group, IReadOnlyDictionary<string, object?> answers)
    {
        var rules = group.Rules ?? [];
        var nested = group.NestedGroups ?? [];

        if (rules.Count == 0 && nested.Count == 0)
        {
            return group.LogicalOperator == LogicalOperator.And;
        }

        if (group.LogicalOperator == LogicalOperator.And)
        {
            // All rules must be true
            foreach (var rule in rules)
            {
                if (!EvaluateRule(rule, answers))
                {
                    return false;
                }
            }

            // All nested groups must be true
            foreach (var child in nested)
            {
                if (!EvaluateRuleGroup(child, answers))
                {
                    return false;
                }
            }

            return true;
        }
        else // LogicalOperator.Or
        {
            // At least one rule must be true
            foreach (var rule in rules)
            {
                if (EvaluateRule(rule, answers))
                {
                    return true;
                }
            }

            // Or at least one nested group must be true
            foreach (var child in nested)
            {
                if (EvaluateRuleGroup(child, answers))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool EvaluateRule(ConditionRule rule, IReadOnlyDictionary<string, object?> answers)
    {
        answers.TryGetValue(rule.FieldName, out var actualRawValue);

        return rule.Operator switch
        {
            RuleOperator.Equals => AreEqual(actualRawValue, rule.Value),
            RuleOperator.NotEquals => !AreEqual(actualRawValue, rule.Value),
            RuleOperator.Contains => DoesContain(actualRawValue, rule.Value),
            RuleOperator.GreaterThan => CompareNumbers(actualRawValue, rule.Value) > 0,
            RuleOperator.LessThan => CompareNumbers(actualRawValue, rule.Value) < 0,
            RuleOperator.In => IsInCollection(actualRawValue, rule.Value),
            RuleOperator.IsEmpty => IsValueEmpty(actualRawValue),
            RuleOperator.IsNotEmpty => !IsValueEmpty(actualRawValue),
            _ => false
        };
    }

    private static bool IsValueEmpty(object? value)
    {
        if (value is null) return true;

        if (value is string s)
        {
            return string.IsNullOrWhiteSpace(s);
        }

        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => true,
                JsonValueKind.String => string.IsNullOrWhiteSpace(je.GetString()),
                JsonValueKind.Array => je.GetArrayLength() == 0,
                _ => false
            };
        }

        if (value is IEnumerable enumerable and not string)
        {
            var enumerator = enumerable.GetEnumerator();
            return !enumerator.MoveNext();
        }

        return false;
    }

    private static bool AreEqual(object? actual, object? expected)
    {
        actual = UnwrapJsonElement(actual);
        expected = UnwrapJsonElement(expected);

        if (actual is null && expected is null) return true;
        if (actual is null || expected is null) return false;

        // Numeric comparison
        if (TryExtractDecimal(actual, out var actualNum) && TryExtractDecimal(expected, out var expectedNum))
        {
            return actualNum == expectedNum;
        }

        // Boolean comparison
        if (TryExtractBool(actual, out var actualBool) && TryExtractBool(expected, out var expectedBool))
        {
            return actualBool == expectedBool;
        }

        // String comparison (case-insensitive)
        var actualStr = actual.ToString()?.Trim() ?? string.Empty;
        var expectedStr = expected.ToString()?.Trim() ?? string.Empty;
        return string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DoesContain(object? actual, object? target)
    {
        actual = UnwrapJsonElement(actual);
        target = UnwrapJsonElement(target);

        if (actual is null || target is null) return false;

        // If actual is collection, check if target is an element
        if (actual is IEnumerable enumerable and not string)
        {
            var targetStrMatch = target.ToString() ?? string.Empty;
            foreach (var item in enumerable)
            {
                if (AreEqual(item, target)) return true;
                var itemStr = item?.ToString();
                if (itemStr != null && itemStr.Contains(targetStrMatch, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // Substring check
        var actualStr = actual.ToString() ?? string.Empty;
        var targetStr = target.ToString() ?? string.Empty;
        return actualStr.Contains(targetStr, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareNumbers(object? actual, object? target)
    {
        actual = UnwrapJsonElement(actual);
        target = UnwrapJsonElement(target);

        if (actual is null && target is null) return 0;
        if (actual is null) return -1;
        if (target is null) return 1;

        if (TryExtractDecimal(actual, out var actualNum) && TryExtractDecimal(target, out var targetNum))
        {
            return actualNum.CompareTo(targetNum);
        }

        // Fallback to string length or ordinal comparison
        var actualStr = actual.ToString() ?? string.Empty;
        var targetStr = target.ToString() ?? string.Empty;
        return string.Compare(actualStr, targetStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInCollection(object? actual, object? collection)
    {
        actual = UnwrapJsonElement(actual);
        collection = UnwrapJsonElement(collection);

        if (actual is null || collection is null) return false;

        if (collection is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                if (AreEqual(actual, item))
                {
                    return true;
                }
            }
            return false;
        }

        if (collection is string csv)
        {
            var parts = csv.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var actualStr = actual.ToString()?.Trim() ?? string.Empty;
            return parts.Any(p => string.Equals(p, actualStr, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static object? UnwrapJsonElement(object? value)
    {
        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number => je.TryGetDecimal(out var d) ? d : je.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => je.EnumerateArray().Select(e => UnwrapJsonElement(e)).ToList(),
                _ => je.ToString()
            };
        }
        return value;
    }

    private static bool TryExtractDecimal(object? val, out decimal result)
    {
        result = 0;
        if (val is null) return false;

        if (val is decimal d) { result = d; return true; }
        if (val is int i) { result = i; return true; }
        if (val is long l) { result = l; return true; }
        if (val is double db) { result = (decimal)db; return true; }
        if (val is float f) { result = (decimal)f; return true; }

        return decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryExtractBool(object? val, out bool result)
    {
        result = false;
        if (val is null) return false;
        if (val is bool b) { result = b; return true; }

        var str = val.ToString()?.Trim();
        if (string.Equals(str, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(str, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(str, "yes", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(str, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(str, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(str, "no", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        return false;
    }
}
