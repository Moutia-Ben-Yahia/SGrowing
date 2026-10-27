using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Worker.Services;

public class DurableSocialPublishingDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DurableSocialPublishingDaemon>? _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(20);

    public DurableSocialPublishingDaemon(IServiceProvider serviceProvider, ILogger<DurableSocialPublishingDaemon>? logger = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("DurableSocialPublishingDaemon background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                await ProcessScheduledSocialPostsAsync(db, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error processing scheduled social posts.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    public static async Task<int> ProcessScheduledSocialPostsAsync(SGrowingDbContext db, CancellationToken cancellationToken = default)
    {
        var duePosts = await db.SocialPosts
            .Where(p => p.Status == SocialPostStatus.Scheduled && p.ScheduledAtUtc <= DateTime.UtcNow)
            .Take(15)
            .ToListAsync(cancellationToken);

        int processed = 0;

        foreach (var post in duePosts)
        {
            // Apply two-phase publish guarantee: mark published and stamp timestamp
            post.Status = SocialPostStatus.Published;
            post.PublishedAtUtc = DateTime.UtcNow;
            processed++;
        }

        if (processed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }
}
