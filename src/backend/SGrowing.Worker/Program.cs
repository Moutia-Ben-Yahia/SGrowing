using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Messaging;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.AI.Personas;
using SGrowing.Core.Messaging;
using SGrowing.Core.Diagnostics;
using SGrowing.Api.Modules.Diagnostics;
using SGrowing.Worker.Services;
using SGrowing.Worker.Services.Dispatchers;
using SGrowing.Worker.BackgroundJobs;
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

// Concurrency & Distributed Lock Provider
builder.Services.AddSingleton<SGrowing.Core.Common.IConcurrencyLockService, SGrowing.Api.Common.Concurrency.RedisConcurrencyLockService>();

// Daemon Heartbeat & Watchdog
builder.Services.AddSingleton<IDaemonHeartbeatService, DaemonHeartbeatService>();
builder.Services.AddHostedService<DaemonWatchdogService>();

// Outbound Dispatchers
builder.Services.AddHttpClient<ISmtpEmailDispatcher, SmtpEmailDispatcher>();
builder.Services.AddHttpClient<ITwilioSmsDispatcher, TwilioSmsDispatcher>();
builder.Services.AddHttpClient<IMetaWhatsAppDispatcher, MetaWhatsAppDispatcher>();

// Hosted background daemons
builder.Services.AddScoped<IMcpServerRegistryService, McpServerRegistryService>();
builder.Services.AddScoped<IAutonomousAgentMissionService, AutonomousAgentMissionService>();
builder.Services.AddScoped<IAgentPersonaService, AgentPersonaService>();
builder.Services.AddScoped<AdvancedWorkflowSchedulerService>();
builder.Services.AddScoped<OutboundWebhookDispatcherService>();
builder.Services.AddHostedService<OutboxProcessorDaemon>();
builder.Services.AddHostedService<OutboxPublisherJob>();
builder.Services.AddHostedService<AgentHeartbeatJob>();
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
builder.Services.AddHostedService<CartReservationSweeperDaemon>();
builder.Services.AddHostedService<CallCenterLockSweeperDaemon>();
builder.Services.AddHostedService<SerpRankScheduledTrackerDaemon>();
builder.Services.AddHostedService<AndroidSmsQueueDispatcherDaemon>();
builder.Services.AddHostedService<MultiInboxDispatchDaemon>();
builder.Services.AddSingleton<SGrowing.Api.Modules.Analytics.IClickHouseAnalyticsProjectionService, SGrowing.Api.Modules.Analytics.ClickHouseAnalyticsProjectionService>();
builder.Services.AddScoped<SGrowing.Api.Modules.AI.Governance.IApprovalGatekeeperService, SGrowing.Api.Modules.AI.Governance.ApprovalGatekeeperService>();
builder.Services.AddScoped<SGrowing.Api.Modules.AI.IAutonomousSwarmPersistenceService, SGrowing.Api.Modules.AI.AutonomousSwarmPersistenceService>();
builder.Services.AddScoped<SGrowing.Api.Modules.AI.IAutonomousCampaignLoopDaemonService, SGrowing.Api.Modules.AI.AutonomousCampaignLoopDaemonService>();
builder.Services.AddHostedService<AutonomousCampaignLoopDaemon>();

var host = builder.Build();
host.Run();
