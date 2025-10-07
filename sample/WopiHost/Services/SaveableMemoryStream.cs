using WopiHost.Models.Database;
using Microsoft.Extensions.Logging;

namespace WopiHost.Services;

/// <summary>
/// A MemoryStream that automatically saves content to the file service when disposed.
/// This ensures that changes made to the stream are persisted to the underlying storage.
/// </summary>
public class SaveableMemoryStream : MemoryStream
{
    private readonly CR02TepDinhKem _dbFile;
    private readonly IFileService _fileService;
    private readonly ILogger _logger;
    private bool _disposed = false;

    public SaveableMemoryStream(CR02TepDinhKem dbFile, IFileService fileService, ILogger logger = null)
    {
        _dbFile = dbFile ?? throw new ArgumentNullException(nameof(dbFile));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _logger = logger;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                // Save the content to the file service when the stream is disposed
                if (Length > 0)
                {
                    var content = ToArray();
                    _logger?.LogInformation("Disposing SaveableMemoryStream - Saving file content for ID: {FileId}, Size: {Size} bytes", _dbFile.Id, content.Length);
                    
                    // Save the content synchronously (we're in dispose)
                    var saveTask = _fileService.SaveFileContentAsync(_dbFile, content);
                    saveTask.Wait(); // Wait for completion since we're in dispose
                    
                    var success = saveTask.Result;
                    if (success)
                    {
                        _logger?.LogInformation("File content saved successfully for ID: {FileId}, new version: {Version}", _dbFile.Id, _dbFile.Version);
                    }
                    else
                    {
                        _logger?.LogError("Failed to save file content for ID: {FileId}", _dbFile.Id);
                    }
                }
                else
                {
                    _logger?.LogWarning("No content to save for file ID: {FileId} - stream is empty", _dbFile.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving file content for ID: {FileId}", _dbFile.Id);
                // Don't throw in dispose - just log the error
            }
            finally
            {
                _disposed = true;
            }
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            try
            {
                // Save the content to the file service when the stream is disposed
                if (Length > 0)
                {
                    var content = ToArray();
                    _logger?.LogInformation("Async disposing SaveableMemoryStream - Saving file content for ID: {FileId}, Size: {Size} bytes", _dbFile.Id, content.Length);
                    
                    var success = await _fileService.SaveFileContentAsync(_dbFile, content);
                    if (success)
                    {
                        _logger?.LogInformation("File content saved successfully for ID: {FileId}, new version: {Version}", _dbFile.Id, _dbFile.Version);
                    }
                    else
                    {
                        _logger?.LogError("Failed to save file content for ID: {FileId}", _dbFile.Id);
                    }
                }
                else
                {
                    _logger?.LogWarning("No content to save for file ID: {FileId} - stream is empty", _dbFile.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving file content for ID: {FileId}", _dbFile.Id);
                // Don't throw in dispose - just log the error
            }
            finally
            {
                _disposed = true;
            }
        }

        await base.DisposeAsync();
    }
}
