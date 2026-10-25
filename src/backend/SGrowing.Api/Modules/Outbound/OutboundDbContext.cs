using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Outbound;

public class OutboundDbContext : VoiceDbContext
{
    public OutboundDbContext(DbContextOptions<SGrowingDbContext> options, ITenantContext? tenantContext = null)
        : base(options, tenantContext)
    {
    }

    public new DbSet<CadenceTouchpoint> CadenceTouchpoints => Set<CadenceTouchpoint>();
    public new DbSet<TouchpointExecution> TouchpointExecutions => Set<TouchpointExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CadenceTouchpoint>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.BodyTemplate).IsRequired();
            b.HasIndex(t => new { t.TenantId, t.CampaignId });
        });

        modelBuilder.Entity<TouchpointExecution>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Status).IsRequired().HasMaxLength(50);
            b.HasIndex(e => new { e.TenantId, e.Status, e.ScheduledAtUtc });
        });
    }
}
