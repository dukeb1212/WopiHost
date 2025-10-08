#nullable enable
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using WopiHost.Services;

namespace WopiHost.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<PdfController> _logger;

    public PdfController(
        IFileService fileService,
        IJwtService jwtService,
        ILogger<PdfController> logger)
    {
        _fileService = fileService;
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPdf(int id, [FromQuery] string? access_token = null)
    {
        try
        {
            _logger.LogInformation("Requesting PDF file with ID: {FileId}", id);

            if (string.IsNullOrWhiteSpace(access_token))
            {
                return Unauthorized("Missing access token");
            }

            var tokenInfo = _jwtService.ValidateToken(access_token);
            if (tokenInfo == null)
            {
                return Unauthorized("Invalid token");
            }

            var fileIdClaim = tokenInfo.Claims.FirstOrDefault(c => c.Type == "file_id")?.Value;
            if (string.IsNullOrEmpty(fileIdClaim) || !fileIdClaim.Equals(id.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized("Token does not match requested file");
            }

            var file = await _fileService.GetFileByIdAsync(id);
            if (file == null)
            {
                return NotFound("File not found");
            }

            if (!file.FileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("File is not a PDF");
            }

            var fileStream = await _fileService.GetFileStreamAsync(id);
            if (fileStream == null)
            {
                return NotFound("File content not available");
            }

            var headers = Response.GetTypedHeaders();
            var contentDisposition = new ContentDispositionHeaderValue("inline")
            {
                FileName = CreateAsciiFileName(file.FileName),
                FileNameStar = file.FileName
            };

            headers.ContentDisposition = contentDisposition;
            headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true,
                MustRevalidate = true
            };

            Response.Headers[HeaderNames.Pragma] = "no-cache";
            Response.Headers[HeaderNames.Expires] = "0";
            Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";

            _logger.LogInformation("Returning PDF file content: {FileName}", file.FileName);
            return File(fileStream, MediaTypeNames.Application.Pdf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PDF file with ID: {FileId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    private static string CreateAsciiFileName(string? originalFileName)
    {
        const string fallbackName = "document";
        const string fallbackExtension = ".pdf";

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return $"{fallbackName}{fallbackExtension}";
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = fallbackExtension;
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(nameWithoutExtension))
        {
            nameWithoutExtension = fallbackName;
        }

        var normalized = nameWithoutExtension.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch <= 0x7F && !char.IsControl(ch))
            {
                builder.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) || ch == '-')
            {
                builder.Append('_');
            }
        }

        if (builder.Length == 0)
        {
            builder.Append(fallbackName);
        }

        return $"{builder}{extension}";
    }
}
