using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Common.Tenancy;
using SGrowing.Api.Data;
using SGrowing.Core.Tenancy;
using Xunit;

namespace SGrowing.Tests.Unit;

public class PostgreSqlRlsInterceptorTests
{
    [Fact]
    public void ConnectionOpened_WithActiveTenant_ExecutesSetLocalTenantId()
    {
        var tenantId = Guid.NewGuid();
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.HasTenant).Returns(true);
        mockTenant.Setup(t => t.TenantId).Returns(tenantId);

        var interceptor = new PostgreSqlRlsInterceptor(mockTenant.Object);
        var conn = new FakeNpgsqlConnection();

        interceptor.ConnectionOpened(conn, null!);

        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].Should().Be($"SET LOCAL app.current_tenant_id = '{tenantId}';");
    }

    [Fact]
    public async Task ConnectionOpenedAsync_WithActiveTenant_ExecutesSetLocalTenantIdAsync()
    {
        var tenantId = Guid.NewGuid();
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.HasTenant).Returns(true);
        mockTenant.Setup(t => t.TenantId).Returns(tenantId);

        var interceptor = new PostgreSqlRlsInterceptor(mockTenant.Object);
        var conn = new FakeNpgsqlConnection();

        await interceptor.ConnectionOpenedAsync(conn, null!);

        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].Should().Be($"SET LOCAL app.current_tenant_id = '{tenantId}';");
    }

    [Fact]
    public void ConnectionOpened_WithoutTenant_ExecutesEmptyTenantString()
    {
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.HasTenant).Returns(false);
        mockTenant.Setup(t => t.TenantId).Returns(Guid.Empty);

        var interceptor = new PostgreSqlRlsInterceptor(mockTenant.Object);
        var conn = new FakeNpgsqlConnection();

        interceptor.ConnectionOpened(conn, null!);

        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].Should().Be("SET LOCAL app.current_tenant_id = '';");
    }

    [Fact]
    public void ConnectionOpened_NonPostgreSqlConnection_BypassesExecution()
    {
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.HasTenant).Returns(true);
        mockTenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        var interceptor = new PostgreSqlRlsInterceptor(mockTenant.Object);
        var conn = new FakeSqliteConnection();

        interceptor.ConnectionOpened(conn, null!);

        conn.ExecutedCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task ConnectionOpenedAsync_WhenExceptionOccurs_DoesNotThrow()
    {
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.HasTenant).Returns(true);
        mockTenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        var interceptor = new PostgreSqlRlsInterceptor(mockTenant.Object);
        var failingConn = new FakeNpgsqlConnection(throwOnExecute: true);

        var act = async () => await interceptor.ConnectionOpenedAsync(failingConn, null!);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void RlsPolicySqlGenerator_GenerateTableRlsDdl_ProducesIdempotentDdl()
    {
        var generator = new RlsPolicySqlGenerator();
        var ddl = generator.GenerateTableRlsDdl("crm", "Contacts");

        ddl.Should().Contain("ALTER TABLE \"crm\".\"Contacts\" ENABLE ROW LEVEL SECURITY;");
        ddl.Should().Contain("ALTER TABLE \"crm\".\"Contacts\" FORCE ROW LEVEL SECURITY;");
        ddl.Should().Contain("DROP POLICY IF EXISTS \"contacts_tenant_isolation_policy\" ON \"crm\".\"Contacts\";");
        ddl.Should().Contain("CREATE POLICY \"contacts_tenant_isolation_policy\" ON \"crm\".\"Contacts\"");
        ddl.Should().Contain("current_setting('app.bypass_rls', true) = 'true'");
        ddl.Should().Contain("\"TenantId\" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid");
    }

    [Fact]
    public void RlsPolicySqlGenerator_GenerateSchemaAwareRlsMigrationDdl_GeneratesHeaderAndAllTables()
    {
        var generator = new RlsPolicySqlGenerator();
        var tables = new List<(string Schema, string Table)>
        {
            ("commerce", "Orders"),
            ("ai", "ApprovalRequests"),
            ("voice", "VoiceCallSessions")
        };

        var ddl = generator.GenerateSchemaAwareRlsMigrationDdl(tables);

        ddl.Should().Contain("SGrowing Enterprise 12-Schema PostgreSQL Row-Level Security (RLS) Hardening");
        ddl.Should().Contain("app.current_tenant_id");
        ddl.Should().Contain("ALTER TABLE \"commerce\".\"Orders\"");
        ddl.Should().Contain("ALTER TABLE \"ai\".\"ApprovalRequests\"");
        ddl.Should().Contain("ALTER TABLE \"voice\".\"VoiceCallSessions\"");
    }

    [Fact]
    public void RlsPolicySqlGenerator_DiscoverTenantPartitionedTables_IdentifiesEntitiesFromDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new SGrowingDbContext(options);
        var generator = new RlsPolicySqlGenerator();

        var discovered = generator.DiscoverTenantPartitionedTables(context.Model);

        discovered.Should().NotBeEmpty();
        discovered.Should().Contain(t => t.Schema == "ai" && t.Table == "ApprovalRequests");
        discovered.Should().Contain(t => t.Schema == "commerce" && t.Table == "CourierDeliveryRecords");
    }

    // Fake connection classes for unit testing
    private class FakeNpgsqlConnection : DbConnection
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

        protected override DbCommand CreateDbCommand() => new FakeCommand(this, _throwOnExecute);
    }

    private class FakeSqliteConnection : DbConnection
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

        protected override DbCommand CreateDbCommand() => new FakeCommand(this, false);
    }

    private class FakeCommand : DbCommand
    {
        private readonly DbConnection _connection;
        private readonly bool _throwOnExecute;

        public FakeCommand(DbConnection connection, bool throwOnExecute)
        {
            _connection = connection;
            _throwOnExecute = throwOnExecute;
        }

        public override void Cancel() { }
        public override int ExecuteNonQuery()
        {
            if (_throwOnExecute) throw new InvalidOperationException("Simulated SQL execution error");
            if (_connection is FakeNpgsqlConnection pg) pg.ExecutedCommands.Add(CommandText);
            else if (_connection is FakeSqliteConnection sq) sq.ExecutedCommands.Add(CommandText);
            return 1;
        }

        public override object? ExecuteScalar() => 1;
        public override void Prepare() { }
        [AllowNull]
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        protected override DbConnection? DbConnection { get => _connection; set { } }
        protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();
        protected override DbTransaction? DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbParameter CreateDbParameter() => throw new NotImplementedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();

        public override Task<int> ExecuteNonQueryAsync(System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(ExecuteNonQuery());
        }
    }
}
