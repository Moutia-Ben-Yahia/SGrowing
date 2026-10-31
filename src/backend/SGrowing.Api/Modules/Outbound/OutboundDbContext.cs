using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Outbound;

public class OutboundDbContext : VoiceDbContext
{
    public OutboundDbContext(DbContextOptions<SGrowingDbContext> options, ITenantContext? tenantContext = null)
        : base(options, tenantContext)
    {
    }
}
