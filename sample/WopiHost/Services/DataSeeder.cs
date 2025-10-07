using Microsoft.EntityFrameworkCore;
using WopiHost.Data;
using WopiHost.Models.Database;

namespace WopiHost.Services;

public class DataSeeder
{
    private readonly WopiDbContext _context;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(WopiDbContext context, ILogger<DataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await _context.Database.EnsureCreatedAsync();

            // Check if we already have data
            if (await _context.CR02TepDinhKem.AnyAsync())
            {
                _logger.LogInformation("Database already contains data, skipping seed");
                return;
            }

            // Create sample files
            var sampleFiles = new[]
            {
                new CR02TepDinhKem
                {
                    Active = 1,
                    Version = "1.0",
                    CreateDate = DateTime.UtcNow,
                    WriteDate = DateTime.UtcNow,
                    TenBang = "documents",
                    RemotePath = "samples",
                    FileName = "sample1.docx",
                    FileExtension = ".docx",
                    SizeInBytes = 15432,
                    MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    FileCategory = "office"
                },
                new CR02TepDinhKem
                {
                    Active = 1,
                    Version = "1.0", 
                    CreateDate = DateTime.UtcNow,
                    WriteDate = DateTime.UtcNow,
                    TenBang = "spreadsheets",
                    RemotePath = "samples",
                    FileName = "sample2.xlsx",
                    FileExtension = ".xlsx",
                    SizeInBytes = 8765,
                    MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileCategory = "office"
                },
                new CR02TepDinhKem
                {
                    Active = 1,
                    Version = "1.0",
                    CreateDate = DateTime.UtcNow,
                    WriteDate = DateTime.UtcNow,
                    TenBang = "presentations",
                    RemotePath = "samples", 
                    FileName = "sample3.pptx",
                    FileExtension = ".pptx",
                    SizeInBytes = 23456,
                    MimeType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                    FileCategory = "office"
                }
            };

            await _context.CR02TepDinhKem.AddRangeAsync(sampleFiles);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully seeded {Count} sample files", sampleFiles.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding data");
            throw;
        }
    }
}
