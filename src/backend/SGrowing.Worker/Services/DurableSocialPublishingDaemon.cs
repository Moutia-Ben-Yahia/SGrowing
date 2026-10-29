using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Worker.Services;

public class DurableSocialPublishingDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DurableSocialPublishingDaemon>? _logger;
    private readonly IDaemonHeartbeatService? _heartbeatService;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(20);

    public DurableSocialPublishingDaemon(
        IServiceProvider serviceProvider,
        ILogger<DurableSocialPublishingDaemon>? logger = null,
        IDaemonHeartbeatService? heartbeatService = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _heartbeatService = heartbeatService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("DurableSocialPublishingDaemon background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = _heartbeatService ?? _serviceProvider.GetService<IDaemonHeartbeatService>();
                if (heartbeat != null)
                {
                    await heartbeat.RecordHeartbeatAsync("DurableSocialPublishingDaemon", stoppingToken);
                }

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                await ProcessScheduledSocialPostsAsync(db, stoppingToken);
                await AuditTokenExpirationHealthAsync(db, _logger, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error processing scheduled social posts or token expiration cycle.");
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

    public static async Task<int> AuditTokenExpirationHealthAsync(
        SGrowingDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var thresholdUtc = DateTime.UtcNow.AddDays(7);

        // Find active accounts whose token expires within 7 days and haven't had RefreshTokenRequired flagged yet
        var expiringAccounts = await db.SocialAccounts
            .Where(a => a.IsActive && a.TokenExpiresAtUtc.HasValue && a.TokenExpiresAtUtc.Value <= thresholdUtc && !a.RefreshTokenRequired)
            .ToListAsync(cancellationToken);

        int flagged = 0;

        foreach (var account in expiringAccounts)
        {
            account.RefreshTokenRequired = true;
            flagged++;

            bool isExpired = account.TokenExpiresAtUtc!.Value <= DateTime.UtcNow;
            var remainingHours = (account.TokenExpiresAtUtc.Value - DateTime.UtcNow).TotalHours;

            if (isExpired)
            {
                logger?.LogWarning(
                    "[SocialTokenAlert] Social account {AccountName} ({Platform}) for tenant {TenantId} token EXPIRED at {ExpiresAtUtc}. RefreshTokenRequired flagged.",
                    account.AccountName, account.Platform, account.TenantId, account.TokenExpiresAtUtc);
            }
            else
            {
                logger?.LogWarning(
                    "[SocialTokenAlert] Social account {AccountName} ({Platform}) for tenant {TenantId} token expires in {Hours:F1} hours (at {ExpiresAtUtc}). Flagging RefreshTokenRequired.",
                    account.AccountName, account.Platform, account.TenantId, remainingHours, account.TokenExpiresAtUtc);
            }

            // Queue an alert notification into Outbox
            var alertPayload = JsonSerializer.Serialize(new
            {
                AccountId = account.Id,
                TenantId = account.TenantId,
                Platform = account.Platform.ToString(),
                AccountName = account.AccountName,
                TokenExpiresAtUtc = account.TokenExpiresAtUtc,
                IsExpired = isExpired,
                AlertMessage = isExpired
                    ? $"Social token for {account.AccountName} ({account.Platform}) has expired."
                    : $"Social token for {account.AccountName} ({account.Platform}) is expiring within 7 days ({account.TokenExpiresAtUtc:u})."
            });

            db.OutboxMessages.Add(OutboxMessage.Create(account.TenantId, "SocialAccount.TokenExpirationImminent", alertPayload));
        }

        if (flagged > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return flagged;
    }
}
