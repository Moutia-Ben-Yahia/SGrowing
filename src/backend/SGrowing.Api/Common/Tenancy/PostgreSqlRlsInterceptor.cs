using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Common.Tenancy;

/// <summary>
/// Intercepts PostgreSQL connection checkouts to inject the active TenantId into session configuration.
/// Enables kernel-level Row Level Security (RLS) enforcement in PostgreSQL (PRD Section 5.1 & 5.2).
/// </summary>
public class PostgreSqlRlsInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext? _tenantContext;
    private readonly ILogger<PostgreSqlRlsInterceptor>? _logger;

    public PostgreSqlRlsInterceptor(
        ITenantContext? tenantContext = null,
        ILogger<PostgreSqlRlsInterceptor>? logger = null)
    {
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        SetTenantSessionVariable(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetTenantSessionVariableAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void SetTenantSessionVariable(DbConnection connection)
    {
        if (!IsPostgreSqlConnection(connection))
            return;

        try
        {
            if (connection.State != ConnectionState.Open) return;

            using var cmd = connection.CreateCommand();
            var tenantIdStr = (_tenantContext != null && _tenantContext.HasTenant && _tenantContext.TenantId != Guid.Empty)
                ? _tenantContext.TenantId.ToString()
                : "";

            cmd.CommandText = $"SET LOCAL app.current_tenant_id = '{tenantIdStr}';";
            cmd.ExecuteNonQuery();
            _logger?.LogDebug("Applied PostgreSQL RLS session parameter: app.current_tenant_id = {TenantId}", tenantIdStr);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "PostgreSQL session SET skipped or not supported on current provider");
        }
    }

    private async Task SetTenantSessionVariableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (!IsPostgreSqlConnection(connection))
            return;

        try
        {
            if (connection.State != ConnectionState.Open) return;

            await using var cmd = connection.CreateCommand();
            var tenantIdStr = (_tenantContext != null && _tenantContext.HasTenant && _tenantContext.TenantId != Guid.Empty)
                ? _tenantContext.TenantId.ToString()
                : "";

            cmd.CommandText = $"SET LOCAL app.current_tenant_id = '{tenantIdStr}';";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger?.LogDebug("Applied PostgreSQL RLS session parameter: app.current_tenant_id = {TenantId}", tenantIdStr);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "PostgreSQL session SET skipped or not supported on current provider");
        }
    }

    private static bool IsPostgreSqlConnection(DbConnection connection)
    {
        var typeName = connection.GetType().Name;
        return typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
    }
}
