using Scalar.AspNetCore;
using SGrowing.Api.Common;
using SGrowing.Api.Data;
using SGrowing.Api.Extensions;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Auth;
using SGrowing.Api.Modules.Billing;
using SGrowing.Api.Modules.Dev;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// 1. Tenancy, Database & PostgreSql RLS
builder.Services.AddDatabaseAndTenancy(builder.Configuration);

// 2. Core Messaging, Redis, SignalR & Outbox
builder.Services.AddCoreAndMessagingServices(builder.Configuration);

// 3. Omnichannel Commerce, COD, WMS & Logistics
builder.Services.AddOmnichannelAndCommerceServices();

// 4. Enterprise Horizons, AI Agents & Advanced Features
builder.Services.AddEnterpriseHorizonsServices();

// 5. Authentication, Authorization & Security
builder.Services.AddSecurityAndAuthServices(builder.Configuration);

var app = builder.Build();
app.MapDefaultEndpoints();

// Middleware Pipeline
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseMiddleware<SGrowing.Api.Common.Security.CspNonceMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<PlanQuotaEnforcementMiddleware>();

// Ensure in-memory dev db is seeded if needed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
    db.Database.EnsureCreated();
}

// ----------------- Documentation & OpenAPI -----------------
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("SGrowing Omnichannel & AI Growth Engine API")
           .WithTheme(ScalarTheme.Moon)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

// ----------------- Modular Endpoint Registration -----------------
app.MapEndpointModules(typeof(Program).Assembly);

// Real-Time SignalR Hub
app.MapHub<OmniHub>("/hubs/omni");
app.MapGrowthOsHubEndpoints();

// Optional CLI Demo Seeding
if (args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedEnterpriseDemoAsync();
    Console.WriteLine("Demo database successfully seeded.");
}

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
