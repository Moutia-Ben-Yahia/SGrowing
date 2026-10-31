using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using SGrowing.Api.Modules.Tenancy;
using SGrowing.Core.Tenancy;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public class TenantRlsConnectionInterceptorTests
{
    [Fact]
    public void ConnectionOpened_WithActiveTenant_ExecutesSetLocalTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(t => t.HasTenant).Returns(true);
        mockTenantContext.Setup(t => t.TenantId).Returns(tenantId);

        var interceptor = new TenantRlsConnectionInterceptor(mockTenantContext.Object);
        var fakeConnection = new FakeNpgsqlConnection();

        // Act
        interceptor.ConnectionOpened(fakeConnection, null!);

        // Assert
        fakeConnection.ExecutedCommands.Should().ContainSingle();
        fakeConnection.ExecutedCommands[0].Should().Be($"SET LOCAL app.current_tenant_id = '{tenantId}';");
    }

    [Fact]
    public async Task ConnectionOpenedAsync_WithActiveTenant_ExecutesSetLocalTenantIdAsync()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(t => t.HasTenant).Returns(true);
        mockTenantContext.Setup(t => t.TenantId).Returns(tenantId);

        var interceptor = new TenantRlsConnectionInterceptor(mockTenantContext.Object);
        var fakeConnection = new FakeNpgsqlConnection();

        // Act
        await interceptor.ConnectionOpenedAsync(fakeConnection, null!);

        // Assert
        fakeConnection.ExecutedCommands.Should().ContainSingle();
        fakeConnection.ExecutedCommands[0].Should().Be($"SET LOCAL app.current_tenant_id = '{tenantId}';");
    }

    [Fact]
    public void ConnectionOpened_WithoutTenant_ExecutesSetLocalEmptyString()
    {
        // Arrange
        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(t => t.HasTenant).Returns(false);

        var interceptor = new TenantRlsConnectionInterceptor(mockTenantContext.Object);
        var fakeConnection = new FakeNpgsqlConnection();

        // Act
        interceptor.ConnectionOpened(fakeConnection, null!);

        // Assert
        fakeConnection.ExecutedCommands.Should().ContainSingle();
        fakeConnection.ExecutedCommands[0].Should().Be("SET LOCAL app.current_tenant_id = '';");
    }

    [Fact]
    public void ConnectionOpened_WithNullTenantContext_ExecutesSetLocalEmptyString()
    {
        // Arrange
        var interceptor = new TenantRlsConnectionInterceptor(null);
        var fakeConnection = new FakeNpgsqlConnection();

        // Act
        interceptor.ConnectionOpened(fakeConnection, null!);

        // Assert
        fakeConnection.ExecutedCommands.Should().ContainSingle();
        fakeConnection.ExecutedCommands[0].Should().Be("SET LOCAL app.current_tenant_id = '';");
    }

    [Fact]
    public void ConnectionOpened_NonPostgreSqlConnection_BypassesRlsExecution()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(t => t.HasTenant).Returns(true);
        mockTenantContext.Setup(t => t.TenantId).Returns(tenantId);

        var interceptor = new TenantRlsConnectionInterceptor(mockTenantContext.Object);
        var nonPgConnection = new FakeSqliteConnection();

        // Act
        interceptor.ConnectionOpened(nonPgConnection, null!);

        // Assert
        nonPgConnection.ExecutedCommands.Should().BeEmpty();
    }

    [Fact]
    public void ConnectionOpened_WhenCommandThrows_SwallowsExceptionDefensively()
    {
        // Arrange
        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(t => t.HasTenant).Returns(true);
        mockTenantContext.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        var interceptor = new TenantRlsConnectionInterceptor(mockTenantContext.Object);
        var failingConnection = new FakeNpgsqlConnection(throwOnExecute: true);

        // Act & Assert (should not throw)
        var act = () => interceptor.ConnectionOpened(failingConnection, null!);
        act.Should().NotThrow();
    }
}

// =============================================================================
// TEST STUBS
// =============================================================================

internal class FakeNpgsqlConnection : DbConnection
{
    private readonly bool _throwOnExecute;
    public List<string> ExecutedCommands { get; } = new();

    public FakeNpgsqlConnection(bool throwOnExecute = false)
    {
        _throwOnExecute = throwOnExecute;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();
    public override void ChangeDatabase(string databaseName) => throw new NotImplementedException();
    public override void Close() { }
    public override void Open() { }
    [AllowNull]
    public override string ConnectionString { get; set; } = "Host=localhost;Database=test";
    public override string Database => "test";
    public override ConnectionState State => ConnectionState.Open;
    public override string DataSource => "localhost";
    public override string ServerVersion => "16.0";

    protected override DbCommand CreateDbCommand()
    {
        return new FakeDbCommand(this, _throwOnExecute);
    }
}

internal class FakeSqliteConnection : DbConnection
{
    public List<string> ExecutedCommands { get; } = new();

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();
    public override void ChangeDatabase(string databaseName) => throw new NotImplementedException();
    public override void Close() { }
    public override void Open() { }
    [AllowNull]
    public override string ConnectionString { get; set; } = "Data Source=:memory:";
    public override string Database => "main";
    public override ConnectionState State => ConnectionState.Open;
    public override string DataSource => ":memory:";
    public override string ServerVersion => "3.40";

    protected override DbCommand CreateDbCommand()
    {
        return new FakeDbCommand(this);
    }
}

internal class FakeDbCommand : DbCommand
{
    private readonly DbConnection _connection;
    private readonly bool _throwOnExecute;

    public FakeDbCommand(DbConnection connection, bool throwOnExecute = false)
    {
        _connection = connection;
        _throwOnExecute = throwOnExecute;
    }

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    protected override DbConnection? DbConnection { get => _connection; set { } }
    protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();
    protected override DbTransaction? DbTransaction { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }

    public override void Cancel() { }
    protected override DbParameter CreateDbParameter() => throw new NotImplementedException();

    public override int ExecuteNonQuery()
    {
        if (_throwOnExecute) throw new InvalidOperationException("Simulated connection error");
        RecordCommand(CommandText);
        return 1;
    }

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        if (_throwOnExecute) throw new InvalidOperationException("Simulated connection error");
        RecordCommand(CommandText);
        return Task.FromResult(1);
    }

    public override object? ExecuteScalar() => null;
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
    public override void Prepare() { }

    private void RecordCommand(string cmd)
    {
        if (_connection is FakeNpgsqlConnection npgsql)
        {
            npgsql.ExecutedCommands.Add(cmd);
        }
        else if (_connection is FakeSqliteConnection sqlite)
        {
            sqlite.ExecutedCommands.Add(cmd);
        }
    }
}
