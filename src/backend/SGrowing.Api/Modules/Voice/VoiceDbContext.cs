using SGrowing.Api.Data;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.Voice;

public class VoiceDbContext : SGrowingDbContext
{
    public VoiceDbContext(DbContextOptions<SGrowingDbContext> options, ITenantContext? tenantContext = null)
        : base(options, tenantContext)
    {
    }
}
