#nullable enable
using WopiHost.Data;
using WopiHost.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace WopiHost.Services;

public interface IFileService
{
    Task<CR02TepDinhKem?> GetFileByIdAsync(int fileId);
    Task<CR02TepDinhKem?> UpdateFileAsync(CR02TepDinhKem file);
    Task<byte[]?> GetFileContentAsync(CR02TepDinhKem file);
    Task<bool> SaveFileContentAsync(CR02TepDinhKem file, byte[] content);
    string GetFullFilePath(CR02TepDinhKem file);
    string GetOwnerName(CR02TepDinhKem file);
    
    // New methods for WOPI functionality
    Task<CR02TepDinhKem?> GetFileByNameAsync(string fileName);
    Task<IEnumerable<CR02TepDinhKem>> GetAllFilesAsync();
    Task<CR02TepDinhKem?> CreateFileAsync(string fileName, string fileExtension, string? mimeType = null);
    Task<bool> DeleteFileAsync(int fileId);
    Task<bool> RenameFileAsync(int fileId, string newName);
    Task<bool> FileExistsByNameAsync(string fileName);
    
    // User management methods
    Task<bool> UserExistsAsync(int userId);
    Task<string?> GetUserNameAsync(int userId);
}

public class FileService : IFileService
{
    private readonly WopiDbContext _dbContext;
    private readonly string _rootPath;
    private readonly ILogger<FileService> _logger;

    public FileService(WopiDbContext dbContext, IConfiguration configuration, ILogger<FileService> logger)
    {
        _dbContext = dbContext;
        _rootPath = configuration.GetSection("FileStorageSettings:RootPath").Value ?? string.Empty;
        _logger = logger;
    }

    public async Task<CR02TepDinhKem?> GetFileByIdAsync(int fileId)
    {
        return await _dbContext.CR02TepDinhKem
            .FirstOrDefaultAsync(f => f.Id == fileId && f.Active == 1);
    }

    public async Task<CR02TepDinhKem?> UpdateFileAsync(CR02TepDinhKem file)
    {
        file.WriteDate = DateTime.UtcNow; // Use UTC for PostgreSQL compatibility
        file.Version = Guid.NewGuid().ToString();
        
        _dbContext.CR02TepDinhKem.Update(file);
        await _dbContext.SaveChangesAsync();
        
        return file;
    }

    // In FileService.cs
    public async Task<byte[]?> GetFileContentAsync(CR02TepDinhKem file)
    {
        // Use GetFullFilePath for consistency with SaveFileContentAsync
        var fullPath = GetFullFilePath(file);
        
        // Add debug logging
        _logger.LogInformation("Getting file content for ID: {FileId}, FullPath: {FullPath}",
            file.Id, fullPath);

        try
        {
            // Ensure path exists
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("File path does not exist: {Path}", fullPath);
                return Array.Empty<byte>();
            }

            var content = await File.ReadAllBytesAsync(fullPath);
            _logger.LogInformation("File content read successfully. Size: {Size} bytes", content.Length);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file content from {Path}", fullPath);
            throw;
        }
    }

    public async Task<bool> SaveFileContentAsync(CR02TepDinhKem file, byte[] content)
    {
        try
        {
            var fullPath = GetFullFilePath(file);
            var directory = Path.GetDirectoryName(fullPath);
            
            _logger.LogInformation("Saving file content for ID: {FileId}, FullPath: {FullPath}, Size: {Size} bytes",
                file.Id, fullPath, content.Length);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                _logger.LogInformation("Creating directory: {Directory}", directory);
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(fullPath, content);
            _logger.LogInformation("File content written successfully to: {FullPath}", fullPath);
            
            // Update file metadata
            file.SizeInBytes = content.Length;
            file.WriteDate = DateTime.UtcNow; // Use UTC for PostgreSQL compatibility
            file.Version = Guid.NewGuid().ToString();
            
            await UpdateFileAsync(file);
            _logger.LogInformation("File metadata updated for ID: {FileId}, new version: {Version}", file.Id, file.Version);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file content for ID: {FileId}", file.Id);
            return false;
        }
    }

    public string GetFullFilePath(CR02TepDinhKem file)
    {
        string relativePath = (file.RemotePath ?? string.Empty).TrimStart('\\');
        return Path.Combine(_rootPath, relativePath, file.FileName);
    }

    public string GetOwnerName(CR02TepDinhKem file)
    {
        try
        {
            if (file.CreateUid == null)
            {
                _logger.LogWarning("File {FileId} has null CreateUid", file.Id);
                return "System";
            }

            var owner = _dbContext.NS01TaiKhoanNguoiDungs
                .Where(u => u.Id == file.CreateUid.Value)
                .Select(u => u.HoTen)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(owner))
            {
                _logger.LogWarning("No user found for CreateUid {CreateUid} on file {FileId}", file.CreateUid, file.Id);
                return "Unknown Owner";
            }

            return owner;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting owner name for file {FileId}", file.Id);
            return "Unknown Owner";
        }
    }
    
    // Implementation for new methods
    
    public async Task<CR02TepDinhKem?> GetFileByNameAsync(string fileName)
    {
        return await _dbContext.CR02TepDinhKem
            .FirstOrDefaultAsync(f => f.FileName == fileName && f.Active == 1);
    }
    
    public async Task<IEnumerable<CR02TepDinhKem>> GetAllFilesAsync()
    {
        return await _dbContext.CR02TepDinhKem
            .Where(f => f.Active == 1)
            .ToListAsync();
    }
    
    public async Task<CR02TepDinhKem?> CreateFileAsync(string fileName, string fileExtension, string? mimeType = null)
    {
        var newFile = new CR02TepDinhKem
        {
            FileName = fileName,
            FileExtension = fileExtension,
            MimeType = mimeType ?? "application/octet-stream",
            SizeInBytes = 0,
            RemotePath = _rootPath + "\\" + fileName,
            CreateDate = DateTime.UtcNow, // Use UTC for PostgreSQL compatibility
            WriteDate = DateTime.UtcNow,  // Use UTC for PostgreSQL compatibility
            Active = 1,
            Version = Guid.NewGuid().ToString()
        };
        
        await _dbContext.CR02TepDinhKem.AddAsync(newFile);
        await _dbContext.SaveChangesAsync();
        
        return newFile;
    }
    
    public async Task<bool> DeleteFileAsync(int fileId)
    {
        var file = await GetFileByIdAsync(fileId);
        if (file == null)
        {
            return false;
        }
        
        try
        {
            // Soft delete - change Active status instead of removing from database
            file.Active = 0;
            await _dbContext.SaveChangesAsync();
            
            // Optionally delete physical file
            var fullPath = GetFullFilePath(file);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> RenameFileAsync(int fileId, string newName)
    {
        var file = await GetFileByIdAsync(fileId);
        if (file == null)
        {
            return false;
        }
        
        try
        {
            var oldPath = GetFullFilePath(file);
            
            // Update the filename in database
            file.FileName = newName;
            await UpdateFileAsync(file);
            
            // Rename the physical file if it exists
            var newPath = GetFullFilePath(file);
            if (File.Exists(oldPath))
            {
                File.Move(oldPath, newPath, true);
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> FileExistsByNameAsync(string fileName)
    {
        return await _dbContext.CR02TepDinhKem
            .AnyAsync(f => f.FileName == fileName && f.Active == 1);
    }
    
    // User management implementation
    public async Task<bool> UserExistsAsync(int userId)
    {
        try
        {
            return await _dbContext.NS01TaiKhoanNguoiDungs
                .AnyAsync(u => u.Id == userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} exists", userId);
            return false;
        }
    }
    
    public async Task<string?> GetUserNameAsync(int userId)
    {
        try
        {
            var user = await _dbContext.NS01TaiKhoanNguoiDungs
                .Where(u => u.Id == userId)
                .Select(u => u.HoTen)
                .FirstOrDefaultAsync();
            
            if (string.IsNullOrEmpty(user))
            {
                _logger.LogWarning("User {UserId} not found or has no name", userId);
                return null;
            }
            
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting name for user {UserId}", userId);
            return null;
        }
    }
}
