using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.CRM.CustomObjects;

#region Query Models

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomObjectFilterOperator
{
    Equals = 1,
    NotEquals = 2,
    GreaterThan = 3,
    LessThan = 4,
    GreaterThanOrEqual = 5,
    LessThanOrEqual = 6,
    Contains = 7,
    StartsWith = 8,
    In = 9
}

public record CustomObjectFilter(
    string Field,
    CustomObjectFilterOperator Operator,
    object? Value
);

public record CustomObjectQueryOptions(
    List<CustomObjectFilter>? Filters = null,
    string? SortBy = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 20
);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

#endregion

#region Query Builder Contract & Implementation

public interface IDynamicCustomObjectQueryBuilder
{
    PagedResult<CustomObjectRecord> ExecuteQuery(
        IEnumerable<CustomObjectRecord> source,
        CustomObjectQueryOptions? options);

    string BuildPostgresJsonbWhereClause(
        string jsonColumnName,
        IEnumerable<CustomObjectFilter> filters);
}

public class DynamicCustomObjectQueryBuilder : IDynamicCustomObjectQueryBuilder
{
    private static readonly DynamicValueComparer ValueComparer = new();

    public PagedResult<CustomObjectRecord> ExecuteQuery(
        IEnumerable<CustomObjectRecord> source,
        CustomObjectQueryOptions? options)
    {
        ArgumentNullException.ThrowIfNull(source);

        var query = source;

        // 1. In-memory dynamic filtering
        if (options?.Filters != null && options.Filters.Count > 0)
        {
            query = query.Where(record => MatchesAllFilters(record, options.Filters));
        }

        var filteredList = query.ToList();
        var totalCount = filteredList.Count;

        // 2. Dynamic sorting
        var sortedList = ApplySorting(filteredList, options?.SortBy, options?.SortDescending ?? false);

        // 3. Pagination
        var page = (options?.Page ?? 1) < 1 ? 1 : options!.Page;
        var pageSize = (options?.PageSize ?? 20);
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 500) pageSize = 500;

        var items = sortedList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedResult<CustomObjectRecord>(
            items,
            totalCount,
            page,
            pageSize,
            totalPages
        );
    }

    private static bool MatchesAllFilters(CustomObjectRecord record, IEnumerable<CustomObjectFilter> filters)
    {
        foreach (var filter in filters)
        {
            if (!MatchesFilter(record, filter))
            {
                return false;
            }
        }
        return true;
    }

    private static bool MatchesFilter(CustomObjectRecord record, CustomObjectFilter filter)
    {
        var rawVal = GetFieldValue(record, filter.Field);
        var filterVal = EavDynamicSchemaValidator.NormalizeValue(filter.Value);

        return filter.Operator switch
        {
            CustomObjectFilterOperator.Equals => EvaluateEquals(rawVal, filterVal),
            CustomObjectFilterOperator.NotEquals => !EvaluateEquals(rawVal, filterVal),
            CustomObjectFilterOperator.GreaterThan => EvaluateComparison(rawVal, filterVal) > 0,
            CustomObjectFilterOperator.LessThan => EvaluateComparison(rawVal, filterVal) < 0,
            CustomObjectFilterOperator.GreaterThanOrEqual => EvaluateComparison(rawVal, filterVal) >= 0,
            CustomObjectFilterOperator.LessThanOrEqual => EvaluateComparison(rawVal, filterVal) <= 0,
            CustomObjectFilterOperator.Contains => EvaluateContains(rawVal, filterVal),
            CustomObjectFilterOperator.StartsWith => EvaluateStartsWith(rawVal, filterVal),
            CustomObjectFilterOperator.In => EvaluateIn(rawVal, filterVal),
            _ => true
        };
    }

    public static object? GetFieldValue(CustomObjectRecord record, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return null;

        var clean = fieldName.Trim().ToLowerInvariant().Replace(" ", "_");

        if (record.Data.TryGetValue(clean, out var val))
        {
            return val;
        }

        // Top-level record metadata properties
        return clean switch
        {
            "id" => record.Id,
            "tenantid" or "tenant_id" => record.TenantId,
            "schemaid" or "schema_id" => record.SchemaId,
            "objectslug" or "object_slug" => record.ObjectSlug,
            "createdatutc" or "created_at_utc" => record.CreatedAtUtc,
            "updatedatutc" or "updated_at_utc" => record.UpdatedAtUtc,
            _ => null
        };
    }

    private static bool EvaluateEquals(object? actual, object? expected)
    {
        if (actual == null && expected == null) return true;
        if (actual == null || expected == null) return false;

        if (TryToDecimal(actual, out var dActual) && TryToDecimal(expected, out var dExpected))
        {
            return dActual == dExpected;
        }

        if (TryToBool(actual, out var bActual) && TryToBool(expected, out var bExpected))
        {
            return bActual == bExpected;
        }

        if (TryToDateTime(actual, out var dtActual) && TryToDateTime(expected, out var dtExpected))
        {
            return dtActual == dtExpected;
        }

        return string.Equals(actual.ToString()?.Trim(), expected.ToString()?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static int EvaluateComparison(object? actual, object? expected)
    {
        if (actual == null && expected == null) return 0;
        if (actual == null) return -1;
        if (expected == null) return 1;

        if (TryToDecimal(actual, out var dActual) && TryToDecimal(expected, out var dExpected))
        {
            return dActual.CompareTo(dExpected);
        }

        if (TryToDateTime(actual, out var dtActual) && TryToDateTime(expected, out var dtExpected))
        {
            return dtActual.CompareTo(dtExpected);
        }

        return string.Compare(actual.ToString()?.Trim(), expected.ToString()?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateContains(object? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        if (actual is IEnumerable enumerable && actual is not string)
        {
            foreach (var item in enumerable)
            {
                if (EvaluateEquals(item, expected)) return true;
            }
            return false;
        }

        var actStr = actual.ToString() ?? string.Empty;
        var expStr = expected.ToString() ?? string.Empty;

        return actStr.Contains(expStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateStartsWith(object? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        var actStr = actual.ToString()?.Trim() ?? string.Empty;
        var expStr = expected.ToString()?.Trim() ?? string.Empty;

        return actStr.StartsWith(expStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateIn(object? actual, object? expected)
    {
        if (actual == null || expected == null) return false;

        if (expected is IEnumerable enumerable && expected is not string)
        {
            foreach (var item in enumerable)
            {
                if (EvaluateEquals(actual, item)) return true;
            }
            return false;
        }

        var expStr = expected.ToString() ?? string.Empty;
        var parts = expStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Any(p => EvaluateEquals(actual, p));
    }

    private static List<CustomObjectRecord> ApplySorting(
        List<CustomObjectRecord> items,
        string? sortBy,
        bool sortDescending)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return sortDescending
                ? items.OrderBy(r => r.CreatedAtUtc).ToList()
                : items.OrderByDescending(r => r.CreatedAtUtc).ToList();
        }

        var sorted = sortDescending
            ? items.OrderByDescending(r => GetFieldValue(r, sortBy), ValueComparer).ThenByDescending(r => r.CreatedAtUtc)
            : items.OrderBy(r => GetFieldValue(r, sortBy), ValueComparer).ThenByDescending(r => r.CreatedAtUtc);

        return sorted.ToList();
    }

    private static bool TryToDecimal(object val, out decimal result)
    {
        result = 0m;
        if (val is decimal d) { result = d; return true; }
        if (val is int i) { result = i; return true; }
        if (val is long l) { result = l; return true; }
        if (val is double dbl) { result = (decimal)dbl; return true; }
        if (val is float f) { result = (decimal)f; return true; }

        var str = val?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return false;

        return decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryToBool(object val, out bool result)
    {
        result = false;
        if (val is bool b) { result = b; return true; }

        var str = val?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return false;

        return bool.TryParse(str, out result);
    }

    private static bool TryToDateTime(object val, out DateTime result)
    {
        result = default;
        if (val is DateTime dt) { result = dt; return true; }
        if (val is DateTimeOffset dto) { result = dto.UtcDateTime; return true; }

        var str = val?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return false;

        return DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result);
    }

    /// <summary>
    /// Generates PostgreSQL JSONB SQL predicate expressions compatible with GIN and B-Tree JSONB indexing.
    /// E.g. (data->>'mileage')::numeric > 50000 AND (data->>'status') = 'Active'
    /// </summary>
    public string BuildPostgresJsonbWhereClause(string jsonColumnName, IEnumerable<CustomObjectFilter> filters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonColumnName);
        if (filters == null) return string.Empty;

        var sb = new StringBuilder();
        var isFirst = true;

        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field)) continue;

            if (!isFirst) sb.Append(" AND ");
            isFirst = false;

            var cleanField = filter.Field.Trim().ToLowerInvariant().Replace(" ", "_");
            var valStr = filter.Value?.ToString()?.Replace("'", "''") ?? string.Empty;

            switch (filter.Operator)
            {
                case CustomObjectFilterOperator.Equals:
                    sb.Append($"({jsonColumnName}->>'{cleanField}') = '{valStr}'");
                    break;

                case CustomObjectFilterOperator.NotEquals:
                    sb.Append($"({jsonColumnName}->>'{cleanField}') <> '{valStr}'");
                    break;

                case CustomObjectFilterOperator.GreaterThan:
                    if (TryToDecimal(filter.Value ?? "", out _))
                        sb.Append($"(({jsonColumnName}->>'{cleanField}')::numeric > {valStr})");
                    else
                        sb.Append($"({jsonColumnName}->>'{cleanField}') > '{valStr}'");
                    break;

                case CustomObjectFilterOperator.LessThan:
                    if (TryToDecimal(filter.Value ?? "", out _))
                        sb.Append($"(({jsonColumnName}->>'{cleanField}')::numeric < {valStr})");
                    else
                        sb.Append($"({jsonColumnName}->>'{cleanField}') < '{valStr}'");
                    break;

                case CustomObjectFilterOperator.GreaterThanOrEqual:
                    if (TryToDecimal(filter.Value ?? "", out _))
                        sb.Append($"(({jsonColumnName}->>'{cleanField}')::numeric >= {valStr})");
                    else
                        sb.Append($"({jsonColumnName}->>'{cleanField}') >= '{valStr}'");
                    break;

                case CustomObjectFilterOperator.LessThanOrEqual:
                    if (TryToDecimal(filter.Value ?? "", out _))
                        sb.Append($"(({jsonColumnName}->>'{cleanField}')::numeric <= {valStr})");
                    else
                        sb.Append($"({jsonColumnName}->>'{cleanField}') <= '{valStr}'");
                    break;

                case CustomObjectFilterOperator.Contains:
                    sb.Append($"({jsonColumnName}->>'{cleanField}') ILIKE '%{valStr}%'");
                    break;

                case CustomObjectFilterOperator.StartsWith:
                    sb.Append($"({jsonColumnName}->>'{cleanField}') ILIKE '{valStr}%'");
                    break;

                case CustomObjectFilterOperator.In:
                    if (filter.Value is IEnumerable enumerable && filter.Value is not string)
                    {
                        var elements = enumerable.Cast<object?>()
                            .Select(e => $"'{e?.ToString()?.Replace("'", "''")}'");
                        sb.Append($"({jsonColumnName}->>'{cleanField}') IN ({string.Join(", ", elements)})");
                    }
                    else
                    {
                        var parts = valStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(p => $"'{p}'");
                        sb.Append($"({jsonColumnName}->>'{cleanField}') IN ({string.Join(", ", parts)})");
                    }
                    break;
            }
        }

        return sb.ToString();
    }
}

internal class DynamicValueComparer : IComparer<object?>
{
    public int Compare(object? x, object? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        if (TryToDecimal(x, out var dx) && TryToDecimal(y, out var dy))
            return dx.CompareTo(dy);

        if (TryToDateTime(x, out var dtx) && TryToDateTime(y, out var dty))
            return dtx.CompareTo(dty);

        return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryToDecimal(object val, out decimal result)
    {
        result = 0m;
        if (val is decimal d) { result = d; return true; }
        if (val is int i) { result = i; return true; }
        if (val is long l) { result = l; return true; }
        if (val is double dbl) { result = (decimal)dbl; return true; }
        if (val is float f) { result = (decimal)f; return true; }

        var str = val?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return false;

        return decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryToDateTime(object val, out DateTime result)
    {
        result = default;
        if (val is DateTime dt) { result = dt; return true; }
        if (val is DateTimeOffset dto) { result = dto.UtcDateTime; return true; }

        var str = val?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return false;

        return DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result);
    }
}

#endregion
