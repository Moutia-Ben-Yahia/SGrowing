using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Messaging;
using SGrowing.Api.Modules.AI;
using SGrowing.Core.Messaging;
using SGrowing.Core.Diagnostics;
using SGrowing.Api.Modules.Diagnostics;
using SGrowing.Worker.Services;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

// Database context
builder.Services.AddDbContext<SGrowingDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseInMemoryDatabase("SGrowingWorkerDb");
    }
});

// Durable Redis Stream Broker
var redisConn = builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["Redis:ConnectionString"];

if (!string.IsNullOrWhiteSpace(redisConn))
{
    try
    {
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
        builder.Services.AddSingleton<IRedisStreamBroker, RedisStreamBroker>();
    }
    catch
    {
        builder.Services.AddSingleton<IRedisStreamBroker, InMemoryStreamBroker>();
    }
}
else
{
    builder.Services.AddSingleton<IRedisStreamBroker, InMemoryStreamBroker>();
}

// Daemon Heartbeat & Watchdog
builder.Services.AddSingleton<IDaemonHeartbeatService, DaemonHeartbeatService>();
builder.Services.AddHostedService<DaemonWatchdogService>();

// Hosted background daemons
builder.Services.AddScoped<IMcpServerRegistryService, McpServerRegistryService>();
builder.Services.AddScoped<IAutonomousAgentMissionService, AutonomousAgentMissionService>();
builder.Services.AddScoped<AdvancedWorkflowSchedulerService>();
builder.Services.AddScoped<OutboundWebhookDispatcherService>();
builder.Services.AddHostedService<OutboxProcessorDaemon>();
builder.Services.AddHostedService<RedisStreamConsumerDaemon>();
builder.Services.AddHostedService<SlaWatchdogDaemon>();
builder.Services.AddHostedService<ResilientDlqDaemon>();
builder.Services.AddHostedService<DripCampaignCadenceDaemon>();
builder.Services.AddHostedService<P2PWarmupRescueDaemon>();
builder.Services.AddHostedService<AutonomousSdrTickDaemon>();
builder.Services.AddHostedService<AutonomousAgentExecutionDaemon>();
builder.Services.AddHostedService<OutwardWebhookDeliveryDaemon>();
builder.Services.AddHostedService<DurableSocialPublishingDaemon>();
builder.Services.AddHostedService<DealRottingWatchdogDaemon>();

var host = builder.Build();
host.Run();
