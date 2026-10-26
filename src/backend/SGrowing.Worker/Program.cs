using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SGrowing.Api.Data;
using SGrowing.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Database context
builder.Services.AddDbContext<SGrowingDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        // Production PostgreSQL
        // options.UseNpgsql(connectionString);
        options.UseInMemoryDatabase("SGrowingWorkerDb");
    }
    else
    {
        options.UseInMemoryDatabase("SGrowingWorkerDb");
    }
});

// Hosted background daemons
builder.Services.AddHostedService<OutboxProcessorDaemon>();
builder.Services.AddHostedService<SlaWatchdogDaemon>();

var host = builder.Build();
host.Run();
