namespace WopiHost.Models;

/// <summary>
/// Extension methods for working with file types
/// </summary>
public static class WopiFileExtensions
{
    /// <summary>
    /// Kiểm tra xem file có phải là Microsoft Office document không
    /// </summary>
    /// <param name="extension">Phần mở rộng của file (bao gồm dấu chấm)</param>
    /// <returns>true nếu là Office document, ngược lại là false</returns>
    public static bool IsOfficeDocument(this string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        var normalizedExtension = extension.ToLowerInvariant();

        return normalizedExtension switch
        {
            ".docx" or ".doc" or ".dotx" or ".dot" or ".docm" or ".dotm" => true, // Word
            ".xlsx" or ".xls" or ".xlsm" or ".xltx" or ".xltm" or ".xlam" or ".xlsb" => true, // Excel
            ".pptx" or ".ppt" or ".pptm" or ".potx" or ".potm" or ".ppsx" or ".ppsm" => true, // PowerPoint
            ".vsdx" or ".vsd" => true, // Visio
            _ => false
        };
    }

    /// <summary>
    /// Kiểm tra xem file có phải là PDF không
    /// </summary>
    /// <param name="extension">Phần mở rộng của file (bao gồm dấu chấm)</param>
    /// <returns>true nếu là PDF, ngược lại là false</returns>
    public static bool IsPdfDocument(this string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        return extension.ToLowerInvariant() == ".pdf";
    }

    /// <summary>
    /// Kiểm tra xem file có phải là ảnh không
    /// </summary>
    /// <param name="extension">Phần mở rộng của file (bao gồm dấu chấm)</param>
    /// <returns>true nếu là ảnh, ngược lại là false</returns>
    public static bool IsImage(this string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        var normalizedExtension = extension.ToLowerInvariant();

        return normalizedExtension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" => true,
            _ => false
        };
    }

    /// <summary>
    /// Kiểm tra xem file có phải là video không
    /// </summary>
    /// <param name="extension">Phần mở rộng của file (bao gồm dấu chấm)</param>
    /// <returns>true nếu là video, ngược lại là false</returns>
    public static bool IsVideo(this string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        var normalizedExtension = extension.ToLowerInvariant();

        return normalizedExtension switch
        {
            ".mp4" or ".webm" or ".ogg" or ".mov" or ".avi" or ".wmv" or ".flv" => true,
            _ => false
        };
    }
}
