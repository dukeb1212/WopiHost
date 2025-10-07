using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WopiHost.Data;

namespace WopiHost;

/// <summary>
/// Design-time DbContext factory for Entity Framework migrations
/// </summary>
public class WopiDbContextFactory : IDesignTimeDbContextFactory<WopiDbContext>
{
    public WopiDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WopiDbContext>();
        
        // Use a default connection string for design-time operations
        // In production, this would come from appsettings.json
        optionsBuilder.UseNpgsql("Host=localhost;Database=digifact_db;Username=postgres;Password=yourpassword;SearchPath=section0");

        return new WopiDbContext(optionsBuilder.Options);
    }
}
