using SGrowing.Api.Modules.Tenancy;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public class PostgreSqlRlsTests
{
    private readonly PostgreSqlRlsService _rlsService = new();

    [Fact]
    public void GenerateRlsMigrationDdl_GeneratesValidPostgreSqlStatements()
    {
        var ddl = _rlsService.GenerateRlsMigrationDdl();

        Assert.NotNull(ddl);
        Assert.Contains("ENABLE ROW LEVEL SECURITY", ddl);
        Assert.Contains("FORCE ROW LEVEL SECURITY", ddl);
        Assert.Contains("app.current_tenant_id", ddl);
        Assert.Contains("app.bypass_rls", ddl);
        Assert.Contains("Contacts", ddl);
        Assert.Contains("Deals", ddl);
        Assert.Contains("DocumentEnvelopes", ddl);
        Assert.Contains("CodOrders", ddl);
    }

    [Fact]
    public void AuditTablesRlsCoverage_ReportsFullComplianceForCoreTables()
    {
        var tables = new[] { "Contacts", "Companies", "Orders", "Deals" };
        var audit = _rlsService.AuditTablesRlsCoverage(tables);

        Assert.Equal(4, audit.TotalAuditedTables);
        Assert.Equal(4, audit.CompliantTablesCount);
        Assert.All(audit.Policies, p =>
        {
            Assert.True(p.IsRowLevelSecurityEnabled);
            Assert.True(p.IsForcedRls);
            Assert.EndsWith("_tenant_isolation_policy", p.PolicyName);
        });
        Assert.False(string.IsNullOrWhiteSpace(audit.VerificationChecksum));
    }

    [Theory]
    [InlineData("11111111-1111-1111-1111-111111111111", "11111111-1111-1111-1111-111111111111", false, true)]
    [InlineData("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", false, false)]
    [InlineData("00000000-0000-0000-0000-000000000000", "11111111-1111-1111-1111-111111111111", false, false)]
    [InlineData("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", true, true)]
    public void EvaluateRlsAccess_CorrectlyEnforcesOrBypassesKernelIsolation(
        string sessionTenant, string rowTenant, bool bypass, bool expectedAccess)
    {
        var sessionGuid = Guid.Parse(sessionTenant);
        var rowGuid = Guid.Parse(rowTenant);

        var result = _rlsService.EvaluateRlsAccess(sessionGuid, rowGuid, bypass);

        Assert.Equal(expectedAccess, result);
    }
}
