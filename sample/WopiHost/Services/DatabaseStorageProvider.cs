// File: Services/DatabaseStorageProvider.cs
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;
using WopiHost.Models;

#nullable enable

namespace WopiHost.Services
{
    /// <summary>
    /// Triển khai các interface của WOPI provider sử dụng IFileService và database.
    /// Lớp này hoạt động với một cấu trúc file "phẳng" và chưa hỗ trợ các tính năng về thư mục.
    /// </summary>
    public class DatabaseStorageProvider : IWopiStorageProvider, IWopiWritableStorageProvider
    {
        private readonly IFileService _fileService;
        private readonly ILogger<DatabaseStorageProvider> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public DatabaseStorageProvider(IFileService fileService, ILogger<DatabaseStorageProvider> logger, ILoggerFactory loggerFactory)
        {
            _fileService = fileService;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _logger.LogInformation("DatabaseStorageProvider initialized");
        }

        #region IWopiStorageProvider Implementation

        /// <summary>
        /// Lấy một tài nguyên (file) dựa trên ID.
        /// </summary>
        public async Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken) where T : class, IWopiResource
        {
            _logger.LogInformation("GetWopiResource<{ResourceType}> called with identifier: {Identifier}", 
                typeof(T).Name, identifier);
            
            if (typeof(T) != typeof(IWopiFile) && typeof(T) != typeof(IWopiResource))
            {
                _logger.LogWarning("Unsupported resource type requested: {ResourceType}", typeof(T).Name);
                // Hệ thống này chỉ hỗ trợ File, không hỗ trợ Folder.
                return null;
            }

            if (!int.TryParse(identifier, out var fileId))
            {
                _logger.LogWarning("Invalid file identifier format: {Identifier}", identifier);
                return null;
            }

            var dbFile = await _fileService.GetFileByIdAsync(fileId);
            if (dbFile is null)
            {
                _logger.LogWarning("File with ID {FileId} not found", fileId);
                return null;
            }

            _logger.LogInformation("Found file: {FileName}, Path: {FilePath}, Size: {FileSize} bytes", 
                dbFile.FileName, dbFile.RemotePath, dbFile.SizeInBytes);

            var wopiFile = new WopiDbFile(dbFile, _fileService, _loggerFactory.CreateLogger<WopiDbFile>());
            _logger.LogInformation("WopiDbFile created for {FileName} with identifier {Identifier}", 
                wopiFile.Name, wopiFile.Identifier);
                
            return wopiFile as T;
        }

        /// <summary>
        /// Lấy một tài nguyên (file) dựa vào tên và ID của container cha.
        /// </summary>
        public async Task<T?> GetWopiResourceByName<T>(string containerId, string name, CancellationToken cancellationToken) where T : class, IWopiResource
        {
            _logger.LogInformation("GetWopiResourceByName<{ResourceType}> called with containerId: {ContainerId}, name: {Name}", 
                typeof(T).Name, containerId, name);
                
            if (typeof(T) != typeof(IWopiFile) && typeof(T) != typeof(IWopiResource))
            {
                _logger.LogWarning("Unsupported resource type requested: {ResourceType}", typeof(T).Name);
                // Hệ thống này chỉ hỗ trợ File, không hỗ trợ Folder.
                return null;
            }

            var dbFile = await _fileService.GetFileByNameAsync(name);
            if (dbFile is null)
            {
                _logger.LogWarning("File with name {FileName} not found", name);
                return null;
            }

            _logger.LogInformation("Found file by name: {FileName}, ID: {FileId}, Path: {FilePath}, Size: {FileSize} bytes", 
                dbFile.FileName, dbFile.Id, dbFile.RemotePath, dbFile.SizeInBytes);

            var wopiFile = new WopiDbFile(dbFile, _fileService, _loggerFactory.CreateLogger<WopiDbFile>());
            return wopiFile as T;
        }

        // --- CÁC PHƯƠTHỨC LIÊN QUAN ĐẾN THƯ MỤC (CONTAINER) ---
        // Do hệ thống của bạn là "phẳng", chúng ta sẽ cung cấp các giá trị mặc định
        // hoặc báo lỗi "Không hỗ trợ" cho các phương thức này.

        public IWopiFolder RootContainerPointer
        {
            get {
                _logger.LogWarning("Attempted to access unsupported RootContainerPointer");
                // Ném lỗi vì không có khái niệm thư mục gốc.
                // Điều này sẽ ngăn các tính năng WOPI liên quan đến cây thư mục hoạt động.
                throw new NotSupportedException("This storage provider does not support hierarchical folder structures.");
            }
        }

        public Task<ReadOnlyCollection<IWopiFolder>> GetAncestors<T>(string identifier, CancellationToken cancellationToken = default) where T : class, IWopiResource
        {
            _logger.LogInformation("GetAncestors<{ResourceType}> called with identifier: {Identifier} - returning empty collection", 
                typeof(T).Name, identifier);
                
            // Trả về một danh sách rỗng vì không có thư mục cha.
            return Task.FromResult(new ReadOnlyCollection<IWopiFolder>(new List<IWopiFolder>()));
        }

        public async IAsyncEnumerable<IWopiFolder> GetWopiContainers(
            string? identifier = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetWopiContainers called with identifier: {Identifier} - returning empty collection", 
                identifier);
                
            // Trả về một danh sách rỗng.
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<IWopiFile> GetWopiFiles(
            string? identifier = null, 
            string? searchPattern = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetWopiFiles called with identifier: {Identifier}, searchPattern: {SearchPattern}", 
                identifier, searchPattern ?? "(null)");
                
            // Lấy tất cả các file từ database
            var files = await _fileService.GetAllFilesAsync();
            _logger.LogInformation("Retrieved {Count} files from database", files.Count());
            
            int matchCount = 0;
            int returnedCount = 0;
            
            foreach (var file in files)
            {
                matchCount++;
                // Nếu có mẫu tìm kiếm, lọc theo tên file
                if (!string.IsNullOrEmpty(searchPattern))
                {
                    // Thực hiện một so sánh đơn giản dựa trên wildcard pattern
                    // Ví dụ: *.docx sẽ khớp với tất cả các file docx
                    bool isMatch = WildcardMatch(file.FileName, searchPattern);
                    if (!isMatch)
                    {
                        _logger.LogDebug("File {FileName} does not match pattern {Pattern}", file.FileName, searchPattern);
                        continue;
                    }
                }
                
                returnedCount++;
                _logger.LogInformation("Returning file: {FileName}, ID: {FileId}, Path: {FilePath}, Size: {FileSize} bytes", 
                    file.FileName, file.Id, file.RemotePath, file.SizeInBytes);
                    
                yield return new WopiDbFile(file, _fileService, _loggerFactory.CreateLogger<WopiDbFile>());
            }
            
            _logger.LogInformation("GetWopiFiles completed. Matched {MatchedCount} files, returned {ReturnedCount} files", 
                matchCount, returnedCount);
        }
        
        /// <summary>
        /// Helper method để so khớp chuỗi với mẫu wildcard đơn giản
        /// </summary>
        private bool WildcardMatch(string input, string pattern)
        {
            _logger.LogDebug("WildcardMatch: Input={Input}, Pattern={Pattern}", input, pattern);
            
            // Nếu pattern là * thì khớp với tất cả
            if (pattern == "*") return true;
            
            // Nếu pattern là *.ext, kiểm tra phần mở rộng của file
            if (pattern.StartsWith("*."))
            {
                var extension = pattern.Substring(1); // lấy ".ext"
                bool result = input.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
                _logger.LogDebug("Extension pattern match: {Result}", result);
                return result;
            }
            
            // So sánh chính xác
            bool exactMatch = string.Equals(input, pattern, StringComparison.OrdinalIgnoreCase);
            _logger.LogDebug("Exact pattern match: {Result}", exactMatch);
            return exactMatch;
        }

        #endregion

        #region IWopiWritableStorageProvider Implementation

        /// <summary>
        /// Độ dài tối đa cho phép của tên file.
        /// </summary>
        public int FileNameMaxLength => 250;


        public async Task<bool> SaveWopiResource<T>(T resource, Stream content, CancellationToken cancellationToken) where T : class, IWopiResource
        {
            _logger.LogInformation("SaveWopiResource<{ResourceType}> called for resource: {ResourceName}", 
                typeof(T).Name, resource.Name);
                
            if (resource is not WopiDbFile wopiFile)
            {
                _logger.LogWarning("Resource is not a WopiDbFile. Resource type: {ResourceType}", resource.GetType().Name);
                return false;
            }

            if (!int.TryParse(wopiFile.Identifier, out var fileId))
            {
                _logger.LogWarning("Invalid file identifier format: {Identifier}", wopiFile.Identifier);
                return false;
            }

            var dbFile = await _fileService.GetFileByIdAsync(fileId);
            if (dbFile is null)
            {
                _logger.LogWarning("File with ID {FileId} not found for saving", fileId);
                return false;
            }

            // Đọc nội dung từ stream và lưu lại bằng FileService
            using var memoryStream = new MemoryStream();
            
            try
            {
                _logger.LogInformation("Copying content stream to memory stream");
                await content.CopyToAsync(memoryStream, cancellationToken);
                var fileBytes = memoryStream.ToArray();
                
                _logger.LogInformation("Content read from stream: {ContentLength} bytes", fileBytes.Length);
                
                if (fileBytes.Length == 0)
                {
                    _logger.LogWarning("Warning: Empty content (0 bytes) being saved for file {FileName}", dbFile.FileName);
                }

                bool saveResult = await _fileService.SaveFileContentAsync(dbFile, fileBytes);
                _logger.LogInformation("SaveFileContentAsync result: {Result} for file {FileName}", saveResult, dbFile.FileName);
                return saveResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving WOPI resource: {FileName}, ID: {FileId}", dbFile.FileName, fileId);
                return false;
            }
        }


        // --- CÁC PHƯƠNG THỨC GHI NÂNG CAO ---

        public async Task<T?> CreateWopiChildResource<T>(string? containerId, string name, CancellationToken cancellationToken = default) where T : class, IWopiResource
        {
            _logger.LogInformation("CreateWopiChildResource<{ResourceType}> called with containerId: {ContainerId}, name: {Name}", 
                typeof(T).Name, containerId, name);
                
            if (typeof(T) != typeof(IWopiFile) && typeof(T) != typeof(IWopiResource))
            {
                _logger.LogWarning("Unsupported resource type for creation: {ResourceType}", typeof(T).Name);
                // Hệ thống này chỉ hỗ trợ File, không hỗ trợ Folder.
                return null;
            }
            
            // Lấy tên file và phần mở rộng từ name
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(name);
            var extension = Path.GetExtension(name);
            
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".txt"; // Mặc định là .txt nếu không có phần mở rộng
                _logger.LogInformation("No extension provided, defaulting to .txt");
            }
            else if (extension.StartsWith("."))
            {
                extension = extension.Substring(1); // Bỏ dấu chấm ở đầu
                _logger.LogDebug("Extension formatted: {Extension}", extension);
            }
            
            // Tạo file mới trong database
            _logger.LogInformation("Creating new file: {FileName} with extension: {Extension}", name, extension);
            var newDbFile = await _fileService.CreateFileAsync(name, extension);
            if (newDbFile == null)
            {
                _logger.LogError("Failed to create file: {FileName}", name);
                return null;
            }
            
            _logger.LogInformation("File created successfully: {FileName}, ID: {FileId}, Path: {FilePath}", 
                newDbFile.FileName, newDbFile.Id, newDbFile.RemotePath);
                
            // Trả về WopiFile tương ứng
            var wopiFile = new WopiDbFile(newDbFile, _fileService, _loggerFactory.CreateLogger<WopiDbFile>());
            return wopiFile as T;
        }

        public async Task<bool> DeleteWopiResource<T>(string identifier, CancellationToken cancellationToken = default) where T : class, IWopiResource
        {
            _logger.LogInformation("DeleteWopiResource<{ResourceType}> called with identifier: {Identifier}", 
                typeof(T).Name, identifier);
                
            if (!int.TryParse(identifier, out var fileId))
            {
                _logger.LogWarning("Invalid file identifier format: {Identifier}", identifier);
                return false;
            }
            
            bool deleteResult = await _fileService.DeleteFileAsync(fileId);
            _logger.LogInformation("DeleteFileAsync result: {Result} for file ID: {FileId}", deleteResult, fileId);
            return deleteResult;
        }

        public async Task<bool> RenameWopiResource<T>(string identifier, string newName, CancellationToken cancellationToken = default) where T : class, IWopiResource
        {
            _logger.LogInformation("RenameWopiResource<{ResourceType}> called with identifier: {Identifier}, newName: {NewName}", 
                typeof(T).Name, identifier, newName);
                
            if (!int.TryParse(identifier, out var fileId))
            {
                _logger.LogWarning("Invalid file identifier format: {Identifier}", identifier);
                return false;
            }
            
            bool renameResult = await _fileService.RenameFileAsync(fileId, newName);
            _logger.LogInformation("RenameFileAsync result: {Result} for file ID: {FileId} with new name: {NewName}", 
                renameResult, fileId, newName);
            return renameResult;
        }

        public async Task<bool> CheckValidName<T>(string name, CancellationToken cancellationToken = default) where T : class, IWopiResource
        {
            _logger.LogInformation("CheckValidName<{ResourceType}> called for name: {Name}", 
                typeof(T).Name, name);
                
            // Kiểm tra tên file có hợp lệ không (không chứa ký tự đặc biệt của hệ điều hành)
            var isValid = !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            
            // Kiểm tra độ dài của tên file
            isValid = isValid && name.Length <= FileNameMaxLength;
            
            _logger.LogDebug("Initial validation (format/length): {Result}", isValid);
            
            // Kiểm tra nếu file đã tồn tại (đối với file mới)
            if (isValid && typeof(T) == typeof(IWopiFile))
            {
                bool exists = await _fileService.FileExistsByNameAsync(name);
                isValid = !exists;
                _logger.LogDebug("File exists check: {Exists}, Final validation result: {Result}", exists, isValid);
            }
            
            _logger.LogInformation("CheckValidName result: {Result} for name: {Name}", isValid, name);
            return isValid;
        }

        public async Task<string> GetSuggestedName<T>(string containerId, string name, CancellationToken cancellationToken = default) where T : class, IWopiResource
        {
            _logger.LogInformation("GetSuggestedName<{ResourceType}> called with containerId: {ContainerId}, name: {Name}", 
                typeof(T).Name, containerId, name);
                
            // Kiểm tra xem file với tên này đã tồn tại chưa
            bool fileExists = await _fileService.FileExistsByNameAsync(name);
            if (!fileExists)
            {
                _logger.LogInformation("File name is available, returning original name: {Name}", name);
                return name; // Trả về tên gốc nếu không có file trùng
            }
            
            _logger.LogInformation("File name already exists, generating alternative name");
            
            // Nếu file đã tồn tại, đề xuất một tên mới bằng cách thêm số vào cuối
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(name);
            var extension = Path.GetExtension(name);
            
            // Tìm tên không trùng lặp
            int counter = 1;
            string suggestedName;
            do
            {
                suggestedName = $"{fileNameWithoutExtension} ({counter}){extension}";
                _logger.LogDebug("Trying alternative name: {SuggestedName}", suggestedName);
                counter++;
                fileExists = await _fileService.FileExistsByNameAsync(suggestedName);
            } while (fileExists && counter < 100); // Giới hạn số lần thử
            
            _logger.LogInformation("Suggested name generated: {SuggestedName}", suggestedName);
            return suggestedName;
        }

        #endregion
    }
}