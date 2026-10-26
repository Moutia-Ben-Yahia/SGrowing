using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SGrowing.Api.Data;

public class SGrowingDbContextFactory : IDesignTimeDbContextFactory<SGrowingDbContext>
{
    public SGrowingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SGrowingDbContext>();
        
        // Standard design-time PostgreSQL connection string matching docker-compose.yml
        var connectionString = "Host=localhost;Port=5432;Database=sgrowing_db;Username=sgrowing_user;Password=sgrowing_password";
        optionsBuilder.UseNpgsql(connectionString);

        return new SGrowingDbContext(optionsBuilder.Options);
    }
}
