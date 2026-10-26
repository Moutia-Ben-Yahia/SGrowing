using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Messaging;
using SGrowing.Core.Messaging;
using SGrowing.Worker.Services;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

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

// Hosted background daemons
builder.Services.AddScoped<AdvancedWorkflowSchedulerService>();
builder.Services.AddScoped<OutboundWebhookDispatcherService>();
builder.Services.AddHostedService<OutboxProcessorDaemon>();
builder.Services.AddHostedService<RedisStreamConsumerDaemon>();
builder.Services.AddHostedService<SlaWatchdogDaemon>();
builder.Services.AddHostedService<ResilientDlqDaemon>();

var host = builder.Build();
host.Run();
