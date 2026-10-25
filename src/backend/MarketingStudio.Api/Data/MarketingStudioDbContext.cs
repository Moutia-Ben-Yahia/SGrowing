using MarketingStudio.Core.Entities;
using MarketingStudio.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MarketingStudio.Api.Data;

public class MarketingStudioDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public MarketingStudioDbContext(DbContextOptions<MarketingStudioDbContext> options, ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Inbox> Inboxes => Set<Inbox>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<AiDraft> AiDrafts => Set<AiDraft>();
    public DbSet<SenderAccount> SenderAccounts => Set<SenderAccount>();
    public DbSet<WarmupRecord> WarmupRecords => Set<WarmupRecord>();
    public DbSet<OutboundCampaign> OutboundCampaigns => Set<OutboundCampaign>();
    public DbSet<CadenceStep> CadenceSteps => Set<CadenceStep>();
    public DbSet<ProspectEnrollment> ProspectEnrollments => Set<ProspectEnrollment>();
    public DbSet<Deal> Deals => Set<Deal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).IsRequired().HasMaxLength(200);
            b.Property(t => t.Slug).IsRequired().HasMaxLength(100);
            b.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Email).IsRequired().HasMaxLength(256);
            b.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.EventType).IsRequired().HasMaxLength(100);
            b.HasIndex(o => new { o.ProcessedOnUtc, o.OccurredOnUtc });
        });

        modelBuilder.Entity<Contact>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.FirstName).HasMaxLength(100);
            b.Property(c => c.LastName).HasMaxLength(100);
            b.Property(c => c.Email).HasMaxLength(256);
            b.Property(c => c.Phone).HasMaxLength(50);
            b.HasIndex(c => new { c.TenantId, c.Email });
            b.HasIndex(c => new { c.TenantId, c.Phone });
        });

        modelBuilder.Entity<Company>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(c => new { c.TenantId, c.Domain });
        });

        modelBuilder.Entity<Activity>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => new { a.TenantId, a.ContactId, a.CreatedAtUtc });
        });

        modelBuilder.Entity<Inbox>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.Name).IsRequired().HasMaxLength(100);
            b.HasIndex(i => new { i.TenantId, i.ChannelType });
        });

        modelBuilder.Entity<Conversation>(b =>
        {
            b.HasKey(c => c.Id);
            b.HasIndex(c => new { c.TenantId, c.Status, c.LastMessageAtUtc });
            b.HasIndex(c => new { c.TenantId, c.ContactId });
        });

        modelBuilder.Entity<Message>(b =>
        {
            b.HasKey(m => m.Id);
            b.HasIndex(m => new { m.TenantId, m.ConversationId, m.CreatedAtUtc });
            b.HasIndex(m => new { m.TenantId, m.ExternalMessageId });
        });

        modelBuilder.Entity<AiDraft>(b =>
        {
            b.HasKey(d => d.Id);
            b.HasIndex(d => new { d.TenantId, d.ConversationId, d.Status });
        });
    }
}
