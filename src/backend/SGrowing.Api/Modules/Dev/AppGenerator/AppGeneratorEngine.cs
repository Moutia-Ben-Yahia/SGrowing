using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Dev.AppGenerator;

public interface IAppGeneratorEngine
{
    AppSchema CreateApp(CreateAppRequest request);
    AppSchema? GetApp(string appId);
    IReadOnlyList<AppSchema> ListApps();
    AppRecord InsertRecord(InsertRecordRequest request);
    IReadOnlyList<AppRecord> ListRecords(string appId);
    AppBuilderSnapshot GetSnapshot();
}

public sealed class AppGeneratorEngine : IAppGeneratorEngine
{
    private readonly ConcurrentDictionary<string, AppSchema> _apps = new();
    private readonly ConcurrentDictionary<string, List<AppRecord>> _recordsByApp = new();

    public AppGeneratorEngine()
    {
        SeedDefaultApps();
    }

    public AppSchema CreateApp(CreateAppRequest request)
    {
        var appId = "app_" + Guid.NewGuid().ToString("N")[..8];
        var slug = request.Name.ToLowerInvariant().Replace(' ', '-');

        var app = new AppSchema(
            AppId: appId,
            Name: request.Name,
            Slug: slug,
            Description: request.Description,
            PrimaryField: request.Fields.FirstOrDefault()?.Name ?? "Name",
            Fields: request.Fields,
            Permissions: request.Permissions,
            CreatedAtUtc: DateTime.UtcNow
        );

        _apps[app.AppId] = app;
        _recordsByApp[app.AppId] = new List<AppRecord>();
        return app;
    }

    public AppSchema? GetApp(string appId)
    {
        _apps.TryGetValue(appId, out var app);
        return app;
    }

    public IReadOnlyList<AppSchema> ListApps()
    {
        return _apps.Values.OrderByDescending(a => a.CreatedAtUtc).ToList();
    }

    public AppRecord InsertRecord(InsertRecordRequest request)
    {
        var recordId = "rec_" + Guid.NewGuid().ToString("N")[..8];
        var record = new AppRecord(recordId, request.AppId, request.Data, DateTime.UtcNow);

        var list = _recordsByApp.GetOrAdd(request.AppId, _ => new List<AppRecord>());
        lock (list)
        {
            list.Add(record);
        }

        return record;
    }

    public IReadOnlyList<AppRecord> ListRecords(string appId)
    {
        if (_recordsByApp.TryGetValue(appId, out var list))
        {
            lock (list)
            {
                return list.OrderByDescending(r => r.CreatedAtUtc).ToList();
            }
        }

        return Array.Empty<AppRecord>();
    }

    public AppBuilderSnapshot GetSnapshot()
    {
        var totalRecords = _recordsByApp.Values.Sum(list =>
        {
            lock (list)
            {
                return list.Count;
            }
        });

        return new AppBuilderSnapshot(
            TotalAppsCount: _apps.Count,
            TotalRecordsCount: totalRecords,
            Apps: ListApps()
        );
    }

    private void SeedDefaultApps()
    {
        var fields1 = new[]
        {
            new AppFieldSchema("Company", "string", true, null, null, null),
            new AppFieldSchema("Tier", "enum", true, "Enterprise", null, new[] { "Starter", "Growth", "Enterprise" }),
            new AppFieldSchema("ContractValue", "currency", true, "50000", null, null),
            new AppFieldSchema("Status", "enum", true, "Kickoff", null, new[] { "Kickoff", "TechnicalIntegration", "Live" })
        };

        var app1 = new AppSchema(
            "app_onboarding_tracker",
            "VIP Client Onboarding Tracker",
            "vip-client-onboarding",
            "Tracks high-touch enterprise customer provisioning milestones",
            "Company",
            fields1,
            "InternalEmployees",
            DateTime.UtcNow.AddDays(-10)
        );

        _apps[app1.AppId] = app1;
        _recordsByApp[app1.AppId] = new List<AppRecord>
        {
            new("rec_01", app1.AppId, new Dictionary<string, object>
            {
                { "Company", "Stripe Inc" },
                { "Tier", "Enterprise" },
                { "ContractValue", 120000 },
                { "Status", "Live" }
            }, DateTime.UtcNow.AddDays(-2)),
            new("rec_02", app1.AppId, new Dictionary<string, object>
            {
                { "Company", "Datadog Corp" },
                { "Tier", "Enterprise" },
                { "ContractValue", 85000 },
                { "Status", "TechnicalIntegration" }
            }, DateTime.UtcNow.AddDays(-1))
        };
    }
}
