using WopiHost.Models;

namespace WopiHost.Services;

/// <summary>
/// Lớp mở rộng cho CR02TepDinhKem để thêm các thuộc tính kiểu file
/// </summary>
public static class FileTypeExtensions
{
    /// <summary>
    /// Kiểm tra xem file có phải là Microsoft Office document không
    /// </summary>
    public static bool IsOfficeDocument(this Models.Database.CR02TepDinhKem file)
    {
        if (file == null) return false;
        string extension = file.FileExtension;
        if (!extension.StartsWith('.'))
            extension = "." + extension;
        return extension.IsOfficeDocument();
    }
    
    /// <summary>
    /// Kiểm tra xem file có phải là PDF không
    /// </summary>
    public static bool IsPdfDocument(this Models.Database.CR02TepDinhKem file)
    {
        if (file == null) return false;
        string extension = file.FileExtension;
        if (!extension.StartsWith('.'))
            extension = "." + extension;
        return extension.IsPdfDocument();
    }
    
    /// <summary>
    /// Kiểm tra xem file có phải là ảnh không
    /// </summary>
    public static bool IsImage(this Models.Database.CR02TepDinhKem file)
    {
        if (file == null) return false;
        string extension = file.FileExtension;
        if (!extension.StartsWith('.'))
            extension = "." + extension;
        return extension.IsImage();
    }
    
    /// <summary>
    /// Kiểm tra xem file có phải là video không
    /// </summary>
    public static bool IsVideo(this Models.Database.CR02TepDinhKem file)
    {
        if (file == null) return false;
        string extension = file.FileExtension;
        if (!extension.StartsWith('.'))
            extension = "." + extension;
        return extension.IsVideo();
    }
}