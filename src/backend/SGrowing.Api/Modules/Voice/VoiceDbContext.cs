using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.Voice;

public class VoiceDbContext : SGrowingDbContext
{
    public VoiceDbContext(DbContextOptions<SGrowingDbContext> options, ITenantContext? tenantContext = null)
        : base(options, tenantContext)
    {
    }

    public new DbSet<VoiceCallSession> VoiceCallSessions => Set<VoiceCallSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VoiceCallSession>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.CallSid).IsRequired().HasMaxLength(100);
            b.Property(s => s.FromNumber).HasMaxLength(50);
            b.Property(s => s.ToNumber).HasMaxLength(50);
            b.HasIndex(s => new { s.TenantId, s.CallSid });
            b.HasIndex(s => new { s.TenantId, s.ContactId });
        });
    }
}
