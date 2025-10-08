// File: Models/WopiDbFile.cs
using WopiHost.Abstractions;
using WopiHost.Models.Database;
using WopiHost.Services;
using Microsoft.Extensions.Logging;

namespace WopiHost.Models;

public class WopiDbFile : IWopiFile
{
    private readonly CR02TepDinhKem _dbFile;
    private readonly IFileService _fileService;
    private readonly string _owner;
    private readonly ILogger<WopiDbFile> _logger;

    public WopiDbFile(CR02TepDinhKem dbFile, IFileService fileService, ILogger<WopiDbFile> logger = null)
    {
        _dbFile = dbFile;
        _fileService = fileService;
        _logger = logger;
        if (_dbFile == null)
        {
            throw new ArgumentNullException(nameof(dbFile), "Database file cannot be null");
        }
        if (_fileService == null)
        {
            throw new ArgumentNullException(nameof(fileService), "File service cannot be null");
        }
        _owner = _fileService.GetOwnerName(_dbFile); // Lấy tên chủ sở hữu từ dịch vụ
    }

    // Triển khai từ IWopiResource
    public string Identifier => _dbFile.Id.ToString();
    public string Name => Path.GetFileNameWithoutExtension(_dbFile.FileName);
    public bool Exists => File.Exists(_fileService.GetFullFilePath(_dbFile)); // Use the full path from FileService

    // Triển khai từ IWopiFile
    public string Extension => _dbFile.FileExtension.StartsWith('.') ? _dbFile.FileExtension : "." + _dbFile.FileExtension;
    public long Size => _dbFile.SizeInBytes;
    public string Version => _dbFile.Version ?? "1.0"; // Cần một giá trị không null
    public bool CanBeReadFromStream => true;
    
    // Các thuộc tính mở rộng để hỗ trợ nhiều loại file
    public bool IsOfficeDocument => Extension.IsOfficeDocument();
    public bool IsPdfDocument => Extension.IsPdfDocument();
    public bool IsImage => Extension.IsImage();
    public bool IsVideo => Extension.IsVideo();

    public string Owner => _owner; // Owner sẽ được lấy từ dịch vụ
    public long Length => _dbFile.SizeInBytes;
    public DateTime LastWriteTimeUtc => _dbFile.WriteDate.ToUniversalTime();
#nullable enable
    public byte[]? Checksum => null; // Chưa triển khai tính toán checksum

    public Task<Stream> GetReadStream(CancellationToken cancellationToken)
    {
        // Sử dụng FileService để lấy nội dung file
        var content = _fileService.GetFileContentAsync(_dbFile);
        return Task.FromResult<Stream>(new MemoryStream(content.Result ?? []));
    }

    // Triển khai từ IWopiWritableFile
    public bool CanBeWrittenTo => true;

    public async Task<Stream> GetWriteStream(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("GetWriteStream called for file ID: {FileId}, Name: {FileName}", _dbFile.Id, _dbFile.FileName);
        // Return a SaveableMemoryStream that will automatically save content when disposed
        var saveableStream = new SaveableMemoryStream(_dbFile, _fileService, _logger);
        return await Task.FromResult<Stream>(saveableStream);
    }
}