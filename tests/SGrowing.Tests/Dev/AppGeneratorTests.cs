using SGrowing.Api.Modules.Dev.AppGenerator;
using Xunit;

namespace SGrowing.Tests.Dev;

public sealed class AppGeneratorTests
{
    private readonly AppGeneratorEngine _engine = new();

    [Fact]
    public void GetSnapshot_ReturnsDefaultSeededAppAndRecords()
    {
        var snapshot = _engine.GetSnapshot();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.TotalAppsCount >= 1);
        Assert.True(snapshot.TotalRecordsCount >= 2);
        Assert.Contains(snapshot.Apps, a => a.AppId == "app_onboarding_tracker");
    }

    [Fact]
    public void CreateApp_InitializesSchemaWithPermissions()
    {
        var request = new CreateAppRequest(
            Name: "Partner Referral Directory",
            Description: "Directory of certified agency implementation partners",
            Fields: new[]
            {
                new AppFieldSchema("AgencyName", "string", true, null, null, null),
                new AppFieldSchema("PartnerTier", "enum", true, "Gold", null, new[] { "Silver", "Gold", "Platinum" }),
                new AppFieldSchema("CertifiedEngineers", "number", true, "5", null, null)
            },
            Permissions: "Public"
        );

        var app = _engine.CreateApp(request);

        Assert.NotNull(app);
        Assert.Equal("Partner Referral Directory", app.Name);
        Assert.Equal("partner-referral-directory", app.Slug);
        Assert.Equal(3, app.Fields.Count);
        Assert.Equal("Public", app.Permissions);
    }

    [Fact]
    public void InsertRecord_AppendsRecordToTargetApp()
    {
        var request = new InsertRecordRequest(
            AppId: "app_onboarding_tracker",
            Data: new Dictionary<string, object>
            {
                { "Company", "Snowflake Inc" },
                { "Tier", "Enterprise" },
                { "ContractValue", 250000 },
                { "Status", "Live" }
            }
        );

        var record = _engine.InsertRecord(request);

        Assert.NotNull(record);
        Assert.Equal("app_onboarding_tracker", record.AppId);
        Assert.Equal("Snowflake Inc", record.Data["Company"]?.ToString());

        var records = _engine.ListRecords("app_onboarding_tracker");
        Assert.Contains(records, r => r.Id == record.Id);
    }
}
